using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum PreparedAsciiFindMode : byte
{
    None = 0,
    Literal = 1,
    LiteralFamily = 2,
    FixedDistanceLiteral = 3,
    FixedDistanceSet = 4,
}

internal readonly struct PreparedAsciiFindPlan
{
    private readonly Utf8FixedDistanceSet[]? _fixedDistanceSets;

    private PreparedAsciiFindPlan(
        PreparedAsciiFindMode mode,
        PreparedSubstringSearch literalSearch,
        PreparedLiteralSetSearch literalFamilySearch,
        Utf8FixedDistanceSet[] fixedDistanceSets,
        int distance)
    {
        Mode = mode;
        LiteralSearch = literalSearch;
        LiteralFamilySearch = literalFamilySearch;
        _fixedDistanceSets = fixedDistanceSets;
        Distance = distance;
    }

    public PreparedAsciiFindMode Mode { get; }

    public PreparedSubstringSearch LiteralSearch { get; }

    public PreparedLiteralSetSearch LiteralFamilySearch { get; }

    public Utf8FixedDistanceSet[] FixedDistanceSets => _fixedDistanceSets ?? [];

    public int Distance { get; }

    public bool HasValue => Mode != PreparedAsciiFindMode.None;

    public static PreparedAsciiFindPlan CreateLiteral(byte[] literalUtf8) =>
        new(PreparedAsciiFindMode.Literal, new PreparedSubstringSearch(literalUtf8, ignoreCase: false), default, [], 0);

    public static PreparedAsciiFindPlan CreateLiteralFamily(byte[][] literalsUtf8) =>
        new(PreparedAsciiFindMode.LiteralFamily, default, new PreparedLiteralSetSearch(literalsUtf8), [], 0);

    public static PreparedAsciiFindPlan CreateLiteralFamily(PreparedLiteralSetSearch literalFamilySearch) =>
        new(PreparedAsciiFindMode.LiteralFamily, default, literalFamilySearch, [], 0);

    public static PreparedAsciiFindPlan CreateFixedDistanceLiteral(byte[] literalUtf8, int distance) =>
        new(PreparedAsciiFindMode.FixedDistanceLiteral, new PreparedSubstringSearch(literalUtf8, ignoreCase: false), default, [], distance);

    public static PreparedAsciiFindPlan CreateFixedDistanceSet(Utf8FixedDistanceSet[] fixedDistanceSets) =>
        new(PreparedAsciiFindMode.FixedDistanceSet, default, default, fixedDistanceSets, 0);

    public static PreparedAsciiFindPlan CreateForOrderedWindow(AsciiOrderedLiteralWindowPlan plan)
    {
        if (plan.TrailingLiteralsUtf8 is { Length: > 1 } trailingLiterals)
        {
            return CreateLiteralFamily(trailingLiterals);
        }

        return plan.TrailingLiteralUtf8.Length > 0
            ? CreateLiteral(plan.TrailingLiteralUtf8)
            : default;
    }
}
