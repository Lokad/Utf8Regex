namespace Lokad.Utf8Regex.Internal.Planning;

internal enum NativeExecutionKind
{
    FallbackRegex = 0,
    ExactAsciiLiteral = 1,
    AsciiLiteralIgnoreCase = 2,
    AsciiSimplePattern = 3,
    ExactUtf8Literal = 4,
    ExactUtf8Literals = 5,
    AsciiStructuralIdentifierFamily = 6,
    AsciiLiteralIgnoreCaseLiterals = 7,
    AsciiStructuralTokenWindow = 8,
    AsciiStructuralRepeatedSegment = 9,
    AsciiStructuralQuotedRelation = 10,
    AsciiOrderedLiteralWindow = 11,
}

