using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    // Check the requested scalar edge with the original invocation.
    internal static Utf8BytePosition AdmitStart(ReadOnlySpan<byte> input,
        int start, ref Utf8SliceInvocation invocation)
    {
        Pcre2SliceDriver.CheckInvocation(ref invocation);
        var position = Utf8BytePosition.CreateScalarChecked(input, start, "startOffsetInBytes");
        Pcre2SliceDriver.ThrowIfSliceBreached(invocation.CheckInput());
        invocation.Budget.ThrowIfCancellationRequested();
        return position;
    }
}
