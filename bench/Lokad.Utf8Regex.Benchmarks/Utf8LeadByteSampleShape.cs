namespace Lokad.Utf8Regex.Benchmarks;

internal readonly struct Utf8LeadByteSampleShape
{
    public Utf8LeadByteSampleShape(
        int sampleLength,
        int asciiBytes,
        int firstNonAsciiOffset,
        int twoByteLeads,
        int threeByteLeads,
        int fourByteLeads)
    {
        SampleLength = sampleLength;
        AsciiBytes = asciiBytes;
        FirstNonAsciiOffset = firstNonAsciiOffset;
        TwoByteLeads = twoByteLeads;
        ThreeByteLeads = threeByteLeads;
        FourByteLeads = fourByteLeads;
    }

    public int SampleLength { get; }

    public int AsciiBytes { get; }

    public int FirstNonAsciiOffset { get; }

    public int TwoByteLeads { get; }

    public int ThreeByteLeads { get; }

    public int FourByteLeads { get; }
}

internal static class Utf8LeadByteSampling
{
    public static Utf8LeadByteSampleShape DescribeLeadByteSample(ReadOnlySpan<byte> input)
        => DescribeLeadByteSample(input, 4096);

    public static Utf8LeadByteSampleShape DescribeLeadByteSample(ReadOnlySpan<byte> input, int maxBytes)
    {
        var sampleLength = Math.Min(input.Length, maxBytes);
        var asciiBytes = 0;
        var firstNonAsciiOffset = -1;
        var twoByteLeads = 0;
        var threeByteLeads = 0;
        var fourByteLeads = 0;

        for (var i = 0; i < sampleLength; i++)
        {
            var value = input[i];
            if (value < 0x80)
            {
                asciiBytes++;
                continue;
            }

            if (firstNonAsciiOffset < 0)
            {
                firstNonAsciiOffset = i;
            }

            if (value is >= 0xC2 and < 0xE0)
            {
                twoByteLeads++;
            }
            else if (value is >= 0xE0 and < 0xF0)
            {
                threeByteLeads++;
            }
            else if (value is >= 0xF0 and < 0xF5)
            {
                fourByteLeads++;
            }
        }

        return new Utf8LeadByteSampleShape(
            sampleLength,
            asciiBytes,
            firstNonAsciiOffset,
            twoByteLeads,
            threeByteLeads,
            fourByteLeads);
    }

}
