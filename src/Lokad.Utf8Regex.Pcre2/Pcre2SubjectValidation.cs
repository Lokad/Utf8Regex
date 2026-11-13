using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    internal static bool SubjectIsAscii(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        try { return Utf8Validation.SubjectIsAscii(input, ref invocation); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach); throw; }
    }

    internal static Utf8ValidatedInput PrepareInput(Pcre2CompiledProgram program, ReadOnlySpan<byte> input,
        int start, scoped ref Utf8SliceInvocation invocation)
    {
        var isAscii = CheckInput(program, input, start, ref invocation);
        return Utf8ValidatedInput.BorrowBytes(input, isAscii);
    }

    // Native UTF checking begins at start minus PCRE2_INFO_MAXLOOKBEHIND
    // characters, then covers the entire remaining subject, even on an early hit.
    // The instance's AssumeValid policy is the subject NO_UTF_CHECK equivalent;
    // it never changes pattern or replacement validation.
    internal static bool CheckInput(Pcre2CompiledProgram program, ReadOnlySpan<byte> input,
        int start, ref Utf8SliceInvocation invocation)
    {
        Pcre2SliceDriver.CheckInvocation(ref invocation);
        _ = Utf8ValidatedInput.GetBytePosition(input, start, "startOffsetInBytes");
        if (program.Request.InputValidation == Utf8InputValidation.AssumeValid || invocation.SubjectValidationCompleted)
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(invocation.TryChargeInputStep());
            return false;
        }
        var from = start;
        for (var remaining = program.Request.MaximumLookbehind; remaining > 0 && from > 0; remaining--)
        {
            // Even malformed continuation runs must remain interruptible.
            Pcre2SliceDriver.ThrowIfSliceBreached(invocation.TryChargeInputStep());
            from--;
            while (from > 0 && (input[from] & 0xc0) == 0x80)
            {
                Pcre2SliceDriver.ThrowIfSliceBreached(invocation.TryChargeInputStep());
                from--;
            }
        }
        Utf8SearchDiagnosticsSession.Current?.CountSubjectValidation();
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8Validation.TryValidateOnly(input[from..], ref invocation, out var isAscii, from));
        invocation.SubjectValidationCompleted = true;
        return from == 0 && isAscii;
    }
}
