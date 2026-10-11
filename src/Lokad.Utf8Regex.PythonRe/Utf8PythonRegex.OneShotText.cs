using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>One-shot string materialization shares discovery's invocation.</summary>
public sealed partial class Utf8PythonRegex
{
    private string? MatchToStringCore(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, CancellationToken cancellationToken)
    {
        ValidateScalarStartOffset(input, startOffsetInBytes);
        var native = !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan &&
            (kind is 0 or 1 && startOffsetInBytes == 0 && _canUseZeroOffsetUtf8SearchValueFastPath ||
             kind == 1 && startOffsetInBytes == 0 && _asciiLiteralPrefixDigitMatchPrefix is not null ||
             kind == 2 && _asciiDotAllFullMatchPrefix is not null && _asciiDotAllFullMatchSuffix is not null);
        if (native)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, cancellationToken);
            Utf8InvocationResultLedger results = default;
            try { return MaterializeNativeMatchText(input, startOffsetInBytes, kind, ref invocation, ref results); }
            finally { results.Release(new Utf8InvocationBufferControl(ref invocation)); }
        }

        var cursor = CreateScalarCursor(input, startOffsetInBytes, kind, cancellationToken);
        try
        {
            string? completed = null;
            if (kind == 0 ? cursor.MoveNext() : cursor.MatchAtCurrentPosition())
                completed = cursor.GetText(cursor.StartOffsetInBytes, cursor.EndOffsetInBytes);
            cursor.Checkpoint();
            return completed;
        }
        finally { cursor.Dispose(); }
    }
}
