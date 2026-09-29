using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AstralUnitVerifier
{
    // R05: unit-aware discovery for fixed-token non-byte-safe simple patterns on astral
    // input. Decoded UTF-16 units are the BCL match domain: dots and negations consume
    // halves exactly like BCL, ASCII literals compare unit values (folded under
    // IgnoreCase exactly like the scalar walk, with halves and fold-less scalars
    // missing), and owned tables answer categories over unit values (halves miss
    // every positive table). Anchored plans, empty branches, and runs without full
    // branch coverage keep BCL routing; astral-free inputs stay on the scalar
    // verifier. Hooks sit past validation, so inputs are well-formed; anything
    // unservable fails closed to BCL.
    public static bool TryIsMatch(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        int startUnit,
        Utf8ExecutionDeadline budget,
        out bool isMatch)
    {
        isMatch = false;
        if (!CanServeUnit(kind, plan, input))
        {
            return false;
        }

        isMatch = FindNextUnit(Encoding.UTF8.GetString(input), plan, budget, startUnit, out _) >= 0;
        return true;
    }

    public static bool TryCount(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        int startUnit,
        Utf8ExecutionDeadline budget,
        out int count)
    {
        count = 0;
        if (!CanServeUnit(kind, plan, input))
        {
            return false;
        }

        var decoded = Encoding.UTF8.GetString(input);
        var position = startUnit;
        while (position <= decoded.Length)
        {
            var matchUnit = FindNextUnit(decoded, plan, budget, position, out var matchedLength);
            if (matchUnit < 0)
            {
                return true;
            }

            count++;
            position = matchUnit + Math.Max(matchedLength, 1);
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

    internal static int FindNextUnit(
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

    // R05: matches one expanded branch token sequence at a unit position, shared by
    // first-win discovery above and winning-branch attribution for unit span maps.
    internal static bool TryVerifyBranchUnits(
        string decoded,
        AsciiSimplePatternToken[] branch,
        int startUnit,
        bool ignoreCase,
        Utf8ExecutionDeadline budget,
        out int matchedLength)
    {
        matchedLength = 0;
        budget.Step();
        if (!TryMatchTokensUnits(decoded, branch, startUnit, ignoreCase, out var endUnit))
        {
            return false;
        }

        matchedLength = endUnit - startUnit;
        return true;
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
        var ignoreCase = plan.IgnoreCase;
        foreach (var branch in plan.Branches)
        {
            if (TryMatchTokensUnits(decoded, branch, startUnit, ignoreCase, out var endUnit))
            {
                matchedLength = endUnit - startUnit;
                return true;
            }
        }

        matchedLength = 0;
        return false;
    }

    // Raw membership in the positive mask definition, ignoring Negated (mirrors
    // the scalar verifier helper for the fold path).
    private static bool PositiveMaskContains(AsciiCharClass charClass, byte value)
    {
        return value < 64
            ? (charClass.PositiveLowMask & (1UL << value)) != 0
            : (charClass.PositiveHighMask & (1UL << (value - 64))) != 0;
    }

    private static bool TryMatchTokensUnits(
        string decoded,
        AsciiSimplePatternToken[] branch,
        int startUnit,
        bool ignoreCase,
        out int endUnit)
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
                // Lowered literals are already folded, so the input unit folds too
                // under IgnoreCase; halves and fold-less scalars behave exactly
                // like BCL (Kelvin meets k through its pair).
                if (unit < 0x80)
                {
                    var candidate = ignoreCase ? AsciiSearch.FoldCase((byte)unit) : (byte)unit;
                    if (candidate != token.Literal)
                    {
                        ok = false;
                        break;
                    }

                    cursor += 1;
                    continue;
                }

                if (!ignoreCase)
                {
                    ok = false;
                    break;
                }

                if (!Utf8OwnedCaseFold.TryGetFold(unit, out var foldLower, out var foldUpper) ||
                    !(((uint)foldLower < 0x80 && (byte)foldLower == token.Literal) ||
                    ((uint)foldUpper < 0x80 && (byte)foldUpper == token.Literal)))
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
            else if (ignoreCase)
            {
                // Mask-based classes only (the shorthand and dot arms above are
                // fold-neutral): a non-ASCII unit meets the folded ASCII mask
                // through its invariant fold pair, mirroring the scalar walk.
                var foldedHit = false;
                if (Utf8OwnedCaseFold.TryGetFold(unit, out var classLower, out var classUpper))
                {
                    foldedHit = ((uint)classLower < 0x80 && PositiveMaskContains(token.CharClass, (byte)classLower)) ||
                        ((uint)classUpper < 0x80 && PositiveMaskContains(token.CharClass, (byte)classUpper));
                }

                matches = token.CharClass.Negated ? !foldedHit : foldedHit;
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
            endUnit = cursor;
            return true;
        }

        endUnit = 0;
        return false;
    }
    // First-hit discovery reports qualification separately from hit presence so misses
    // answer definitively without BCL verification while half-involving hits shape
    // without byte ranges exactly like the BCL projector.
    public static bool TryFindFirst(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget,
        out int startUnit,
        out int matchedLength)
    {
        startUnit = -1;
        matchedLength = 0;
        if (!CanServeUnit(kind, plan, input))
        {
            return false;
        }

        startUnit = FindNextUnit(Encoding.UTF8.GetString(input), plan, budget, 0, out matchedLength);
        return true;
    }

    // Unit-space literal replacement for the byte splicer, which cannot represent
    // half-involving matches as byte ranges: ranges collect in units over the decoded
    // string, splice with the replacement text, and encode once. Matches the BCL
    // replacement tiling exactly (leftmost-first, non-overlapping, branch order).
    public static bool TryReplaceLiteral(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        ReadOnlySpan<byte> replacementBytes,
        Utf8ExecutionDeadline budget,
        out string replaced,
        out int replacementCount)
    {
        replaced = string.Empty;
        replacementCount = 0;
        if (!CanServeUnit(kind, plan, input))
        {
            return false;
        }

        var decoded = Encoding.UTF8.GetString(input);
        var replacementText = Encoding.UTF8.GetString(replacementBytes);
        var ranges = new List<(int Start, int End)>();
        var matchedUnits = 0;
        var startUnit = 0;
        while (startUnit <= decoded.Length)
        {
            var matchUnit = FindNextUnit(decoded, plan, budget, startUnit, out var matchedLength);
            if (matchUnit < 0)
            {
                break;
            }

            ranges.Add((matchUnit, matchUnit + matchedLength));
            matchedUnits += matchedLength;
            startUnit = matchUnit + Math.Max(matchedLength, 1);
        }

        var result = new StringBuilder(decoded.Length - matchedUnits + ranges.Count * replacementText.Length);
        var previousEnd = 0;
        foreach (var range in ranges)
        {
            result.Append(decoded, previousEnd, range.Start - previousEnd);
            result.Append(replacementText);
            previousEnd = range.End;
        }

        result.Append(decoded, previousEnd, decoded.Length - previousEnd);
        replaced = result.ToString();
        replacementCount = ranges.Count;
        return true;
    }
}
