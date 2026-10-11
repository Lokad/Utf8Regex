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
    private bool TryFindAllViaUtf8Regex(ReadOnlySpan<byte> input, int startOffsetInBytes, out Utf8PythonMatchData[] matches)
    {
        if (!TryCollectUtf8MatchRanges(input, startOffsetInBytes, out var ranges))
        {
            matches = [];
            return false;
        }

        try
        {
            var completed = ranges.AllocateResultArray<Utf8PythonMatchData>(ranges.Count);
            for (var i = 0; i < completed.Length; i++)
            {
                var range = ranges[i];
                completed[i] = new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = range.IndexInBytes,
                    EndOffsetInBytes = range.IndexInBytes + range.LengthInBytes,
                    StartOffsetInUtf16 = range.IndexInUtf16,
                    EndOffsetInUtf16 = range.IndexInUtf16 + range.LengthInUtf16,
                    ValueText = ranges.GetText(input, range),
                };
            }
            ranges.Checkpoint();
            matches = completed;
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), Pattern, MatchTimeout);
        }
        finally
        {
            ranges.Dispose();
        }
    }

    private bool TryFindAllCanonicalLiteral(ReadOnlySpan<byte> input, int startOffsetInBytes,
        out Utf8PythonFindAllResult result)
    {
        result = default;
        if (_repeatedExactFindAllString is not { } canonical || _utf8Regex is null) return false;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, CancellationToken.None);
        Utf8InvocationResultLedger ledger = default;
        try
        {
            var count = _utf8Regex.CountForResult(input, startOffsetInBytes, ref invocation);
            var control = new Utf8InvocationBufferControl(ref invocation);
            var values = ledger.AllocateArray<string, Utf8InvocationBufferControl>(count, control);
            for (var offset = 0; offset < values.Length;)
            {
                Utf8InvocationOutput.Checkpoint(control);
                var length = Math.Min(256, values.Length - offset);
                values.AsSpan(offset, length).Fill(canonical);
                offset += length;
            }
            Utf8InvocationOutput.Checkpoint(control);
            result = new Utf8PythonFindAllResult { Shape = Utf8PythonFindAllShape.FullMatch, ScalarValues = values, TupleValues = [] };
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), Pattern, MatchTimeout);
        }
        finally { ledger.Release(new Utf8InvocationBufferControl(ref invocation)); }
    }

    private bool TryCollectUtf8MatchRanges(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        out PythonReUtf8MatchRangeBuffer ranges)
    {
        if (_utf8Regex is null)
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

            ranges = new PythonReUtf8MatchRangeBuffer(enumerator.ExportWorkingInvocation());
            while (enumerator.MoveNext())
            {
                var match = enumerator.Current;
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
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            ranges.Dispose();
            ranges = default;
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), Pattern, MatchTimeout);
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
        if (_canMatchEmpty || count < 0 ||
            startOffsetInBytes != 0 ||
            !_canUseZeroOffsetUtf8ValueFastPath ||
            _utf8Regex is null ||
            !plan.TryGetLiteralUtf8(out var literalBytes))
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
