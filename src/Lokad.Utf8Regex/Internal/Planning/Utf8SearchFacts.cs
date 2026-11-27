namespace Lokad.Utf8Regex.Internal.Planning;

/// <summary>
/// Transient search hints passed from analysis to preparation. Missing arrays
/// represent absent hints. Present arrays are borrowed from their producers.
/// </summary>
internal readonly struct Utf8SearchFacts(Utf8SearchKind kind)
{
    public Utf8SearchKind Kind { get; init; } = kind;
    public byte[]? LiteralUtf8 { get; init; }
    public byte[][]? AlternateLiteralsUtf8 { get; init; }
    public bool CanGuideFallbackStarts { get; init; }
    public byte[]? RequiredPrefilterLiteralUtf8 { get; init; }
    public byte[][]? RequiredPrefilterAlternateLiteralsUtf8 { get; init; }
    public byte[]? TrailingLiteralUtf8 { get; init; }
    public int MinRequiredLength { get; init; }
    public int? ExactRequiredLength { get; init; }
    public int? MaxPossibleLength { get; init; }
    public Utf8BoundaryRequirement LeadingBoundary { get; init; }
    public Utf8BoundaryRequirement TrailingBoundary { get; init; }
}
