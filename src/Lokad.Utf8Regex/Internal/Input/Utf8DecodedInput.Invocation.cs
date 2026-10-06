using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8DecodedInput
{
    internal static string Create(ReadOnlySpan<byte> validatedInput, Utf8ValidationResult validation,
        scoped ref Utf8SliceInvocation invocation, out ulong reservation)
    {
        var breach = TryCreate(validatedInput, validation, ref invocation, out var decoded, out reservation);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"UTF-8 decoding exceeded the invocation {breach} budget.");
        return decoded ?? throw new InvalidOperationException("A successful decode needs its text.");
    }

    // The caller has validated the original slice. Admit UTF-16 payload capacity
    // (including the string terminator) alongside its coordinate/working buffers.
    // CLR object overhead remains separate policy debt. The callback receives
    // references to the invocation and breach; neither counters nor input copy.
    internal static Utf8ExecutionLimitKind TryCreate(ReadOnlySpan<byte> validatedInput, Utf8ValidationResult validation,
        scoped ref Utf8SliceInvocation invocation, out string? decoded, out ulong reservation)
    {
        decoded = null;
        reservation = 0;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        if (validation.Utf16Length == 0)
        {
            decoded = string.Empty;
            return Utf8ExecutionLimitKind.None;
        }
        var bytes = ((ulong)validation.Utf16Length + 1) * sizeof(char);
        if (bytes > (ulong)Array.MaxLength || !invocation.Ledger.TryReserve(bytes))
            return Utf8ExecutionLimitKind.WorkingBytes;
        var transferred = false;
        try
        {
            invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
            var state = new DecodeState(validatedInput, ref invocation, ref breach);
            var candidate = string.Create(validation.Utf16Length, state, static (characters, data) =>
            {
                var written = 0;
                for (var offset = 0; offset < data.Input.Length;)
                {
                    data.Breach = data.Invocation.TryChargeInputStep();
                    if (data.Breach != Utf8ExecutionLimitKind.None) return;
                    data.Invocation.Budget.ThrowIfCancellationRequested();
                    var length = Math.Min(2048, data.Input.Length - offset);
                    if (length < data.Input.Length - offset)
                    {
                        var end = offset + length;
                        for (var count = 0; count < 3 && (data.Input[end] & 0xc0) == 0x80; count++) end--;
                        length = end - offset;
                    }
                    written += Encoding.UTF8.GetChars(data.Input.Slice(offset, length), characters[written..]);
                    offset += length;
                }
                if (written != characters.Length)
                    throw new InvalidOperationException("The validated UTF-16 length changed while decoding.");
            });
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            decoded = candidate;
            reservation = bytes;
            transferred = true;
            return Utf8ExecutionLimitKind.None;
        }
        finally
        {
            if (!transferred) invocation.Ledger.Release(bytes);
        }
    }

    private ref struct DecodeState
    {
        internal readonly ReadOnlySpan<byte> Input;
        internal readonly ref Utf8SliceInvocation Invocation;
        internal readonly ref Utf8ExecutionLimitKind Breach;
        internal DecodeState(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation, ref Utf8ExecutionLimitKind breach)
        {
            Input = input;
            Invocation = ref invocation;
            Breach = ref breach;
        }
    }
}
