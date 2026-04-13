using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiOrderedLiteralWindowPlan
{
    public AsciiOrderedLiteralWindowPlan(
        byte[] leadingLiteralUtf8,
        byte[][]? leadingLiteralsUtf8,
        byte[] trailingLiteralUtf8,
        byte[][]? trailingLiteralsUtf8,
        int maxGap,
        bool gapSameLine,
        int gapLeadingSeparatorMinCount,
        bool yieldLeadingLiteralOnly,
        Utf8BoundaryRequirement leadingLiteralLeadingBoundary,
        Utf8BoundaryRequirement leadingLiteralTrailingBoundary,
        Utf8BoundaryRequirement trailingLiteralLeadingBoundary,
        Utf8BoundaryRequirement trailingLiteralTrailingBoundary)
    {
        LeadingLiteralUtf8 = leadingLiteralUtf8;
        LeadingLiteralsUtf8 = leadingLiteralsUtf8;
        TrailingLiteralUtf8 = trailingLiteralUtf8;
        TrailingLiteralsUtf8 = trailingLiteralsUtf8;
        MaxGap = maxGap;
        GapSameLine = gapSameLine;
        GapLeadingSeparatorMinCount = gapLeadingSeparatorMinCount;
        YieldLeadingLiteralOnly = yieldLeadingLiteralOnly;
        LeadingLiteralLeadingBoundary = leadingLiteralLeadingBoundary;
        LeadingLiteralTrailingBoundary = leadingLiteralTrailingBoundary;
        TrailingLiteralLeadingBoundary = trailingLiteralLeadingBoundary;
        TrailingLiteralTrailingBoundary = trailingLiteralTrailingBoundary;
    }

    /// <summary>Search anchor literal (shortest, or the only one for single-literal patterns).</summary>
    public byte[] LeadingLiteralUtf8 { get; }

    /// <summary>All leading literal alternatives (null for single-literal patterns).</summary>
    public byte[][]? LeadingLiteralsUtf8 { get; }

    public byte[] TrailingLiteralUtf8 { get; }

    /// <summary>Optional trailing literal alternatives aligned with <see cref="LeadingLiteralsUtf8"/>.</summary>
    public byte[][]? TrailingLiteralsUtf8 { get; }

    public int MaxGap { get; }

    /// <summary>
    /// When true, the gap must not contain a newline (the gap set is '.' without Singleline).
    /// </summary>
    public bool GapSameLine { get; }

    /// <summary>
    /// Minimum whitespace characters required between literal1 and the gap (from \s+ or \s*).
    /// Zero means no separator is required.
    /// </summary>
    public int GapLeadingSeparatorMinCount { get; }

    /// <summary>
    /// When true, the match length stays on the leading literal and the trailing literal only acts as a lookahead guard.
    /// </summary>
    public bool YieldLeadingLiteralOnly { get; }

    public Utf8BoundaryRequirement LeadingLiteralLeadingBoundary { get; }

    public Utf8BoundaryRequirement LeadingLiteralTrailingBoundary { get; }

    public Utf8BoundaryRequirement TrailingLiteralLeadingBoundary { get; }

    public Utf8BoundaryRequirement TrailingLiteralTrailingBoundary { get; }

    public bool HasValue => LeadingLiteralUtf8 is { Length: > 0 } && TrailingLiteralUtf8 is { Length: > 0 };

    public bool IsLiteralFamily => LeadingLiteralsUtf8 is { Length: > 1 };

    public bool HasPairedTrailingLiterals =>
        LeadingLiteralsUtf8 is { Length: > 1 } &&
        TrailingLiteralsUtf8 is { Length: > 1 } &&
        LeadingLiteralsUtf8.Length == TrailingLiteralsUtf8.Length;
}
