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

        var prefilter = PreparedMultiLiteralCandidatePrefilter.CreatePackedNibbleSimd(literals);
        if (!prefilter.HasValue)
        {
            return false;
        }

        strategy = new Utf8PackedNibbleLiteralFamilyCountStrategy(prefilter);
        return true;
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
