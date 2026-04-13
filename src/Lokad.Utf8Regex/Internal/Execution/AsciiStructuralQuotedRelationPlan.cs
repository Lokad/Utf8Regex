namespace Lokad.Utf8Regex.Internal.Execution;

internal enum AsciiStructuralQuotedOperandKind : byte
{
    None = 0,
    QuotedPrefixedRun = 1,
    QuotedAsciiRun = 2,
}

internal readonly struct AsciiStructuralQuotedRelationBranchPlan
{
    public AsciiStructuralQuotedRelationBranchPlan(
        AsciiStructuralQuotedOperandKind leadingKind,
        bool leadingRepeat,
        AsciiStructuralQuotedOperandKind trailingKind,
        bool trailingRepeat,
        int maxLineBreaks)
    {
        LeadingKind = leadingKind;
        LeadingRepeat = leadingRepeat;
        TrailingKind = trailingKind;
        TrailingRepeat = trailingRepeat;
        MaxLineBreaks = maxLineBreaks;
    }

    public AsciiStructuralQuotedOperandKind LeadingKind { get; }

    public bool LeadingRepeat { get; }

    public AsciiStructuralQuotedOperandKind TrailingKind { get; }

    public bool TrailingRepeat { get; }

    public int MaxLineBreaks { get; }

    public bool HasValue =>
        LeadingKind != AsciiStructuralQuotedOperandKind.None &&
        TrailingKind != AsciiStructuralQuotedOperandKind.None &&
        MaxLineBreaks >= 0;
}

internal readonly struct AsciiStructuralQuotedRelationPlan
{
    public AsciiStructuralQuotedRelationPlan(
        byte[][] prefixesUtf8,
        AsciiCharClass prefixedTailClass,
        int prefixedTailLength,
        AsciiCharClass quotedRunClass,
        int quotedRunLength,
        AsciiStructuralQuotedRelationBranchPlan firstBranch,
        AsciiStructuralQuotedRelationBranchPlan secondBranch)
    {
        PrefixesUtf8 = prefixesUtf8;
        PrefixedTailClass = prefixedTailClass;
        PrefixedTailLength = prefixedTailLength;
        QuotedRunClass = quotedRunClass;
        QuotedRunLength = quotedRunLength;
        FirstBranch = firstBranch;
        SecondBranch = secondBranch;
    }

    public byte[][] PrefixesUtf8 { get; }

    public AsciiCharClass? PrefixedTailClass { get; }

    public int PrefixedTailLength { get; }

    public AsciiCharClass? QuotedRunClass { get; }

    public int QuotedRunLength { get; }

    public AsciiStructuralQuotedRelationBranchPlan FirstBranch { get; }

    public AsciiStructuralQuotedRelationBranchPlan SecondBranch { get; }

    public bool HasValue =>
        PrefixesUtf8 is { Length: > 0 } &&
        PrefixedTailClass is not null &&
        PrefixedTailLength > 0 &&
        QuotedRunClass is not null &&
        QuotedRunLength > 0 &&
        FirstBranch.HasValue &&
        SecondBranch.HasValue;
}
