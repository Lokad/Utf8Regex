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
        if (tree is null) return default;
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<Utf8ExecutionNode>.Create(scope.Budget, tree.Root);
        var skipLeadingAsciiWhitespace = searchPlan.FallbackStartTransform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace;
        var prefix = skipLeadingAsciiWhitespace ? SkipLeadingAsciiWhitespacePrefix(tree.Root) : (Node: tree.Root, StartIndex: 0);
        // A forced or stale ASCII transform must never certify Unicode whitespace.
        if (skipLeadingAsciiWhitespace && ReferenceEquals(prefix.Node, tree.Root) && prefix.StartIndex == 0) return default;
        var prefixGuards = TryExtractPrefixGuards(prefix.Node, prefix.StartIndex);
        scope.Complete();

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
                var index = (long)startIndex + guard.Offset;
                if ((ulong)index >= (ulong)input.Length || !guard.Matches(input[(int)index]))
                {
                    return false;
                }
            }
        }

        if (FixedLiteralUtf8 is { Length: > 0 } literal)
        {
            var offset = (long)startIndex + FixedLiteralOffset;
            if (offset < 0 || offset > input.Length - literal.Length)
            {
                return false;
            }

            if (!input.Slice((int)offset, literal.Length).SequenceEqual(literal))
            {
                return false;
            }
        }

        if (FixedDistanceSets is { Length: > 0 } sets)
        {
            foreach (var set in sets)
            {
                var index = (long)startIndex + set.Distance;
                if ((ulong)index >= (ulong)input.Length || !set.Contains(input[(int)index]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private readonly record struct GuardFrame(Utf8ExecutionNode Node, int Next);
    private static Utf8DeterministicByteGuard[]? TryExtractPrefixGuards(Utf8ExecutionNode? node, int startIndex)
    {
        if (node is null) return null;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        using var frames = Utf8ConstructionFrames<GuardFrame>.Create(budget, new(node, startIndex));
        using var guards = Utf8ConstructionBuffer<Utf8DeterministicByteGuard>.Create(budget);
        var distance = 0;
        while (frames.Count != 0 && guards.Count < MaxPrefixGuards)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; node = frame.Node;
            if (Utf8ExecutionPrefixFacts.Transparent(node) && node.Children.Count == 1 || node.Kind == Utf8ExecutionNodeKind.Concatenate)
            {
                if (frame.Next < 0 || frame.Next > node.Children.Count) break;
                if (frame.Next == node.Children.Count) { frames.Pop(); continue; }
                frames[index] = frame with { Next = frame.Next + 1 };
                frames.Push(new(node.Children[frame.Next], 0)); continue;
            }
            if (!Append(node)) break;
            frames.Pop();
        }
        if (guards.Count == 0) return null;
        var result = guards.Take(); scope.Complete(); return result;

        bool Append(Utf8ExecutionNode atom)
        {
            if (atom.Kind == Utf8ExecutionNodeKind.One) return Scalar(atom.Ch, atom);
            if (atom.Kind == Utf8ExecutionNodeKind.Multi)
            {
                if (atom.LiteralUtf8 is { } bytes)
                {
                    budget.ChargeWork((ulong)bytes.Length);
                    for (var offset = 0; offset < bytes.Length;)
                    {
                        if (guards.Count == MaxPrefixGuards) return false;
                        if (Rune.DecodeFromUtf8(bytes.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done ||
                            !Scalar(scalar.Value, atom)) return false;
                        offset += consumed;
                    }
                    return true;
                }
                budget.ChargeWork((ulong)atom.Text.Length);
                for (var offset = 0; offset < atom.Text.Length;)
                {
                    if (guards.Count == MaxPrefixGuards) return false;
                    if (Rune.DecodeFromUtf16(atom.Text.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done || !Scalar(scalar.Value, atom)) return false;
                    offset += consumed;
                }
                return true;
            }
            if (atom.Kind == Utf8ExecutionNodeKind.Set && Utf8ExecutionPrefixFacts.ClassMask(atom, out var mask)) return Add(mask);
            return Advance(Utf8ExecutionPrefixFacts.Width(atom));
        }
        bool Scalar(int scalar, Utf8ExecutionNode atom) => Utf8ExecutionPrefixFacts.ScalarMask(scalar, atom, out var mask)
            ? Add(mask) : Advance(Utf8OwnedFindOptimizations.ComputeScalarByteWidth(scalar, Utf8ExecutionPrefixFacts.Folded(atom), atom.Options));
        bool Add(AsciiCharClass mask)
        {
            if (distance == int.MaxValue) return false;
            if (mask.Count == 1)
            {
                var literal = mask.ActualLowMask != 0 ? System.Numerics.BitOperations.TrailingZeroCount(mask.ActualLowMask) :
                    64 + System.Numerics.BitOperations.TrailingZeroCount(mask.ActualHighMask);
                guards.Add(new(distance, (byte)literal));
            }
            else guards.Add(new(distance, mask));
            distance++; return true;
        }
        bool Advance(Utf8OwnedFindOptimizations.ByteWidth width)
        {
            if (!width.IsFixed || width.Minimum > int.MaxValue - distance) return false;
            distance += width.Minimum; return true;
        }
    }

    private static (Utf8ExecutionNode? Node, int StartIndex) SkipLeadingAsciiWhitespacePrefix(Utf8ExecutionNode? node)
    {
        if (node is null) return (null, 0);
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<Utf8ExecutionNode>.Create(scope.Budget, node);
        node = Unwrap(node);
        if (node.Kind != Utf8ExecutionNodeKind.Concatenate)
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

        if (startIndex >= node.Children.Count || !IsAsciiWhitespaceLoop(node.Children[startIndex]))
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

        Utf8ExecutionNode Unwrap(Utf8ExecutionNode current)
        {
            while (Utf8ExecutionPrefixFacts.Transparent(current) && current.Children.Count == 1)
            { scope.Budget.ChargeWork(1); current = current.Children[0]; frames.Push(current); }
            return current;
        }
    }

    private static bool IsAsciiWhitespaceLoop(Utf8ExecutionNode? node)
    {
        if (node is null) return false;
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<Utf8ExecutionNode>.Create(scope.Budget, node);
        while (true)
        {
            scope.Budget.ChargeWork(1);
            if (node.Kind is Utf8ExecutionNodeKind.Set or Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop &&
                node.ScalarClass is not null && Utf8ExecutionPrefixFacts.ClassMask(node, out var mask))
                return mask.ActualLowMask == 0x100003e00UL && mask.ActualHighMask == 0;
            if (!(Utf8ExecutionPrefixFacts.Transparent(node) || node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop) || node.Children.Count != 1) return false;
            node = node.Children[0]; frames.Push(node);
        }
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return Utf8AsciiBytePredicates.IsSixByteWhitespace(value);
    }


}

internal readonly struct Utf8DeterministicByteGuard
{
    public Utf8DeterministicByteGuard(int offset, byte literal)
    {
        Offset = offset;
        Literal = literal;
        CharClass = null;
    }

    public Utf8DeterministicByteGuard(int offset, AsciiCharClass charClass)
    {
        Offset = offset;
        Literal = null;
        CharClass = charClass;
    }

    public int Offset { get; }

    public byte? Literal { get; }

    public AsciiCharClass? CharClass { get; }

    public bool Matches(byte value)
    {
        if (Literal is { } literal)
        {
            return value == literal;
        }

        return CharClass is { } charClass && charClass.Contains(value);
    }
}
