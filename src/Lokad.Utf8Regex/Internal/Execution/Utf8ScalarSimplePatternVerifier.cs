using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ScalarSimplePatternVerifier
{
    // R05: scalar-aware discovery for fixed-token non-byte-safe simple patterns on
    // astral-free non-ASCII input. ASCII search literals stay sound candidates through
    // UTF-8 self-synchronization (ASCII bytes never appear inside multibyte scalars);
    // width-sensitive tokens consume whole scalars with unit predicates (literals
    // byte-compare, singleline dots match any scalar, ASCII-closed classes apply the
    // byte predicate on ASCII scalars and their negation bit beyond). BCL parity holds
    // on BMP input where units equal scalars. Astral inputs, IgnoreCase plans, runs,
    // shorthand/Unicode-dependent classes, anchored plans, and the ASCII fast path
    // decline to the existing routes. Hooks sit past validation, so inputs are
    // well-formed; malformed scalars fail the walk closed.
    public static bool TryIsMatch(
        ReadOnlySpan<byte> input,
        NativeExecutionKind kind,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget,
        out bool isMatch)
    {
        isMatch = false;
        if (!Qualifies(kind, plan, input))
        {
            return false;
        }

        isMatch = FindNext(input, plan, budget, 0, out _) >= 0;
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
        if (!Qualifies(kind, plan, input))
        {
            return false;
        }

        var startByte = 0;
        while (startByte <= input.Length)
        {
            var matchByte = FindNext(input, plan, budget, startByte, out var matchedByteLength);
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

    private static bool Qualifies(NativeExecutionKind kind, in AsciiSimplePatternPlan plan, ReadOnlySpan<byte> input)
    {
        if (kind != NativeExecutionKind.AsciiSimplePattern ||
            plan.IsUtf8ByteSafe ||
            plan.IgnoreCase ||
            plan.IsStartAnchored ||
            plan.IsEndAnchored ||
            plan.RunPlan.HasValue ||
            plan.Branches.Length == 0)
        {
            return false;
        }

        foreach (var branch in plan.Branches)
        {
            foreach (var token in branch)
            {
                switch (token.Kind)
                {
                    case AsciiSimplePatternTokenKind.Literal:
                    case AsciiSimplePatternTokenKind.Dot:
                        break;
                    case AsciiSimplePatternTokenKind.CharClass:
                        if (token.CharClass.IsEmpty ||
                            (token.RequiresAsciiInput && token.ScalarClassKind != Utf8SimplePatternScalarClassKind.AsciiClosedNegation))
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

    private static int FindNext(
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
                    if (lead != token.Literal)
                    {
                        ok = false;
                        break;
                    }

                    cursor += 1;
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
                // negations match every one of them; ASCII scalars use the byte rule.
                var matches = scalar < 0x80
                    ? token.CharClass.Contains((byte)scalar)
                    : token.CharClass.Negated;
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
        if (!Qualifies(kind, plan, input))
        {
            return false;
        }

        // A qualified scan is definitive either way: hits project owned while
        // misses never touch BCL verification.
        startByte = FindNext(input, plan, budget, 0, out matchedByteLength);
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
