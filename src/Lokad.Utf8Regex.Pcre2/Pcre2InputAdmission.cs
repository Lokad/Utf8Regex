using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    internal static Utf8ValidatedInput PrepareInput(ReadOnlySpan<byte> input,
        int start, scoped ref Utf8SliceInvocation invocation)
    {
        CheckInput(input, start, ref invocation);
        return Utf8ValidatedInput.BorrowBytes(input);
    }

    // Caller-valid text needs byte-start admission and the original invocation
    // checkpoint, not a lookbehind-based validity window or a second policy.
    internal static void CheckInput(ReadOnlySpan<byte> input,
        int start, ref Utf8SliceInvocation invocation)
    {
        Pcre2SliceDriver.CheckInvocation(ref invocation);
        _ = Utf8ValidatedInput.GetBytePosition(input, start, "startOffsetInBytes");
        Pcre2SliceDriver.ThrowIfSliceBreached(invocation.TryChargeInputStep());
        invocation.Budget.ThrowIfCancellationRequested();
    }
}
