using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8DeterministicVerifierGuards
{
    private const int MaxPrefixGuards = 32;

    public Utf8DeterministicVerifierGuards(
        Utf8DeterministicByteGuard[]? prefixGuards,
        byte[]? fixedLiteralUtf8,
        int fixedLiteralOffset,
        Utf8FixedDistanceSet[]? fixedDistanceSets,
        int minRequiredLength,
        bool skipLeadingAsciiWhitespace = false)
    {
        PrefixGuards = prefixGuards;
        FixedLiteralUtf8 = fixedLiteralUtf8;
        FixedLiteralOffset = fixedLiteralOffset;
        FixedDistanceSets = fixedDistanceSets;
        MinRequiredLength = minRequiredLength;
        SkipLeadingAsciiWhitespace = skipLeadingAsciiWhitespace;
    }

    public Utf8DeterministicByteGuard[]? PrefixGuards { get; }

    public byte[]? FixedLiteralUtf8 { get; }

    public int FixedLiteralOffset { get; }

    public Utf8FixedDistanceSet[]? FixedDistanceSets { get; }

    public int MinRequiredLength { get; }

    public bool SkipLeadingAsciiWhitespace { get; }

    public bool HasValue =>
        PrefixGuards is { Length: > 0 } ||
        MinRequiredLength > 0 ||
        FixedLiteralUtf8 is { Length: > 0 } ||
        FixedDistanceSets is { Length: > 0 } ||
        SkipLeadingAsciiWhitespace;

    public static Utf8DeterministicVerifierGuards Create(Utf8ExecutionTree? tree, Utf8SearchPlan searchPlan)
    {
        var skipLeadingAsciiWhitespace = searchPlan.FallbackStartTransform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace;
        var prefixRoot = skipLeadingAsciiWhitespace ? SkipLeadingAsciiWhitespacePrefix(tree?.Root) : tree?.Root;
        var prefixGuards = TryExtractPrefixGuards(prefixRoot);

        if (skipLeadingAsciiWhitespace)
        {
            return prefixGuards is { Length: > 0 } || searchPlan.MinRequiredLength > 0
                ? new Utf8DeterministicVerifierGuards(prefixGuards, null, 0, null, searchPlan.MinRequiredLength, skipLeadingAsciiWhitespace: true)
                : default;
        }

        return searchPlan.Kind switch
        {
            Utf8SearchKind.FixedDistanceAsciiLiteral when searchPlan.LiteralUtf8 is { Length: > 0 } literal
                => new Utf8DeterministicVerifierGuards(prefixGuards, literal, searchPlan.Distance, null, searchPlan.MinRequiredLength),
            Utf8SearchKind.FixedDistanceAsciiChar when searchPlan.LiteralUtf8 is { Length: 1 } literal
                => new Utf8DeterministicVerifierGuards(prefixGuards, literal, searchPlan.Distance, null, searchPlan.MinRequiredLength),
            Utf8SearchKind.FixedDistanceAsciiSets when searchPlan.FixedDistanceSets is { Length: > 0 } sets
                => new Utf8DeterministicVerifierGuards(prefixGuards, null, 0, sets, searchPlan.MinRequiredLength),
            _ when searchPlan.MinRequiredLength > 0
                => new Utf8DeterministicVerifierGuards(prefixGuards, null, 0, null, searchPlan.MinRequiredLength),
            _ when prefixGuards is { Length: > 0 }
                => new Utf8DeterministicVerifierGuards(prefixGuards, null, 0, null, 0),
            _ => default
        };
    }

    public bool Matches(ReadOnlySpan<byte> input, int startIndex)
    {
        if ((uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        if (MinRequiredLength > 0 && startIndex > input.Length - MinRequiredLength)
        {
            return false;
        }

        if (SkipLeadingAsciiWhitespace)
        {
            while ((uint)startIndex < (uint)input.Length && IsAsciiWhitespace(input[startIndex]))
            {
                startIndex++;
            }
        }

        if (PrefixGuards is { Length: > 0 } guards)
        {
            foreach (var guard in guards)
            {
                var index = startIndex + guard.Offset;
                if ((uint)index >= (uint)input.Length || !guard.Matches(input[index]))
                {
                    return false;
                }
            }
        }

        if (FixedLiteralUtf8 is { Length: > 0 } literal)
        {
            var offset = startIndex + FixedLiteralOffset;
            if (offset < 0 || offset > input.Length - literal.Length)
            {
                return false;
            }

            if (!input.Slice(offset, literal.Length).SequenceEqual(literal))
            {
                return false;
            }
        }

        if (FixedDistanceSets is { Length: > 0 } sets)
        {
            foreach (var set in sets)
            {
                var index = startIndex + set.Distance;
                if ((uint)index >= (uint)input.Length || !MatchesSet(input[index], set))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static Utf8DeterministicByteGuard[]? TryExtractPrefixGuards(Utf8ExecutionNode? node)
    {
        if (node is null)
        {
            return null;
        }

        var guards = new List<Utf8DeterministicByteGuard>();
        var offset = 0;
        if (!TryAppendPrefixGuards(node, guards, ref offset))
        {
            return null;
        }

        return guards.Count == 0 ? null : [.. guards];
    }

    private static Utf8ExecutionNode? SkipLeadingAsciiWhitespacePrefix(Utf8ExecutionNode? node)
    {
        node = UnwrapStructuralNode(node);
        if (node is null || node.Kind != Utf8ExecutionNodeKind.Concatenate)
        {
            return node;
        }

        var startIndex = 0;
        while (startIndex < node.Children.Count &&
               node.Children[startIndex].Kind is Utf8ExecutionNodeKind.Bol or Utf8ExecutionNodeKind.Eol or Utf8ExecutionNodeKind.Boundary or Utf8ExecutionNodeKind.NonBoundary or Utf8ExecutionNodeKind.Beginning or Utf8ExecutionNodeKind.Start)
        {
            startIndex++;
        }

        if (startIndex >= node.Children.Count || !IsAsciiWhitespaceLoop(UnwrapStructuralNode(node.Children[startIndex])))
        {
            return node;
        }

        startIndex++;
        while (startIndex < node.Children.Count &&
               node.Children[startIndex].Kind is Utf8ExecutionNodeKind.Bol or Utf8ExecutionNodeKind.Eol or Utf8ExecutionNodeKind.Boundary or Utf8ExecutionNodeKind.NonBoundary or Utf8ExecutionNodeKind.Beginning or Utf8ExecutionNodeKind.Start)
        {
            startIndex++;
        }

        if (startIndex >= node.Children.Count)
        {
            return null;
        }

        if (startIndex == node.Children.Count - 1)
        {
            return node.Children[startIndex];
        }

        return new Utf8ExecutionNode(Utf8ExecutionNodeKind.Concatenate, node.Options, node.Children.Skip(startIndex).ToArray());
    }

    private static bool TryAppendPrefixGuards(Utf8ExecutionNode node, List<Utf8DeterministicByteGuard> guards, ref int offset)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                return true;

            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
                return node.Children.Count == 1 && TryAppendPrefixGuards(node.Children[0], guards, ref offset);

            case Utf8ExecutionNodeKind.Concatenate:
                foreach (var child in node.Children)
                {
                    if (!TryAppendPrefixGuards(child, guards, ref offset))
                    {
                        return guards.Count > 0;
                    }
                }

                return true;

            case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                return TryAddGuard(guards, new Utf8DeterministicByteGuard(offset++, literal: (byte)node.Ch));

            case Utf8ExecutionNodeKind.Multi when node.Text is { Length: > 0 } text:
                foreach (var ch in text)
                {
                    if (ch > 0x7F || !TryAddGuard(guards, new Utf8DeterministicByteGuard(offset++, literal: (byte)ch)))
                    {
                        return false;
                    }
                }

                return true;

            case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set && Internal.FrontEnd.Runtime.RegexCharClass.IsAscii(set):
                return TryAddGuard(guards, new Utf8DeterministicByteGuard(offset++, set));

            default:
                return false;
        }
    }

    private static Utf8ExecutionNode? UnwrapStructuralNode(Utf8ExecutionNode? node)
    {
        while (node is not null &&
               node.Kind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group &&
               node.Children.Count == 1)
        {
            node = node.Children[0];
        }

        return node;
    }

    private static bool IsAsciiWhitespaceLoop(Utf8ExecutionNode? node)
    {
        if (node is null)
        {
            return false;
        }

        if (node.Kind is Utf8ExecutionNodeKind.Set or Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop)
        {
            if (node.Text is Internal.FrontEnd.Runtime.RegexCharClass.SpaceClass or Internal.FrontEnd.Runtime.RegexCharClass.ECMASpaceClass)
            {
                return true;
            }
        }

        return node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop &&
            node.Children.Count == 1 &&
            IsAsciiWhitespaceLoop(UnwrapStructuralNode(node.Children[0]));
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C;
    }

    private static bool TryAddGuard(List<Utf8DeterministicByteGuard> guards, Utf8DeterministicByteGuard guard)
    {
        guards.Add(guard);
        return guards.Count <= MaxPrefixGuards;
    }

    private static bool MatchesSet(byte value, Utf8FixedDistanceSet set)
    {
        var matched = false;
        if (set.Chars is { Length: > 0 } chars)
        {
            matched = chars.AsSpan().IndexOf(value) >= 0;
        }
        else if (set.HasRange)
        {
            matched = value >= set.RangeLow && value <= set.RangeHigh;
        }

        return set.Negated ? !matched : matched;
    }
}

internal readonly struct Utf8DeterministicByteGuard
{
    public Utf8DeterministicByteGuard(int offset, byte literal)
    {
        Offset = offset;
        Literal = literal;
        Set = null;
    }

    public Utf8DeterministicByteGuard(int offset, string set)
    {
        Offset = offset;
        Literal = null;
        Set = set;
    }

    public int Offset { get; }

    public byte? Literal { get; }

    public string? Set { get; }

    public bool Matches(byte value)
    {
        if (Literal is { } literal)
        {
            return value == literal;
        }

        return Set is { } set && Internal.FrontEnd.Runtime.RegexCharClass.CharInClass((char)value, set);
    }
}
