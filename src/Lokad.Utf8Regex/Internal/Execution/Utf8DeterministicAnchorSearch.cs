using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.FrontEnd;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8DeterministicAnchorSearch(PreparedSearcher Searcher, int Offset)
{
    private const int MaxDeterministicAnchorBranches = 32;

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
        if (tree is null) return default;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        using var frames = Utf8ConstructionFrames<Utf8ExecutionNode>.Create(budget, tree.Root);
        var literals = Utf8ExecutionPrefixFamilies.Find(tree.Root);
        Utf8DeterministicAnchorSearch result;
        if (searchPlan.Kind == Utf8SearchKind.FixedDistanceAsciiLiteral &&
            searchPlan.LiteralUtf8 is { Length: > 0 } fixedLiteral && literals is { Length: 1 } &&
            Covers(literals[0], fixedLiteral, searchPlan.Distance))
            result = new(new PreparedSearcher(new PreparedSubstringSearch(fixedLiteral, false), false), searchPlan.Distance);
        else if (literals is { Length: > 1 })
        {
            if (TryCreateAsciiIgnoreCaseAnchorLiteral(literals, out var folded))
                result = new(new PreparedSearcher(new PreparedSubstringSearch(folded, true), true), 0);
            else
            {
                var bytes = budget.AllocateArray<byte[]>(literals.Length, false);
                for (var index = 0; index < literals.Length; index++) { budget.ChargeWork(1); bytes[index] = budget.EncodeUtf8(literals[index]); }
                result = new(new PreparedSearcher(new PreparedMultiLiteralSearch(bytes, false)), 0);
            }
        }
        else if (TryGetLeadingAsciiByteMask(tree.Root, out var low, out var high))
        {
            var mask = new AsciiCharClass(low, high);
            var values = budget.AllocateArray<byte>(mask.Count, false);
            budget.ChargeWork(128); mask.CopyMatchBytes(values);
            result = new(new PreparedSearcher(PreparedByteSearch.Create(values)), 0);
        }
        else return default;
        scope.Complete();
        return result;

        bool Covers(string prefix, ReadOnlySpan<byte> literal, int distance)
        {
            if (distance < 0 || (long)distance + literal.Length > prefix.Length) return false;
            for (var index = 0; index < literal.Length; index++)
            { budget.ChargeWork(1); if (prefix[distance + index] != literal[index]) return false; }
            return true;
        }
    }

    private static bool TryGetLeadingAsciiByteMask(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
    {
        var proved = Utf8ExecutionPrefixFacts.FirstMask(node, out var mask);
        lowMask = mask.ActualLowMask; highMask = mask.ActualHighMask;
        return proved;
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
                        if (node.Ch > 0x7F || Utf8ExecutionPrefixFacts.Folded(node))
                        {
                            return false;
                        }

                        break;

                    case Utf8ExecutionNodeKind.Multi:
                        if (node.Text is null || !IsAsciiText(node.Text) || Utf8ExecutionPrefixFacts.Folded(node))
                        {
                            return false;
                        }

                        break;

                    case Utf8ExecutionNodeKind.Set:
                        if (Utf8ExecutionPrefixFacts.Folded(node) && !Utf8ExecutionPrefixFacts.ClassMask(node, out _)) return false;
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
        using var scope = Utf8ConstructionScope.Enter();
        bytes = scope.Budget.AllocateArray<byte>(text.Length, false);
        scope.Budget.ChargeWork((ulong)text.Length);
        for (var index = 0; index < text.Length; index++) bytes[index] = (byte)text[index];
        scope.Complete();
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

    private static bool TryCreateAsciiIgnoreCaseAnchorLiteral(string[] literals, out byte[] literalUtf8)
    {
        literalUtf8 = [];
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        if (literals.Length <= 1 || literals.Length > MaxDeterministicAnchorBranches)
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
        // Count and per-position case coverage imply the complete Cartesian
        // family only when every member is distinct. Compare borrowed text;
        // no validation scratch may precede the successful payload admission.
        for (var left = 0; left < literals.Length; left++)
        for (var right = left + 1; right < literals.Length; right++)
        {
            var equal = true;
            for (var offset = 0; offset < width; offset++)
            {
                budget.ChargeWork(1);
                if (literals[left][offset] == literals[right][offset]) continue;
                equal = false; break;
            }
            if (equal) return false;
        }
        var expectedCount = 1;
        for (var offset = 0; offset < width; offset++)
        {
            budget.ChargeWork(1);
            if (literals[0][offset] > 127) return false;
            var first = (byte)literals[0][offset];
            var folded = AsciiSearch.FoldCase(first);
            var sawLower = first == folded;
            var sawUpper = first != folded;

            for (var i = 1; i < literals.Length; i++)
            {
                budget.ChargeWork(1);
                if (literals[i][offset] > 127) return false;
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
