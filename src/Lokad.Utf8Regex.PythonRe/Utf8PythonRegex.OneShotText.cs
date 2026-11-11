using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>One-shot string materialization shares discovery's invocation.</summary>
public sealed partial class Utf8PythonRegex
{
    private string? MatchToStringCore(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        ValidateScalarStartOffset(input, startOffsetInBytes);
        var native = kind == 2 && CanUseLiteralFullMatch ||
            !HasFiniteExecutionLimits && !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan &&
            (kind is 0 or 1 && startOffsetInBytes == 0 && _canUseZeroOffsetUtf8SearchValueFastPath ||
             kind == 1 && startOffsetInBytes == 0 && _asciiLiteralPrefixDigitMatchPrefix is not null ||
             kind == 2 && _asciiDotAllFullMatchPrefix is not null && _asciiDotAllFullMatchSuffix is not null);
        if (native)
        {
            var invocation = CreateInvocation(cancellationToken);
            Utf8InvocationResultLedger results = default;
            try { return MaterializeNativeMatchText(input, startOffsetInBytes, kind, ref invocation, ref results); }
            finally { results.Release(ref invocation); }
        }

        var cursor = CreateScalarCursor(input, startOffsetInBytes, kind, cancellationToken);
        Utf8InvocationResultLedger scalarResults = default;
        try
        {
            string? completed = null;
            if (kind == 0 ? cursor.MoveNext() : cursor.MatchAtCurrentPosition())
            {
                cursor.Checkpoint();
                // This operation returns one string. Size its actual range;
                // batch output keeps its useful shared subject facts.
                var start = cursor.StartOffsetInBytes;
                var end = cursor.EndOffsetInBytes;
                var description = cursor.InputDescription;
                int? count = description is { IsAscii: true } ? end - start
                    : start == 0 && end == input.Length ? description?.Utf16Length : null;
                completed = MaterializeMatchText(input, start, end,
                    count, ref cursor.WorkingInvocation, ref scalarResults);
            }
            cursor.Checkpoint();
            return completed;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            cursor.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw cursor.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw cursor.TimeoutFailure(); }
        finally { scalarResults.Release(ref cursor.WorkingInvocation); cursor.Dispose(); }
    }

    private string MaterializeMatchText(ReadOnlySpan<byte> input, int start, int end,
        int? characterCount, ref Utf8SliceInvocation invocation, ref Utf8InvocationResultLedger results)
    {
        try
        {
            var bytes = input[start..end];
            var count = characterCount ?? Utf8SliceBufferWork.CountUtf16(bytes, ref invocation);
            var completed = results.DecodeUtf8(bytes, count, ref invocation);
            Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
            return completed;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw invocation.Budget.CreateResourceFailure();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
    }
}
