using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8CompiledPatternCategory : byte
{
    None = 0,
    AnchoredWhole = 1,
    DeterministicLinear = 3,
    Literal = 4,
}

internal static class Utf8CompiledPatternCategories
{

    public static Utf8CompiledPatternCategory GetNativeCategory(NativeExecutionKind kind)
    {
        return kind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.AsciiLiteralIgnoreCase or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.ExactUtf8Literals or
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => Utf8CompiledPatternCategory.Literal,

            NativeExecutionKind.AsciiStructuralIdentifierFamily
                => Utf8CompiledPatternCategory.DeterministicLinear,

            _ => Utf8CompiledPatternCategory.None,
        };
    }

}
