namespace Lokad.Utf8Regex.Internal.Input;

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
