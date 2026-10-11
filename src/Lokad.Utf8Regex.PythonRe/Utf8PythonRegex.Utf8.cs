using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

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
            var values = ledger.AllocateArray<string>(count, ref invocation);
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
        finally { ledger.Release(ref invocation); }
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
        ReadOnlySpan<byte> input, PythonReReplacementPlan plan, int count, int startOffsetInBytes,
        bool textOutput, out byte[] resultBytes, out string resultText, out int replacementCount)
    {
        resultBytes = [];
        resultText = string.Empty;
        replacementCount = 0;
        if (_canMatchEmpty || count < 0 || startOffsetInBytes != 0 ||
            !_canUseZeroOffsetUtf8ValueFastPath || _utf8Regex is null ||
            !plan.TryGetLiteralUtf8(out var literalBytes)) return false;

        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, CancellationToken.None);
        Utf8InvocationResultLedger results = default;
        try
        {
            var bytes = _utf8Regex.ReplaceExactLiteralForResult(input, literalBytes,
                count == 0 ? int.MaxValue : count, ref invocation, ref results, out var replaced);
            var text = string.Empty;
            if (textOutput)
            {
                var validation = Utf8Validation.Validate(bytes, ref invocation);
                text = results.DecodeUtf8(bytes, validation.Utf16Length, ref invocation);
                results.ReleaseArray(bytes, ref invocation);
            }
            Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
            if (textOutput) resultText = text;
            else resultBytes = bytes;
            replacementCount = replaced;
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), Pattern, MatchTimeout);
        }
        finally { results.Release(ref invocation); }
    }

    internal string? MaterializeNativeMatchText(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, ref Utf8SliceInvocation invocation, ref Utf8InvocationResultLedger results)
    {
        try
        {
            var control = new Utf8InvocationBufferControl(ref invocation);
            Utf8InvocationOutput.Checkpoint(control);
            if (!TryDiscoverNativeMatch(input, startOffsetInBytes, kind, ref invocation,
                out var start, out var end, out var validation))
            {
                Utf8InvocationOutput.Checkpoint(control);
                return null;
            }
            var bytes = input[start..end];
            var count = validation.IsAscii ? bytes.Length
                : start == 0 && end == input.Length ? validation.Utf16Length
                : Utf8Validation.Validate(bytes, ref invocation).Utf16Length;
            var completed = results.DecodeUtf8(bytes, count, ref invocation);
            Utf8InvocationOutput.Checkpoint(control);
            return completed;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), _translation.Pattern, MatchTimeout);
        }
    }

    internal Utf8PythonValueMatch MaterializeNativeMatchValue(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, scoped ref Utf8SliceInvocation invocation)
    {
        try
        {
            var control = new Utf8InvocationBufferControl(ref invocation);
            Utf8PythonValueMatch result = default;
            if (TryDiscoverNativeMatch(input, startOffsetInBytes, kind, ref invocation,
                out var start, out var end, out var validation))
            {
                var startUtf16 = validation.IsAscii || start == 0 ? start
                    : Utf8Validation.Validate(input[..start], ref invocation).Utf16Length;
                var endUtf16 = validation.IsAscii ? end : end == input.Length ? validation.Utf16Length
                    : startUtf16 + Utf8Validation.Validate(input[start..end], ref invocation).Utf16Length;
                result = Utf8PythonValueMatch.Create(input, new PythonReGroupData
                {
                    Number = 0, Success = true,
                    StartOffsetInBytes = start, EndOffsetInBytes = end,
                    StartOffsetInUtf16 = startUtf16, EndOffsetInUtf16 = endUtf16,
                });
            }
            Utf8InvocationOutput.Checkpoint(control);
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), _translation.Pattern, MatchTimeout);
        }
    }

    private bool TryDiscoverNativeMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, int kind,
        ref Utf8SliceInvocation invocation, out int start, out int end, out Utf8ValidationResult validation)
    {
        ValidateScalarStartOffset(input, startOffsetInBytes);
        var control = new Utf8InvocationBufferControl(ref invocation);
        Utf8InvocationOutput.Checkpoint(control);
        start = end = -1;
        validation = default;
        if (kind == 1 && _asciiLiteralPrefixDigitMatchPrefix is { } prefix)
        {
            validation = Utf8Validation.Validate(input, ref invocation);
            start = 0;
            end = PythonReAsciiPrefixDigitMatcher.MatchUnchecked(input, prefix, ref invocation);
            return end != 0;
        }
        if (kind == 2 && _asciiDotAllFullMatchPrefix is { } head && _asciiDotAllFullMatchSuffix is { } tail)
        {
            validation = Utf8Validation.Validate(input, ref invocation);
            var subject = input[startOffsetInBytes..];
            if (subject.Length < head.Length || subject.Length - head.Length < tail.Length ||
                !PythonReAsciiPrefixDigitMatcher.MatchesPrefix(subject, head, ref invocation) ||
                !PythonReAsciiPrefixDigitMatcher.MatchesPrefix(subject[(subject.Length - tail.Length)..], tail, ref invocation))
                return false;
            start = startOffsetInBytes;
            end = input.Length;
            return true;
        }
        if (_utf8Regex is null) throw new InvalidOperationException("Native result discovery requires its prepared core.");
        return _utf8Regex.FindLiteralForResult(input, ref invocation, out start, out end, out validation) &&
            (kind != 1 || start == 0);
    }
}
