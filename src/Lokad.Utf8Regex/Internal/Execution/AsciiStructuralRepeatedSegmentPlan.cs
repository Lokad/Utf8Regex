namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiStructuralRepeatedSegmentPlan
{
    public AsciiStructuralRepeatedSegmentPlan(
        AsciiCharClass leadingCharClass,
        AsciiCharClass trailingCharClass,
        int trailingMinCount,
        AsciiCharClass separatorCharClass,
        int separatorMinCount,
        int repetitionMinCount,
        int repetitionMaxCount)
    {
        LeadingCharClass = leadingCharClass;
        TrailingCharClass = trailingCharClass;
        TrailingMinCount = trailingMinCount;
        SeparatorCharClass = separatorCharClass;
        SeparatorMinCount = separatorMinCount;
        RepetitionMinCount = repetitionMinCount;
        RepetitionMaxCount = repetitionMaxCount;
    }

    public AsciiCharClass LeadingCharClass { get; }

    public AsciiCharClass TrailingCharClass { get; }

    public int TrailingMinCount { get; }

    public AsciiCharClass SeparatorCharClass { get; }

    public int SeparatorMinCount { get; }

    public int RepetitionMinCount { get; }

    public int RepetitionMaxCount { get; }

    public bool HasValue =>
        !LeadingCharClass.IsEmpty &&
        !TrailingCharClass.IsEmpty &&
        TrailingMinCount > 0 &&
        RepetitionMinCount > 0 &&
        RepetitionMaxCount >= RepetitionMinCount &&
        !SeparatorCharClass.IsEmpty;

    public bool MatchesSeparator(byte value)
    {
        return SeparatorCharClass.Contains(value);
    }
}
