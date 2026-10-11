using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Caching;

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

    internal void CollectStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage, Utf8ConstructionBuffer<object> pending)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ValueTuple<Segment[]>>())) return;
        storage.Array(_segments);
        foreach (ref readonly var segment in _segments.AsSpan())
        {
            storage.Array(segment.Mask16);
            storage.Array(segment.Members);
            storage.Array(segment.Categories);
            foreach (var member in segment.Categories) { storage.Check(); pending.Push(member); }
        }
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
            matched = SegmentMatches(scalar, _segments[segment], behavior) && !matched;
        }
        breach = Utf8ExecutionLimitKind.None;
        return matched;
    }

    private static bool SegmentMatches(Rune scalar, in Segment segment, Utf8ScalarCaseBehavior behavior)
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

    // The complete segment field state lives inline in the admitted array.
    // A reference array would leave one unadmitted heap object per operand.
    private readonly record struct Segment(byte[] Mask16, Utf8SliceShorthand[] Members,
        Utf8SliceNode.CategorySetMember[] Categories, bool Negated, bool FoldCase);
}
