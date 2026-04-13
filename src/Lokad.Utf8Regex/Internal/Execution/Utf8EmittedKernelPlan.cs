namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8EmittedKernelBlockKind : byte
{
    None = 0,
    FindAnchorSet = 1,
    DispatchPrefixesAtAnchor = 2,
    ConsumeAsciiWhitespace = 3,
    RequireAsciiUpper = 4,
    ConsumeAsciiWordTail = 5,
    AcceptAndAdvance = 6,
    FindCommonPrefix = 7,
    MatchSharedPrefixSuffix = 8,
    FindTrailingLiteral = 9,
    ConsumeReverseAsciiWhitespace = 10,
    MatchLeadingLiteralBeforeSeparator = 11,
}

internal readonly struct Utf8EmittedKernelBlock
{
    public Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind kind)
    {
        Kind = kind;
    }

    public Utf8EmittedKernelBlockKind Kind { get; }
}

internal enum Utf8EmittedKernelKind : byte
{
    None = 0,
    UpperWordIdentifierFamily = 1,
    SharedPrefixAsciiWhitespaceSuffix = 2,
    OrderedAsciiWhitespaceLiteralWindow = 3,
    PairedOrderedAsciiWhitespaceLiteralWindow = 4,
}

internal readonly struct Utf8EmittedKernelPlan
{
    public Utf8EmittedKernelPlan(
        Utf8EmittedKernelKind kind,
        Utf8CompiledFindOptimization findOptimization,
        byte[][] prefixes,
        byte[][]? trailingLiterals = null,
        byte requiredSuffixByte = 0,
        int requiredSeparatorCount = 0,
        int maxGap = 0,
        bool gapSameLine = false,
        Utf8BoundaryRequirement leadingLeadingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement leadingTrailingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement trailingLeadingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement trailingTrailingBoundary = Utf8BoundaryRequirement.None,
        Utf8EmittedKernelBlock[]? blocks = null)
    {
        Kind = kind;
        FindOptimization = findOptimization;
        Prefixes = prefixes;
        TrailingLiterals = trailingLiterals;
        RequiredSuffixByte = requiredSuffixByte;
        RequiredSeparatorCount = requiredSeparatorCount;
        MaxGap = maxGap;
        GapSameLine = gapSameLine;
        LeadingLeadingBoundary = leadingLeadingBoundary;
        LeadingTrailingBoundary = leadingTrailingBoundary;
        TrailingLeadingBoundary = trailingLeadingBoundary;
        TrailingTrailingBoundary = trailingTrailingBoundary;
        Blocks = blocks ?? [];
    }

    public Utf8EmittedKernelKind Kind { get; }

    public Utf8CompiledFindOptimization FindOptimization { get; }

    public byte[][] Prefixes { get; }

    public byte[][]? TrailingLiterals { get; }

    public byte RequiredSuffixByte { get; }

    public int RequiredSeparatorCount { get; }

    public int MaxGap { get; }

    public bool GapSameLine { get; }

    public Utf8BoundaryRequirement LeadingLeadingBoundary { get; }

    public Utf8BoundaryRequirement LeadingTrailingBoundary { get; }

    public Utf8BoundaryRequirement TrailingLeadingBoundary { get; }

    public Utf8BoundaryRequirement TrailingTrailingBoundary { get; }

    public Utf8EmittedKernelBlock[] Blocks { get; }

    public bool HasValue => Kind != Utf8EmittedKernelKind.None;

    public string RouteName => Kind switch
    {
        Utf8EmittedKernelKind.UpperWordIdentifierFamily => "native_structural_family_emit_upper_word_identifier",
        Utf8EmittedKernelKind.SharedPrefixAsciiWhitespaceSuffix => "native_structural_family_emit_shared_prefix_suffix",
        Utf8EmittedKernelKind.OrderedAsciiWhitespaceLiteralWindow => MaxGap > 0
            ? "native_ordered_literal_window_emit_bounded_gap_literal"
            : "native_ordered_literal_window_emit_separator_literal",
        Utf8EmittedKernelKind.PairedOrderedAsciiWhitespaceLiteralWindow => "native_ordered_literal_window_emit_paired_bounded_gap_literal",
        _ => "native_structural_family_emit",
    };
}
