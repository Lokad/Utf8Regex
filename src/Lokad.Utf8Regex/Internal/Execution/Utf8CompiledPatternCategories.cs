using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8CompiledPatternCategory : byte
{
    None = 0,
    AnchoredWhole = 1,
    SearchGuided = 2,
    DeterministicLinear = 3,
    Literal = 4,
}

internal static class Utf8CompiledPatternCategories
{
    public static Utf8CompiledPatternCategory GetSimplePatternCategory(Utf8CompiledPatternFamilyPlan family)
    {
        return family.Kind switch
        {
            Utf8CompiledPatternFamilyKind.AnchoredValidator or
            Utf8CompiledPatternFamilyKind.AnchoredBoundedDate or
            Utf8CompiledPatternFamilyKind.RepeatedDigitGroup
                => Utf8CompiledPatternCategory.AnchoredWhole,

            Utf8CompiledPatternFamilyKind.BoundedSuffixLiteral or
            Utf8CompiledPatternFamilyKind.SymmetricLiteralWindow
                => Utf8CompiledPatternCategory.SearchGuided,

            _ => Utf8CompiledPatternCategory.None,
        };
    }

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

            NativeExecutionKind.AsciiStructuralIdentifierFamily or
            NativeExecutionKind.AsciiStructuralTokenWindow or
            NativeExecutionKind.AsciiStructuralRepeatedSegment or
            NativeExecutionKind.AsciiStructuralQuotedRelation or
            NativeExecutionKind.AsciiOrderedLiteralWindow
                => Utf8CompiledPatternCategory.DeterministicLinear,

            _ => Utf8CompiledPatternCategory.None,
        };
    }

    public static Utf8CompiledPatternCategory GetRegexPlanCategory(Utf8RegexPlan regexPlan)
    {
        var nativeCategory = GetNativeCategory(regexPlan.ExecutionKind);
        if (nativeCategory != Utf8CompiledPatternCategory.None)
        {
            return nativeCategory;
        }

        if (regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern)
        {
            var simplePatternCategory = GetSimplePatternCategory(regexPlan.SimplePatternPlan.CompiledPatternFamily);
            if (simplePatternCategory != Utf8CompiledPatternCategory.None)
            {
                return simplePatternCategory;
            }

            if (regexPlan.StructuralLinearProgram.Kind != Utf8StructuralLinearProgramKind.None)
            {
                return Utf8CompiledPatternCategory.DeterministicLinear;
            }
        }

        return Utf8CompiledPatternCategory.None;
    }
}
