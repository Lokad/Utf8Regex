using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum PreparedAsciiFindMode : byte
{
    None = 0,
    Literal = 1,
    LiteralFamily = 2,
    FixedDistanceLiteral = 3,
    FixedDistanceSet = 4,
    LiteralAfterLoop = 5,
}

internal readonly struct PreparedAsciiFindPlan
{
    private PreparedAsciiFindPlan(
        PreparedAsciiFindMode mode,
        PreparedSubstringSearch literalSearch = default,
        PreparedLiteralSetSearch literalFamilySearch = default,
        Utf8FixedDistanceSet[]? fixedDistanceSets = null,
        int distance = 0,
        int minLoopLength = 0)
    {
        Mode = mode;
        LiteralSearch = literalSearch;
        LiteralFamilySearch = literalFamilySearch;
        FixedDistanceSets = fixedDistanceSets;
        Distance = distance;
        MinLoopLength = minLoopLength;
    }

    public PreparedAsciiFindMode Mode { get; }

    public PreparedSubstringSearch LiteralSearch { get; }

    public PreparedLiteralSetSearch LiteralFamilySearch { get; }

    public Utf8FixedDistanceSet[]? FixedDistanceSets { get; }

    public int Distance { get; }

    public int MinLoopLength { get; }

    public bool HasValue => Mode != PreparedAsciiFindMode.None;

    public static PreparedAsciiFindPlan CreateLiteral(byte[] literalUtf8) =>
        new(PreparedAsciiFindMode.Literal, literalSearch: new PreparedSubstringSearch(literalUtf8, ignoreCase: false));

    public static PreparedAsciiFindPlan CreateLiteralFamily(byte[][] literalsUtf8) =>
        new(PreparedAsciiFindMode.LiteralFamily, literalFamilySearch: new PreparedLiteralSetSearch(literalsUtf8));

    public static PreparedAsciiFindPlan CreateLiteralFamily(PreparedLiteralSetSearch literalFamilySearch) =>
        new(PreparedAsciiFindMode.LiteralFamily, literalFamilySearch: literalFamilySearch);

    public static PreparedAsciiFindPlan CreateFixedDistanceLiteral(byte[] literalUtf8, int distance) =>
        new(PreparedAsciiFindMode.FixedDistanceLiteral, literalSearch: new PreparedSubstringSearch(literalUtf8, ignoreCase: false), distance: distance);

    public static PreparedAsciiFindPlan CreateFixedDistanceSet(Utf8FixedDistanceSet[] fixedDistanceSets) =>
        new(PreparedAsciiFindMode.FixedDistanceSet, fixedDistanceSets: fixedDistanceSets);

    public static PreparedAsciiFindPlan CreateLiteralAfterLoop(byte[] literalUtf8, int minLoopLength = 0) =>
        new(PreparedAsciiFindMode.LiteralAfterLoop, literalSearch: new PreparedSubstringSearch(literalUtf8, ignoreCase: false), minLoopLength: minLoopLength);

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
