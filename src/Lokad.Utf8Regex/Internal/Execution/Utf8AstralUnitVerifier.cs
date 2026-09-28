using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AstralUnitVerifier
{
    // R05: unit-aware discovery for fixed-token non-byte-safe simple patterns on astral
    // input. Decoded UTF-16 units are the BCL match domain: dots and negations consume
    // halves exactly like BCL, ASCII literals compare unit values, and owned tables answer
    // categories over unit values (halves miss every positive table). Anchored plans,
    // IgnoreCase plans, empty branches, and runs without full branch coverage keep BCL
    // routing; astral-free inputs stay on the scalar verifier. Hooks sit past validation,
    // so inputs are well-formed; anything unservable fails closed to BCL.
    public static bool TryIsMatch(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget,
        out bool isMatch)
    {
        isMatch = false;
        if (!CanServeUnit(kind, plan, input))
        {
            return false;
        }

        isMatch = FindNextUnit(Encoding.UTF8.GetString(input), plan, budget, 0, out _) >= 0;
        return true;
    }

    public static bool TryCount(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget,
        out int count)
    {
        count = 0;
        if (!CanServeUnit(kind, plan, input))
        {
            return false;
        }

        var decoded = Encoding.UTF8.GetString(input);
        var startUnit = 0;
        while (startUnit <= decoded.Length)
        {
            var matchUnit = FindNextUnit(decoded, plan, budget, startUnit, out var matchedLength);
            if (matchUnit < 0)
            {
                return true;
            }

            count++;
            startUnit = matchUnit + Math.Max(matchedLength, 1);
        }

        return true;
    }

    // Construction sites qualify once up front; stepping trusts the verdict.
    // Empty branches match empty, which needs end-of-input advancement rules the unit
    // stepping does not model, so they keep BCL routing like everything else declined here.
    internal static bool CanServeUnit(NativeExecutionKind kind, in AsciiSimplePatternPlan plan, ReadOnlySpan<byte> input)
    {
        if (kind != NativeExecutionKind.AsciiSimplePattern ||
            plan.IsUtf8ByteSafe ||
            plan.IsStartAnchored ||
            plan.IsEndAnchored ||
            plan.IgnoreCase ||
            plan.Branches.Length == 0)
        {
            return false;
        }

        // Bounded runs lower to one branch per length and serve through the branches
        // below, so every length in the run envelope must have its branch or
        // matching falls back closed. Unbounded repetitions never reach simple plans.
        if (plan.RunPlan.HasValue)
        {
            for (var length = plan.RunPlan.MinLength; length <= plan.RunPlan.MaxLength; length++)
            {
                var covered = false;
                foreach (var branch in plan.Branches)
                {
                    if (branch.Length == length)
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered)
                {
                    return false;
                }
            }
        }

        foreach (var branch in plan.Branches)
        {
            if (branch.Length == 0)
            {
                return false;
            }

            foreach (var token in branch)
            {
                switch (token.Kind)
                {
                    case AsciiSimplePatternTokenKind.Literal:
                    case AsciiSimplePatternTokenKind.Dot:
                        break;
                    case AsciiSimplePatternTokenKind.CharClass:
                        if (token.CharClass.IsEmpty || !IsUnitVerifiableClass(token))
                        {
                            return false;
                        }

                        break;
                    default:
                        return false;
                }
            }
        }

        if (Utf8InputAnalyzer.IsAscii(input))
        {
            return false;
        }

        return input.IndexOfAnyInRange((byte)0xF0, byte.MaxValue) >= 0;
    }

    // A class verifies on units exactly when the walk below can evaluate it, mirroring
    // the scalar qualifier over decoded unit values instead of scalar widths.
    private static bool IsUnitVerifiableClass(AsciiSimplePatternToken token)
    {
        if (!token.RequiresAsciiInput)
        {
            return true;
        }

        if (token.ScalarClassKind is Utf8SimplePatternScalarClassKind.AsciiClosedNegation or
            Utf8SimplePatternScalarClassKind.UnicodeWhitespace or
            Utf8SimplePatternScalarClassKind.NegatedDigit or
            Utf8SimplePatternScalarClassKind.NegatedWord or
            Utf8SimplePatternScalarClassKind.NegatedWhitespace or
            Utf8SimplePatternScalarClassKind.SinglelineDot)
        {
            return true;
        }

        return token.CharClass.TryGetKnownPredicateKind(out var predicateKind) &&
            (predicateKind == AsciiCharClassPredicateKind.Digit ||
            predicateKind == AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore);
    }

    private static int FindNextUnit(
        string decoded,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget,
        int startUnit,
        out int matchedLength)
    {
        matchedLength = 0;
        // Every UTF-16 unit is a candidate start, halves included: BCL dots and
        // negations consume halves, so scalar-boundary stepping would miss them.
        var index = startUnit;
        while (index <= decoded.Length)
        {
            budget.Step();
            if (TryMatchBranchUnits(decoded, plan, index, budget, out matchedLength))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static bool TryMatchBranchUnits(
        string decoded,
        in AsciiSimplePatternPlan plan,
        int startUnit,
        Utf8ExecutionDeadline budget,
        out int matchedLength)
    {
        matchedLength = 0;
        budget.Step();
        foreach (var branch in plan.Branches)
        {
            var cursor = startUnit;
            var ok = true;
            for (var i = 0; i < branch.Length && ok; i++)
            {
                if ((uint)cursor >= (uint)decoded.Length)
                {
                    ok = false;
                    break;
                }

                var token = branch[i];
                var unit = decoded[cursor];
                if (token.Kind == AsciiSimplePatternTokenKind.Literal)
                {
                    // Lowered literals are ASCII, so halves and non-ASCII units miss here.
                    if (unit != token.Literal)
                    {
                        ok = false;
                        break;
                    }

                    cursor += 1;
                    continue;
                }

                // Dots consume any single unit, halves included, exactly like BCL.
                if (token.Kind == AsciiSimplePatternTokenKind.Dot)
                {
                    cursor += 1;
                    continue;
                }

                if (token.Kind != AsciiSimplePatternTokenKind.CharClass || token.CharClass.IsEmpty)
                {
                    ok = false;
                    break;
                }

                uint scalar = unit;
                // Positive ASCII sets never match non-ASCII units while ASCII-closed
                // negations match every one of them (halves included); decimal digits,
                // word chars, and whitespace consult the owned tables over unit values,
                // where halves miss every positive table.
                bool matches;
                if (scalar < 0x80)
                {
                    matches = token.CharClass.Contains((byte)scalar);
                }
                else if (token.ScalarClassKind == Utf8SimplePatternScalarClassKind.SinglelineDot)
                {
                    matches = true;
                }
                else if (token.ScalarClassKind == Utf8SimplePatternScalarClassKind.UnicodeWhitespace)
                {
                    matches = Utf8WhiteSpaceRanges.IsWhiteSpace(scalar);
                }
                else if (token.ScalarClassKind == Utf8SimplePatternScalarClassKind.NegatedDigit)
                {
                    matches = !Utf8DecimalDigitRanges.IsDecimalDigit(scalar);
                }
                else if (token.ScalarClassKind == Utf8SimplePatternScalarClassKind.NegatedWord)
                {
                    matches = !Utf8WordRanges.IsWordChar(scalar);
                }
                else if (token.ScalarClassKind == Utf8SimplePatternScalarClassKind.NegatedWhitespace)
                {
                    matches = !Utf8WhiteSpaceRanges.IsWhiteSpace(scalar);
                }
                else if (token.RequiresAsciiInput &&
                    token.CharClass.TryGetKnownPredicateKind(out var predicateKind) &&
                    predicateKind == AsciiCharClassPredicateKind.Digit)
                {
                    matches = Utf8DecimalDigitRanges.IsDecimalDigit(scalar);
                }
                else if (token.RequiresAsciiInput &&
                    token.CharClass.TryGetKnownPredicateKind(out var wordKind) &&
                    wordKind == AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore)
                {
                    matches = Utf8WordRanges.IsWordChar(scalar);
                }
                else
                {
                    matches = token.CharClass.Negated;
                }

                if (!matches)
                {
                    ok = false;
                    break;
                }

                cursor += 1;
            }

            if (ok)
            {
                matchedLength = cursor - startUnit;
                return true;
            }
        }

        matchedLength = 0;
        return false;
    }
}
