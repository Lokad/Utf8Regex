namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8SearchKind
{
    None = 0,
    ExactAsciiLiteral = 1,
    AsciiLiteralIgnoreCase = 2,
    ExactAsciiLiterals = 3,
    ExactUtf8Literals = 4,
    AsciiLiteralIgnoreCaseLiterals = 5,
    FixedDistanceAsciiLiteral = 6,
    FixedDistanceAsciiChar = 7,
    FixedDistanceAsciiSets = 8,
    TrailingAnchorFixedLengthEnd = 9,
    TrailingAnchorFixedLengthEndZ = 10,
}
