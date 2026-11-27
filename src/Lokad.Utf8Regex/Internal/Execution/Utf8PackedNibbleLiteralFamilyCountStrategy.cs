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
        => ScanWithBoundaries(input, 0, input.Length, firstOnly: false, ref invocation, out count, out _);

    internal Utf8ExecutionLimitKind TryFindWithBoundaries(ReadOnlySpan<byte> input, int start, int end,
        ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
        var breach = ScanWithBoundaries(input, start, end, firstOnly: true, ref invocation, out var count, out match);
        found = count != 0;
        return breach;
    }

    private Utf8ExecutionLimitKind ScanWithBoundaries(ReadOnlySpan<byte> input, int start, int end, bool firstOnly,
        ref Utf8SliceInvocation invocation, out int count, out PreparedSearchMatch match)
    {
        count = 0;
        match = default;
        var total = 0;
        var nextStart = start;
        // The admitted block includes the longest possible overlap. Each
        // candidate start belongs to one block, even across a match.
        var lookahead = _prefilter.LongestLength - 1;
        while (nextStart <= end - _prefilter.ShortestLength)
        {
            var breach = invocation.CheckInput();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var first = nextStart;
            var block = input.Slice(first, Math.Min(2048, end - first));
            var starts = first + block.Length == end ? block.Length : block.Length - lookahead;
            var state = default(PreparedMultiLiteralScanState);
            while (_prefilter.TryFindNextCandidate(block, ref state, out var relative) && relative < starts)
            {
                var at = first + relative;
                if (at < nextStart) continue;
                // At most eight short comparisons and their boundary predicates.
                // Only an exact hit proves a scalar boundary at the candidate.
                breach = invocation.CheckBoundedProgress();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var remaining = block[relative..];
                var literals = _prefilter.Literals;
                for (var ordinal = 0; ordinal < literals.Length; ordinal++)
                {
                    var literal = literals[ordinal];
                    if (!remaining.StartsWith(literal))
                        continue;
                    if (!Utf8ScalarWordBoundary.IsBoundary(input, at))
                        break;
                    if (!Utf8ScalarWordBoundary.IsBoundary(input, at + literal.Length))
                        continue;
                    total++;
                    if (firstOnly)
                    {
                        breach = invocation.CheckInput();
                        invocation.Budget.ThrowIfCancellationRequested();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        match = new(at, literal.Length, ordinal);
                        count = 1;
                        Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                        return Utf8ExecutionLimitKind.None;
                    }
                    nextStart = at + literal.Length;
                    break;
                }
            }
            nextStart = Math.Max(nextStart, first + starts);
        }

        var outcome = invocation.CheckInput();
        invocation.Budget.ThrowIfCancellationRequested();
        if (outcome == Utf8ExecutionLimitKind.None)
        {
            if (!firstOnly)
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
