using System.Globalization;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Immutable UTF-16 class subtraction chain. Each segment owns its
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
        var anyHalf = false;
        var everyHalf = true;
        for (var unit = 0xD800; unit <= 0xDFFF; unit++)
        {
            var matched = false;
            for (var segment = segments.Length - 1; segment >= 0; segment--)
                matched = SegmentMatches((char)unit, segments[segment]) && !matched;
            anyHalf |= matched;
            everyHalf &= matched;
        }
        HalvesVerdict = everyHalf ? 1 : anyHalf ? 2 : 0;
    }

    internal int HalvesVerdict { get; }

    internal static Utf8SliceNode Subtract(IReadOnlyList<Utf8SliceNode> operands)
    {
        static Segment FromNode(Utf8SliceNode node) => node switch
        {
            Utf8SliceNode.SetNode set => new Segment(set.Mask16, set.Members, [], set.Negated, set.FoldCase),
            Utf8SliceNode.CategorySetNode set => new Segment(set.Mask16, set.Members, set.Categories, set.Negated, set.FoldCase),
            Utf8SliceNode.ShorthandNode shorthand => new Segment(new byte[16], [shorthand.Shorthand], [], false, false),
            Utf8SliceNode.CategoryNode category => new Segment(new byte[16], [],
                [new Utf8SliceNode.CategorySetMember(new Utf8SliceCategoryTable(category.Mask, category.Ranges), category.Negated)], false, category.FoldCase),
            _ => throw new ArgumentOutOfRangeException(nameof(node), "Class subtraction requires class operands."),
        };

        var segments = new Segment[operands.Count];
        for (var index = 0; index < operands.Count; index++)
            segments[index] = FromNode(operands[index]);

        var program = new Utf8SliceClassProgram(segments);
        return new Utf8SliceNode.CategorySetNode(new byte[16], false, [],
            [new Utf8SliceNode.CategorySetMember(new Utf8SliceCategoryTable(program), false)], false);
    }

    internal bool TryMatch(char unit, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        var matched = false;
        for (var segment = _segments.Length - 1; segment >= 0; segment--)
        {
            breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            matched = SegmentMatches(unit, _segments[segment]) && !matched;
        }
        breach = Utf8ExecutionLimitKind.None;
        return matched;
    }

    private static bool SegmentMatches(char unit, Segment segment)
    {
        static bool MaskHit(char value, byte[] mask) => value < 128 &&
            (mask[value >> 3] & (1 << (value & 7))) != 0;

        var hit = MaskHit(unit, segment.Mask16);
        if (!hit && segment.FoldCase)
        {
            foreach (var equivalent in Utf8SliceCaseEquivalences.GetInvariantEquivalents(unit))
            {
                if (MaskHit(equivalent, segment.Mask16)) { hit = true; break; }
            }
        }

        if (!hit)
        {
            foreach (var shorthand in segment.Members)
            {
                var positive = (int)shorthand >> 1;
                var memberHit = positive switch
                {
                    0 => char.GetUnicodeCategory(unit) == UnicodeCategory.DecimalDigitNumber,
                    1 => Utf8OwnedCharClass.IsWordChar(unit),
                    _ => char.IsWhiteSpace(unit),
                };
                if (memberHit != (((int)shorthand & 1) != 0)) { hit = true; break; }
            }
        }

        if (!hit)
        {
            foreach (var member in segment.Categories)
            {
                var category = char.GetUnicodeCategory(unit);
                var maskHit = segment.FoldCase && category is UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter
                    ? (member.Table.Mask & Utf8SliceCategories.CasedLetterMask) != 0
                    : ((member.Table.Mask >> (int)category) & 1u) != 0;
                bool memberHit;
                if (maskHit)
                {
                    memberHit = !member.Negated;
                }
                else
                {
                    var rangeHit = Utf8SliceCaseEquivalences.RangesHit(member.Table.Ranges, unit);
                    memberHit = rangeHit != member.Negated;
                    if (segment.FoldCase && member.Table.Ranges.Length != 0)
                        memberHit |= Utf8SliceCaseEquivalences.EquivalentsHitRanges(unit, member.Table.Ranges, member.Negated);
                }
                if (memberHit) { hit = true; break; }
            }
        }

        return hit != segment.Negated;
    }

    private sealed record Segment(byte[] Mask16, Utf8SliceShorthand[] Members,
        Utf8SliceNode.CategorySetMember[] Categories, bool Negated, bool FoldCase);
}
