using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ScalarSimplePatternVerifier
{
    // R05: scalar-aware discovery for fixed-token non-byte-safe simple patterns on
    // astral-free non-ASCII input. ASCII search literals stay sound candidates through
    // UTF-8 self-synchronization (ASCII bytes never appear inside multibyte scalars);
    // width-sensitive tokens consume whole scalars with unit predicates (literals
    // byte-compare, singleline dots match any scalar, ASCII-closed classes apply the
    // byte predicate on ASCII scalars and their negation bit beyond). BCL parity holds
    // on BMP input where units equal scalars. IgnoreCase plans fold through the
    // owned invariant table (ASCII bytes fold inline, other BMP scalars meet the
    // folded ASCII mask through their fold pair, Kelvin included); shorthands,
    // dots, and singleline dots are fold-neutral. Astral inputs, runs,
    // non-Digit, non-word shorthand and Unicode-dependent classes, anchored plans,
    // and the ASCII fast path decline to the existing routes. Hooks sit past
    // validation, so inputs are well-formed; malformed scalars fail the walk closed.
    public static bool TryIsMatch(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget,
        out bool isMatch)
    {
        isMatch = false;
        if (!CanServeScalar(kind, plan, input))
        {
            return false;
        }

        isMatch = FindNextScalar(input, plan, budget, 0, out _) >= 0;
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
        if (!CanServeScalar(kind, plan, input))
        {
            return false;
        }

        var startByte = 0;
        while (startByte <= input.Length)
        {
            var matchByte = FindNextScalar(input, plan, budget, startByte, out var matchedByteLength);
            if (matchByte < 0)
            {
                return true;
            }

            count++;
            var advance = matchedByteLength;
            if (advance == 0)
            {
                advance = 1;
                if (matchByte < input.Length)
                {
                    var lead = input[matchByte];
                    advance = lead < 0x80 ? 1 : lead < 0xE0 ? 2 : 3;
                }
            }

            startByte = matchByte + advance;
        }

        return true;
    }

    // Construction sites qualify once up front; stepping trusts the verdict.
    // Empty branches match empty, which needs end-of-input advancement rules the
    // scalar stepping does not model, so they keep BCL routing like everything
    // else declined here.
    internal static bool CanServeScalar(NativeExecutionKind kind, in AsciiSimplePatternPlan plan, ReadOnlySpan<byte> input)
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
                        if (token.CharClass.IsEmpty || !IsScalarVerifiableClass(token))
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

        return input.IndexOfAnyInRange((byte)0xF0, byte.MaxValue) < 0;
    }

    // A class verifies on scalars exactly when the walk below can evaluate it:
    // plain ASCII classes, ASCII-closed negations, whitespace, decimal digits,
    // word chars, and negated known categories (each complemented through its
    // table). Anything else uses the cached scalar program.
    private static bool IsScalarVerifiableClass(AsciiSimplePatternToken token)
    {
        if (!token.RequiresAsciiInput)
        {
            return true;
        }

        if (token.ScalarClassKind is Utf8SimplePatternScalarClassKind.AsciiClosedNegation or
            Utf8SimplePatternScalarClassKind.UnicodeWhitespace or
            Utf8SimplePatternScalarClassKind.NegatedDigit or
            Utf8SimplePatternScalarClassKind.NegatedWord or
            Utf8SimplePatternScalarClassKind.NegatedWhitespace)
        {
            return true;
        }

        return token.CharClass.TryGetKnownPredicateKind(out var predicateKind) &&
            (predicateKind == AsciiCharClassPredicateKind.Digit ||
             predicateKind == AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore);
    }

    // Raw membership in the positive mask definition, ignoring Negated.
    private static bool PositiveMaskContains(AsciiCharClass charClass, byte value)
    {
        return value < 64
            ? (charClass.PositiveLowMask & (1UL << value)) != 0
            : (charClass.PositiveHighMask & (1UL << (value - 64))) != 0;
    }

    public static int FindNextScalar(
        ReadOnlySpan<byte> input,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget,
        int startByte,
        out int matchedByteLength)
    {
        matchedByteLength = 0;
        // Byte-anchored literal candidates assume byte buildup past the match start,
        // which multibyte scalars invalidate, so the scalar walk tries every scalar
        // start directly; the verifier stays authoritative while the first token
        // rejects most positions. Fixed literal checks are byte-offset filters with
        // the same shift, so they stay off on this path too.
        var index = startByte;
        while (index <= input.Length)
        {
            budget.Step();
            if ((index == 0 || index == input.Length || (input[index] & 0xC0) != 0x80) &&
                TryMatchBranch(input, plan, index, budget, out matchedByteLength))
            {
                return index;
            }

            var lead = index < input.Length ? input[index] : (byte)0;
            index += lead < 0x80 ? 1 : lead < 0xE0 ? 2 : 3;
        }

        return -1;
    }

    private static bool TryMatchBranch(
        ReadOnlySpan<byte> input,
        in AsciiSimplePatternPlan plan,
        int startByte,
        Utf8ExecutionDeadline budget,
        out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget.Step();
        var ignoreCase = plan.IgnoreCase;
        foreach (var branch in plan.Branches)
        {
            var cursor = startByte;
            var ok = true;
            for (var i = 0; i < branch.Length && ok; i++)
            {
                if ((uint)cursor >= (uint)input.Length)
                {
                    ok = false;
                    break;
                }

                var token = branch[i];
                var lead = input[cursor];
                if (token.Kind == AsciiSimplePatternTokenKind.Literal)
                {
                    if (lead < 0x80)
                    {
                        // Lowered literals are already folded, so the input byte
                        // folds too under IgnoreCase; the case-sensitive check is
                        // byte-identical to the old direct comparison.
                        var candidate = ignoreCase ? AsciiSearch.FoldCase(lead) : lead;
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

                    // A non-ASCII scalar meets a folded ASCII literal only through
                    // its invariant fold pair (Kelvin meets k); anything else,
                    // including fold-less scalars like long-s and U+0130, misses.
                    // Astral leads decline up front, so fail those closed too.
                    var literalWidth = lead < 0xE0 ? 2 : 3;
                    if (lead < 0xC0 || lead >= 0xF0 || cursor + literalWidth > input.Length ||
                        (input[cursor + 1] & 0xC0) != 0x80 ||
                        (literalWidth == 3 && (input[cursor + 2] & 0xC0) != 0x80))
                    {
                        ok = false;
                        break;
                    }

                    uint literalScalar = (uint)(lead & (literalWidth == 2 ? 0x1F : 0x0F));
                    literalScalar = (literalScalar << 6) | (uint)(input[cursor + 1] & 0x3F);
                    if (literalWidth == 3)
                    {
                        literalScalar = (literalScalar << 6) | (uint)(input[cursor + 2] & 0x3F);
                    }

                    if (!Utf8OwnedCaseFold.TryGetFold((char)literalScalar, out var foldLower, out var foldUpper) ||
                        !(((uint)foldLower < 0x80 && (byte)foldLower == token.Literal) ||
                        ((uint)foldUpper < 0x80 && (byte)foldUpper == token.Literal)))
                    {
                        ok = false;
                        break;
                    }

                    cursor += literalWidth;
                    continue;
                }

                // Width-sensitive tokens consume one well-formed BMP scalar; anything
                // else fails closed (astral inputs decline up front, malformed inputs
                // never reach this walk past validation).
                var width = lead < 0x80 ? 1 : lead < 0xE0 ? 2 : 3;
                if (cursor + width > input.Length)
                {
                    ok = false;
                    break;
                }

                if (width > 1 && ((input[cursor + 1] & 0xC0) != 0x80 ||
                    (width == 3 && (input[cursor + 2] & 0xC0) != 0x80)))
                {
                    ok = false;
                    break;
                }

                if (token.Kind == AsciiSimplePatternTokenKind.Dot)
                {
                    cursor += width;
                    continue;
                }

                if (token.Kind != AsciiSimplePatternTokenKind.CharClass || token.CharClass.IsEmpty)
                {
                    ok = false;
                    break;
                }

                uint scalar = lead;
                if (width == 2)
                {
                    scalar = (uint)(((lead & 0x1F) << 6) | (input[cursor + 1] & 0x3F));
                }
                else if (width == 3)
                {
                    scalar = (uint)(((lead & 0x0F) << 12) | ((input[cursor + 1] & 0x3F) << 6) | (input[cursor + 2] & 0x3F));
                }

                // Positive ASCII sets never match non-ASCII scalars while ASCII-closed
                // negations match every one of them; decimal digits consult the owned
                // Nd table. ASCII scalars always use the byte rule.
                bool matches;
                if (scalar < 0x80)
                {
                    matches = token.CharClass.Contains((byte)scalar);
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
                    // fold-neutral): a non-ASCII scalar meets the folded ASCII mask
                    // through its invariant fold pair, so Kelvin meets [k] and is
                    // excluded by [^k], while fold-less scalars keep the
                    // case-sensitive outcome. Complements apply below.
                    var foldedHit = false;
                    if (Utf8OwnedCaseFold.TryGetFold((char)scalar, out var classLower, out var classUpper))
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

                cursor += width;
            }

            if (ok)
            {
                matchedByteLength = cursor - startByte;
                return true;
            }
        }

        matchedByteLength = 0;
        return false;
    }
    public static bool TryFindFirst(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget,
        out int startByte,
        out int matchedByteLength)
    {
        startByte = -1;
        matchedByteLength = 0;
        if (!CanServeScalar(kind, plan, input))
        {
            return false;
        }

        // A qualified scan is definitive either way: hits project owned while
        // misses never touch BCL verification.
        startByte = FindNextScalar(input, plan, budget, 0, out matchedByteLength);
        return true;
    }

    public static bool TryProjectMatch(
        ReadOnlySpan<byte> input,
        int startByte,
        int matchedByteLength,
        out Utf8ValueMatch match)
    {
        // Both boundaries are scalar-aligned by construction (boundary-filtered
        // starts plus whole-scalar consumption), so the boundary map projects
        // them exactly; anything else fails closed to BCL routing.
        match = Utf8ValueMatch.NoMatch;
        if (startByte < 0 || matchedByteLength < 0 || startByte + matchedByteLength > input.Length)
        {
            return false;
        }

        var map = Utf8BoundaryMap.Create(input);
        if (!map.TryGetUtf16OffsetForByteOffset(startByte, out var startUnit) ||
            !map.TryGetUtf16OffsetForByteOffset(startByte + matchedByteLength, out var endUnit))
        {
            return false;
        }

        match = new Utf8ValueMatch(true, true, startUnit, endUnit - startUnit, startByte, matchedByteLength);
        return true;
    }
}
