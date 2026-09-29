namespace Lokad.Utf8Regex.Internal.Execution;

// R05: BCL tests astral halves (lone surrogates, category Cs) while owned
// walkers consume whole UTF-8 scalars, so routing must know exactly how each
// atom meets halves. Halves have no case and never hit ASCII masks, hence
// folding and ASCII members never enter the verdicts below. Verdicts are
// 0 = misses both halves, 1 = matches both halves, 2 = splits halves (lead
// and trail differ, e.g. surrogate blocks); only uniform verdicts admit
// whole-loop indifference, since split halves desynchronize iteration.
// A verdict of 0 coincides with PositiveTableMissesHalves for tables.
internal static class Utf8SliceHalvesMirror
{
    internal static int TableHalvesVerdict(uint mask, ushort[] ranges)
    {
        if ((mask & (1u << (int)System.Globalization.UnicodeCategory.Surrogate)) != 0)
        {
            return 1;
        }

        var leadFull = false;
        var trailFull = false;
        var leadPart = false;
        var trailPart = false;
        for (var range = 0; range + 1 < ranges.Length; range += 2)
        {
            var start = ranges[range];
            var end = ranges[range + 1];
            if (start < 0xDC00 && end > 0xD800)
            {
                if (start <= 0xD800 && end >= 0xDC00)
                {
                    leadFull = true;
                }
                else
                {
                    leadPart = true;
                }
            }

            if (start < 0xE000 && end > 0xDC00)
            {
                if (start <= 0xDC00 && end >= 0xE000)
                {
                    trailFull = true;
                }
                else
                {
                    trailPart = true;
                }
            }
        }

        if ((ranges.Length & 1) != 0)
        {
            var last = ranges[ranges.Length - 1];
            if (last <= 0xD800)
            {
                leadFull = true;
                trailFull = true;
            }
            else if (last < 0xDC00)
            {
                leadPart = true;
                trailFull = true;
            }
            else if (last < 0xE000)
            {
                trailPart = true;
            }
        }

        if (leadFull && trailFull)
        {
            return 1;
        }

        if (!leadFull && !trailFull && !leadPart && !trailPart)
        {
            return 0;
        }

        return 2;
    }

    internal static int CategorySetHalvesVerdict(int shorthandBits, Utf8SliceNode.CategorySetMember[] members)
    {
        var verdict = 0;
        for (var bit = 0; bit < 6; bit++)
        {
            // Even bits are positive shorthands (digit, word, space) whose
            // halves miss; odd bits are their negations, which halves match.
            if (((shorthandBits >> bit) & 1) != 0 && (bit & 1) != 0)
            {
                verdict = 1;
            }
        }

        foreach (var member in members)
        {
            var table = TableHalvesVerdict(member.Table.Mask, member.Table.Ranges);
            if (member.Negated)
            {
                table = table == 2 ? 2 : 1 - table;
            }

            if (table > verdict)
            {
                verdict = table;
            }
        }

        return verdict;
    }
}
