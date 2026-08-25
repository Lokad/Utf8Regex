namespace Lokad.Utf8Regex.Benchmarks;

internal readonly record struct Pcre2BenchmarkResultChecksum(ulong Value)
{
    public override string ToString() => Value.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
}

internal static class Pcre2BenchmarkRangeSink
{
    internal static int Add(int sink, int start, int length)
        => unchecked(((sink * 16777619) ^ start) * 16777619 ^ length);

    internal static int Complete(int sink, int count, bool isMore)
        => unchecked(((sink * 16777619) ^ count) * 16777619 ^ (isMore ? 1 : 0));
}

internal struct Pcre2BenchmarkChecksumBuilder
{
    private const ulong OffsetBasis = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    private ulong _value;
    private int _rangeCount;

    internal Pcre2BenchmarkChecksumBuilder(Utf8Pcre2BenchmarkOperation operation)
    {
        _value = OffsetBasis;
        _rangeCount = 0;
        AppendByte(1);
        AppendByte((byte)operation);
    }

    internal void AddRange(int start, int length)
    {
        AppendByte(0xA1);
        AppendInt32(start);
        AppendInt32(length);
        _rangeCount++;
    }

    internal Pcre2BenchmarkResultChecksum Complete(int resultCount, bool isMore)
    {
        AppendByte(0xFF);
        AppendInt32(_rangeCount);
        AppendInt32(resultCount);
        AppendByte(isMore ? (byte)1 : (byte)0);
        return new Pcre2BenchmarkResultChecksum(_value);
    }

    private void AppendInt32(int value)
    {
        var bits = unchecked((uint)value);
        AppendByte((byte)bits);
        AppendByte((byte)(bits >> 8));
        AppendByte((byte)(bits >> 16));
        AppendByte((byte)(bits >> 24));
    }

    private void AppendByte(byte value)
    {
        _value ^= value;
        _value = unchecked(_value * Prime);
    }
}
