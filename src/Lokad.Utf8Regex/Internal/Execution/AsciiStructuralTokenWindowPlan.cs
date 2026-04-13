using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiStructuralTokenWindowPlan
{
    public AsciiStructuralTokenWindowPlan(
        AsciiCharClass leadingCharClass,
        int leadingLength,
        string separatorSet,
        int separatorMinCount,
        int leadingGapMax,
        byte[] anchorLiteralUtf8,
        int trailingGapMax,
        AsciiCharClass trailingCharClass,
        int trailingLength)
    {
        LeadingCharClass = leadingCharClass;
        LeadingLength = leadingLength;
        SeparatorSet = separatorSet;
        SeparatorMinCount = separatorMinCount;
        LeadingGapMax = leadingGapMax;
        AnchorLiteralUtf8 = anchorLiteralUtf8;
        TrailingGapMax = trailingGapMax;
        TrailingCharClass = trailingCharClass;
        TrailingLength = trailingLength;
        LeadingRunPlan = new AsciiSimplePatternRunPlan(leadingCharClass, leadingLength, leadingLength);
    }

    public AsciiCharClass? LeadingCharClass { get; }

    public int LeadingLength { get; }

    public string SeparatorSet { get; }

    public int SeparatorMinCount { get; }

    public int LeadingGapMax { get; }

    public byte[] AnchorLiteralUtf8 { get; }

    public int TrailingGapMax { get; }

    public AsciiCharClass? TrailingCharClass { get; }

    public int TrailingLength { get; }

    public AsciiSimplePatternRunPlan LeadingRunPlan { get; }

    public bool HasValue =>
        LeadingCharClass is not null &&
        TrailingCharClass is not null &&
        LeadingLength > 0 &&
        TrailingLength > 0 &&
        !string.IsNullOrEmpty(SeparatorSet) &&
        SeparatorMinCount > 0 &&
        AnchorLiteralUtf8 is { Length: > 0 };

    public bool MatchesSeparator(byte value)
    {
        return value < 128 && RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, SeparatorSet);
    }
}
