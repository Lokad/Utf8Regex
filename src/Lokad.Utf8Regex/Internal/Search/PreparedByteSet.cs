using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Lokad.Utf8Regex.Internal.Search;

// Complete field-state carrier for independently admitted byte-set objects.
internal readonly struct PreparedByteSetState(
    ulong mask0, ulong mask1, ulong mask2, ulong mask3,
    Vector128<byte> lower, Vector128<byte> upper, byte minimum, byte maximum, bool range, bool asciiOnly)
{
    internal readonly ulong Mask0 = mask0, Mask1 = mask1, Mask2 = mask2, Mask3 = mask3;
    internal readonly Vector128<byte> Lower = lower, Upper = upper;
    internal readonly byte Minimum = minimum, Maximum = maximum;
    internal readonly bool Range = range;
    internal readonly bool AsciiOnly = asciiOnly;

    internal static PreparedByteSetState FromValues(ReadOnlySpan<byte> values)
    {
        ulong a = 0, b = 0, c = 0, d = 0;
        foreach (var value in values)
        {
            var bit = 1ul << (value & 63);
            switch (value >> 6)
            {
                case 0: a |= bit; break;
                case 1: b |= bit; break;
                case 2: c |= bit; break;
                default: d |= bit; break;
            }
        }
        return FromMasks(a, b, c, d);
    }

    internal static PreparedByteSetState FromMasks(ulong a, ulong b, ulong c, ulong d)
    {
        // A byte is (high nibble, low nibble). Each lookup column stores eight
        // high-nibble membership bits; the two tables cover all sixteen rows.
        // Derive this fixed matrix from the membership masks, not runtime internals.
        Span<byte> lower = stackalloc byte[16];
        Span<byte> upper = stackalloc byte[16];
        lower.Clear(); upper.Clear();
        var minimum = 256; var maximum = -1; var count = 0;
        for (var value = 0; value < 256; value++)
        {
            var mask = (value >> 6) switch { 0 => a, 1 => b, 2 => c, _ => d };
            if ((mask & (1ul << (value & 63))) == 0) continue;
            minimum = Math.Min(minimum, value); maximum = value; count++;
            if (value < 128) lower[value & 15] |= (byte)(1 << (value >> 4));
            else upper[value & 15] |= (byte)(1 << ((value >> 4) - 8));
        }
        return new(a, b, c, d, Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(lower)),
            Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(upper)), (byte)minimum, (byte)maximum,
            count > 0 && maximum - minimum + 1 == count, (c | d) == 0);
    }
}

internal sealed class PreparedByteSet(PreparedByteSetState state)
{
    private readonly PreparedByteSetState _state = state;
    internal static readonly PreparedByteSet Empty = new(default);
    private static readonly Vector128<byte> s_bits = Vector128.Create(
        (byte)1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);

    internal static PreparedByteSet CreateOwned(ReadOnlySpan<byte> values)
    {
        if (values.IsEmpty) return Empty;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork((ulong)values.Length + 256);
            var result = budget.AllocateObject(PreparedByteSetState.FromValues(values), static value => new PreparedByteSet(value));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    internal static PreparedByteSet CreateOwned(ulong mask0, ulong mask1, ulong mask2, ulong mask3)
    {
        if ((mask0 | mask1 | mask2 | mask3) == 0) return Empty;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(256);
            var result = budget.AllocateObject(PreparedByteSetState.FromMasks(mask0, mask1, mask2, mask3), static value => new PreparedByteSet(value));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Contains(byte value)
    {
        var mask = (value >> 6) switch
        { 0 => _state.Mask0, 1 => _state.Mask1, 2 => _state.Mask2, _ => _state.Mask3 };
        return (mask & (1ul << (value & 63))) != 0;
    }

    internal int IndexOf(ReadOnlySpan<byte> input) => Find(input, false, false);
    internal int LastIndexOf(ReadOnlySpan<byte> input) => Find(input, true, false);
    internal int IndexOfAnyExcept(ReadOnlySpan<byte> input) => Find(input, false, true);
    internal int LastIndexOfAnyExcept(ReadOnlySpan<byte> input) => Find(input, true, true);

    private int Find(ReadOnlySpan<byte> input, bool reverse, bool except)
    {
        if (_state.Range)
            return reverse
                ? except ? input.LastIndexOfAnyExceptInRange(_state.Minimum, _state.Maximum)
                    : input.LastIndexOfAnyInRange(_state.Minimum, _state.Maximum)
                : except ? input.IndexOfAnyExceptInRange(_state.Minimum, _state.Maximum)
                    : input.IndexOfAnyInRange(_state.Minimum, _state.Maximum);
        var width = input.Length >= 32 && Avx2.IsSupported ? 32
            : input.Length >= 16 && Vector128.IsHardwareAccelerated ? 16 : 0;
        if (width != 0)
        {
            var all = width == 32 ? uint.MaxValue : 0xffffu;
            var last = input.Length - width;
            var position = reverse ? last : 0;
            while (true)
            {
                var mask = width == 32 ? Misses256(input, position) : Misses128(input, position);
                if (!except) mask ^= all;
                if (mask != 0) return position + (reverse ? BitOperations.Log2(mask) : BitOperations.TrailingZeroCount(mask));
                // The final overlapping vector stays inside the span. Its
                // overlap was already proved empty, so it cannot reorder a hit.
                if (reverse ? position == 0 : position == last) return -1;
                position = reverse ? Math.Max(0, position - width) : Math.Min(last, position + width);
            }
        }
        if (reverse)
        {
            for (var index = input.Length - 1; index >= 0; index--)
                if (Contains(input[index]) != except) return index;
        }
        else
            for (var index = 0; index < input.Length; index++) if (Contains(input[index]) != except) return index;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint Misses128(ReadOnlySpan<byte> input, int position)
    {
        var values = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(input), (nuint)position);
        var low = Vector128.BitwiseAnd(values, Vector128.Create(_state.AsciiOnly ? (byte)143 : (byte)15));
        var high = Vector128.BitwiseAnd(Vector128.ShiftRightLogical(values, 4), Vector128.Create((byte)15));
        var lower = Vector128.Shuffle(_state.Lower, low);
        var table = lower;
        if (!_state.AsciiOnly)
        {
            var upper = Vector128.Shuffle(_state.Upper, low);
            table = Vector128.ConditionalSelect(Vector128.LessThan(values.AsSByte(), Vector128<sbyte>.Zero).AsByte(), upper, lower);
        }
        var bit = Vector128.Shuffle(s_bits, high);
        return Vector128.ExtractMostSignificantBits(Vector128.Equals(Vector128.BitwiseAnd(table, bit), Vector128<byte>.Zero));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint Misses256(ReadOnlySpan<byte> input, int position)
    {
        var values = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(input), (nuint)position);
        // PSHUFB itself clears high-bit byte indices for the ASCII-only table.
        var low = _state.AsciiOnly ? values : Avx2.And(values, Vector256.Create((byte)15));
        var high = Avx2.And(Avx2.ShiftRightLogical(values.AsUInt16(), 4).AsByte(), Vector256.Create((byte)15));
        var lower = Avx2.Shuffle(Vector256.Create(_state.Lower, _state.Lower), low);
        var table = lower;
        if (!_state.AsciiOnly)
        {
            var upper = Avx2.Shuffle(Vector256.Create(_state.Upper, _state.Upper), low);
            table = Avx2.BlendVariable(lower, upper, values);
        }
        var bit = Avx2.Shuffle(Vector256.Create(s_bits, s_bits), high);
        return (uint)Avx2.MoveMask(Avx2.CompareEqual(Avx2.And(table, bit), Vector256<byte>.Zero));
    }
}
