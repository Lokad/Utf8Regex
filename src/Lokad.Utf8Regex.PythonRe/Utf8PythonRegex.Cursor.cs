using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonRegex
{
    /// <summary>Creates a capture cursor with one cumulative invocation token, deadline and resource policy.</summary>
    /// <remarks>Search finds successive matches. Match and FullMatch publish at most one result; completion metadata remains available in the returned snapshot. A null working allowance inherits program funding or the numeric engine policy; a non-null allowance overrides only working funding and refusal is terminal. Start/end are scalar-aligned byte bounds, with the complete prefix available to anchors and lookbehind. Dispose abandoned cursors. Snapshots survive advances/disposal; keep borrowed input unchanged and its manager alive. Deadlines include host waits.</remarks>
    public Utf8PythonMatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int startOffsetInBytes = 0,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null,
        Utf8PythonMatchMode mode = Utf8PythonMatchMode.Search, int? endOffsetInBytes = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var programUse = EnterProgramUse();
        try
        {
            if ((uint)mode > (uint)Utf8PythonMatchMode.FullMatch)
                throw new ArgumentOutOfRangeException(nameof(mode));
            var invocation = CreateInvocation(cancellationToken, workingMemoryAllowance);
            var end = GetScalarEndOffset(input.Span, endOffsetInBytes);
            var subject = input[..end];
            ValidateScalarStartOffset(subject.Span, startOffsetInBytes);
            return new Utf8PythonMatchCursor(subject, ScalarProgram, startOffsetInBytes,
                MatchTimeout, this, invocation, _utf8Regex, programUse, mode);
        }
        catch { programUse?.Dispose(); throw; }
    }

    private static int GetScalarEndOffset(ReadOnlySpan<byte> input, int? endOffsetInBytes)
    {
        var end = endOffsetInBytes ?? input.Length;
        if (!Utf8BytePosition.IsScalarBoundary(input, end))
            throw new ArgumentOutOfRangeException(nameof(endOffsetInBytes));
        return end;
    }
}
