namespace Lokad.Utf8Regex.Internal.Planning;

internal enum NativeExecutionKind
{
    FallbackRegex = 0, ExactAsciiLiteral = 1, AsciiLiteralIgnoreCase = 2, ExactUtf8Literal = 4, ExactUtf8Literals = 5, AsciiLiteralIgnoreCaseLiterals = 7
}

