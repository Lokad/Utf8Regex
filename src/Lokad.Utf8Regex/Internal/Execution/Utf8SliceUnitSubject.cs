using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Invocation-owned private char alphabet for the .NET code-unit VM.
/// Validated public UTF-8 is converted only when it contains supplementary
/// scalars. Each surrogate half gets a distinct three-byte char encoding.
/// Public validation never accepts that encoding; results project through the
/// original subject map. All new payload storage is admitted before allocation.</summary>
internal ref struct Utf8SliceUnitSubject
{
    private ulong _reservedBytes;
    internal ReadOnlySpan<byte> Input;
    internal Utf8BoundaryMap Map;

    internal static Utf8ExecutionLimitKind TryCreate(
        ReadOnlySpan<byte> validatedInput,
        Utf8BoundaryMap originalMap,
        scoped ref Utf8SliceInvocation invocation,
        out Utf8SliceUnitSubject subject)
    {
        subject = default;
        var supplementary = false;
        if (invocation.IsUnmetered)
        {
            supplementary = validatedInput.IndexOfAnyInRange((byte)0xf0, byte.MaxValue) >= 0;
        }
        else
        {
            // A single whole-buffer vector scan would leave a long ASCII
            // subject outside the caller's cancellation/deadline checkpoints.
            for (var index = 0; index < validatedInput.Length;)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(4096, validatedInput.Length - index);
                if (validatedInput.Slice(index, length).IndexOfAnyInRange((byte)0xf0, byte.MaxValue) >= 0)
                {
                    supplementary = true;
                    break;
                }
                index += length;
            }
        }
        if (!supplementary)
        {
            subject.Input = validatedInput;
            subject.Map = originalMap;
            return Utf8ExecutionLimitKind.None;
        }
        long encodedLength = validatedInput.Length;
        var nextCheckpoint = 0;
        for (var index = 0; index < validatedInput.Length;)
        {
            if (index >= nextCheckpoint)
            {
                if (!invocation.IsUnmetered)
                {
                    var breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                }
                nextCheckpoint = index > int.MaxValue - 4096 ? int.MaxValue : index + 4096;
            }
            var lead = validatedInput[index];
            var width = lead < 0x80 ? 1 : lead < 0xe0 ? 2 : lead < 0xf0 ? 3 : 4;
            if (width == 4)
            {
                encodedLength += 2;
            }
            index += width;
        }
        var offsetCount = (long)originalMap.Utf16Length + 1;
        if (encodedLength > Array.MaxLength || offsetCount > Array.MaxLength)
            return Utf8ExecutionLimitKind.WorkingBytes;
        var reservation = (ulong)encodedLength + (ulong)offsetCount * 4;
        if (!invocation.Ledger.TryReserve(reservation)) return Utf8ExecutionLimitKind.WorkingBytes;
        try
        {
            var bytes = new byte[(int)encodedLength];
            var offsets = new int[(int)offsetCount];
            var output = 0;
            var unit = 0;
            nextCheckpoint = 0;
            for (var index = 0; index < validatedInput.Length;)
            {
                if (index >= nextCheckpoint)
                {
                    if (!invocation.IsUnmetered)
                    {
                        var breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None)
                        {
                            invocation.Ledger.Release(reservation);
                            return breach;
                        }
                    }
                    nextCheckpoint = index > int.MaxValue - 4096 ? int.MaxValue : index + 4096;
                }
                var lead = validatedInput[index];
                var width = lead < 0x80 ? 1 : lead < 0xe0 ? 2 : lead < 0xf0 ? 3 : 4;
                offsets[unit++] = output;
                if (width < 4)
                {
                    validatedInput.Slice(index, width).CopyTo(bytes.AsSpan(output));
                    output += width;
                }
                else
                {
                    var value = ((lead & 7) << 18) | ((validatedInput[index + 1] & 63) << 12) |
                        ((validatedInput[index + 2] & 63) << 6) | (validatedInput[index + 3] & 63);
                    var leadHalf = 0xd800 + ((value - 0x10000) >> 10);
                    var trailHalf = 0xdc00 + ((value - 0x10000) & 1023);
                    bytes[output++] = (byte)(0xe0 | (leadHalf >> 12));
                    bytes[output++] = (byte)(0x80 | ((leadHalf >> 6) & 63));
                    bytes[output++] = (byte)(0x80 | (leadHalf & 63));
                    offsets[unit++] = output;
                    bytes[output++] = (byte)(0xe0 | (trailHalf >> 12));
                    bytes[output++] = (byte)(0x80 | ((trailHalf >> 6) & 63));
                    bytes[output++] = (byte)(0x80 | (trailHalf & 63));
                }
                index += width;
            }
            offsets[unit] = output;
            subject.Input = bytes;
            subject.Map = Utf8BoundaryMap.FromUnitEncoding(output, offsets);
            subject._reservedBytes = reservation;
            return Utf8ExecutionLimitKind.None;
        }
        catch
        {
            invocation.Ledger.Release(reservation);
            throw;
        }
    }

    internal static Utf8SliceUnitSubject Create(
        ReadOnlySpan<byte> validatedInput,
        Utf8BoundaryMap originalMap,
        scoped ref Utf8SliceInvocation invocation)
    {
        var breach = TryCreate(validatedInput, originalMap, ref invocation, out var subject);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned unit subject exceeded the invocation storage or work budget.");
        return subject;
    }

    internal void Release(ref Utf8SliceInvocation invocation)
    {
        if (_reservedBytes != 0)
        {
            invocation.Ledger.Release(_reservedBytes);
            _reservedBytes = 0;
        }
        Input = default;
        Map = default;
    }
}
