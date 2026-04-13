using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SimplePatternCompiledRuntimePolicy
{
    public static bool ShouldUseEmittedAnchoredValidator(AsciiSimplePatternAnchoredValidatorPlan plan)
    {
        if (!plan.HasValue)
        {
            return false;
        }

        foreach (var segment in plan.Segments)
        {
            if (!segment.IsLiteral &&
                segment.MaxLength != int.MaxValue &&
                segment.MinLength != segment.MaxLength)
            {
                return false;
            }

            if (segment.IsLiteral)
            {
                return true;
            }

            if (plan.IgnoreCase &&
                segment.CharClass is { Negated: false } charClass &&
                segment.MinLength == 1 &&
                segment.MaxLength == 1 &&
                charClass.GetPositiveMatchBytes().Length is > 0 and <= 8)
            {
                return true;
            }
        }

        return false;
    }

    public static bool ShouldFallbackAfterAnchoredValidatorMiss(ReadOnlySpan<byte> input, Utf8ValidationResult validation, bool allowTrailingNewline)
    {
        return validation.IsAscii &&
            allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n';
    }

    public static bool CanUseDirectAnchoredFixedLengthSimplePattern(Utf8RegexPlan regexPlan)
    {
        return regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            regexPlan.SimplePatternPlan.Branches.Length == 1 &&
            regexPlan.SimplePatternPlan.IsFixedLength &&
            regexPlan.SimplePatternPlan.IsStartAnchored &&
            regexPlan.SimplePatternPlan.IsEndAnchored &&
            regexPlan.SearchPlan.Kind is Utf8SearchKind.TrailingAnchorFixedLengthEnd or Utf8SearchKind.TrailingAnchorFixedLengthEndZ;
    }

    public static bool CanUseDirectAnchoredFixedAlternationSimplePattern(Utf8RegexPlan regexPlan)
    {
        if (regexPlan.ExecutionKind != NativeExecutionKind.AsciiSimplePattern ||
            !regexPlan.SimplePatternPlan.IsStartAnchored ||
            !regexPlan.SimplePatternPlan.IsEndAnchored ||
            regexPlan.SimplePatternPlan.Branches.Length <= 1)
        {
            return false;
        }

        foreach (var branch in regexPlan.SimplePatternPlan.Branches)
        {
            if (branch.Length == 0)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsEndAnchoredWholeInputMatch(Utf8SearchPlan searchPlan, ReadOnlySpan<byte> input, int matchedLength)
    {
        return searchPlan.Kind switch
        {
            Utf8SearchKind.TrailingAnchorFixedLengthEnd or Utf8SearchKind.None => matchedLength == input.Length,
            Utf8SearchKind.TrailingAnchorFixedLengthEndZ => matchedLength == input.Length ||
                (matchedLength + 1 == input.Length && input[matchedLength] == (byte)'\n'),
            _ => matchedLength == input.Length,
        };
    }
}
