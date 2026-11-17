using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8InputAnalyzer
{
    // Callers supply valid text. These facts serve live route eligibility only;
    // they neither validate the subject nor derive character coordinates.
    internal static Utf8ValidationResult Describe(ReadOnlySpan<byte> input)
    {
        var ascii = IsAscii(input);
        return new(input.Length, ascii,
            !ascii && input.IndexOfAnyInRange((byte)0xf0, (byte)0xf4) >= 0);
    }

    internal static Utf8ValidationResult Describe(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        var breach = TryDescribe(input, ref invocation, out var result);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Subject inspection exceeded the invocation {breach} budget.");
        return result;
    }

    internal static bool IsAscii(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        if (invocation.IsUnmetered && invocation.CheckpointHook is null) return IsAscii(input);
        for (var offset = 0; offset < input.Length;)
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var count = Math.Min(2048, input.Length - offset);
            if (!IsAscii(input.Slice(offset, count))) return false;
            offset += count;
        }
        return true;
    }

    internal static Utf8ExecutionLimitKind TryDescribe(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out Utf8ValidationResult result)
    {
        result = default;
        var breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            result = Describe(input);
            return Utf8ExecutionLimitKind.None;
        }
        var ascii = true;
        var supplementary = false;
        for (var offset = 0; offset < input.Length;)
        {
            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var count = Math.Min(4096, input.Length - offset);
            var block = Describe(input.Slice(offset, count));
            ascii &= block.IsAscii;
            supplementary |= block.ContainsSupplementaryScalars;
            offset += count;
        }
        breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach == Utf8ExecutionLimitKind.None) result = new(input.Length, ascii, supplementary);
        return breach;
    }
}
