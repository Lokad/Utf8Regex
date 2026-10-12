using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

// Borrow the admitted scalar program. Internal compiled consumers receive the
// caller's absolute entry deadline; controlled consumers can pass the invocation
// directly. Matching/counting never decode or build a coordinate map.
internal static class Utf8PreparedScalarExecutor
{
    internal static bool IsMatch(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, Utf8ExecutionDeadline deadline)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, default);
        var breach = TryIsMatch(prepared, input, validation, ref invocation, out var result);
        ThrowIfBreached(breach);
        return result;
    }

    internal static int Count(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, Utf8ExecutionDeadline deadline)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, default);
        var breach = TryCount(prepared, input, validation, ref invocation, out var result);
        ThrowIfBreached(breach);
        return result;
    }

    internal static Utf8ValueMatch Match(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, Utf8ExecutionDeadline deadline)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, default);
        var breach = TryMatch(prepared, input, validation, ref invocation, out var result);
        ThrowIfBreached(breach);
        return result;
    }

    internal static Utf8ExecutionLimitKind TryIsMatch(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, ref Utf8SliceInvocation invocation, out bool result)
    {
        result = false;
        var breach = TryAcquire(prepared, input, ref validation, ref invocation, out var program, out var slots);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        try
        {
            var outcome = Utf8SliceRunner.IsMatch(program, input, slots.Starts, slots.Ends, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None) return outcome.Breach;
            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach == Utf8ExecutionLimitKind.None) result = outcome.IsMatch;
            return breach;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    internal static Utf8ExecutionLimitKind TryCount(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, ref Utf8SliceInvocation invocation, out int result)
    {
        result = 0;
        var breach = TryAcquire(prepared, input, ref validation, ref invocation, out var program, out var slots);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        try
        {
            var outcome = Utf8SliceRunner.Count(program, input, slots.Starts, slots.Ends, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None) return outcome.Breach;
            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach == Utf8ExecutionLimitKind.None) result = outcome.Count;
            return breach;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    internal static Utf8ExecutionLimitKind TryMatch(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, ref Utf8SliceInvocation invocation, out Utf8ValueMatch result)
    {
        result = Utf8ValueMatch.NoMatch;
        var breach = TryAcquire(prepared, input, ref validation, ref invocation, out var program, out var slots);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        try
        {
            var start = program.RightToLeft ? input.Length : 0;
            invocation.AnchorOriginByte = start;
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, start, slots.Starts, slots.Ends, false, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None) return outcome.Breach;
            if (!outcome.Found)
            {
                breach = invocation.TryChargeInputStep();
                invocation.Budget.ThrowIfCancellationRequested();
                return breach;
            }
            return Utf8ProjectionExecutor.TryProjectScalarVerification(input, validation,
                new(true, outcome.StartByte, outcome.EndByte - outcome.StartByte), ref invocation, out result);
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    private static Utf8ExecutionLimitKind TryAcquire(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        ref Utf8ValidationResult validation, ref Utf8SliceInvocation invocation,
        out Utf8SliceProgram program, out Utf8SliceSlots slots)
    {
        slots = default;
        program = prepared.ScalarProgram ?? throw new InvalidOperationException("Prepared scalar execution requires its admitted program.");
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (validation.ByteLength != input.Length)
        {
            breach = Utf8Validation.TryValidate(input, ref invocation, out validation);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
        }
        breach = Utf8SliceRunner.TryRentSlots(program.GroupCount, ref invocation, out slots);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        return Utf8ExecutionLimitKind.None;
    }

    private static void ThrowIfBreached(Utf8ExecutionLimitKind breach)
    {
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Prepared scalar execution exceeded {breach}.");
    }
}
