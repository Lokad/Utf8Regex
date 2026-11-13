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

    internal static void ThrowIfInvalidOnly(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        var breach = TryValidateOnly(input, ref invocation);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"UTF-8 validation exceeded the invocation {breach} budget.");
    }

    // PCRE2-INTEGRATION-POINT: byte-only Count needs the existing bounded
    // validator's breach result to retain its profile-specific failure mapping.
    internal static Utf8ExecutionLimitKind TryValidateOnly(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation, int byteOffset = 0)
        => TryValidateOnly(input, ref invocation, out _, byteOffset);

    internal static Utf8ExecutionLimitKind TryValidateOnly(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation,
        out bool isAscii, int byteOffset = 0)
        => TryValidate(input, ref invocation, computeUtf16Length: false, out _, out isAscii, byteOffset);

    // Byte-only counters need ASCII eligibility, not derived coordinates.
    internal static bool ValidateIsAscii(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        var breach = TryValidate(input, ref invocation, computeUtf16Length: false, out _, out var isAscii);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"UTF-8 validation exceeded the invocation {breach} budget.");
        return isAscii;
    }

    // Work is charged per bounded block. Boundary adjustment examines at most
    // three bytes and keeps complete scalars together; malformed runs retain the
    // existing validator's first error offset over the original slice.
    internal static Utf8ExecutionLimitKind TryValidate(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out Utf8ValidationResult validation)
        => TryValidate(input, ref invocation, computeUtf16Length: true, out validation, out _);

    private static Utf8ExecutionLimitKind TryValidate(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, bool computeUtf16Length, out Utf8ValidationResult validation, out bool isAscii, int byteOffset = 0)
    {
        validation = default;
        isAscii = true;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            if (computeUtf16Length)
            {
                validation = Validate(input);
                isAscii = validation.IsAscii;
            }
            else if (!Utf8InputAnalyzer.TryValidateOnly(input, out var errorOffset, out isAscii))
                throw CreateInvalidUtf8Exception(byteOffset + errorOffset);
            return Utf8ExecutionLimitKind.None;
        }

        var utf16Length = 0;
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
            var span = input.Slice(offset, length);
            if (computeUtf16Length)
            {
                if (!Utf8ValidationCore.TryValidate(span, true, out var block, out var errorOffset))
                    throw CreateInvalidUtf8Exception(byteOffset + offset + errorOffset);
                utf16Length += block.Utf16Length;
                isAscii &= block.IsAscii;
                supplementary |= block.ContainsSupplementaryScalars;
            }
            else
            {
                if (!Utf8InputAnalyzer.TryValidateOnly(span, out var errorOffset, out var blockIsAscii))
                    throw CreateInvalidUtf8Exception(byteOffset + offset + errorOffset);
                isAscii &= blockIsAscii;
            }
            offset += length;
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        if (computeUtf16Length) validation = new(input.Length, utf16Length, isAscii, supplementary);
        return Utf8ExecutionLimitKind.None;
    }
}
