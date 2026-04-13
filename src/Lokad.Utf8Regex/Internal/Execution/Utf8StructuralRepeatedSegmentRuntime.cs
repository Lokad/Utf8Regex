using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8StructuralRepeatedSegmentRuntime
{
    private readonly AsciiStructuralRepeatedSegmentPlan _plan;

    public Utf8StructuralRepeatedSegmentRuntime(AsciiStructuralRepeatedSegmentPlan plan)
    {
        _plan = plan;
    }

    public bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return FindNext(input, 0, budget, out _) >= 0;
    }

    public int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_structural_linear_automaton");
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                break;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        var index = FindNext(input, 0, budget, out var matchedLength);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
    }

    public bool TryFindNext(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchIndex, out int matchedLength)
    {
        matchIndex = FindNext(input, startIndex, budget, out matchedLength);
        return matchIndex >= 0;
    }

    private int FindNext(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        matchedLength = 0;
        var candidateStart = startIndex;
        while (candidateStart < input.Length)
        {
            while (candidateStart < input.Length && !_plan.LeadingCharClass!.Contains(input[candidateStart]))
            {
                candidateStart++;
            }

            if (candidateStart >= input.Length)
            {
                return -1;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (TryMatchAt(input, candidateStart, out matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return candidateStart;
            }

            candidateStart++;
        }

        return -1;
    }

    private bool TryMatchAt(ReadOnlySpan<byte> input, int startIndex, out int matchedLength)
    {
        matchedLength = 0;
        var index = startIndex;
        var segments = 0;
        while (segments < _plan.RepetitionMaxCount && TryConsumeSegment(input, ref index))
        {
            segments++;
        }

        if (segments < _plan.RepetitionMinCount)
        {
            return false;
        }

        matchedLength = index - startIndex;
        return true;
    }

    private bool TryConsumeSegment(ReadOnlySpan<byte> input, ref int index)
    {
        if ((uint)index >= (uint)input.Length || !_plan.LeadingCharClass!.Contains(input[index]))
        {
            return false;
        }

        index++;

        var tailCount = 0;
        while ((uint)index < (uint)input.Length && _plan.TrailingCharClass!.Contains(input[index]))
        {
            index++;
            tailCount++;
        }

        if (tailCount < _plan.TrailingMinCount)
        {
            return false;
        }

        var separatorCount = 0;
        while ((uint)index < (uint)input.Length && _plan.MatchesSeparator(input[index]))
        {
            index++;
            separatorCount++;
        }

        return separatorCount >= _plan.SeparatorMinCount;
    }
}
