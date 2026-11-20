using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum PreparedAsciiFindMode : byte
{
    None = 0,
    FixedDistanceLiteral = 3,
    FixedDistanceSet = 4,
}

internal readonly struct PreparedAsciiFindPlan
{
    private readonly Utf8FixedDistanceSet[]? _fixedDistanceSets;

    private PreparedAsciiFindPlan(
        PreparedAsciiFindMode mode,
        PreparedSubstringSearch literalSearch,
        Utf8FixedDistanceSet[] fixedDistanceSets,
        int distance)
    {
        Mode = mode;
        LiteralSearch = literalSearch;
        _fixedDistanceSets = fixedDistanceSets;
        Distance = distance;
    }

    public PreparedAsciiFindMode Mode { get; }

    public PreparedSubstringSearch LiteralSearch { get; }


    public Utf8FixedDistanceSet[] FixedDistanceSets => _fixedDistanceSets ?? [];

    public int Distance { get; }

    public bool HasValue => Mode != PreparedAsciiFindMode.None;

    public static PreparedAsciiFindPlan CreateFixedDistanceLiteral(byte[] literalUtf8, int distance) =>
        new(PreparedAsciiFindMode.FixedDistanceLiteral, new PreparedSubstringSearch(literalUtf8, ignoreCase: false), [], distance);

    public static PreparedAsciiFindPlan CreateFixedDistanceSet(Utf8FixedDistanceSet[] fixedDistanceSets) =>
        new(PreparedAsciiFindMode.FixedDistanceSet, default, fixedDistanceSets, 0);
}
