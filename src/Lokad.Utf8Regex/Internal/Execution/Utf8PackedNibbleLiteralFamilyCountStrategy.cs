using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8PackedNibbleLiteralFamilyCountStrategy
{
    private readonly PreparedMultiLiteralCandidatePrefilter _prefilter;

    private Utf8PackedNibbleLiteralFamilyCountStrategy(PreparedMultiLiteralCandidatePrefilter prefilter)
    {
        _prefilter = prefilter;
    }

    public static bool TryCreate(
        Utf8PreparedRegex preparedRegex,
        RegexOptions options,
        TimeSpan matchTimeout,
        out Utf8PackedNibbleLiteralFamilyCountStrategy? strategy)
    {
        strategy = null;
        var searchPlan = preparedRegex.SearchPlan;
        if (preparedRegex.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            (options & RegexOptions.RightToLeft) != 0 ||
            matchTimeout != Regex.InfiniteMatchTimeout ||
            searchPlan.HasBoundaryRequirements ||
            searchPlan.HasTrailingLiteralRequirement ||
            searchPlan.AlternateLiteralsUtf8 is not { Length: >= 2 and <= 8 } literals ||
            !HasThreeByteLiteralPrefix(literals))
        {
            return false;
        }

        strategy = Create(literals);
        return strategy is not null;
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (_prefilter.TryFindNextCandidate(input, ref state, out var candidateIndex))
        {
            if (!_prefilter.TryGetMatchedLength(input, candidateIndex, out var matchedLength))
            {
                continue;
            }

            count++;
            state = new PreparedMultiLiteralScanState(candidateIndex + matchedLength, 0, 0);
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.LiteralFamilyPackedNibbleSimdCount);
        return count;
    }

    internal static Utf8PackedNibbleLiteralFamilyCountStrategy? CreateBoundaryCounter(byte[][] literals)
    {
        if (literals.Length is < 5 or > 8)
            return null;
        foreach (var literal in literals)
        {
            if (literal.Length is < 3 or > 8)
                return null;
        }

        return Create(literals);
    }

    internal Utf8ExecutionLimitKind TryCountWithBoundaries(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out int count)
    {
        count = 0;
        var total = 0;
        var nextStart = 0;
        // The admitted block includes the longest possible overlap. Each
        // candidate start belongs to one block, even across a match.
        var lookahead = _prefilter.LongestLength - 1;
        while (nextStart <= input.Length - _prefilter.ShortestLength)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var first = nextStart;
            var block = input.Slice(first, Math.Min(2048, input.Length - first));
            var starts = first + block.Length == input.Length ? block.Length : block.Length - lookahead;
            var state = default(PreparedMultiLiteralScanState);
            while (_prefilter.TryFindNextCandidate(block, ref state, out var relative) && relative < starts)
            {
                var at = first + relative;
                if (at < nextStart) continue;
                // At most eight short comparisons and their boundary predicates.
                // Only an exact hit proves a scalar boundary at the candidate.
                breach = invocation.TryChargeBoundedCheck();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var remaining = block[relative..];
                foreach (var literal in _prefilter.Literals)
                {
                    if (!remaining.StartsWith(literal))
                        continue;
                    if (!Utf8ScalarWordBoundary.IsBoundary(input, at))
                        break;
                    if (!Utf8ScalarWordBoundary.IsBoundary(input, at + literal.Length))
                        continue;
                    total++;
                    nextStart = at + literal.Length;
                    break;
                }
            }
            nextStart = Math.Max(nextStart, first + starts);
        }

        var outcome = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (outcome == Utf8ExecutionLimitKind.None)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.LiteralFamilyPackedNibbleSimdCount);
            count = total;
        }
        return outcome;
    }

    private static Utf8PackedNibbleLiteralFamilyCountStrategy? Create(byte[][] literals)
    {
        var prefilter = PreparedMultiLiteralCandidatePrefilter.CreatePackedNibbleSimd(literals);
        return prefilter.HasValue
            ? Utf8ConstructionScope.CurrentBudget.AllocateObject(prefilter,
                static state => new Utf8PackedNibbleLiteralFamilyCountStrategy(state))
            : null;
    }

    private static bool HasThreeByteLiteralPrefix(ReadOnlySpan<byte[]> literals)
    {
        foreach (var literal in literals)
        {
            if (literal.Length < 9 ||
                literal[0] is not (>= 0xE0 and < 0xF0) ||
                literal[1] is not (>= 0x80 and < 0xC0) ||
                literal[2] is not (>= 0x80 and < 0xC0) ||
                literal[3] is not (>= 0xE0 and < 0xF0) ||
                literal[4] is not (>= 0x80 and < 0xC0) ||
                literal[5] is not (>= 0x80 and < 0xC0) ||
                literal[6] is not (>= 0xE0 and < 0xF0) ||
                literal[7] is not (>= 0x80 and < 0xC0) ||
                literal[8] is not (>= 0x80 and < 0xC0))
            {
                return false;
            }
        }

        return true;
    }
}
