namespace Lokad.Utf8Regex.Internal.Search;

/// <summary>Compact immutable membership carrier for ASCII search kernels.</summary>
internal readonly struct Utf8SearchAsciiSet
{
    private Utf8SearchAsciiSet(ulong low, ulong high)
    {
        Low = low;
        High = high;
    }

    public ulong Low { get; }

    public ulong High { get; }

    public bool HasValue => (Low | High) != 0;

    public bool Contains(byte value)
    {
        if (value >= 128)
        {
            return false;
        }

        var bit = 1UL << (value & 63);
        return value < 64 ? (Low & bit) != 0 : (High & bit) != 0;
    }

    public Utf8SearchAsciiSet With(byte value)
    {
        if (value >= 128)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var bit = 1UL << (value & 63);
        return value < 64
            ? new Utf8SearchAsciiSet(Low | bit, High)
            : new Utf8SearchAsciiSet(Low, High | bit);
    }

    public static Utf8SearchAsciiSet FromBytes(ReadOnlySpan<byte> values)
    {
        var result = default(Utf8SearchAsciiSet);
        foreach (var value in values)
        {
            result = result.With(value);
        }

        return result;
    }
}
