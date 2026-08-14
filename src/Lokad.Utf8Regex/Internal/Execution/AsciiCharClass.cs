using System.Numerics;
using System.Runtime.CompilerServices;

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
        KnownPredicateKind = Classify(lowMask, highMask, negated);
    }

    public bool Negated { get; }

    public AsciiCharClassPredicateKind KnownPredicateKind { get; }

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

    public bool HasSameDefinition(AsciiCharClass other) => Equals(other);

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

    public static bool TryUseNonEmpty(AsciiCharClass? value, out AsciiCharClass result)
    {
        result = value ?? Empty;
        return value.HasValue && !value.Value.IsEmpty;
    }

    public static AsciiCharClass FromPredicate(Func<byte, bool> predicate) => FromPredicate(predicate, false);

    public static AsciiCharClass FromPredicate(Func<byte, bool> predicate, bool negated)
    {
        ArgumentNullException.ThrowIfNull(predicate);

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

        return new AsciiCharClass(low, high, negated);
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

    public static AsciiCharClass FromRange(Utf8InclusiveByteRange range) => FromRange(range, false);

    public static AsciiCharClass FromRange(Utf8InclusiveByteRange range, bool negated)
    {
        ulong low = 0;
        ulong high = 0;
        for (var value = range.Low; value <= range.High; value++)
        {
            if (value < 64)
            {
                low |= 1UL << value;
            }
            else
            {
                high |= 1UL << (value - 64);
            }

            if (value == byte.MaxValue)
            {
                break;
            }
        }

        return new AsciiCharClass(low, high, negated);
    }

    public static AsciiCharClass CombinePositive(AsciiCharClass left, AsciiCharClass right) =>
        CombinePositive(left, right, false);

    public static AsciiCharClass CombinePositive(AsciiCharClass left, AsciiCharClass right, bool negated)
    {
        if (left.Negated || right.Negated)
        {
            throw new ArgumentException("Only positive ASCII definitions can be combined.");
        }

        return new AsciiCharClass(
            left.PositiveLowMask | right.PositiveLowMask,
            left.PositiveHighMask | right.PositiveHighMask,
            negated);
    }

    public byte[] GetPositiveMatchBytes()
    {
        if (Negated)
        {
            return [];
        }

        var values = new byte[Count];
        var index = 0;
        for (var value = 0; value < 128; value++)
        {
            if (Contains((byte)value))
            {
                values[index++] = (byte)value;
            }
        }

        return values;
    }

    public byte[] GetMatchBytes()
    {
        var values = new byte[Count];
        var index = 0;
        for (var value = 0; value < 128; value++)
        {
            if (Contains((byte)value))
            {
                values[index++] = (byte)value;
            }
        }

        return values;
    }

    public AsciiCharClass ToIgnoreCaseInvariant()
    {
        ulong low = _lowMask;
        ulong high = _highMask;
        for (var value = 0; value < 128; value++)
        {
            if (!ContainsPositive((byte)value))
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

    public string ToRuntimeCharClassString()
    {
        var runtime = new Internal.FrontEnd.Runtime.RegexCharClass
        {
            Negate = Negated,
        };

        var start = -1;
        for (var value = 0; value <= 0x7F; value++)
        {
            if (ContainsPositive((byte)value))
            {
                if (start < 0)
                {
                    start = value;
                }

                continue;
            }

            if (start >= 0)
            {
                runtime.AddRange((char)start, (char)(value - 1));
                start = -1;
            }
        }

        if (start >= 0)
        {
            runtime.AddRange((char)start, (char)0x7F);
        }

        return runtime.ToStringClass();
    }

    public bool TryGetKnownPredicateKind(out AsciiCharClassPredicateKind predicateKind)
    {
        predicateKind = KnownPredicateKind;
        return predicateKind != AsciiCharClassPredicateKind.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ContainsPositive(byte value) => value < 64
        ? (_lowMask & (1UL << value)) != 0
        : (_highMask & (1UL << (value - 64))) != 0;

    private static AsciiCharClassPredicateKind Classify(ulong low, ulong high, bool negated)
    {
        if (negated)
        {
            return AsciiCharClassPredicateKind.None;
        }

        if (MatchesExactly(low, high, Utf8AsciiBytePredicates.IsDigit))
        {
            return AsciiCharClassPredicateKind.Digit;
        }

        if (MatchesExactly(low, high, Utf8AsciiBytePredicates.IsLetter))
        {
            return AsciiCharClassPredicateKind.AsciiLetter;
        }

        if (MatchesExactly(low, high, Utf8AsciiBytePredicates.IsLetterOrDigit))
        {
            return AsciiCharClassPredicateKind.AsciiLetterOrDigit;
        }

        if (MatchesExactly(low, high, Utf8AsciiBytePredicates.IsWord))
        {
            return AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore;
        }

        if (MatchesExactly(low, high, Utf8AsciiBytePredicates.IsHexDigit))
        {
            return AsciiCharClassPredicateKind.AsciiHexDigit;
        }

        return AsciiCharClassPredicateKind.None;
    }

    private static bool MatchesExactly(ulong low, ulong high, Func<byte, bool> predicate)
    {
        for (var value = 0; value < 128; value++)
        {
            var stored = value < 64
                ? (low & (1UL << value)) != 0
                : (high & (1UL << (value - 64))) != 0;
            if (stored != predicate((byte)value))
            {
                return false;
            }
        }

        return true;
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

internal enum AsciiCharClassPredicateKind : byte
{
    None = 0,
    Digit = 1,
    AsciiLetter = 2,
    AsciiLetterOrDigit = 3,
    AsciiLetterDigitUnderscore = 4,
    AsciiHexDigit = 5,
}

internal readonly record struct Utf8InclusiveByteRange
{
    private Utf8InclusiveByteRange(byte low, byte high)
    {
        Low = low;
        High = high;
    }

    public byte Low { get; }

    public byte High { get; }

    public bool Contains(byte value) => value >= Low && value <= High;

    public static Utf8InclusiveByteRange Create(byte low, byte high)
    {
        if (low > high || high >= 0x80)
        {
            throw new ArgumentOutOfRangeException(nameof(high));
        }

        return new Utf8InclusiveByteRange(low, high);
    }
}
