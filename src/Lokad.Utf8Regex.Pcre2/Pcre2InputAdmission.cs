using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    // The malformed-subject matcher normalizes byte starts inside fragments.
    internal static Utf8BytePosition AdmitStart(ReadOnlySpan<byte> input,
        int start, ref Utf8SliceInvocation invocation, bool matchInvalidUtf8 = false)
    {
        Pcre2SliceDriver.CheckInvocation(ref invocation);
        var position = matchInvalidUtf8
            ? Utf8BytePosition.CreateChecked(input.Length, start, "startOffsetInBytes")
            : Utf8BytePosition.CreateScalarChecked(input, start, "startOffsetInBytes");
        Pcre2SliceDriver.ThrowIfSliceBreached(invocation.CheckInput());
        invocation.Budget.ThrowIfCancellationRequested();
        return position;
    }
}
