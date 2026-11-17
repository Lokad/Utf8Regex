using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    internal static bool SubjectIsAscii(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        try { return Utf8InputAnalyzer.IsAscii(input, ref invocation); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach); throw; }
    }

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
