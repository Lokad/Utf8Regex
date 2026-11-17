namespace Lokad.Utf8Regex.PythonRe;

// split entry points, split from the Python facade along the split ownership boundary.
/// <summary>Split entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Splits into byte ranges without decoding text; captured separators retain their numbers and unmatched status.</summary>
    /// <remarks>Number zero denotes an ordinary segment. Ranges refer to the original unchanged input and do not own its bytes.</remarks>
    public Utf8PythonCaptureRange[] SplitRanges(ReadOnlySpan<byte> input) => SplitRanges(input, 0, 0, default);

    /// <summary>Splits a UTF-8 suffix into byte ranges under the original invocation controls.</summary>
    /// <remarks>Zero maxSplit is unlimited; negative performs no splits. Captured separators have positive numbers. No captured text or whole-subject coordinate map is materialized.</remarks>
    public Utf8PythonCaptureRange[] SplitRanges(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = EnterProgramUse();
        return SplitRangesScalar(input, maxSplit, startOffsetInBytes, cancellationToken);
    }


}
