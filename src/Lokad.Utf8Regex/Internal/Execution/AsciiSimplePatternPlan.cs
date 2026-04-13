namespace Lokad.Utf8Regex.Internal.Execution;

using System.Buffers;
using Lokad.Utf8Regex.Internal.Utilities;

internal readonly struct AsciiSimplePatternPlan
{
    public AsciiSimplePatternPlan(
        AsciiSimplePatternToken[][] branches,
        int searchLiteralOffset,
        byte[][] searchLiterals,
        AsciiFixedLiteralCheck[] fixedLiteralChecks,
        bool isStartAnchored,
        bool isEndAnchored,
        bool allowsTrailingNewlineBeforeEnd,
        bool ignoreCase,
        bool isUtf8ByteSafe,
        AsciiSimplePatternRunPlan runPlan = default,
        AsciiSimplePatternAnchoredHeadTailRunPlan anchoredHeadTailRunPlan = default,
        AsciiSimplePatternAnchoredValidatorPlan anchoredValidatorPlan = default,
        AsciiSimplePatternAnchoredBoundedDatePlan anchoredBoundedDatePlan = default,
        AsciiSimplePatternRepeatedDigitGroupPlan repeatedDigitGroupPlan = default,
        AsciiSimplePatternBoundedSuffixLiteralPlan boundedSuffixLiteralPlan = default,
        AsciiSimplePatternSymmetricLiteralWindowPlan symmetricLiteralWindowPlan = default)
    {
        Branches = branches;
        MinLength = 0;
        MaxLength = 0;
        if (branches.Length > 0)
        {
            MinLength = int.MaxValue;
            foreach (var branch in branches)
            {
                if (branch.Length < MinLength)
                {
                    MinLength = branch.Length;
                }

                if (branch.Length > MaxLength)
                {
                    MaxLength = branch.Length;
                }
            }
        }

        SearchLiteralOffset = searchLiteralOffset;
        SearchLiterals = searchLiterals;
        FixedLiteralChecks = fixedLiteralChecks;
        IsStartAnchored = isStartAnchored;
        IsEndAnchored = isEndAnchored;
        AllowsTrailingNewlineBeforeEnd = allowsTrailingNewlineBeforeEnd;
        IgnoreCase = ignoreCase;
        IsUtf8ByteSafe = isUtf8ByteSafe;
        RunPlan = runPlan;
        AnchoredHeadTailRunPlan = anchoredHeadTailRunPlan;
        AnchoredValidatorPlan = anchoredValidatorPlan;
        AnchoredBoundedDatePlan = anchoredBoundedDatePlan;
        RepeatedDigitGroupPlan = repeatedDigitGroupPlan;
        BoundedSuffixLiteralPlan = boundedSuffixLiteralPlan;
        SymmetricLiteralWindowPlan = symmetricLiteralWindowPlan;
    }

    public AsciiSimplePatternToken[][] Branches { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public bool IsFixedLength => MinLength == MaxLength;

    public int SearchLiteralOffset { get; }

    public byte[][] SearchLiterals { get; }

    public AsciiFixedLiteralCheck[] FixedLiteralChecks { get; }

    public bool IsStartAnchored { get; }

    public bool IsEndAnchored { get; }

    public bool AllowsTrailingNewlineBeforeEnd { get; }

    public bool IgnoreCase { get; }

    public bool IsUtf8ByteSafe { get; }

    public AsciiSimplePatternRunPlan RunPlan { get; }

    public AsciiSimplePatternAnchoredHeadTailRunPlan AnchoredHeadTailRunPlan { get; }

    public AsciiSimplePatternAnchoredValidatorPlan AnchoredValidatorPlan { get; }

    public AsciiSimplePatternAnchoredBoundedDatePlan AnchoredBoundedDatePlan { get; }

    public AsciiSimplePatternRepeatedDigitGroupPlan RepeatedDigitGroupPlan { get; }

    public AsciiSimplePatternBoundedSuffixLiteralPlan BoundedSuffixLiteralPlan { get; }

    public AsciiSimplePatternSymmetricLiteralWindowPlan SymmetricLiteralWindowPlan { get; }

    public Utf8CompiledPatternFamilyPlan CompiledPatternFamily => Utf8CompiledPatternFamilyPlan.FromSimplePatternPlan(this);

    public Utf8CompiledPatternCategory CompiledPatternCategory => CompiledPatternFamily.Category;

    public bool HasWholeInputCompiledSpecialization => CompiledPatternCategory == Utf8CompiledPatternCategory.AnchoredWhole;

    public bool HasSearchCompiledSpecialization => CompiledPatternCategory == Utf8CompiledPatternCategory.SearchGuided;
}

internal enum Utf8CompiledPatternFamilyKind : byte
{
    None = 0,
    AnchoredValidator = 1,
    AnchoredBoundedDate = 2,
    RepeatedDigitGroup = 3,
    BoundedSuffixLiteral = 4,
    SymmetricLiteralWindow = 5,
}

internal readonly struct Utf8CompiledPatternFamilyPlan
{
    public Utf8CompiledPatternFamilyPlan(
        Utf8CompiledPatternFamilyKind kind,
        AsciiSimplePatternAnchoredValidatorPlan anchoredValidatorPlan = default,
        AsciiSimplePatternAnchoredBoundedDatePlan anchoredBoundedDatePlan = default,
        AsciiSimplePatternRepeatedDigitGroupPlan repeatedDigitGroupPlan = default,
        AsciiSimplePatternBoundedSuffixLiteralPlan boundedSuffixLiteralPlan = default,
        AsciiSimplePatternSymmetricLiteralWindowPlan symmetricLiteralWindowPlan = default)
    {
        Kind = kind;
        AnchoredValidatorPlan = anchoredValidatorPlan;
        AnchoredBoundedDatePlan = anchoredBoundedDatePlan;
        RepeatedDigitGroupPlan = repeatedDigitGroupPlan;
        BoundedSuffixLiteralPlan = boundedSuffixLiteralPlan;
        SymmetricLiteralWindowPlan = symmetricLiteralWindowPlan;
    }

    public Utf8CompiledPatternFamilyKind Kind { get; }

    public AsciiSimplePatternAnchoredValidatorPlan AnchoredValidatorPlan { get; }

    public AsciiSimplePatternAnchoredBoundedDatePlan AnchoredBoundedDatePlan { get; }

    public AsciiSimplePatternRepeatedDigitGroupPlan RepeatedDigitGroupPlan { get; }

    public AsciiSimplePatternBoundedSuffixLiteralPlan BoundedSuffixLiteralPlan { get; }

    public AsciiSimplePatternSymmetricLiteralWindowPlan SymmetricLiteralWindowPlan { get; }

    public bool HasValue => Kind != Utf8CompiledPatternFamilyKind.None;

    public Utf8CompiledPatternCategory Category => Utf8CompiledPatternCategories.GetSimplePatternCategory(this);

    public bool HasWholeInputSpecialization => Category == Utf8CompiledPatternCategory.AnchoredWhole;

    public bool HasSearchSpecialization => Category == Utf8CompiledPatternCategory.SearchGuided;

    public static Utf8CompiledPatternFamilyPlan FromSimplePatternPlan(AsciiSimplePatternPlan plan)
    {
        if (plan.AnchoredValidatorPlan.HasValue)
        {
            return new Utf8CompiledPatternFamilyPlan(
                Utf8CompiledPatternFamilyKind.AnchoredValidator,
                anchoredValidatorPlan: plan.AnchoredValidatorPlan);
        }

        if (plan.AnchoredBoundedDatePlan.HasValue)
        {
            return new Utf8CompiledPatternFamilyPlan(
                Utf8CompiledPatternFamilyKind.AnchoredBoundedDate,
                anchoredBoundedDatePlan: plan.AnchoredBoundedDatePlan);
        }

        if (plan.RepeatedDigitGroupPlan.HasValue)
        {
            return new Utf8CompiledPatternFamilyPlan(
                Utf8CompiledPatternFamilyKind.RepeatedDigitGroup,
                repeatedDigitGroupPlan: plan.RepeatedDigitGroupPlan);
        }

        if (plan.BoundedSuffixLiteralPlan.HasValue)
        {
            return new Utf8CompiledPatternFamilyPlan(
                Utf8CompiledPatternFamilyKind.BoundedSuffixLiteral,
                boundedSuffixLiteralPlan: plan.BoundedSuffixLiteralPlan);
        }

        if (plan.SymmetricLiteralWindowPlan.HasValue)
        {
            return new Utf8CompiledPatternFamilyPlan(
                Utf8CompiledPatternFamilyKind.SymmetricLiteralWindow,
                symmetricLiteralWindowPlan: plan.SymmetricLiteralWindowPlan);
        }

        return default;
    }
}

internal readonly struct AsciiSimplePatternRunPlan
{
    public AsciiSimplePatternRunPlan(AsciiCharClass charClass, int minLength, int maxLength)
    {
        CharClass = charClass;
        PredicateKind = charClass.TryGetKnownPredicateKind(out var predicateKind)
            ? predicateKind
            : AsciiCharClassPredicateKind.None;
        MinLength = minLength;
        MaxLength = maxLength;
        Search = PreparedByteSearch.Create(charClass.GetPositiveMatchBytes());
    }

    public AsciiCharClass? CharClass { get; }

    public AsciiCharClassPredicateKind PredicateKind { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public PreparedByteSearch Search { get; }

    public bool HasValue => CharClass is not null && MinLength > 0 && MaxLength >= MinLength;
}

internal readonly struct AsciiFixedLiteralCheck
{
    public AsciiFixedLiteralCheck(int offset, byte[] literal)
    {
        Offset = offset;
        Literal = literal;
    }

    public int Offset { get; }

    public byte[] Literal { get; }
}

internal readonly struct AsciiSimplePatternAnchoredHeadTailRunPlan
{
    public AsciiSimplePatternAnchoredHeadTailRunPlan(
        AsciiCharClass headCharClass,
        AsciiCharClass tailCharClass,
        int tailMinLength)
    {
        HeadCharClass = headCharClass;
        TailCharClass = tailCharClass;
        TailMinLength = tailMinLength;
        TailBytes = tailCharClass.GetPositiveMatchBytes();
        TailSearchValues = TailBytes.Length > 0 ? SearchValues.Create(TailBytes) : null;
    }

    public AsciiCharClass? HeadCharClass { get; }

    public AsciiCharClass? TailCharClass { get; }

    public int TailMinLength { get; }

    public byte[] TailBytes { get; }

    public SearchValues<byte>? TailSearchValues { get; }

    public bool HasValue => HeadCharClass is not null && TailCharClass is not null && TailMinLength >= 0;

    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        if (!HasValue || input.Length < 1 + TailMinLength)
        {
            return false;
        }

        if (!HeadCharClass!.Contains(input[0]))
        {
            return false;
        }

        if (!TailCharClass!.Negated && TailSearchValues is not null)
        {
            return input[1..].IndexOfAnyExcept(TailSearchValues) < 0;
        }

        for (var i = 1; i < input.Length; i++)
        {
            if (!TailCharClass.Contains(input[i]))
            {
                return false;
            }
        }

        return true;
    }
}

internal readonly struct AsciiSimplePatternAnchoredValidatorPlan
{
    public AsciiSimplePatternAnchoredValidatorPlan(AsciiSimplePatternAnchoredValidatorSegment[] segments, bool ignoreCase = false)
    {
        Segments = segments;
        IgnoreCase = ignoreCase;
    }

    public AsciiSimplePatternAnchoredValidatorSegment[] Segments { get; }

    public bool IgnoreCase { get; }

    public bool HasValue => Segments is { Length: > 0 };
}

internal readonly struct AsciiSimplePatternAnchoredValidatorSegment
{
    public AsciiSimplePatternAnchoredValidatorSegment(byte[] literal)
    {
        Literal = literal;
        CharClass = null;
        MinLength = literal.Length;
        MaxLength = literal.Length;
    }

    public AsciiSimplePatternAnchoredValidatorSegment(AsciiCharClass charClass, int minLength, int maxLength)
    {
        Literal = [];
        CharClass = charClass;
        PredicateKind = charClass.TryGetKnownPredicateKind(out var predicateKind)
            ? predicateKind
            : AsciiCharClassPredicateKind.None;
        MinLength = minLength;
        MaxLength = maxLength;
    }

    public byte[] Literal { get; }

    public AsciiCharClass? CharClass { get; }

    public AsciiCharClassPredicateKind PredicateKind { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public bool IsLiteral => Literal.Length > 0;
}

internal readonly struct AsciiSimplePatternAnchoredBoundedDatePlan
{
    public AsciiSimplePatternAnchoredBoundedDatePlan(
        byte firstFieldMinCount,
        byte firstFieldMaxCount,
        byte secondFieldMinCount,
        byte secondFieldMaxCount,
        byte thirdFieldMinCount,
        byte thirdFieldMaxCount,
        byte separatorByte,
        byte secondSeparatorByte)
    {
        FirstFieldMinCount = firstFieldMinCount;
        FirstFieldMaxCount = firstFieldMaxCount;
        SecondFieldMinCount = secondFieldMinCount;
        SecondFieldMaxCount = secondFieldMaxCount;
        ThirdFieldMinCount = thirdFieldMinCount;
        ThirdFieldMaxCount = thirdFieldMaxCount;
        SeparatorByte = separatorByte;
        SecondSeparatorByte = secondSeparatorByte;
    }

    public byte FirstFieldMinCount { get; }

    public byte FirstFieldMaxCount { get; }

    public byte SecondFieldMinCount { get; }

    public byte SecondFieldMaxCount { get; }

    public byte ThirdFieldMinCount { get; }

    public byte ThirdFieldMaxCount { get; }

    public byte SeparatorByte { get; }

    public byte SecondSeparatorByte { get; }

    public bool HasValue => FirstFieldMinCount > 0;
}

internal readonly struct AsciiSimplePatternRepeatedDigitGroupPlan
{
    public AsciiSimplePatternRepeatedDigitGroupPlan(
        byte repeatedGroupCount,
        byte groupDigitCount,
        byte trailingMinDigits,
        byte trailingMaxDigits,
        byte[] separatorBytes)
    {
        RepeatedGroupCount = repeatedGroupCount;
        GroupDigitCount = groupDigitCount;
        TrailingMinDigits = trailingMinDigits;
        TrailingMaxDigits = trailingMaxDigits;
        SeparatorBytes = separatorBytes;
    }

    public byte RepeatedGroupCount { get; }

    public byte GroupDigitCount { get; }

    public byte TrailingMinDigits { get; }

    public byte TrailingMaxDigits { get; }

    public byte[] SeparatorBytes { get; }

    public int MinimumLength => RepeatedGroupCount * (GroupDigitCount + 1) + TrailingMinDigits;

    public int MaximumLength => RepeatedGroupCount * (GroupDigitCount + 1) + TrailingMaxDigits;

    public bool HasValue => RepeatedGroupCount > 0 && GroupDigitCount > 0 && TrailingMinDigits > 0 && SeparatorBytes.Length > 0;
}

internal readonly struct AsciiSimplePatternBoundedSuffixLiteralPlan
{
    public AsciiSimplePatternBoundedSuffixLiteralPlan(
        AsciiCharClass prefixCharClass,
        AsciiCharClass repeatedCharClass,
        int repeatedMinLength,
        int repeatedMaxLength,
        byte[] literalUtf8,
        AsciiCharClass suffixCharClass)
    {
        PrefixCharClass = prefixCharClass;
        RepeatedCharClass = repeatedCharClass;
        RepeatedMinLength = repeatedMinLength;
        RepeatedMaxLength = repeatedMaxLength;
        LiteralUtf8 = literalUtf8;
        SuffixCharClass = suffixCharClass;
        LiteralLastByte = literalUtf8[^1];
    }

    public AsciiCharClass? PrefixCharClass { get; }

    public AsciiCharClass? RepeatedCharClass { get; }

    public int RepeatedMinLength { get; }

    public int RepeatedMaxLength { get; }

    public byte[] LiteralUtf8 { get; }

    public AsciiCharClass? SuffixCharClass { get; }

    public byte LiteralLastByte { get; }

    public bool HasValue =>
        PrefixCharClass is not null &&
        RepeatedCharClass is not null &&
        RepeatedMinLength >= 0 &&
        RepeatedMaxLength >= RepeatedMinLength &&
        LiteralUtf8.Length > 0 &&
        SuffixCharClass is not null;
}

internal readonly struct AsciiSimplePatternSymmetricLiteralWindowPlan
{
    public AsciiSimplePatternSymmetricLiteralWindowPlan(
        byte[] firstLiteralUtf8,
        byte[] secondLiteralUtf8,
        AsciiExactLiteralSearchData searchData,
        int anchorOffset,
        byte anchorByteA,
        byte anchorByteB,
        int minGap,
        int maxGap,
        bool gapSameLine,
        int firstFilterOffset,
        byte firstFilterByteA,
        byte firstFilterByteB,
        int secondFilterOffset,
        byte secondFilterByteA,
        byte secondFilterByteB)
    {
        FirstLiteralUtf8 = firstLiteralUtf8;
        SecondLiteralUtf8 = secondLiteralUtf8;
        SearchData = searchData;
        AnchorOffset = anchorOffset;
        AnchorByteA = anchorByteA;
        AnchorByteB = anchorByteB;
        MinGap = minGap;
        MaxGap = maxGap;
        GapSameLine = gapSameLine;
        FirstFilterOffset = firstFilterOffset;
        FirstFilterByteA = firstFilterByteA;
        FirstFilterByteB = firstFilterByteB;
        SecondFilterOffset = secondFilterOffset;
        SecondFilterByteA = secondFilterByteA;
        SecondFilterByteB = secondFilterByteB;
    }

    public byte[] FirstLiteralUtf8 { get; }

    public byte[] SecondLiteralUtf8 { get; }

    public AsciiExactLiteralSearchData SearchData { get; }

    public int AnchorOffset { get; }

    public byte AnchorByteA { get; }

    public byte AnchorByteB { get; }

    public int MinGap { get; }

    public int MaxGap { get; }

    public bool GapSameLine { get; }

    public int FirstFilterOffset { get; }

    public byte FirstFilterByteA { get; }

    public byte FirstFilterByteB { get; }

    public int SecondFilterOffset { get; }

    public byte SecondFilterByteA { get; }

    public byte SecondFilterByteB { get; }

    public bool HasValue =>
        FirstLiteralUtf8 is { Length: > 0 } &&
        SecondLiteralUtf8 is { Length: > 0 } &&
        MaxGap >= MinGap &&
        FirstLiteralUtf8[0] != SecondLiteralUtf8[0];
}
