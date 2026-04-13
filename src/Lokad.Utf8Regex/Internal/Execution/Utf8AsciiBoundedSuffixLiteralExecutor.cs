using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiBoundedSuffixLiteralExecutor
{
    public static bool IsMatch(ReadOnlySpan<byte> input, AsciiSimplePatternBoundedSuffixLiteralPlan plan, Utf8ExecutionBudget? budget)
    {
        return FindNext(input, plan, 0, out _, out _, budget) >= 0;
    }

    public static int Count(ReadOnlySpan<byte> input, AsciiSimplePatternBoundedSuffixLiteralPlan plan, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_bounded_suffix_literal");
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, plan, startIndex, out var matchedLength, out _, budget);
            if (matchIndex < 0)
            {
                break;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, AsciiSimplePatternBoundedSuffixLiteralPlan plan, Utf8ExecutionBudget? budget)
    {
        var index = FindNext(input, plan, 0, out var matchedLength, out _, budget);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
    }

    public static int FindNext(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternBoundedSuffixLiteralPlan plan,
        int startIndex,
        out int matchedLength,
        out int literalIndex,
        Utf8ExecutionBudget? budget)
    {
        matchedLength = 0;
        literalIndex = -1;

        if (!plan.HasValue || startIndex < 0 || startIndex >= input.Length)
        {
            return -1;
        }

        var literal = plan.LiteralUtf8;
        if (literal.Length == 0 || input.Length < literal.Length + 1)
        {
            return -1;
        }

        var searchFrom = Math.Max(startIndex, 0);
        var latestLiteralEnd = input.Length - 2;
        var literalSuffixOffset = literal.Length - 1;
        var candidateSearchStart = Math.Max(searchFrom + literalSuffixOffset, literalSuffixOffset);
        while (candidateSearchStart <= latestLiteralEnd)
        {
            var relative = input[candidateSearchStart..].IndexOf(plan.LiteralLastByte);
            if (relative < 0)
            {
                return -1;
            }

            var literalEndIndex = candidateSearchStart + relative;
            candidateSearchStart = literalEndIndex + 1;
            if (!plan.SuffixCharClass!.Contains(input[literalEndIndex + 1]))
            {
                continue;
            }

            var candidateLiteralIndex = literalEndIndex - literalSuffixOffset;
            if (candidateLiteralIndex < searchFrom ||
                !input.Slice(candidateLiteralIndex, literal.Length).SequenceEqual(literal))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            budget?.Step(input);

            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!TryMatchAt(input, plan, candidateLiteralIndex, startIndex, out var candidateStart, out matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            literalIndex = candidateLiteralIndex;
            return candidateStart;
        }

        return -1;
    }

    private static bool TryMatchAt(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternBoundedSuffixLiteralPlan plan,
        int literalIndex,
        int minStartIndex,
        out int matchStart,
        out int matchedLength)
    {
        matchStart = -1;
        matchedLength = 0;

        var index = literalIndex - 1;
        var repeatedCount = 0;
        while (index >= minStartIndex &&
            repeatedCount < plan.RepeatedMaxLength &&
            plan.RepeatedCharClass!.Contains(input[index]))
        {
            repeatedCount++;
            index--;
        }

        if (repeatedCount < plan.RepeatedMinLength ||
            index < minStartIndex ||
            !plan.PrefixCharClass!.Contains(input[index]))
        {
            return false;
        }

        matchStart = index;
        matchedLength = plan.LiteralUtf8.Length + repeatedCount + 2;
        return true;
    }
}
