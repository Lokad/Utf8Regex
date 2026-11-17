using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8Validation
{
    internal static void ThrowIfInvalidOnly(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        var breach = TryValidateOnly(input, ref invocation);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"UTF-8 validation exceeded the invocation {breach} budget.");
    }

    // Patterns, templates and assembled outputs retain bounded strict checking.
    // Regex subjects already satisfy their caller-valid text contract.
    internal static Utf8ExecutionLimitKind TryValidateOnly(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        var breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            ThrowIfInvalidOnly(input);
            return Utf8ExecutionLimitKind.None;
        }
        for (var offset = 0; offset < input.Length;)
        {
            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var count = Math.Min(2048, input.Length - offset);
            if (count < input.Length - offset)
            {
                var end = offset + count;
                for (var tail = 0; tail < 3 && (input[end] & 0xc0) == 0x80; tail++) end--;
                if ((input[end] & 0xc0) != 0x80) count = end - offset;
            }
            if (!Utf8InputAnalyzer.TryValidateOnly(input.Slice(offset, count), out var error))
                throw CreateInvalidUtf8Exception(offset + error);
            offset += count;
        }
        breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return breach;
    }
}
