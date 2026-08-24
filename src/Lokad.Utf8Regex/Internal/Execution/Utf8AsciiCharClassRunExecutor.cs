namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiCharClassRunExecutor
{
    public static bool IsMatch(ReadOnlySpan<byte> input, AsciiSimplePatternRunPlan runPlan, Utf8ExecutionDeadline budget)
    {
        return FindNext(input, runPlan, 0, out _, budget) >= 0;
    }

    public static int Count(ReadOnlySpan<byte> input, AsciiSimplePatternRunPlan runPlan, Utf8ExecutionDeadline budget)
    {
        if (!runPlan.HasValue)
        {
            return 0;
        }

        var charClass = runPlan.CharClass;

        if (runPlan.PredicateKind != AsciiCharClassPredicateKind.None)
        {
            return CountKnownPredicateRun(input, runPlan, budget);
        }

        var count = 0;
        var index = 0;
        while (index < input.Length)
        {
            budget.Step();
            var relative = runPlan.Search.IndexOf(input[index..]);
            if (relative < 0)
            {
                break;
            }

            var runStart = index + relative;
            var runEnd = runStart + 1;
            while (runEnd < input.Length && charClass.Contains(input[runEnd]))
            {
                runEnd++;
            }

            var remaining = runEnd - runStart;
            while (remaining >= runPlan.MinLength)
            {
                count++;
                remaining -= remaining > runPlan.MaxLength ? runPlan.MaxLength : remaining;
            }

            index = runEnd;
        }

        return count;
    }

    private static int CountKnownPredicateRun(ReadOnlySpan<byte> input, AsciiSimplePatternRunPlan runPlan, Utf8ExecutionDeadline budget)
    {
        var count = 0;
        var index = 0;
        var minLength = runPlan.MinLength;
        var maxLength = runPlan.MaxLength;
        var lookahead = Math.Min(minLength - 1, 2);

        while (index < input.Length)
        {
            budget.Step();
            var relative = runPlan.Search.IndexOf(input[index..]);
            if (relative < 0)
            {
                break;
            }

            var runStart = index + relative;
            if (input.Length - runStart < minLength)
            {
                break;
            }

            if (lookahead > 0 && !MatchesPredicatePrefix(input, runStart + 1, lookahead, runPlan.PredicateKind))
            {
                index = runStart + 1;
                continue;
            }

            var runEnd = runStart + 1 + lookahead;
            while (runEnd < input.Length && Utf8AsciiBytePredicates.MatchesKnownClass(input[runEnd], runPlan.PredicateKind))
            {
                runEnd++;
            }

            var remaining = runEnd - runStart;
            while (remaining >= minLength)
            {
                count++;
                remaining -= remaining > maxLength ? maxLength : remaining;
            }

            index = runEnd;
        }

        return count;
    }

    public static int FindNext(ReadOnlySpan<byte> input, AsciiSimplePatternRunPlan runPlan, int startIndex, out int matchedLength, Utf8ExecutionDeadline budget)
    {
        matchedLength = 0;
        if (!runPlan.HasValue || startIndex >= input.Length)
        {
            return -1;
        }

        var charClass = runPlan.CharClass;

        if (runPlan.PredicateKind != AsciiCharClassPredicateKind.None)
        {
            return FindNextKnownPredicateRun(input, runPlan, startIndex, out matchedLength, budget);
        }

        var index = startIndex;
        while (index < input.Length)
        {
            budget.Step();
            if (charClass.Contains(input[index]))
            {
                var remaining = 0;
                while (index + remaining < input.Length && charClass.Contains(input[index + remaining]))
                {
                    remaining++;
                }

                if (remaining >= runPlan.MinLength)
                {
                    matchedLength = remaining > runPlan.MaxLength ? runPlan.MaxLength : remaining;
                    return index;
                }

                index += remaining == 0 ? 1 : remaining;
                continue;
            }

            var relative = runPlan.Search.IndexOf(input[index..]);
            if (relative < 0)
            {
                return -1;
            }

            index += relative;
        }

        return -1;
    }

    private static int FindNextKnownPredicateRun(ReadOnlySpan<byte> input, AsciiSimplePatternRunPlan runPlan, int startIndex, out int matchedLength, Utf8ExecutionDeadline budget)
    {
        matchedLength = 0;
        var index = startIndex;
        var minLength = runPlan.MinLength;
        var maxLength = runPlan.MaxLength;
        var lookahead = Math.Min(minLength - 1, 2);

        while (index < input.Length)
        {
            budget.Step();
            var relative = runPlan.Search.IndexOf(input[index..]);
            if (relative < 0)
            {
                return -1;
            }

            var runStart = index + relative;
            if (input.Length - runStart < minLength)
            {
                return -1;
            }

            if (lookahead > 0 && !MatchesPredicatePrefix(input, runStart + 1, lookahead, runPlan.PredicateKind))
            {
                index = runStart + 1;
                continue;
            }

            var runEnd = runStart + 1 + lookahead;
            while (runEnd < input.Length && Utf8AsciiBytePredicates.MatchesKnownClass(input[runEnd], runPlan.PredicateKind))
            {
                runEnd++;
            }

            var remaining = runEnd - runStart;
            if (remaining >= minLength)
            {
                matchedLength = remaining > maxLength ? maxLength : remaining;
                return runStart;
            }

            index = runEnd;
        }

        return -1;
    }

    private static bool MatchesPredicatePrefix(ReadOnlySpan<byte> input, int startIndex, int length, AsciiCharClassPredicateKind predicateKind)
    {
        for (var i = 0; i < length; i++)
        {
            if (!Utf8AsciiBytePredicates.MatchesKnownClass(input[startIndex + i], predicateKind))
            {
                return false;
            }
        }

        return true;
    }
}
