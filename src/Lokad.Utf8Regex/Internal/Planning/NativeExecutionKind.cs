namespace Lokad.Utf8Regex.Internal.Planning;

internal enum NativeExecutionKind
{
    FallbackRegex = 0,
    ExactAsciiLiteral = 1,
    AsciiLiteralIgnoreCase = 2,
    ExactUtf8Literal = 4,
    ExactUtf8Literals = 5,
    AsciiStructuralIdentifierFamily = 6,
    AsciiLiteralIgnoreCaseLiterals = 7,
    AsciiOrderedLiteralWindow = 11,
}

