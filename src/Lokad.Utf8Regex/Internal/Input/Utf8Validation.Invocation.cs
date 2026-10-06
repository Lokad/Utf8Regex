using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8Validation
{
    internal static Utf8ValidationResult Validate(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        var breach = TryValidate(input, ref invocation, out var validation);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"UTF-8 validation exceeded the invocation {breach} budget.");
        return validation;
    }

    // Work is charged per bounded block. Boundary adjustment examines at most
    // three bytes and keeps complete scalars together; malformed runs retain the
    // existing validator's first error offset over the original slice.
    internal static Utf8ExecutionLimitKind TryValidate(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out Utf8ValidationResult validation)
    {
        validation = default;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            validation = Validate(input);
            return Utf8ExecutionLimitKind.None;
        }

        var utf16Length = 0;
        var ascii = true;
        var supplementary = false;
        for (var offset = 0; offset < input.Length;)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            var length = Math.Min(2048, input.Length - offset);
            if (length < input.Length - offset)
            {
                var end = offset + length;
                for (var count = 0; count < 3 && (input[end] & 0xc0) == 0x80; count++) end--;
                // Four continuation bytes crossing the boundary are malformed;
                // let the ordinary validator diagnose the unchanged block.
                if ((input[end] & 0xc0) != 0x80) length = end - offset;
            }
            if (!Utf8ValidationCore.TryValidate(input.Slice(offset, length), true, out var block, out var errorOffset))
                throw CreateInvalidUtf8Exception(offset + errorOffset);
            utf16Length += block.Utf16Length;
            ascii &= block.IsAscii;
            supplementary |= block.ContainsSupplementaryScalars;
            offset += length;
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        validation = new(input.Length, utf16Length, ascii, supplementary);
        return Utf8ExecutionLimitKind.None;
    }
}
