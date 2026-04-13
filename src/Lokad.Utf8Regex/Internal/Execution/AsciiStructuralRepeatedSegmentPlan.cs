using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiStructuralRepeatedSegmentPlan
{
    public AsciiStructuralRepeatedSegmentPlan(
        AsciiCharClass leadingCharClass,
        AsciiCharClass trailingCharClass,
        int trailingMinCount,
        string separatorSet,
        int separatorMinCount,
        int repetitionMinCount,
        int repetitionMaxCount)
    {
        LeadingCharClass = leadingCharClass;
        TrailingCharClass = trailingCharClass;
        TrailingMinCount = trailingMinCount;
        SeparatorSet = separatorSet;
        SeparatorMinCount = separatorMinCount;
        RepetitionMinCount = repetitionMinCount;
        RepetitionMaxCount = repetitionMaxCount;
    }

    public AsciiCharClass? LeadingCharClass { get; }

    public AsciiCharClass? TrailingCharClass { get; }

    public int TrailingMinCount { get; }

    public string SeparatorSet { get; }

    public int SeparatorMinCount { get; }

    public int RepetitionMinCount { get; }

    public int RepetitionMaxCount { get; }

    public bool HasValue =>
        LeadingCharClass is not null &&
        TrailingCharClass is not null &&
        TrailingMinCount > 0 &&
        RepetitionMinCount > 0 &&
        RepetitionMaxCount >= RepetitionMinCount &&
        SeparatorSet is not null;

    public bool MatchesSeparator(byte value)
    {
        return value < 128 && RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, SeparatorSet);
    }
}
