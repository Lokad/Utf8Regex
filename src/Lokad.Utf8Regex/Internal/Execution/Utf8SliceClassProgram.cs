using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Immutable scalar class subtraction chain. Each segment owns its
/// union, folding and class negation; subtraction is applied from the innermost
/// segment outward. Matching allocates no state and charges the shared invocation
/// once per segment plus bounded blocks of long predicate scans. Construction
/// flattens the complete chain once; execution
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
            Utf8SliceNode.SetNode set => new Segment(set.Mask16, MemberBits(set.Members), [], set.Negated, set.FoldCase),
            Utf8SliceNode.CategorySetNode set => new Segment(set.Mask16, MemberBits(set.Members), set.Categories, set.Negated, set.FoldCase),
            _ => throw new ArgumentOutOfRangeException(nameof(node), "Class subtraction requires class operands."),
        };

        static int MemberBits(ReadOnlySpan<Utf8SliceShorthand> members)
        {
            var bits = 0;
            foreach (var member in members)
            {
                Utf8ConstructionScope.ChargeWork(1);
                bits |= 1 << (int)member;
            }
            return bits;
        }

        var segments = arena.Allocate<Segment>(operands.Count, true);
        arena.Budget.ChargeWork((ulong)operands.Count);
        for (var index = 0; index < operands.Count; index++)
            segments[index] = FromNode(operands[index]);

        var program = arena.Budget.AllocateObject(segments, static state => new Utf8SliceClassProgram(state));
        var categories = arena.Allocate<Utf8SliceNode.CategorySetMember>(1, true);
        categories[0] = Utf8SliceNode.CategorySetMember.Create(Utf8SliceCategoryTable.Create(program), false);
        return new Utf8SliceNode.CategorySetNode(arena.Allocate<byte>(16, true), false, [], categories, false);
    }

    internal bool TryMatch(Rune scalar, Utf8ScalarCaseBehavior behavior, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        var matched = false;
        for (var segment = _segments.Length - 1; segment >= 0; segment--)
        {
            breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            var segmentHit = SegmentMatches(scalar, _segments[segment], behavior, ref invocation, out breach);
            if (breach != Utf8ExecutionLimitKind.None) return false;
            matched = segmentHit && !matched;
        }
        breach = Utf8ExecutionLimitKind.None;
        return matched;
    }

    private static bool SegmentMatches(Rune scalar, in Segment segment, Utf8ScalarCaseBehavior behavior,
        ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        var hit = Utf8SliceScalarClasses.MatchesMask(scalar, segment.Mask16, segment.FoldCase, behavior) ||
            Utf8SliceScalarClasses.MatchesMembers(scalar, segment.ShorthandBits);

        if (!hit)
        {
            hit = Utf8SliceScalarClasses.MatchesTables(scalar, segment.Categories, segment.FoldCase,
                behavior, ref invocation, out breach);
            if (breach != Utf8ExecutionLimitKind.None) return false;
        }

        return hit != segment.Negated;
    }

    // The complete segment field state lives inline in the admitted array.
    // A reference array would leave one unadmitted heap object per operand.
    private readonly record struct Segment(byte[] Mask16, int ShorthandBits,
        Utf8SliceNode.CategorySetMember[] Categories, bool Negated, bool FoldCase);
}
