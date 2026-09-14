using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: UTF-8 backend engines, split from the Python facade along the backend ownership boundary.
/// <summary>UTF-8 backend engines for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    private Utf8Regex? GetUtf8FullRegex() => _lazyUtf8FullRegex?.Value;

    private Utf8Regex? CreateUtf8FullRegex()
    {
        try
        {
            var regex = new Utf8Regex(
                @"\G(?:" + _translation.Pattern + @")\z",
                _translation.RegexOptions,
                MatchTimeout);
            return regex.Inspection.ExecutionKind != NativeExecutionKind.FallbackRegex ? regex : null;
        }
        catch (Exception exception) when (IsOptionalUtf8BackendUnavailableException(exception))
        {
            return null;
        }
    }

    private bool TryFindAllViaUtf8Regex(ReadOnlySpan<byte> input, int startOffsetInBytes, out Utf8PythonMatchData[] matches)
    {
        if (!TryCollectUtf8MatchRanges(input, startOffsetInBytes, out var ranges))
        {
            matches = [];
            return false;
        }

        try
        {
            matches = new Utf8PythonMatchData[ranges.Count];
            for (var i = 0; i < matches.Length; i++)
            {
                var range = ranges[i];
                matches[i] = new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = range.IndexInBytes,
                    EndOffsetInBytes = range.IndexInBytes + range.LengthInBytes,
                    StartOffsetInUtf16 = range.IndexInUtf16,
                    EndOffsetInUtf16 = range.IndexInUtf16 + range.LengthInUtf16,
                    ValueText = Encoding.UTF8.GetString(input.Slice(range.IndexInBytes, range.LengthInBytes)),
                };
            }

            return true;
        }
        finally
        {
            ranges.Dispose();
        }
    }

    private bool TryCollectUtf8MatchRanges(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        out PythonReUtf8MatchRangeBuffer ranges)
    {
        if (_findAllBackend != PythonReDirectBackendKind.Utf8Regex || _utf8Regex is null || _hasLookbehind)
        {
            ranges = default;
            return false;
        }

        ranges = default;
        try
        {
            var enumerator = startOffsetInBytes == 0
                ? _utf8Regex.EnumerateMatches(input)
                : _utf8Regex.EnumerateMatchesFromUtf16Offset(input, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));

            foreach (var match in enumerator)
            {
                if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
                {
                    ranges.Dispose();
                    ranges = default;
                    return false;
                }

                ranges.Add(new PythonReUtf8MatchRange(
                    indexInBytes,
                    lengthInBytes,
                    match.IndexInUtf16,
                    match.LengthInUtf16));
            }

            return true;
        }
        catch
        {
            ranges.Dispose();
            ranges = default;
            throw;
        }
    }

    private bool TryReplaceViaUtf8Literal(
        ReadOnlySpan<byte> input,
        PythonReReplacementPlan plan,
        int count,
        int startOffsetInBytes,
        out byte[] resultBytes,
        out int replacementCount)
    {
        if (count < 0 ||
            startOffsetInBytes != 0 ||
            !_canUseZeroOffsetUtf8ValueFastPath ||
            _utf8Regex is null ||
            !plan.TryGetLiteralText(out var literalText))
        {
            resultBytes = [];
            replacementCount = 0;
            return false;
        }

        byte[] literalBytes;
        try
        {
            literalBytes = s_strictUtf8Encoding.GetBytes(literalText);
        }
        catch (EncoderFallbackException)
        {
            resultBytes = [];
            replacementCount = 0;
            return false;
        }

        var maximumReplacementCount = count == 0 ? int.MaxValue : count;
        resultBytes = _utf8Regex.ReplaceLiteralWithCount(
            input,
            literalBytes,
            maximumReplacementCount,
            out replacementCount);
        return true;
    }
}
