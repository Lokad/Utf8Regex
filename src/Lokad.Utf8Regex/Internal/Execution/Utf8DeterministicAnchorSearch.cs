using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.FrontEnd;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8DeterministicAnchorSearch(PreparedSearcher Searcher, int Offset)
{
    private const int MaxDeterministicAnchorBranches = 32;
    private const int MaxDeterministicSetChoices = 4;

    public bool HasValue => Searcher.HasValue;

    public static bool CanUseForByteSafeLinear(
        Utf8ExecutionTree? tree,
        Utf8DeterministicAnchorSearch anchor,
        Utf8StructuralSearchPlan structuralSearchPlan)
    {
        return tree is not null &&
            IsByteSafeNode(tree.Root) &&
            ((anchor.HasValue &&
              anchor.Offset == 0 &&
              anchor.Searcher.Kind is not PreparedSearcherKind.ByteSet) ||
             HasSelectiveStructuralStartPlan(structuralSearchPlan));
    }

    public static Utf8DeterministicAnchorSearch Create(Utf8ExecutionTree? tree, Utf8SearchPlan searchPlan)
    {
        if (tree is null)
        {
            return default;
        }

        if (searchPlan.Kind == Utf8SearchKind.FixedDistanceAsciiLiteral &&
            searchPlan.LiteralUtf8 is { Length: > 0 } fixedLiteral &&
            HasDeterministicLeadingAnchor(tree.Root, fixedLiteral, searchPlan.Distance))
        {
            return new Utf8DeterministicAnchorSearch(
                new PreparedSearcher(new Internal.Search.PreparedSubstringSearch(fixedLiteral, ignoreCase: false), ignoreCase: false),
                searchPlan.Distance);
        }

        if (TryExtractDeterministicLeadingAsciiLiteralFamily(tree.Root, out var leadingLiterals))
        {
            if (TryCreateAsciiIgnoreCaseAnchorLiteral(leadingLiterals, out var ignoreCaseLiteral))
            {
                return new Utf8DeterministicAnchorSearch(
                    new PreparedSearcher(new Internal.Search.PreparedSubstringSearch(ignoreCaseLiteral, ignoreCase: true), ignoreCase: true),
                    0);
            }

            var literalFamilyUtf8 = leadingLiterals.Select(Encoding.UTF8.GetBytes).ToArray();
            return new Utf8DeterministicAnchorSearch(
                new PreparedSearcher(new PreparedMultiLiteralSearch(literalFamilyUtf8, ignoreCase: false)),
                0);
        }

        if (TryExtractDeterministicLeadingAsciiByteSet(tree.Root, out var leadingBytes))
        {
            return new Utf8DeterministicAnchorSearch(
                new PreparedSearcher(PreparedByteSearch.Create(leadingBytes)),
                0);
        }

        return default;
    }

    private static bool HasDeterministicLeadingAnchor(Utf8ExecutionNode root, ReadOnlySpan<byte> literal, int distance)
    {
        var prefix = new StringBuilder();
        AppendDeterministicLeadingAscii(root, prefix);
        if (distance < 0 || distance + literal.Length > prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < literal.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (prefix[distance + i] != (char)literal[i])
            {
                return false;
            }
        }

        return true;
    }

    private struct PrefixFrame(Utf8ExecutionNode node)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index;
        internal int Before;
        internal bool Entered;
    }

    private static void AppendDeterministicLeadingAscii(Utf8ExecutionNode node, StringBuilder prefix)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<PrefixFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (!frame.Entered)
            {
                frame.Node = Unwrap(frame.Node);
                frame.Entered = true;
            }
            node = frame.Node;
            if (node.Kind == Utf8ExecutionNodeKind.Concatenate && node.Children.Count > 0)
            {
                // A child with no progress stops this sequence; an enclosing
                // sequence can still continue from its own completed prefix.
                if ((frame.Index > 0 && prefix.Length == frame.Before) || frame.Index == node.Children.Count)
                {
                    frames.Pop();
                    continue;
                }
                frame.Before = prefix.Length;
                var child = node.Children[frame.Index++];
                frames[index] = frame;
                frames.Push(new(child));
                continue;
            }
            AppendScalar(node, prefix);
            frames.Pop();
        }

        static void AppendScalar(Utf8ExecutionNode node, StringBuilder prefix)
        {
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.Empty:
                case Utf8ExecutionNodeKind.Bol:
                case Utf8ExecutionNodeKind.Beginning:
                case Utf8ExecutionNodeKind.Start:
                    return;

                case Utf8ExecutionNodeKind.One:
                    if (node.Ch <= 0x7F)
                    {
                        prefix.Append(node.Ch);
                    }

                    return;

                case Utf8ExecutionNodeKind.Multi:
                    if (node.Text is { } text && IsAsciiText(text))
                    {
                        Utf8ConstructionScope.ChargeWork((ulong)text.Length);
                        prefix.Append(text);
                    }

                    return;

                default:
                    return;
            }
        }
    }

    private static bool TryExtractDeterministicLeadingAsciiLiteralFamily(Utf8ExecutionNode node, out string[] literals)
    {
        var results = TryExtractLeadingAsciiLiteralsCore(node);
        if (results is not { Count: > 0 })
        {
            literals = [];
            return false;
        }

        literals = [.. results
            .Where(static value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)];
        return literals.Length > 1 && literals[0].Length > 0;
    }

    private static bool TryExtractDeterministicLeadingAsciiByteSet(Utf8ExecutionNode node, out byte[] values)
    {
        values = [];
        if (!TryGetLeadingAsciiByteMask(node, out var lowMask, out var highMask))
        {
            return false;
        }

        var bytes = new List<byte>(128);
        for (var i = 0; i < 64; i++)
        {
            if (((lowMask >> i) & 1UL) != 0)
            {
                bytes.Add((byte)i);
            }
        }

        for (var i = 0; i < 64; i++)
        {
            if (((highMask >> i) & 1UL) != 0)
            {
                bytes.Add((byte)(i + 64));
            }
        }

        values = [.. bytes];
        return values.Length > 0;
    }

    private struct MaskFrame(Utf8ExecutionNode node)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index;
        internal bool Waiting;
        internal ulong Low;
        internal ulong High;
    }

    private static bool TryGetLeadingAsciiByteMask(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<MaskFrame>.Create(budget, new(node));
        var result = false;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = Unwrap(frame.Node);
            if (node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop && node.Children.Count == 1)
            {
                frames[index] = new(node.Children[0]);
                continue;
            }
            if (node.Kind == Utf8ExecutionNodeKind.Concatenate)
            {
                Utf8ExecutionNode? first = null;
                foreach (var child in node.Children)
                {
                    budget.ChargeWork(1);
                    var candidate = Unwrap(child);
                    if (IsZeroWidthNode(candidate)) continue;
                    first = candidate;
                    break;
                }
                if (first is not null)
                {
                    frames[index] = new(first);
                    continue;
                }
            }
            if (node.Kind == Utf8ExecutionNodeKind.Alternate && node.Children.Count > 0)
            {
                if (frame.Waiting)
                {
                    if (!result)
                    {
                        // A failed union keeps only its completed siblings.
                        lowMask = frame.Low;
                        highMask = frame.High;
                        frames.Pop();
                        continue;
                    }
                    frame.Low |= lowMask;
                    frame.High |= highMask;
                    if (frame.Index == node.Children.Count)
                    {
                        lowMask = frame.Low;
                        highMask = frame.High;
                        result = (lowMask | highMask) != 0;
                        frames.Pop();
                        continue;
                    }
                }
                frame.Node = node;
                frame.Waiting = true;
                var child = node.Children[frame.Index++];
                frames[index] = frame;
                frames.Push(new(child));
                continue;
            }
            result = ReadScalar(node, out lowMask, out highMask);
            frames.Pop();
        }
        return result;

        static bool ReadScalar(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
        {
            lowMask = 0;
            highMask = 0;
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.Empty:
                case Utf8ExecutionNodeKind.Bol:
                case Utf8ExecutionNodeKind.Beginning:
                case Utf8ExecutionNodeKind.Start:
                    return false;

                case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                    AddByteToMask((byte)node.Ch, ref lowMask, ref highMask);
                    return true;

                case Utf8ExecutionNodeKind.Multi when TryGetAsciiBytes(node.Text, out var text) && text.Length > 0:
                    AddByteToMask(text[0], ref lowMask, ref highMask);
                    return true;

                case Utf8ExecutionNodeKind.Set when Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(node.ScalarClass, out var set):
                    return TryBuildAsciiSetMask(set, out lowMask, out highMask);

                case Utf8ExecutionNodeKind.Loop:
                case Utf8ExecutionNodeKind.LazyLoop:
                    if (node.Ch is > (char)0 and <= (char)0x7F)
                    {
                        AddByteToMask((byte)node.Ch, ref lowMask, ref highMask);
                        return true;
                    }

                    if (Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(node.ScalarClass, out var loopSet))
                    {
                        return TryBuildAsciiSetMask(loopSet, out lowMask, out highMask);
                    }

                    if (TryGetAsciiBytes(node.Text, out var loopText) && loopText.Length > 0)
                    {
                        AddByteToMask(loopText[0], ref lowMask, ref highMask);
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }
    }

    private static bool TryBuildAsciiSetMask(AsciiCharClass set, out ulong lowMask, out ulong highMask)
    {
        lowMask = set.LowMask;
        highMask = set.HighMask;
        return (lowMask | highMask) != 0;
    }

    private static void AddByteToMask(byte value, ref ulong lowMask, ref ulong highMask)
    {
        if (value < 64)
        {
            lowMask |= 1UL << value;
        }
        else
        {
            highMask |= 1UL << (value - 64);
        }
    }

    // PLAN.md D06: single owner for execution-tree analysis primitives shared with the byte-safe linear verifier.
    internal static bool IsZeroWidthNode(Utf8ExecutionNode node)
    {
        return node.Kind is Utf8ExecutionNodeKind.Empty
            or Utf8ExecutionNodeKind.Bol
            or Utf8ExecutionNodeKind.Beginning
            or Utf8ExecutionNodeKind.Start
            or Utf8ExecutionNodeKind.Eol
            or Utf8ExecutionNodeKind.EndZ
            or Utf8ExecutionNodeKind.End
            or Utf8ExecutionNodeKind.Boundary
            or Utf8ExecutionNodeKind.NonBoundary;
    }

    private struct ByteSafeFrame(Utf8ExecutionNode node)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index;
        internal bool Entered;
    }

    private static bool IsByteSafeNode(Utf8ExecutionNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ByteSafeFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = frame.Node;
            if (!frame.Entered)
            {
                frame.Entered = true;
                switch (node.Kind)
                {
                    case Utf8ExecutionNodeKind.Empty:
                    case Utf8ExecutionNodeKind.Capture:
                    case Utf8ExecutionNodeKind.Group:
                    case Utf8ExecutionNodeKind.Concatenate:
                    case Utf8ExecutionNodeKind.Alternate:
                    case Utf8ExecutionNodeKind.Loop:
                    case Utf8ExecutionNodeKind.LazyLoop:
                    case Utf8ExecutionNodeKind.Bol:
                    case Utf8ExecutionNodeKind.Beginning:
                    case Utf8ExecutionNodeKind.Start:
                    case Utf8ExecutionNodeKind.Eol:
                    case Utf8ExecutionNodeKind.EndZ:
                    case Utf8ExecutionNodeKind.End:
                        break;

                    case Utf8ExecutionNodeKind.One:
                        if (node.Ch > 0x7F)
                        {
                            return false;
                        }

                        break;

                    case Utf8ExecutionNodeKind.Multi:
                        if (node.Text is null || !IsAsciiText(node.Text))
                        {
                            return false;
                        }

                        break;

                    case Utf8ExecutionNodeKind.Set:
                        if (!Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(node.ScalarClass, out _))
                        {
                            return false;
                        }

                        break;

                    default:
                        return false;
                }
            }
            if (frame.Index == node.Children.Count) { frames.Pop(); continue; }
            var child = node.Children[frame.Index++];
            frames[index] = frame;
            frames.Push(new(child));
        }
        return true;
    }

    internal static Utf8ExecutionNode Unwrap(Utf8ExecutionNode node)
    {
        while (node.Kind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group &&
               node.Children.Count == 1)
        {
            Utf8ConstructionScope.ChargeWork(1);
            node = node.Children[0];
        }

        return node;
    }

    internal static bool TryGetAsciiBytes(string? text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!IsAsciiText(text)) return false;

        // The scan/copy share construction work. This returned byte array
        // retains separate payload admission work.
        Utf8ConstructionScope.ChargeWork((ulong)text.Length);
        bytes = new byte[text.Length];
        for (var index = 0; index < text.Length; index++) bytes[index] = (byte)text[index];
        return true;
    }

    private static bool IsAsciiText(ReadOnlySpan<char> text)
    {
        foreach (var ch in text)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (ch > 0x7F) return false;
        }
        return true;
    }

    private struct FamilyFrame(Utf8ExecutionNode node)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index;
        internal bool Waiting;
        internal List<string>? Families;
    }

    private static List<string>? TryExtractLeadingAsciiLiteralsCore(Utf8ExecutionNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<FamilyFrame>.Create(budget, new(node));
        List<string>? result = null;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = Unwrap(frame.Node);
            if (node.Kind is Utf8ExecutionNodeKind.Concatenate or Utf8ExecutionNodeKind.Alternate && node.Children.Count > 0)
            {
                if (frame.Waiting)
                {
                    var union = node.Kind == Utf8ExecutionNodeKind.Alternate;
                    if (result is not { Count: > 0 })
                    {
                        result = union ? null : frame.Families is { Count: > 0 } ? frame.Families : null;
                        frames.Pop();
                        continue;
                    }
                    List<string>? families;
                    if (union)
                    {
                        families = frame.Families ?? [];
                        foreach (var literal in result)
                        {
                            var duplicate = false;
                            foreach (var existing in families)
                            {
                                budget.ChargeWork(1 + (ulong)Math.Min(existing.Length, literal.Length));
                                if (!StringComparer.Ordinal.Equals(existing, literal)) continue;
                                duplicate = true;
                                break;
                            }
                            if (duplicate) continue;
                            budget.ChargeWork(1);
                            families.Add(literal);
                            if (families.Count <= MaxDeterministicAnchorBranches) continue;
                            families = null;
                            break;
                        }
                    }
                    else
                        families = frame.Families is null ? result : CombineLeadingLiteralFamilies(frame.Families, result);
                    result = families;
                    if (families is null || frame.Index == node.Children.Count)
                    {
                        frames.Pop();
                        continue;
                    }
                    frame.Families = families;
                }
                frame.Node = node;
                frame.Waiting = true;
                var child = node.Children[frame.Index++];
                frames[index] = frame;
                frames.Push(new(child));
                continue;
            }
            result = ReadScalar(node);
            frames.Pop();
        }
        return result;

        static List<string>? ReadScalar(Utf8ExecutionNode node)
        {
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.Empty:
                case Utf8ExecutionNodeKind.Bol:
                case Utf8ExecutionNodeKind.Beginning:
                case Utf8ExecutionNodeKind.Start:
                    return [string.Empty];

                case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                    return [node.Ch.ToString()];

                case Utf8ExecutionNodeKind.Multi when node.Text is { Length: > 0 } text && IsAsciiText(text):
                    return [text];

                case Utf8ExecutionNodeKind.Set when Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(node.ScalarClass, out var set):
                    return TryExtractSetChoices(set);

                default:
                    return null;
            }
        }
    }

    private static List<string>? CombineLeadingLiteralFamilies(List<string> left, List<string> right)
    {
        Utf8ConstructionScope.ChargeWork((ulong)(left.Count * right.Count));
        var combined = new List<string>(left.Count * right.Count);
        foreach (var leftLiteral in left)
        {
            foreach (var rightLiteral in right)
            {
                Utf8ConstructionScope.ChargeWork(1 + (ulong)leftLiteral.Length + (ulong)rightLiteral.Length);
                combined.Add(leftLiteral + rightLiteral);
                if (combined.Count > MaxDeterministicAnchorBranches)
                {
                    return null;
                }
            }
        }

        return combined;
    }

    private static List<string>? TryExtractSetChoices(AsciiCharClass set)
    {
        if (set.Count == 0 || set.Count > MaxDeterministicSetChoices) return null;
        var choices = new List<string>(set.Count);
        for (var value = 0; value < 128; value++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (set.Contains((byte)value)) choices.Add(((char)value).ToString());
        }
        return choices;
    }

    private static bool TryCreateAsciiIgnoreCaseAnchorLiteral(string[] literals, out byte[] literalUtf8)
    {
        literalUtf8 = [];
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        if (literals.Length <= 1)
        {
            return false;
        }

        var width = literals[0].Length;
        if (width == 0)
        {
            return false;
        }
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            if (literal.Length != width) return false;
        }
        var expectedCount = 1;
        for (var offset = 0; offset < width; offset++)
        {
            budget.ChargeWork(1);
            var first = (byte)literals[0][offset];
            var folded = AsciiSearch.FoldCase(first);
            var sawLower = first == folded;
            var sawUpper = first != folded;

            for (var i = 1; i < literals.Length; i++)
            {
                budget.ChargeWork(1);
                var value = (byte)literals[i][offset];
                if (AsciiSearch.FoldCase(value) != folded)
                {
                    return false;
                }

                sawLower |= value == folded;
                sawUpper |= value != folded;
            }

            if (IsAsciiLetter(folded))
            {
                if (!(sawLower && sawUpper))
                {
                    return false;
                }

                expectedCount *= 2;
                if (expectedCount > literals.Length) return false;
            }
        }

        if (expectedCount != literals.Length)
        {
            return false;
        }

        // Validation needs no width-sized scratch. Acquire only the successful
        // folded payload; its loan transfers to the enclosing preparation owner.
        var foldedBytes = budget.AllocateArray<byte>(width, instructions: false);
        budget.ChargeWork((ulong)width);
        for (var offset = 0; offset < width; offset++)
            foldedBytes[offset] = AsciiSearch.FoldCase((byte)literals[0][offset]);
        literalUtf8 = foldedBytes;
        construction.Complete();
        return true;
    }

    private static bool IsAsciiLetter(byte value)
    {
        return (uint)(AsciiSearch.FoldCase(value) - (byte)'a') <= (byte)('z' - 'a');
    }

    private static bool HasSelectiveStructuralStartPlan(Utf8StructuralSearchPlan plan)
    {
        if (!(plan.HasValue && plan.YieldKind == Utf8StructuralSearchYieldKind.Start) ||
            plan.Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        foreach (var stage in stages)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (stage.Kind == Utf8StructuralSearchStageKind.TransformCandidateStart)
            {
                return true;
            }
        }

        return false;
    }
}
