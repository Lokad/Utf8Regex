using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Compact immutable membership carrier for the 128 ASCII byte values.
/// The stored masks describe the positive definition; <see cref="Negated"/>
/// complements that definition inside the ASCII domain only.
/// </summary>
internal readonly struct AsciiCharClass : IEquatable<AsciiCharClass>
{
    private readonly ulong _lowMask;
    private readonly ulong _highMask;

    public static AsciiCharClass Empty => default;

    public AsciiCharClass(ulong lowMask, ulong highMask)
        : this(lowMask, highMask, false)
    {
    }

    public AsciiCharClass(ulong lowMask, ulong highMask, bool negated)
    {
        _lowMask = lowMask;
        _highMask = highMask;
        Negated = negated;
    }

    public bool Negated { get; }


    public bool IsEmpty => !Negated && (_lowMask | _highMask) == 0;

    public int Count => BitOperations.PopCount(ActualLowMask) + BitOperations.PopCount(ActualHighMask);

    internal ulong ActualLowMask => Negated ? ~_lowMask : _lowMask;

    internal ulong ActualHighMask => Negated ? ~_highMask : _highMask;

    internal ulong LowMask => ActualLowMask;

    internal ulong HighMask => ActualHighMask;

    internal ulong PositiveLowMask => _lowMask;

    internal ulong PositiveHighMask => _highMask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(byte value)
    {
        if (value >= 0x80)
        {
            return false;
        }

        var bit = value < 64
            ? (_lowMask & (1UL << value)) != 0
            : (_highMask & (1UL << (value - 64))) != 0;
        return Negated ? !bit : bit;
    }

    public bool IsDisjoint(AsciiCharClass other) =>
        (ActualLowMask & other.ActualLowMask) == 0 &&
        (ActualHighMask & other.ActualHighMask) == 0;

    public bool Equals(AsciiCharClass other) =>
        _lowMask == other._lowMask &&
        _highMask == other._highMask &&
        Negated == other.Negated;

    public override bool Equals(object? obj) => obj is AsciiCharClass other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_lowMask, _highMask, Negated);

    public static bool operator ==(AsciiCharClass left, AsciiCharClass right) => left.Equals(right);

    public static bool operator !=(AsciiCharClass left, AsciiCharClass right) => !(left == right);

    public static AsciiCharClass ForByte(byte value)
    {
        if (value >= 0x80)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value < 64
            ? new AsciiCharClass(1UL << value, 0)
            : new AsciiCharClass(0, 1UL << (value - 64));
    }

    public static AsciiCharClass FromPredicate(Func<byte, bool> predicate)
    {
        ulong low = 0;
        ulong high = 0;
        for (var value = 0; value < 128; value++)
        {
            if (!predicate((byte)value))
            {
                continue;
            }

            if (value < 64)
            {
                low |= 1UL << value;
            }
            else
            {
                high |= 1UL << (value - 64);
            }
        }

        return new AsciiCharClass(low, high);
    }

    public static AsciiCharClass FromBytes(ReadOnlySpan<byte> values) => FromBytes(values, false);

    public static AsciiCharClass FromBytes(ReadOnlySpan<byte> values, bool negated)
    {
        ulong low = 0;
        ulong high = 0;
        foreach (var value in values)
        {
            if (value >= 0x80)
            {
                throw new ArgumentOutOfRangeException(nameof(values));
            }

            if (value < 64)
            {
                low |= 1UL << value;
            }
            else
            {
                high |= 1UL << (value - 64);
            }
        }

        return new AsciiCharClass(low, high, negated);
    }

    public AsciiCharClass ToIgnoreCaseInvariant()
    {
        ulong low = _lowMask;
        ulong high = _highMask;
        for (var value = 0; value < 128; value++)
        {
            var isPositive = value < 64
                ? (_lowMask & (1UL << value)) != 0
                : (_highMask & (1UL << (value - 64))) != 0;
            if (!isPositive)
            {
                continue;
            }

            Add(ref low, ref high, Internal.Search.AsciiSearch.FoldCase((byte)value));
            var upper = char.ToUpperInvariant((char)value);
            if (upper < 128)
            {
                Add(ref low, ref high, (byte)upper);
            }
        }

        return new AsciiCharClass(low, high, Negated);
    }

    private static void Add(ref ulong low, ref ulong high, byte value)
    {
        if (value < 64)
        {
            low |= 1UL << value;
        }
        else
        {
            high |= 1UL << (value - 64);
        }
    }
}
