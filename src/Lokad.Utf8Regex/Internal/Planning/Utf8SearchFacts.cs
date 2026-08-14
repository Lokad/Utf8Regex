using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Planning;

/// <summary>
/// Named analysis payload for a search-fact shape. Reference-valued members use
/// empty storage for absence; nullable lengths represent a genuine unbounded or
/// unknown fact rather than selecting the search kind.
/// </summary>
internal sealed class Utf8SearchFactData
{
    public byte[] LiteralUtf8 { get; init; } = [];
    public byte[][] AlternateLiteralsUtf8 { get; init; } = [];
    public bool CanGuideFallbackStarts { get; init; }
    public byte[] RequiredPrefilterLiteralUtf8 { get; init; } = [];
    public byte[][] RequiredPrefilterAlternateLiteralsUtf8 { get; init; } = [];
    public Utf8SearchAsciiSet SecondaryRequiredPrefilterQuotedAsciiSet { get; init; }
    public int SecondaryRequiredPrefilterQuotedAsciiLength { get; init; }
    public Utf8FixedDistanceSet[] FixedDistanceSets { get; init; } = [];
    public byte[] TrailingLiteralUtf8 { get; init; } = [];
    public byte[][] OrderedWindowLeadingLiteralsUtf8 { get; init; } = [];
    public byte[] OrderedWindowTrailingLiteralUtf8 { get; init; } = [];
    public Utf8WindowSearchFacts[] RequiredWindowPrefilters { get; init; } = [];
    public int? OrderedWindowMaxGap { get; init; }
    public bool OrderedWindowSameLine { get; init; }
    public Utf8FallbackStartTransform FallbackStartTransform { get; init; }
    public int Distance { get; init; }
    public int MinRequiredLength { get; init; }
    public int? ExactRequiredLength { get; init; }
    public int? MaxPossibleLength { get; init; }
    public Utf8BoundaryRequirement LeadingBoundary { get; init; }
    public Utf8BoundaryRequirement TrailingBoundary { get; init; }
}

internal readonly struct Utf8SearchFacts
{
    private static readonly Utf8SearchFactData s_empty = new();
    private readonly Utf8SearchFactData? _data;

    private Utf8SearchFacts(Utf8SearchKind kind, Utf8SearchFactData data)
    {
        Kind = kind;
        _data = data;
    }

    public Utf8SearchKind Kind { get; }
    public byte[] LiteralUtf8 => Data.LiteralUtf8;
    public byte[][] AlternateLiteralsUtf8 => Data.AlternateLiteralsUtf8;
    public bool CanGuideFallbackStarts => Data.CanGuideFallbackStarts;
    public byte[] RequiredPrefilterLiteralUtf8 => Data.RequiredPrefilterLiteralUtf8;
    public byte[][] RequiredPrefilterAlternateLiteralsUtf8 => Data.RequiredPrefilterAlternateLiteralsUtf8;
    public Utf8SearchAsciiSet SecondaryRequiredPrefilterQuotedAsciiSet => Data.SecondaryRequiredPrefilterQuotedAsciiSet;
    public int SecondaryRequiredPrefilterQuotedAsciiLength => Data.SecondaryRequiredPrefilterQuotedAsciiLength;
    public Utf8FixedDistanceSet[] FixedDistanceSets => Data.FixedDistanceSets;
    public byte[] TrailingLiteralUtf8 => Data.TrailingLiteralUtf8;
    public byte[][] OrderedWindowLeadingLiteralsUtf8 => Data.OrderedWindowLeadingLiteralsUtf8;
    public byte[] OrderedWindowTrailingLiteralUtf8 => Data.OrderedWindowTrailingLiteralUtf8;
    public Utf8WindowSearchFacts[] RequiredWindowPrefilters => Data.RequiredWindowPrefilters;
    public int? OrderedWindowMaxGap => Data.OrderedWindowMaxGap;
    public bool OrderedWindowSameLine => Data.OrderedWindowSameLine;
    public Utf8FallbackStartTransform FallbackStartTransform => Data.FallbackStartTransform;
    public int Distance => Data.Distance;
    public int MinRequiredLength => Data.MinRequiredLength;
    public int? ExactRequiredLength => Data.ExactRequiredLength;
    public int? MaxPossibleLength => Data.MaxPossibleLength;
    public Utf8BoundaryRequirement LeadingBoundary => Data.LeadingBoundary;
    public Utf8BoundaryRequirement TrailingBoundary => Data.TrailingBoundary;

    private Utf8SearchFactData Data => _data ?? s_empty;

    public static Utf8SearchFacts Create(Utf8SearchKind kind) => new(kind, s_empty);

    public static Utf8SearchFacts Create(Utf8SearchKind kind, Utf8SearchFactData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new Utf8SearchFacts(kind, data);
    }
}
