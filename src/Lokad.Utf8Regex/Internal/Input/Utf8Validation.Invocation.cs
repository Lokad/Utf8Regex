using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8Validation
{
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
            var count = Utf8SliceBufferWork.Utf8BlockLength(input[offset..], 2048);
            if (!TryValidateOnly(input.Slice(offset, count), out var error))
                throw CreateInvalidUtf8Exception(offset + error);
            offset += count;
        }
        breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return breach;
    }
}
