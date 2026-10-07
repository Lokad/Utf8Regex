using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Immutable scalar class subtraction chain. Each segment owns its
/// union, folding and class negation; subtraction is applied from the innermost
/// segment outward. Matching allocates no state and charges the shared invocation
/// once per segment. Construction flattens the complete chain once; execution
/// uses no nested call frames.</summary>
internal sealed class Utf8SliceClassProgram
{
    private readonly Segment[] _segments;

    private Utf8SliceClassProgram(Segment[] segments)
    {
        _segments = segments;
    }

    internal static Utf8SliceNode Subtract(Utf8ConstructionBuffer<Utf8SliceNode> operands, Utf8ConstructionArena arena)
    {
        static Segment FromNode(Utf8SliceNode node) => node switch
        {
            Utf8SliceNode.SetNode set => new Segment(set.Mask16, set.Members, [], set.Negated, set.FoldCase),
            Utf8SliceNode.CategorySetNode set => new Segment(set.Mask16, set.Members, set.Categories, set.Negated, set.FoldCase),
            _ => throw new ArgumentOutOfRangeException(nameof(node), "Class subtraction requires class operands."),
        };

        var segments = arena.Allocate<Segment>(operands.Count, true);
        for (var index = 0; index < operands.Count; index++)
            segments[index] = FromNode(operands[index]);

        var program = new Utf8SliceClassProgram(segments);
        var categories = arena.Allocate<Utf8SliceNode.CategorySetMember>(1, true);
        categories[0] = new Utf8SliceNode.CategorySetMember(new Utf8SliceCategoryTable(program), false);
        return new Utf8SliceNode.CategorySetNode(arena.Allocate<byte>(16, true), false, [], categories, false);
    }

    internal bool TryMatch(Rune scalar, Utf8ScalarCaseBehavior behavior, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        var matched = false;
        for (var segment = _segments.Length - 1; segment >= 0; segment--)
        {
            breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            matched = SegmentMatches(scalar, _segments[segment], behavior) && !matched;
        }
        breach = Utf8ExecutionLimitKind.None;
        return matched;
    }

    private static bool SegmentMatches(Rune scalar, Segment segment, Utf8ScalarCaseBehavior behavior)
    {
        var hit = Utf8SliceScalarClasses.MatchesMask(scalar, segment.Mask16, segment.FoldCase, behavior);

        if (!hit)
        {
            foreach (var shorthand in segment.Members)
            {
                if (Utf8SliceScalarClasses.MatchesShorthand(scalar, shorthand)) { hit = true; break; }
            }
        }

        if (!hit)
        {
            foreach (var member in segment.Categories)
            {
                if (Utf8SliceScalarClasses.MatchesTable(scalar, member.Table, segment.FoldCase, member.Negated, behavior))
                { hit = true; break; }
            }
        }

        return hit != segment.Negated;
    }

    private sealed record Segment(byte[] Mask16, Utf8SliceShorthand[] Members,
        Utf8SliceNode.CategorySetMember[] Categories, bool Negated, bool FoldCase);
}
