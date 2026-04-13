namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal readonly struct Utf8AnalyzedSearchInfo
{
    public Utf8AnalyzedSearchInfo(
        Utf8SearchKind kind,
        byte[]? literalUtf8 = null,
        byte[][]? alternateLiteralsUtf8 = null,
        bool canGuideFallbackStarts = false,
        byte[]? requiredPrefilterLiteralUtf8 = null,
        byte[][]? requiredPrefilterAlternateLiteralsUtf8 = null,
        string? secondaryRequiredPrefilterQuotedAsciiSet = null,
        int secondaryRequiredPrefilterQuotedAsciiLength = 0,
        Utf8FixedDistanceSet[]? fixedDistanceSets = null,
        byte[]? trailingLiteralUtf8 = null,
        byte[][]? orderedWindowLeadingLiteralsUtf8 = null,
        byte[]? orderedWindowTrailingLiteralUtf8 = null,
        Utf8WindowSearchInfo[]? requiredWindowPrefilters = null,
        int? orderedWindowMaxGap = null,
        bool orderedWindowSameLine = false,
        Utf8FallbackStartTransform fallbackStartTransform = default,
        int distance = 0,
        int minRequiredLength = 0,
        int? exactRequiredLength = null,
        int? maxPossibleLength = null,
        Utf8BoundaryRequirement leadingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement trailingBoundary = Utf8BoundaryRequirement.None)
    {
        Kind = kind;
        LiteralUtf8 = literalUtf8;
        AlternateLiteralsUtf8 = alternateLiteralsUtf8;
        CanGuideFallbackStarts = canGuideFallbackStarts;
        RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteralUtf8;
        RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiteralsUtf8;
        SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet;
        SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength;
        FixedDistanceSets = fixedDistanceSets;
        TrailingLiteralUtf8 = trailingLiteralUtf8;
        OrderedWindowLeadingLiteralsUtf8 = orderedWindowLeadingLiteralsUtf8;
        OrderedWindowTrailingLiteralUtf8 = orderedWindowTrailingLiteralUtf8;
        RequiredWindowPrefilters = requiredWindowPrefilters;
        OrderedWindowMaxGap = orderedWindowMaxGap;
        OrderedWindowSameLine = orderedWindowSameLine;
        FallbackStartTransform = fallbackStartTransform;
        Distance = distance;
        MinRequiredLength = minRequiredLength;
        ExactRequiredLength = exactRequiredLength;
        MaxPossibleLength = maxPossibleLength;
        LeadingBoundary = leadingBoundary;
        TrailingBoundary = trailingBoundary;
    }

    public Utf8SearchKind Kind { get; }

    public byte[]? LiteralUtf8 { get; }

    public byte[][]? AlternateLiteralsUtf8 { get; }

    public bool CanGuideFallbackStarts { get; }

    public byte[]? RequiredPrefilterLiteralUtf8 { get; }

    public byte[][]? RequiredPrefilterAlternateLiteralsUtf8 { get; }

    public string? SecondaryRequiredPrefilterQuotedAsciiSet { get; }

    public int SecondaryRequiredPrefilterQuotedAsciiLength { get; }

    public Utf8FixedDistanceSet[]? FixedDistanceSets { get; }

    public byte[]? TrailingLiteralUtf8 { get; }

    public byte[][]? OrderedWindowLeadingLiteralsUtf8 { get; }

    public byte[]? OrderedWindowTrailingLiteralUtf8 { get; }

    public Utf8WindowSearchInfo[]? RequiredWindowPrefilters { get; }

    public int? OrderedWindowMaxGap { get; }

    public bool OrderedWindowSameLine { get; }

    public Utf8FallbackStartTransform FallbackStartTransform { get; }

    public int Distance { get; }

    public int MinRequiredLength { get; }

    public int? ExactRequiredLength { get; }

    public int? MaxPossibleLength { get; }

    public Utf8BoundaryRequirement LeadingBoundary { get; }

    public Utf8BoundaryRequirement TrailingBoundary { get; }
}
