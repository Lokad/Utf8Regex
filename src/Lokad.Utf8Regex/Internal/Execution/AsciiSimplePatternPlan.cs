using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.Execution;

using System.Buffers;

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
        AsciiSimplePatternRunPlan runPlan,
        AsciiSimplePatternAnchoredHeadTailRunPlan anchoredHeadTailRunPlan,
        AsciiSimplePatternAnchoredValidatorPlan anchoredValidatorPlan,
        AsciiSimplePatternAnchoredBoundedDatePlan anchoredBoundedDatePlan,
        AsciiSimplePatternAnchoredOptionalFieldPlan anchoredOptionalFieldPlan,
        AsciiSimplePatternRepeatedDigitGroupPlan repeatedDigitGroupPlan,
        AsciiSimplePatternBoundedSuffixLiteralPlan boundedSuffixLiteralPlan,
        AsciiSimplePatternSymmetricLiteralWindowPlan symmetricLiteralWindowPlan)
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
        AnchoredOptionalFieldPlan = anchoredOptionalFieldPlan;
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

    public AsciiSimplePatternAnchoredOptionalFieldPlan AnchoredOptionalFieldPlan { get; }

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
    private Utf8CompiledPatternFamilyPlan(
        Utf8CompiledPatternFamilyKind kind,
        AsciiSimplePatternAnchoredValidatorPlan anchoredValidatorPlan,
        AsciiSimplePatternAnchoredBoundedDatePlan anchoredBoundedDatePlan,
        AsciiSimplePatternRepeatedDigitGroupPlan repeatedDigitGroupPlan,
        AsciiSimplePatternBoundedSuffixLiteralPlan boundedSuffixLiteralPlan,
        AsciiSimplePatternSymmetricLiteralWindowPlan symmetricLiteralWindowPlan)
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

    private static Utf8CompiledPatternFamilyPlan ForAnchoredValidator(AsciiSimplePatternAnchoredValidatorPlan plan) =>
        new(Utf8CompiledPatternFamilyKind.AnchoredValidator, plan, default, default, default, default);

    private static Utf8CompiledPatternFamilyPlan ForAnchoredBoundedDate(AsciiSimplePatternAnchoredBoundedDatePlan plan) =>
        new(Utf8CompiledPatternFamilyKind.AnchoredBoundedDate, default, plan, default, default, default);

    private static Utf8CompiledPatternFamilyPlan ForRepeatedDigitGroup(AsciiSimplePatternRepeatedDigitGroupPlan plan) =>
        new(Utf8CompiledPatternFamilyKind.RepeatedDigitGroup, default, default, plan, default, default);

    private static Utf8CompiledPatternFamilyPlan ForBoundedSuffixLiteral(AsciiSimplePatternBoundedSuffixLiteralPlan plan) =>
        new(Utf8CompiledPatternFamilyKind.BoundedSuffixLiteral, default, default, default, plan, default);

    private static Utf8CompiledPatternFamilyPlan ForSymmetricLiteralWindow(AsciiSimplePatternSymmetricLiteralWindowPlan plan) =>
        new(Utf8CompiledPatternFamilyKind.SymmetricLiteralWindow, default, default, default, default, plan);

    public static Utf8CompiledPatternFamilyPlan FromSimplePatternPlan(AsciiSimplePatternPlan plan)
    {
        if (plan.AnchoredBoundedDatePlan.HasValue)
        {
            return ForAnchoredBoundedDate(plan.AnchoredBoundedDatePlan);
        }

        if (plan.AnchoredValidatorPlan.HasValue)
        {
            return ForAnchoredValidator(plan.AnchoredValidatorPlan);
        }

        if (plan.RepeatedDigitGroupPlan.HasValue)
        {
            return ForRepeatedDigitGroup(plan.RepeatedDigitGroupPlan);
        }

        if (plan.BoundedSuffixLiteralPlan.HasValue)
        {
            return ForBoundedSuffixLiteral(plan.BoundedSuffixLiteralPlan);
        }

        if (plan.SymmetricLiteralWindowPlan.HasValue)
        {
            return ForSymmetricLiteralWindow(plan.SymmetricLiteralWindowPlan);
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

    public AsciiCharClass CharClass { get; }

    public AsciiCharClassPredicateKind PredicateKind { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public PreparedByteSearch Search { get; }

    public bool HasValue => !CharClass.IsEmpty && MinLength > 0 && MaxLength >= MinLength;
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

    public AsciiCharClass HeadCharClass { get; }

    public AsciiCharClass TailCharClass { get; }

    public int TailMinLength { get; }

    public byte[] TailBytes { get; }

    public SearchValues<byte>? TailSearchValues { get; }

    public bool HasValue => !HeadCharClass.IsEmpty && !TailCharClass.IsEmpty && TailMinLength >= 0;

    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        if (!HasValue || input.Length < 1 + TailMinLength)
        {
            return false;
        }

        if (!HeadCharClass.Contains(input[0]))
        {
            return false;
        }

        if (!TailCharClass.Negated && TailSearchValues is not null)
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
    public AsciiSimplePatternAnchoredValidatorPlan(AsciiSimplePatternAnchoredValidatorSegment[] segments)
        : this(segments, false)
    {
    }

    public AsciiSimplePatternAnchoredValidatorPlan(AsciiSimplePatternAnchoredValidatorSegment[] segments, bool ignoreCase)
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
    private readonly byte[]? _literal;
    private readonly AsciiCharClass _charClass;

    public AsciiSimplePatternAnchoredValidatorSegment(byte[] literal)
    {
        _literal = literal;
        _charClass = default;
        MinLength = literal.Length;
        MaxLength = literal.Length;
    }

    public AsciiSimplePatternAnchoredValidatorSegment(AsciiCharClass charClass, int minLength, int maxLength)
    {
        _literal = [];
        _charClass = charClass;
        PredicateKind = charClass.TryGetKnownPredicateKind(out var predicateKind)
            ? predicateKind
            : AsciiCharClassPredicateKind.None;
        MinLength = minLength;
        MaxLength = maxLength;
    }

    public byte[] Literal => _literal ?? [];

    public AsciiCharClass CharClass => _charClass;

    public AsciiCharClassPredicateKind PredicateKind { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public bool IsLiteral => _literal is { Length: > 0 };
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

internal readonly struct AsciiSimplePatternAnchoredOptionalFieldPlan
{
    public AsciiSimplePatternAnchoredOptionalFieldPlan(
        AsciiCharClass headClass,
        byte headMinCount,
        byte headMaxCount,
        AsciiCharClass firstRequiredClass,
        AsciiCharClass optionalClass,
        byte optionalLiteral,
        AsciiCharClass secondRequiredClass,
        AsciiCharClass tailClass,
        byte tailCount)
    {
        HeadClass = headClass;
        HeadMinCount = headMinCount;
        HeadMaxCount = headMaxCount;
        FirstRequiredClass = firstRequiredClass;
        OptionalClass = optionalClass;
        OptionalLiteral = optionalLiteral;
        SecondRequiredClass = secondRequiredClass;
        TailClass = tailClass;
        TailCount = tailCount;
        CanUseShortAsciiWholeMatcher =
            headMinCount == 1 &&
            headMaxCount == 2 &&
            tailCount == 2 &&
            HasPredicate(headClass, AsciiCharClassPredicateKind.AsciiLetter) &&
            HasPredicate(firstRequiredClass, AsciiCharClassPredicateKind.Digit) &&
            HasPredicate(optionalClass, AsciiCharClassPredicateKind.AsciiLetterOrDigit) &&
            !optionalClass.Contains(optionalLiteral) &&
            HasPredicate(secondRequiredClass, AsciiCharClassPredicateKind.Digit) &&
            HasPredicate(tailClass, AsciiCharClassPredicateKind.AsciiLetter);

        static bool HasPredicate(AsciiCharClass charClass, AsciiCharClassPredicateKind expected)
        {
            return charClass.TryGetKnownPredicateKind(out var actual) && actual == expected;
        }
    }

    public AsciiCharClass HeadClass { get; }

    public byte HeadMinCount { get; }

    public byte HeadMaxCount { get; }

    public AsciiCharClass FirstRequiredClass { get; }

    public AsciiCharClass OptionalClass { get; }

    public byte OptionalLiteral { get; }

    public AsciiCharClass SecondRequiredClass { get; }

    public AsciiCharClass TailClass { get; }

    public byte TailCount { get; }

    public bool CanUseShortAsciiWholeMatcher { get; }

    public bool HasValue => HeadMinCount > 0 &&
        HeadMaxCount >= HeadMinCount &&
        !HeadClass.IsEmpty &&
        !FirstRequiredClass.IsEmpty &&
        !OptionalClass.IsEmpty &&
        !SecondRequiredClass.IsEmpty &&
        !TailClass.IsEmpty &&
        TailCount > 0;
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
    private readonly AsciiCharClass _prefixCharClass;
    private readonly AsciiCharClass _repeatedCharClass;
    private readonly byte[]? _literalUtf8;
    private readonly AsciiCharClass _suffixCharClass;

    public AsciiSimplePatternBoundedSuffixLiteralPlan(
        AsciiCharClass prefixCharClass,
        AsciiCharClass repeatedCharClass,
        int repeatedMinLength,
        int repeatedMaxLength,
        byte[] literalUtf8,
        AsciiCharClass suffixCharClass)
    {
        _prefixCharClass = prefixCharClass;
        _repeatedCharClass = repeatedCharClass;
        RepeatedMinLength = repeatedMinLength;
        RepeatedMaxLength = repeatedMaxLength;
        _literalUtf8 = literalUtf8;
        _suffixCharClass = suffixCharClass;
        LiteralLastByte = literalUtf8[^1];
    }

    public AsciiCharClass PrefixCharClass => _prefixCharClass;

    public AsciiCharClass RepeatedCharClass => _repeatedCharClass;

    public int RepeatedMinLength { get; }

    public int RepeatedMaxLength { get; }

    public byte[] LiteralUtf8 => _literalUtf8 ?? [];

    public AsciiCharClass SuffixCharClass => _suffixCharClass;

    public byte LiteralLastByte { get; }

    public bool HasValue =>
        !_prefixCharClass.IsEmpty &&
        !_repeatedCharClass.IsEmpty &&
        RepeatedMinLength >= 0 &&
        RepeatedMaxLength >= RepeatedMinLength &&
        _literalUtf8 is { Length: > 0 } &&
        !_suffixCharClass.IsEmpty;
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
