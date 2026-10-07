using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.FrontEnd;

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
        bool skipLeadingAsciiWhitespace)
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
        var prefix = skipLeadingAsciiWhitespace ? SkipLeadingAsciiWhitespacePrefix(tree?.Root) : (Node: tree?.Root, StartIndex: 0);
        var prefixGuards = TryExtractPrefixGuards(prefix.Node, prefix.StartIndex);

        if (skipLeadingAsciiWhitespace)
        {
            return prefixGuards is { Length: > 0 } || searchPlan.MinRequiredLength > 0
                ? new Utf8DeterministicVerifierGuards(prefixGuards, null, 0, null, searchPlan.MinRequiredLength, skipLeadingAsciiWhitespace: true)
                : default;
        }

        return searchPlan.Kind switch
        {
            Utf8SearchKind.FixedDistanceAsciiLiteral when searchPlan.LiteralUtf8 is { Length: > 0 } literal
                => new Utf8DeterministicVerifierGuards(prefixGuards, literal, searchPlan.Distance, null, searchPlan.MinRequiredLength, false),
            Utf8SearchKind.FixedDistanceAsciiChar when searchPlan.LiteralUtf8 is { Length: 1 } literal
                => new Utf8DeterministicVerifierGuards(prefixGuards, literal, searchPlan.Distance, null, searchPlan.MinRequiredLength, false),
            Utf8SearchKind.FixedDistanceAsciiSets when searchPlan.FixedDistanceSets is { Length: > 0 } sets
                => new Utf8DeterministicVerifierGuards(prefixGuards, null, 0, sets, searchPlan.MinRequiredLength, false),
            _ when searchPlan.MinRequiredLength > 0
                => new Utf8DeterministicVerifierGuards(prefixGuards, null, 0, null, searchPlan.MinRequiredLength, false),
            _ when prefixGuards is { Length: > 0 }
                => new Utf8DeterministicVerifierGuards(prefixGuards, null, 0, null, 0, false),
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
                if ((uint)index >= (uint)input.Length || !set.Contains(input[index]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static Utf8DeterministicByteGuard[]? TryExtractPrefixGuards(Utf8ExecutionNode? node, int startIndex)
    {
        if (node is null)
        {
            return null;
        }

        var guards = new List<Utf8DeterministicByteGuard>();
        var offset = 0;
        if (!TryAppendPrefixGuards(node, guards, ref offset, startIndex))
        {
            return null;
        }

        return guards.Count == 0 ? null : [.. guards];
    }

    private static (Utf8ExecutionNode? Node, int StartIndex) SkipLeadingAsciiWhitespacePrefix(Utf8ExecutionNode? node)
    {
        node = UnwrapStructuralNode(node);
        if (node is null || node.Kind != Utf8ExecutionNodeKind.Concatenate)
        {
            return (node, 0);
        }

        var startIndex = 0;
        while (startIndex < node.Children.Count &&
               node.Children[startIndex].Kind is Utf8ExecutionNodeKind.Bol or Utf8ExecutionNodeKind.Eol or Utf8ExecutionNodeKind.Boundary or Utf8ExecutionNodeKind.NonBoundary or Utf8ExecutionNodeKind.Beginning or Utf8ExecutionNodeKind.Start)
        {
            Utf8ConstructionScope.ChargeWork(1);
            startIndex++;
        }

        if (startIndex >= node.Children.Count || !IsAsciiWhitespaceLoop(UnwrapStructuralNode(node.Children[startIndex])))
        {
            return (node, 0);
        }

        startIndex++;
        while (startIndex < node.Children.Count &&
               node.Children[startIndex].Kind is Utf8ExecutionNodeKind.Bol or Utf8ExecutionNodeKind.Eol or Utf8ExecutionNodeKind.Boundary or Utf8ExecutionNodeKind.NonBoundary or Utf8ExecutionNodeKind.Beginning or Utf8ExecutionNodeKind.Start)
        {
            Utf8ConstructionScope.ChargeWork(1);
            startIndex++;
        }

        if (startIndex >= node.Children.Count)
        {
            return (null, 0);
        }

        if (startIndex == node.Children.Count - 1)
        {
            return (node.Children[startIndex], 0);
        }

        // Borrow the remaining child range; no synthetic node or child array.
        return (node, startIndex);
    }

    private struct GuardFrame(Utf8ExecutionNode node, int index)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index = index;
        internal bool Waiting;
    }

    private static bool TryAppendPrefixGuards(Utf8ExecutionNode node, List<Utf8DeterministicByteGuard> guards, ref int offset, int startIndex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<GuardFrame>.Create(budget, new(node, startIndex));
        var result = false;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = Utf8DeterministicAnchorSearch.Unwrap(frame.Node);
            if (node.Kind == Utf8ExecutionNodeKind.Concatenate && node.Children.Count > 0)
            {
                if (frame.Waiting && !result)
                {
                    // Stop locally; an outer sequence may keep earlier guards.
                    result = guards.Count > 0;
                    frames.Pop();
                    continue;
                }
                if (frame.Index == node.Children.Count) { frames.Pop(); continue; }
                frame.Node = node;
                frame.Waiting = true;
                var child = node.Children[frame.Index++];
                frames[index] = frame;
                frames.Push(new(child, 0));
                continue;
            }
            result = ReadScalar(node, guards, ref offset);
            frames.Pop();
        }
        return result;

        static bool ReadScalar(Utf8ExecutionNode node, List<Utf8DeterministicByteGuard> guards, ref int offset)
        {
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.Empty:
                case Utf8ExecutionNodeKind.Bol:
                case Utf8ExecutionNodeKind.Beginning:
                case Utf8ExecutionNodeKind.Start:
                    return true;

                case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                    return TryAddGuard(guards, new Utf8DeterministicByteGuard(offset++, literal: (byte)node.Ch));

                case Utf8ExecutionNodeKind.Multi when node.Text is { Length: > 0 } text:
                    foreach (var ch in text)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        if (ch > 0x7F || !TryAddGuard(guards, new Utf8DeterministicByteGuard(offset++, literal: (byte)ch)))
                        {
                            return false;
                        }
                    }

                    return true;

                case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set && Internal.FrontEnd.Utf8OwnedCharClass.IsAscii(set):
                    return TryAddGuard(guards, new Utf8DeterministicByteGuard(offset++, set));

                case Utf8ExecutionNodeKind.Concatenate:
                    return true;

                default:
                    return false;
            }
        }
    }

    private static Utf8ExecutionNode? UnwrapStructuralNode(Utf8ExecutionNode? node)
    {
        return node is null ? null : Utf8DeterministicAnchorSearch.Unwrap(node);
    }

    private static bool IsAsciiWhitespaceLoop(Utf8ExecutionNode? node)
    {
        while (node is not null)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (node.Kind is Utf8ExecutionNodeKind.Set or Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop &&
                (node.Text == Internal.FrontEnd.Utf8OwnedCharClass.SpaceClass || node.Text == Internal.FrontEnd.Utf8OwnedCharClass.ECMASpaceClass))
                return true;
            if (node.Kind is not (Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop) || node.Children.Count != 1)
                return false;
            node = UnwrapStructuralNode(node.Children[0]);
        }
        return false;
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return Utf8AsciiBytePredicates.IsSixByteWhitespace(value);
    }

    private static bool TryAddGuard(List<Utf8DeterministicByteGuard> guards, Utf8DeterministicByteGuard guard)
    {
        guards.Add(guard);
        return guards.Count <= MaxPrefixGuards;
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

        return Set is { } set && Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)value, set);
    }
}
