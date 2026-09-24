using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Recognizes the owned R02a slice: ASCII-only patterns built from
/// literals, concatenation, ordered alternation, greedy/lazy repetition,
/// numbered and named groups (BCL unnamed-first numbering), balancing
/// captures, atomic groups, transparent non-capturing groups and inline comments,
/// (R03) dots
/// with Singleline-scoped newline matching, anchors with Multiline-scoped line
/// edges, ASCII character classes (literals, ranges, supported escapes, and
/// shorthand members), word boundaries, shorthand classes, backreferences,
/// conditional alternations (group tests with committed branches, expression
/// tests via lookahead desugar), and zero-width lookarounds (dots, negated classes,
/// and negated shorthands
/// decline astral inputs at routing). IgnoreCase folds per-scalar BCL-invariant
/// case (Turkic instances keep legacy routing through the construction gate).
/// Unicode property escapes (general categories/groups and named blocks as
/// bare atoms or single-category classes; mixed category classes and blocks
/// under IgnoreCase stay legacy-exact). Anything else (duplicate or
/// explicitly-numbered names, backreferences and conditionals inside
/// lookbehind bodies, lookbehind bodies of balancing shapes) declines to
/// <c>null</c> so the existing
/// routes preserve their behavior, including construction failures. Group
/// nesting beyond <see cref="MaxNestingDepth"/> also declines. R02b construction
/// admission: patterns longer than <see cref="MaxPatternLength"/> decline before any
/// parse work so retained slice programs stay bounded; declined shapes keep
/// legacy routing.</summary>
internal static class Utf8SliceParser
{
    internal const int MaxNestingDepth = 64;

    // Defense cap, not a language limit: the longest surveyed corpus and test
    // patterns stay under a hundred characters, so 64 KiB leaves orders of
    // magnitude of headroom while bounding retained programs to single-digit
    // megabytes. Over-length shapes decline to legacy routing with identical
    // behavior; construction never fails on length.
    internal const int MaxPatternLength = 65536;

    private const RegexOptions RejectedOptions =
        RegexOptions.ECMAScript |
        RegexOptions.NonBacktracking;

    internal static Utf8SliceProgram? TryParse(string pattern, RegexOptions options)
    {
        if (pattern is null)
        {
            return null;
        }

        if (pattern.Length > MaxPatternLength)
        {
            return null;
        }

        if ((options & RejectedOptions) != 0)
        {
            return null;
        }

        // IgnorePatternWhitespace strips insignificant pattern whitespace and
        // #-to-newline comments before parsing (escapes, classes, and (?#)
        // comments pass through verbatim). Only the ASCII insignificance set
        // matters: slice patterns are ASCII-only by the gate below, and BCL skips
        // exactly space/tab/LF/FF/CR (vertical tab stays significant, probed).
        if ((options & RegexOptions.IgnorePatternWhitespace) != 0)
        {
            pattern = StripInsignificantPatternWhitespace(pattern);
        }

        foreach (var character in pattern)
        {
            if (character > 127)
            {
                return null;
            }
        }

        var parser = new Parser(pattern, (options & RegexOptions.Singleline) != 0, (options & RegexOptions.Multiline) != 0, (options & RegexOptions.IgnoreCase) != 0, (options & RegexOptions.ExplicitCapture) != 0, (options & RegexOptions.IgnorePatternWhitespace) != 0);
        if (!parser.TryParseAlternation(0, out var root) || parser.Position != pattern.Length)
        {
            return null;
        }

        // .NET 10 per-lane contract: the BCL compiler keeps one iteration of
        // progress when a finite-maximum lazy loop abandons at its maximum
        // while the interpreter abandons fully (pinned by
        // LazyLoopAbandonmentPinsDotNet10OracleDisagreement). The shared
        // runner follows interpreter abandonment, so compiled-lane shapes
        // with finite-maximum lazy repeats decline to their BCL-compiled
        // fallback; infinite-maximum lazy loops agree on both lanes.
        if ((options & RegexOptions.Compiled) != 0 && parser.SawFiniteMaximumLazyRepeat)
        {
            return null;
        }

        root ??= new Utf8SliceNode.EmptyNode();

        // BCL numbers unnamed groups left-to-right first, then named
        // groups; backreference digits already denote final numbers
        // (digit minus one), so only captures rewrite from temporary
        // open-order slots. Named shapes also publish names by final
        // number for projection.
        static Utf8SliceNode RenumberCaptures(Utf8SliceNode node, int[] finals)
        {
            switch (node)
            {
                case Utf8SliceNode.CaptureNode capture:
                    return new Utf8SliceNode.CaptureNode(
                        finals[capture.Slot] - 1,
                        RenumberCaptures(capture.Body, finals));
                case Utf8SliceNode.ConcatNode concat:
                    var children = new Utf8SliceNode[concat.Children.Length];
                    for (var child = 0; child < children.Length; child++)
                    {
                        children[child] = RenumberCaptures(concat.Children[child], finals);
                    }

                    return new Utf8SliceNode.ConcatNode(children);
                case Utf8SliceNode.AlternationNode alternation:
                    var branches = new Utf8SliceNode[alternation.Branches.Length][];
                    for (var branch = 0; branch < branches.Length; branch++)
                    {
                        var rewritten = new Utf8SliceNode[alternation.Branches[branch].Length];
                        for (var child = 0; child < rewritten.Length; child++)
                        {
                            rewritten[child] = RenumberCaptures(alternation.Branches[branch][child], finals);
                        }

                        branches[branch] = rewritten;
                    }

                    return new Utf8SliceNode.AlternationNode(branches);
                case Utf8SliceNode.RepeatNode repeat:
                    return new Utf8SliceNode.RepeatNode(
                        RenumberCaptures(repeat.Body, finals),
                        repeat.Minimum,
                        repeat.Maximum,
                        repeat.Lazy);
                case Utf8SliceNode.LookaheadNode lookahead:
                    return new Utf8SliceNode.LookaheadNode(
                        RenumberCaptures(lookahead.Body, finals),
                        lookahead.Positive);
                case Utf8SliceNode.LookbehindNode lookbehind:
                    return new Utf8SliceNode.LookbehindNode(
                        RenumberCaptures(lookbehind.Body, finals),
                        lookbehind.Positive);
                case Utf8SliceNode.AtomicNode atomic:
                    return new Utf8SliceNode.AtomicNode(
                        RenumberCaptures(atomic.Body, finals));
                case Utf8SliceNode.BalanceNode balance:
                    return new Utf8SliceNode.BalanceNode(
                        finals[balance.PopSlot] - 1,
                        balance.CaptureSlot < 0 ? -1 : finals[balance.CaptureSlot] - 1,
                        RenumberCaptures(balance.Body, finals));
                case Utf8SliceNode.ConditionalNode conditional:
                    // Template slots renumber like captures; raw digits already denote
                    // final numbers (validated range above), so they shift to zero-based
                    // slots directly, and group zero stays always-false.
                    var resolvedTest = conditional.TestSlot >= 0
                        ? finals[conditional.TestSlot] - 1
                        : conditional.TestSlot == -1 ? -1 : -conditional.TestSlot - 3;
                    return new Utf8SliceNode.ConditionalNode(
                        resolvedTest,
                        RenumberCaptures(conditional.YesBody, finals),
                        RenumberCaptures(conditional.NoBody, finals),
                        conditional.TestBody is null ? null : RenumberCaptures(conditional.TestBody, finals));
                default:
                    return node;
            }
        }

        // Single-caller tree walks stay static-local: reversal mirrors BCL
        // RightToLeft parsing (main-stream concatenations back-to-front,
        // assertion bodies untouched), and the decline walk keeps exotic
        // direction-sensitive mixes on legacy-exact routing.
        static Utf8SliceNode ReverseRtlMainStream(Utf8SliceNode node, bool inAssertion)
        {
            switch (node)
            {
                case Utf8SliceNode.ConcatNode concat:
                    var children = new Utf8SliceNode[concat.Children.Length];
                    for (var child = 0; child < children.Length; child++)
                    {
                        children[child] = ReverseRtlMainStream(concat.Children[child], inAssertion);
                    }

                    if (!inAssertion)
                    {
                        Array.Reverse(children);
                    }

                    return new Utf8SliceNode.ConcatNode(children);
                case Utf8SliceNode.AlternationNode alternation:
                    var branches = new Utf8SliceNode[alternation.Branches.Length][];
                    for (var branch = 0; branch < branches.Length; branch++)
                    {
                        var source = alternation.Branches[branch];
                        var rewritten = new Utf8SliceNode[source.Length];
                        for (var child = 0; child < rewritten.Length; child++)
                        {
                            rewritten[child] = ReverseRtlMainStream(source[child], inAssertion);
                        }

                        if (!inAssertion)
                        {
                            Array.Reverse(rewritten);
                        }

                        branches[branch] = rewritten;
                    }

                    return new Utf8SliceNode.AlternationNode(branches);
                case Utf8SliceNode.RepeatNode repeat:
                    return new Utf8SliceNode.RepeatNode(
                        ReverseRtlMainStream(repeat.Body, inAssertion),
                        repeat.Minimum,
                        repeat.Maximum,
                        repeat.Lazy);
                case Utf8SliceNode.CaptureNode capture:
                    return new Utf8SliceNode.CaptureNode(
                        capture.Slot,
                        ReverseRtlMainStream(capture.Body, inAssertion));
                case Utf8SliceNode.AtomicNode atomic:
                    return new Utf8SliceNode.AtomicNode(
                        ReverseRtlMainStream(atomic.Body, inAssertion));
                case Utf8SliceNode.ConditionalNode conditional:
                    return new Utf8SliceNode.ConditionalNode(
                        conditional.TestSlot,
                        ReverseRtlMainStream(conditional.YesBody, inAssertion),
                        ReverseRtlMainStream(conditional.NoBody, inAssertion),
                        conditional.TestBody is null ? null : ReverseRtlMainStream(conditional.TestBody, true));
                case Utf8SliceNode.BalanceNode balance:
                    return new Utf8SliceNode.BalanceNode(
                        balance.PopSlot,
                        balance.CaptureSlot,
                        ReverseRtlMainStream(balance.Body, inAssertion));
                case Utf8SliceNode.LookaheadNode lookahead:
                    return new Utf8SliceNode.LookaheadNode(
                        ReverseRtlMainStream(lookahead.Body, true),
                        lookahead.Positive);
                case Utf8SliceNode.LookbehindNode lookbehind:
                    return new Utf8SliceNode.LookbehindNode(
                        ReverseRtlMainStream(lookbehind.Body, true),
                        lookbehind.Positive);
                default:
                    return node;
            }
        }

        static bool RtlTreeDeclines(Utf8SliceNode node, bool inLookbehindBody, bool inLookaheadBody)
        {
            switch (node)
            {
                case Utf8SliceNode.LookaheadNode lookahead:
                    if (inLookbehindBody)
                    {
                        return true;
                    }

                    return RtlTreeDeclines(lookahead.Body, false, true);
                case Utf8SliceNode.LookbehindNode lookbehind:
                    if (inLookbehindBody || inLookaheadBody)
                    {
                        return true;
                    }

                    return RtlTreeDeclines(lookbehind.Body, true, false);
                case Utf8SliceNode.BackrefNode:
                    if (inLookbehindBody || inLookaheadBody)
                    {
                        return true;
                    }

                    break;
                case Utf8SliceNode.ConcatNode concat:
                    foreach (var child in concat.Children)
                    {
                        if (RtlTreeDeclines(child, inLookbehindBody, inLookaheadBody))
                        {
                            return true;
                        }
                    }

                    break;
                case Utf8SliceNode.AlternationNode alternation:
                    foreach (var branch in alternation.Branches)
                    {
                        foreach (var child in branch)
                        {
                            if (RtlTreeDeclines(child, inLookbehindBody, inLookaheadBody))
                            {
                                return true;
                            }
                        }
                    }

                    break;
                case Utf8SliceNode.RepeatNode repeat:
                    if (RtlTreeDeclines(repeat.Body, inLookbehindBody, inLookaheadBody))
                    {
                        return true;
                    }

                    break;
                case Utf8SliceNode.CaptureNode capture:
                    if (RtlTreeDeclines(capture.Body, inLookbehindBody, inLookaheadBody))
                    {
                        return true;
                    }

                    break;
                case Utf8SliceNode.AtomicNode atomic:
                    if (RtlTreeDeclines(atomic.Body, inLookbehindBody, inLookaheadBody))
                    {
                        return true;
                    }

                    break;
                case Utf8SliceNode.ConditionalNode conditional:
                    if (RtlTreeDeclines(conditional.YesBody, inLookbehindBody, inLookaheadBody)
                        || RtlTreeDeclines(conditional.NoBody, inLookbehindBody, inLookaheadBody)
                        || (conditional.TestBody is not null && RtlTreeDeclines(conditional.TestBody, inLookbehindBody, true)))
                    {
                        return true;
                    }

                    break;
                case Utf8SliceNode.BalanceNode balance:
                    return true;
                default:
                    break;
            }

            return false;
        }

        // Conditional number tests denote final groups, so out-of-range digits
        // decline exactly like the BCL construction throw (forward references read
        // groups defined anywhere). Group tests inside lookbehind bodies also
        // decline: BCL evaluates bodies back-to-front in one shot while the shared
        // machinery enumerates starts forward, which diverges on order-sensitive
        // tests (and on backreferences, for the same reason).
        static bool ConditionalTestsValidate(Utf8SliceNode node, int groupCount)
        {
            switch (node)
            {
                case Utf8SliceNode.ConcatNode concat:
                    foreach (var child in concat.Children)
                    {
                        if (!ConditionalTestsValidate(child, groupCount))
                        {
                            return false;
                        }
                    }

                    break;
                case Utf8SliceNode.AlternationNode alternation:
                    foreach (var branch in alternation.Branches)
                    {
                        foreach (var child in branch)
                        {
                            if (!ConditionalTestsValidate(child, groupCount))
                            {
                                return false;
                            }
                        }
                    }

                    break;
                case Utf8SliceNode.RepeatNode repeat:
                    if (!ConditionalTestsValidate(repeat.Body, groupCount))
                    {
                        return false;
                    }

                    break;
                case Utf8SliceNode.CaptureNode capture:
                    if (!ConditionalTestsValidate(capture.Body, groupCount))
                    {
                        return false;
                    }

                    break;
                case Utf8SliceNode.AtomicNode atomic:
                    if (!ConditionalTestsValidate(atomic.Body, groupCount))
                    {
                        return false;
                    }

                    break;
                case Utf8SliceNode.BalanceNode balance:
                    if (!ConditionalTestsValidate(balance.Body, groupCount))
                    {
                        return false;
                    }

                    break;
                case Utf8SliceNode.LookaheadNode lookahead:
                    if (!ConditionalTestsValidate(lookahead.Body, groupCount))
                    {
                        return false;
                    }

                    break;
                case Utf8SliceNode.LookbehindNode lookbehind:
                    if (!ConditionalTestsValidate(lookbehind.Body, groupCount))
                    {
                        return false;
                    }

                    break;
                case Utf8SliceNode.ConditionalNode conditional:
                    if (conditional.TestSlot <= -2)
                    {
                        var rawNumber = -conditional.TestSlot - 2;
                        if (rawNumber < 1 || rawNumber > groupCount)
                        {
                            return false;
                        }
                    }

                    if (!ConditionalTestsValidate(conditional.YesBody, groupCount)
                        || !ConditionalTestsValidate(conditional.NoBody, groupCount)
                        || (conditional.TestBody is not null && !ConditionalTestsValidate(conditional.TestBody, groupCount)))
                    {
                        return false;
                    }

                    break;
                default:
                    break;
            }

            return true;
        }

        static bool LookbehindBodyDeclines(Utf8SliceNode node, bool inBody)
        {
            switch (node)
            {
                case Utf8SliceNode.BackrefNode when inBody:
                case Utf8SliceNode.ConditionalNode when inBody:
                    return true;
                case Utf8SliceNode.LookbehindNode lookbehind:
                    return LookbehindBodyDeclines(lookbehind.Body, true);
                case Utf8SliceNode.ConcatNode concat:
                    foreach (var child in concat.Children)
                    {
                        if (LookbehindBodyDeclines(child, inBody))
                        {
                            return true;
                        }
                    }

                    break;
                case Utf8SliceNode.AlternationNode alternation:
                    foreach (var branch in alternation.Branches)
                    {
                        foreach (var child in branch)
                        {
                            if (LookbehindBodyDeclines(child, inBody))
                            {
                                return true;
                            }
                        }
                    }

                    break;
                case Utf8SliceNode.RepeatNode repeat:
                    return LookbehindBodyDeclines(repeat.Body, inBody);
                case Utf8SliceNode.CaptureNode capture:
                    return LookbehindBodyDeclines(capture.Body, inBody);
                case Utf8SliceNode.AtomicNode atomic:
                    return LookbehindBodyDeclines(atomic.Body, inBody);
                case Utf8SliceNode.BalanceNode balance:
                    return LookbehindBodyDeclines(balance.Body, inBody);
                case Utf8SliceNode.LookaheadNode lookahead:
                    return LookbehindBodyDeclines(lookahead.Body, inBody);
                case Utf8SliceNode.ConditionalNode conditional when inBody:
                    return true;
                case Utf8SliceNode.ConditionalNode conditionalOut:
                    return LookbehindBodyDeclines(conditionalOut.YesBody, inBody)
                        || LookbehindBodyDeclines(conditionalOut.NoBody, inBody)
                        || (conditionalOut.TestBody is not null && LookbehindBodyDeclines(conditionalOut.TestBody, inBody));
                default:
                    break;
            }

            return false;
        }

        var finals = new int[parser.GroupCount];
        var final = 1;
        foreach (var decl in parser.GroupDecls)
        {
            if (decl.Name is null)
            {
                finals[decl.Slot] = final++;
            }
        }

        foreach (var decl in parser.GroupDecls)
        {
            if (decl.Name is not null)
            {
                finals[decl.Slot] = final++;
            }
        }

        var groupNames = new string[parser.GroupCount + 1];
        groupNames[0] = "0";
        foreach (var decl in parser.GroupDecls)
        {
            groupNames[finals[decl.Slot]] = decl.Name ?? ((uint)finals[decl.Slot]).ToString();
        }

        // Conditional number tests validate against the full group tally here
        // (out-of-range digits decline like the BCL construction throw), and
        // lookbehind bodies with backreferences or conditionals decline: BCL
        // evaluates bodies back-to-front in one shot while the shared machinery
        // enumerates starts forward.
        if (!ConditionalTestsValidate(root, parser.GroupCount) || LookbehindBodyDeclines(root, false))
        {
            return null;
        }

        // Right-to-left programs run the main stream mirrored: every main-stream
        // concatenation reverses (BCL parses concatenations back-to-front in
        // RightToLeft mode) while assertion bodies keep left-to-right order and
        // execute through their own machinery. Balancing intervals assume
        // forward positions, lookbehind bodies with nested assertions,
        // backreferences, or possessive bodies stay legacy, and lookahead bodies
        // with backreferences, balancing, or nested lookbehinds stay legacy,
        // all with identical behavior through the existing fallback routes.
        var rightToLeft = (options & RegexOptions.RightToLeft) != 0;
        if (rightToLeft)
        {
            if (RtlTreeDeclines(root, false, false))
            {
                return null;
            }

            root = ReverseRtlMainStream(root, false);
        }

        // Balancing stacks live in attempt-local chunks while nested
        // lookbehind runs share live slots and heads: a capture or pop
        // inside a lookbehind body would alias shared balance heads through
        // the nested tables, so such mixes decline to legacy-exact routing.
        // (Inline lookahead shares the same tables and stays owned.)
        if (parser.SawBalancing && parser.SawLookbehindGroupWrite)
        {
            return null;
        }

        return Utf8SliceLowerer.Lower(RenumberCaptures(root, finals), groupNames, parser.SawBalancing, parser.MatchIgnoreCase || parser.SawFold, rightToLeft);
    }

    // Hex, unicode, and octal escapes with ASCII results (BCL-exact shapes:
    // exactly-2-hex, exactly-4-hex, and up to 3 octal digits; malformed or
    // non-ASCII results decline). Shared by literal and class positions (two
    // callers, so the single-caller debt ceiling does not apply).
    private static bool TryParseAsciiNumericEscape(string pattern, int start, bool bareOctal, out int value, out int next)
    {
        static int HexValue(char digit)
        {
            return digit <= '9' ? digit - '0' : (digit & ~32) - 'A' + 10;
        }

        value = 0;
        next = start;
        if (start >= pattern.Length)
        {
            return false;
        }

        var marker = pattern[start];
        if (marker == 'x')
        {
            if (start + 2 >= pattern.Length || !char.IsAsciiHexDigit(pattern[start + 1]) || !char.IsAsciiHexDigit(pattern[start + 2]))
            {
                return false;
            }

            value = HexValue(pattern[start + 1]) * 16 + HexValue(pattern[start + 2]);
            if (value > 127)
            {
                return false;
            }

            next = start + 3;
            return true;
        }

        if (marker == 'u')
        {
            if (start + 4 >= pattern.Length)
            {
                return false;
            }

            value = 0;
            for (var digit = 1; digit <= 4; digit++)
            {
                if (!char.IsAsciiHexDigit(pattern[start + digit]))
                {
                    return false;
                }

                value = value * 16 + HexValue(pattern[start + digit]);
            }

            if (value > 127)
            {
                return false;
            }

            next = start + 5;
            return true;
        }

        if (marker >= '0' && marker <= '7' && (bareOctal || marker == '0'))
        {
            value = 0;
            var end = start;
            while (end < pattern.Length && end - start < 3 && pattern[end] >= '0' && pattern[end] <= '7')
            {
                value = value * 8 + (pattern[end] - '0');
                end++;
            }

            if (value > 127)
            {
                return false;
            }

            next = end;
            return true;
        }

        return false;
    }

    // Unicode property escapes (BCL ParseProperty shapes: braces required,
    // word-char/dash names, unknown names decline). General categories and
    // groups resolve to masks, Is*/_xml* blocks to half-open range strings;
    // blocks decline under IgnoreCase since case-equivalent range expansion
    // lives in legacy. Shared by bare atoms and single-category classes (two
    // callers, so the single-caller debt ceiling does not apply).
    private static bool TryParseCategoryAtom(string pattern, int backslashIndex, bool negated, bool foldCase, out Utf8SliceNode? node, out int nextIndex)
    {
        node = null;
        nextIndex = backslashIndex;
        if (backslashIndex + 3 >= pattern.Length || pattern[backslashIndex] != '\\' ||
            (pattern[backslashIndex + 1] != 'p' && pattern[backslashIndex + 1] != 'P') ||
            pattern[backslashIndex + 2] != '{')
        {
            return false;
        }

        var nameStart = backslashIndex + 3;
        var nameEnd = nameStart;
        while (nameEnd < pattern.Length && (char.IsAsciiLetterOrDigit(pattern[nameEnd]) || pattern[nameEnd] == '_' || pattern[nameEnd] == '-'))
        {
            nameEnd++;
        }

        if (nameEnd == nameStart || nameEnd >= pattern.Length || pattern[nameEnd] != '}' ||
            !Utf8SliceCategories.TryResolve(pattern.Substring(nameStart, nameEnd - nameStart), out var mask, out var ranges, out var isBlock) ||
            (isBlock && foldCase))
        {
            return false;
        }

        node = new Utf8SliceNode.CategoryNode(mask, ranges, negated, foldCase);
        nextIndex = nameEnd + 1;
        return true;
    }


    static string StripInsignificantPatternWhitespace(string source)
    {
        var filtered = new StringBuilder(source.Length);
        var index = 0;
        while (index < source.Length)
        {
            var current = source[index];
            if (current == '\\' && index + 1 < source.Length)
            {
                // Unicode property spans lex verbatim: BCL reads the property
                // name raw (a space like \p{Is Greek} throws), so stripping
                // inside the braces would invent an accepted name. Classes
                // already pass through verbatim below.
                if ((source[index + 1] == 'p' || source[index + 1] == 'P') &&
                    index + 2 < source.Length && source[index + 2] == '{')
                {
                    var propertyEnd = source.IndexOf('}', index + 3);
                    var spanEnd = propertyEnd < 0 ? source.Length : propertyEnd + 1;
                    filtered.Append(source, index, spanEnd - index);
                    index = spanEnd;
                }
                else
                {
                    filtered.Append(current).Append(source[index + 1]);
                    index += 2;
                }
            }
            else if (current == '(' && index + 2 < source.Length &&
                source[index + 1] == '?' && source[index + 2] == '#')
            {
                var commentEnd = source.IndexOf(')', index + 3);
                if (commentEnd < 0)
                {
                    filtered.Append(source, index, source.Length - index);
                    break;
                }

                filtered.Append(source, index, commentEnd - index + 1);
                index = commentEnd + 1;
            }
            else if (current == '[')
            {
                filtered.Append(current);
                index++;
                if (index < source.Length && source[index] == ']')
                {
                    filtered.Append(']');
                    index++;
                }

                while (index < source.Length && source[index] != ']')
                {
                    if (source[index] == '\\' && index + 1 < source.Length)
                    {
                        filtered.Append(source[index]).Append(source[index + 1]);
                        index += 2;
                    }
                    else
                    {
                        filtered.Append(source[index]);
                        index++;
                    }
                }

                if (index < source.Length)
                {
                    filtered.Append(']');
                    index++;
                }
            }
            else if (current == '#')
            {
                while (index < source.Length && source[index] != '\n' && source[index] != '\r')
                {
                    index++;
                }
            }
            else if (current == ' ' || current == '\t' || current == '\n' || current == '\f' || current == '\r')
            {
                index++;
            }
            else
            {
                filtered.Append(current);
                index++;
            }
        }

        return filtered.ToString();
    }

    private sealed class Parser
    {
        private readonly string _pattern;
        private bool _matchNewline;
        private bool _matchMultiline;
        private bool _matchIgnoreCase;
        private bool _explicitCapture;
        private bool _sawFold;
        private int _expressionBranchDepth = -1;
        private bool _whitespaceStripped;
        private int _position;
        private int _groups;
        private readonly List<(int Slot, string? Name)> _groupDecls = new();
        private readonly Dictionary<string, int> _groupSlots = new(StringComparer.Ordinal);
        private bool _sawBalancing;
        private bool _sawFiniteMaximumLazyRepeat;
        private int _lookbehindDepth;
        private bool _sawLookbehindGroupWrite;

        internal Parser(string pattern, bool matchNewline, bool matchMultiline, bool matchIgnoreCase, bool explicitCapture, bool whitespaceStripped)
        {
            _pattern = pattern;
            _matchNewline = matchNewline;
            _matchMultiline = matchMultiline;
            _matchIgnoreCase = matchIgnoreCase;
            _explicitCapture = explicitCapture;
            _whitespaceStripped = whitespaceStripped;
        }

        internal int Position => _position;
        internal int GroupCount => _groups;
        internal List<(int Slot, string? Name)> GroupDecls => _groupDecls;
        internal bool SawBalancing => _sawBalancing;
        internal bool MatchIgnoreCase => _matchIgnoreCase;
        internal bool SawFold => _sawFold;
        internal bool SawLookbehindGroupWrite => _sawLookbehindGroupWrite;
        internal bool SawFiniteMaximumLazyRepeat => _sawFiniteMaximumLazyRepeat;

        internal bool TryParseAlternation(int depth, out Utf8SliceNode? root)
        {
            var branches = new List<List<Utf8SliceNode>> { new() };
            while (true)
            {
                if (!TryParseConcatenation(depth, branches[^1]))
                {
                    root = null;
                    return false;
                }

                if (AtEnd || _pattern[_position] != '|')
                {
                    break;
                }

                _position++;
                branches.Add(new List<Utf8SliceNode>());
            }

            root = branches.Count == 1 && branches[0].Count == 1
                ? branches[0][0]
                : new Utf8SliceNode.AlternationNode(branches.Select(static branch => branch.ToArray()).ToArray());
            return true;
        }

        private bool TryParseConcatenation(int depth, List<Utf8SliceNode> children)
        {
            while (!AtEnd && _pattern[_position] != '|' && _pattern[_position] != ')')
            {
                if (!TryParseRepeat(depth, out var child) || child is null)
                {
                    return false;
                }

                children.Add(child);
            }

            return true;
        }

        private bool TryParseRepeat(int depth, out Utf8SliceNode? node)
        {
            node = null;
            if (!TryParseAtom(depth, out var atom) || atom is null)
            {
                return false;
            }

            if (AtEnd)
            {
                node = atom;
                return true;
            }

            var minimum = 1;
            var maximum = 1;
            var quantified = true;
            switch (_pattern[_position])
            {
                case '*':
                    minimum = 0;
                    maximum = int.MaxValue;
                    _position++;
                    break;
                case '+':
                    minimum = 1;
                    maximum = int.MaxValue;
                    _position++;
                    break;
                case '?':
                    minimum = 0;
                    maximum = 1;
                    _position++;
                    break;
                case '{':
                    if (!TryParseBraceBounds(out minimum, out maximum))
                    {
                        node = atom;
                        return true;
                    }

                    break;
                default:
                    quantified = false;
                    break;
            }

            if (!quantified)
            {
                node = atom;
                return true;
            }

            var lazy = false;
            if (!AtEnd && _pattern[_position] == '?')
            {
                lazy = true;
                _position++;
            }

            if (lazy && maximum != int.MaxValue)
            {
                _sawFiniteMaximumLazyRepeat = true;
            }

            node = new Utf8SliceNode.RepeatNode(atom, minimum, maximum, lazy);
            return true;
        }

        private bool TryParseBraceBounds(out int minimum, out int maximum)
        {
            minimum = 0;
            maximum = 0;
            var start = _position;
            _position++;
            if (!TryParseDigits(out minimum))
            {
                _position = start;
                return false;
            }

            maximum = minimum;
            if (!AtEnd && _pattern[_position] == ',')
            {
                _position++;
                if (!AtEnd && _pattern[_position] == '}')
                {
                    maximum = int.MaxValue;
                }
                else if (!TryParseDigits(out maximum))
                {
                    _position = start;
                    return false;
                }
            }

            if (AtEnd || _pattern[_position] != '}' || maximum < minimum)
            {
                _position = start;
                return false;
            }

            _position++;
            return true;
        }

        private bool TryParseDigits(out int value)
        {
            value = 0;
            var start = _position;
            while (!AtEnd && _pattern[_position] >= '0' && _pattern[_position] <= '9')
            {
                var next = (long)value * 10 + (_pattern[_position] - '0');
                if (next > int.MaxValue)
                {
                    _position = start;
                    value = 0;
                    return false;
                }

                value = (int)next;
                _position++;
            }

            return _position != start;
        }

        private bool TryParseAtom(int depth, out Utf8SliceNode? node)
        {
            node = null;
            if (AtEnd)
            {
                return false;
            }

            var character = _pattern[_position];
            switch (character)
            {
                case '(':
                    if (depth >= MaxNestingDepth)
                    {
                        return false;
                    }

                    // Zero-width lookarounds bind their bodies without
                    // consuming a group number; every other (? form keeps
                    // declining through the capture path below.
                    if (_position + 2 < _pattern.Length && _pattern[_position + 1] == '?')
                    {
                        var marker = _pattern[_position + 2];
                        if (marker == '=' || marker == '!')
                        {
                            var positive = marker == '=';
                            _position += 3;
                            var savedLookahead = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            if (!TryParseAlternation(depth + 1, out var lookahead) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedLookahead;
                            node = new Utf8SliceNode.LookaheadNode(lookahead ?? new Utf8SliceNode.EmptyNode(), positive);
                            return true;
                        }

                        if (marker == '<' && _position + 3 < _pattern.Length &&
                            (_pattern[_position + 3] == '=' || _pattern[_position + 3] == '!'))
                        {
                            var behindPositive = _pattern[_position + 3] == '=';
                            _position += 4;
                            var savedBehind = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            _lookbehindDepth++;
                            var parsedBehind = TryParseAlternation(depth + 1, out var lookbehind);
                            _lookbehindDepth--;
                            if (!parsedBehind || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedBehind;
                            node = new Utf8SliceNode.LookbehindNode(lookbehind ?? new Utf8SliceNode.EmptyNode(), behindPositive);
                            return true;
                        }

                        if (marker == 'i' || marker == 'I' || marker == 'm' || marker == 'M' ||
                            marker == 'n' || marker == 'N' || marker == 's' || marker == 'S' ||
                            marker == 'x' || marker == 'X' || marker == '-' || marker == '+')
                        {
                            // Inline options (BCL ScanOptions): letters apply case-insensitively
                            // with -/+ prefixes; an unknown letter stops the scan and anything
                            // but ) or : declines. Option constructs directly inside
                            // expression-conditional branches decline (BCL throws there). x-forms
                            // decline unless they only switch whitespace off with global
                            // stripping already off (regional lexing lives in legacy).
                            if (_expressionBranchDepth == depth)
                            {
                                return false;
                            }

                            var scan = _position + 2;
                            var off = false;
                            var nextIgnoreCase = _matchIgnoreCase;
                            var nextMultiline = _matchMultiline;
                            var nextNewline = _matchNewline;
                            var nextExplicit = _explicitCapture;
                            var sawX = false;
                            var xValue = false;
                            while (scan < _pattern.Length)
                            {
                                var optionChar = _pattern[scan];
                                if (optionChar == '-')
                                {
                                    off = true;
                                    scan++;
                                }
                                else if (optionChar == '+')
                                {
                                    off = false;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 'i')
                                {
                                    nextIgnoreCase = !off;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 'm')
                                {
                                    nextMultiline = !off;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 'n')
                                {
                                    nextExplicit = !off;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 's')
                                {
                                    nextNewline = !off;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 'x')
                                {
                                    sawX = true;
                                    xValue = !off;
                                    scan++;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (scan >= _pattern.Length || (_pattern[scan] != ')' && _pattern[scan] != ':'))
                            {
                                return false;
                            }

                            if (sawX && (_whitespaceStripped || xValue))
                            {
                                return false;
                            }

                            if (_pattern[scan] == ')')
                            {
                                _position = scan + 1;
                                if (!AtEnd && (_pattern[_position] == '*' || _pattern[_position] == '+' || _pattern[_position] == '?' || _pattern[_position] == '{'))
                                {
                                    return false;
                                }

                                _matchIgnoreCase = nextIgnoreCase;
                                _matchMultiline = nextMultiline;
                                _matchNewline = nextNewline;
                                _explicitCapture = nextExplicit;
                                node = new Utf8SliceNode.EmptyNode();
                                return true;
                            }

                            var savedOptions = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            _position = scan + 1;
                            _matchIgnoreCase = nextIgnoreCase;
                            _matchMultiline = nextMultiline;
                            _matchNewline = nextNewline;
                            _explicitCapture = nextExplicit;
                            if (!TryParseAlternation(depth + 1, out var scopedBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedOptions;
                            node = scopedBody ?? new Utf8SliceNode.EmptyNode();
                            return true;
                        }

                        if (marker == '>')
                        {
                            // Atomic (possessive) groups bind their bodies without
                            // consuming a group number.
                            _position += 3;
                            var savedAtomic = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            if (!TryParseAlternation(depth + 1, out var atomicBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedAtomic;
                            node = new Utf8SliceNode.AtomicNode(atomicBody ?? new Utf8SliceNode.EmptyNode());
                            return true;
                        }

                        if (marker == ':')
                        {
                            // Non-capturing groups are transparent: the body
                            // lowers as-is with no slot and no group number.
                            _position += 3;
                            var savedTransparent = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            if (!TryParseAlternation(depth + 1, out var plainBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedTransparent;
                            node = plainBody ?? new Utf8SliceNode.EmptyNode();
                            return true;
                        }

                        if (marker == '(')
                        {
                            // Conditional alternation (?(test)yes|no): number tests bind final
                            // group numbers (validated post-parse since forward references read
                            // groups defined anywhere), backward-defined names bind template slots,
                            // and every other test parses as an expression subpattern that desugars
                            // to a lookahead-guarded alternation. A second | declines (BCL throws
                            // AlternationHasTooManyConditions); a named capture or comment opening
                            // the test declines (BCL throws).
                            var savedConditional = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            var testStart = _position + 3;
                            var testChar = testStart < _pattern.Length ? _pattern[testStart] : '\0';
                            int testSlot = int.MinValue;
                            Utf8SliceNode? testBody = null;
                            if (testChar >= '0' && testChar <= '9')
                            {
                                var testEnd = testStart;
                                var rawNumber = 0L;
                                while (testEnd < _pattern.Length && _pattern[testEnd] >= '0' && _pattern[testEnd] <= '9')
                                {
                                    rawNumber = rawNumber * 10 + (_pattern[testEnd] - '0');
                                    if (rawNumber > int.MaxValue)
                                    {
                                        return false;
                                    }
                                    testEnd++;
                                }
                                if (testEnd >= _pattern.Length || _pattern[testEnd] != ')')
                                {
                                    return false;
                                }
                                testSlot = rawNumber == 0 ? -1 : -(int)rawNumber - 2;
                                _position = testEnd + 1;
                            }
                            else
                            {
                                var nameEnd = testStart;
                                while (nameEnd < _pattern.Length && (( _pattern[nameEnd] >= '0' && _pattern[nameEnd] <= '9') || (_pattern[nameEnd] >= 'A' && _pattern[nameEnd] <= 'Z') || _pattern[nameEnd] == '_' || (_pattern[nameEnd] >= 'a' && _pattern[nameEnd] <= 'z')))
                                {
                                    nameEnd++;
                                }
                                if (nameEnd > testStart && nameEnd < _pattern.Length && _pattern[nameEnd] == ')' && _groupSlots.TryGetValue(_pattern.Substring(testStart, nameEnd - testStart), out var testTemp))
                                {
                                    testSlot = testTemp;
                                    _position = nameEnd + 1;
                                }
                                else
                                {
                                    // The expression test starts at its own paren. A (?-form parses
                                    // as one atom (those forms never capture, so no phantom group
                                    // arises); a named capture or comment opener declines exactly
                                    // like the BCL construction throw. A plain group parses with its
                                    // parens stripped (BCL ignores the test paren while inner groups
                                    // bind normally).
                                    var testParen = _position + 2;
                                    if (testParen + 1 < _pattern.Length && _pattern[testParen + 1] == '?')
                                    {
                                        if (testParen + 2 < _pattern.Length)
                                        {
                                            var testMarker = _pattern[testParen + 2];
                                            if (testMarker == '#'
                                                || testMarker == '\''
                                                || (testMarker == '<' && testParen + 3 < _pattern.Length && _pattern[testParen + 3] != '=' && _pattern[testParen + 3] != '!'))
                                            {
                                                return false;
                                            }
                                        }

                                        _position = testParen;
                                        if (!TryParseAtom(depth + 1, out testBody) || testBody is null)
                                        {
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        var innerStart = testParen + 1;
                                        if (innerStart < _pattern.Length && _pattern[innerStart] == ')')
                                        {
                                            testBody = new Utf8SliceNode.EmptyNode();
                                            _position = innerStart + 1;
                                        }
                                        else
                                        {
                                            _position = innerStart;
                                            if (!TryParseAlternation(depth + 1, out testBody) || testBody is null || AtEnd || _pattern[_position] != ')')
                                            {
                                                return false;
                                            }

                                            _position++;
                                        }
                                    }
                                }
                            }
                            var savedExprDepth = _expressionBranchDepth;
                            if (testBody is not null)
                            {
                                _expressionBranchDepth = depth + 1;
                            }

                            var yesChildren = new List<Utf8SliceNode>();
                            if (!TryParseConcatenation(depth + 1, yesChildren))
                            {
                                return false;
                            }
                            var yesBranch = yesChildren.Count == 0 ? (Utf8SliceNode)new Utf8SliceNode.EmptyNode() : yesChildren.Count == 1 ? yesChildren[0] : new Utf8SliceNode.AlternationNode(new[] { yesChildren.ToArray() });
                            var noBranch = (Utf8SliceNode)new Utf8SliceNode.EmptyNode();
                            if (!AtEnd && _pattern[_position] == '|')
                            {
                                _position++;
                                var noChildren = new List<Utf8SliceNode>();
                                if (!TryParseConcatenation(depth + 1, noChildren))
                                {
                                    return false;
                                }
                                noBranch = noChildren.Count == 0 ? (Utf8SliceNode)new Utf8SliceNode.EmptyNode() : noChildren.Count == 1 ? noChildren[0] : new Utf8SliceNode.AlternationNode(new[] { noChildren.ToArray() });
                                if (!AtEnd && _pattern[_position] == '|')
                                {
                                    return false;
                                }
                            }
                            if (AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }
                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedConditional;
                            _expressionBranchDepth = savedExprDepth;
                            if (testBody is not null)
                            {
                                // Expression tests stay on the node (desugared at lowering so
                                // consumers can distinguish assertion capture flow); group tests
                                // carry a null test body.
                                node = new Utf8SliceNode.ConditionalNode(-1, yesBranch, noBranch, testBody);
                            }
                            else
                            {
                                node = new Utf8SliceNode.ConditionalNode(testSlot, yesBranch, noBranch, null);
                            }
                            return true;
                        }

                        if (marker == '#')
                        {
                            // Inline comments run to the next closing paren;
                            // an unterminated comment declines so legacy
                            // routing preserves the construction failure.
                            var commentEnd = _pattern.IndexOf(')', _position + 3);
                            if (commentEnd < 0)
                            {
                                return false;
                            }

                            _position = commentEnd + 1;
                            if (!AtEnd && (_pattern[_position] == '*' || _pattern[_position] == '+' || _pattern[_position] == '?' || _pattern[_position] == '{'))
                            {
                                return false;
                            }

                            node = new Utf8SliceNode.EmptyNode();
                            return true;
                        }

                        if (marker == '<' || marker == '\'')
                        {
                            // Named captures bind like numbered captures;
                            // BCL numbers unnamed groups left-to-right first
                            // and named groups after, so temporary slots
                            // renumber once the full parse lands (see
                            // TryParse). Names are ASCII letters, digits,
                            // and underscores with a non-digit start;
                            // all-digit spellings (explicit numbers),
                            // digit starts, empty names, mismatched closers,
                            // duplicates, and balancing subtraction all
                            // decline so legacy routing preserves exact BCL
                            // behavior including construction failures.
                            var closer = marker == '<' ? '>' : '\'';
                            var scan = _position + 3;
                            var nameScanEnd = scan;
                            while (nameScanEnd < _pattern.Length)
                            {
                                var scanChar = _pattern[nameScanEnd];
                                if ((scanChar >= 'a' && scanChar <= 'z') ||
                                    (scanChar >= 'A' && scanChar <= 'Z') ||
                                    (scanChar >= '0' && scanChar <= '9') ||
                                    scanChar == '_')
                                {
                                    nameScanEnd++;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if ((nameScanEnd > scan || (scan < _pattern.Length && _pattern[scan] == '-')) &&
                                nameScanEnd < _pattern.Length &&
                                _pattern[nameScanEnd] == '-')
                            {
                                // Balancing `(?<n1-n2>`, `(?<-n2>` (and
                                // '-quoted forms): pop n2 (fail if empty),
                                // optionally capturing [popped end,
                                // pop-time position) into n1. n1 follows
                                // named rules and defines a group; n2 must
                                // be backward-defined; forward or undefined
                                // targets, duplicates, digit starts,
                                // mismatched closers, and self-pops decline
                                // so legacy routing preserves exact BCL
                                // behavior including construction failures.
                                string? captureName = nameScanEnd > scan
                                    ? _pattern.Substring(scan, nameScanEnd - scan)
                                    : null;
                                if (captureName is not null &&
                                    (captureName[0] >= '0' && captureName[0] <= '9'))
                                {
                                    return false;
                                }

                                var popStart = nameScanEnd + 1;
                                var popEnd = popStart;
                                while (popEnd < _pattern.Length)
                                {
                                    var popChar = _pattern[popEnd];
                                    if ((popChar >= 'a' && popChar <= 'z') ||
                                        (popChar >= 'A' && popChar <= 'Z') ||
                                        (popChar >= '0' && popChar <= '9') ||
                                        popChar == '_')
                                    {
                                        popEnd++;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                if (popEnd == popStart ||
                                    _pattern[popStart] >= '0' && _pattern[popStart] <= '9' ||
                                    popEnd >= _pattern.Length ||
                                    _pattern[popEnd] != closer)
                                {
                                    return false;
                                }

                                var popName = _pattern.Substring(popStart, popEnd - popStart);
                                if (captureName == popName)
                                {
                                    return false;
                                }

                                if (!_groupSlots.TryGetValue(popName, out var popTemp))
                                {
                                    return false;
                                }

                                var captureTemp = -1;
                                if (captureName is not null)
                                {
                                    captureTemp = _groups++;
                                    if (!_groupSlots.TryAdd(captureName, captureTemp))
                                    {
                                        return false;
                                    }

                                    _groupDecls.Add((captureTemp, captureName));
                                }

                                _position = popEnd + 1;
                                var savedBalance = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                                if (!TryParseAlternation(depth + 1, out var balanceBody) || AtEnd || _pattern[_position] != ')')
                                {
                                    return false;
                                }

                                _position++;
                                (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedBalance;
                                node = new Utf8SliceNode.BalanceNode(popTemp, captureTemp, balanceBody ?? new Utf8SliceNode.EmptyNode());
                                _sawBalancing = true;
                                if (_lookbehindDepth > 0)
                                {
                                    _sawLookbehindGroupWrite = true;
                                }
                                return true;
                            }

                            var nameStart = _position + 3;
                            var nameEnd = nameStart;
                            while (nameEnd < _pattern.Length)
                            {
                                var nameChar = _pattern[nameEnd];
                                if ((nameChar >= 'a' && nameChar <= 'z') ||
                                    (nameChar >= 'A' && nameChar <= 'Z') ||
                                    (nameChar >= '0' && nameChar <= '9') ||
                                    nameChar == '_')
                                {
                                    nameEnd++;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (nameEnd == nameStart ||
                                nameEnd >= _pattern.Length ||
                                _pattern[nameEnd] != closer)
                            {
                                return false;
                            }

                            if (_pattern[nameStart] >= '0' && _pattern[nameStart] <= '9')
                            {
                                return false;
                            }

                            var name = _pattern.Substring(nameStart, nameEnd - nameStart);
                            _position = nameEnd + 1;
                            var namedSlot = _groups++;
                            if (!_groupSlots.TryAdd(name, namedSlot))
                            {
                                return false;
                            }

                            _groupDecls.Add((namedSlot, name));
                            if (_lookbehindDepth > 0)
                            {
                                _sawLookbehindGroupWrite = true;
                            }
                            var savedNamed = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            if (!TryParseAlternation(depth + 1, out var namedBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedNamed;
                            node = new Utf8SliceNode.CaptureNode(namedSlot, namedBody ?? new Utf8SliceNode.EmptyNode());
                            return true;
                        }
                    }

                    _position++;
                    if (_explicitCapture)
                    {
                        // ExplicitCapture leaves unnamed groups unnumbered, exactly
                        // like non-capturing groups (backreference digits still denote
                        // final numbers, so unbound digits decline as usual).
                        var savedExplicitBody = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                        if (!TryParseAlternation(depth + 1, out var plainBody) || AtEnd || _pattern[_position] != ')')
                        {
                            return false;
                        }

                        _position++;
                        (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedExplicitBody;
                        node = plainBody ?? new Utf8SliceNode.EmptyNode();
                        return true;
                    }

                    var slot = _groups++;
                    _groupDecls.Add((slot, null));
                    if (_lookbehindDepth > 0)
                    {
                        _sawLookbehindGroupWrite = true;
                    }
                    var savedPlain = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                    if (!TryParseAlternation(depth + 1, out var body) || AtEnd || _pattern[_position] != ')')
                    {
                        return false;
                    }

                    _position++;
                    (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedPlain;
                    node = new Utf8SliceNode.CaptureNode(slot, body ?? new Utf8SliceNode.EmptyNode());
                    return true;
                case '\\':
                    if (_position + 1 >= _pattern.Length)
                    {
                        return false;
                    }

                    var escaped = _pattern[_position + 1];
                    if (escaped == 'b' || escaped == 'B')
                    {
                        _position += 2;
                        node = new Utf8SliceNode.BoundaryNode(escaped == 'b');
                        return true;
                    }

                    if (escaped == 'A')
                    {
                        _position += 2;
                        node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Beginning, false);
                        return true;
                    }

                    if (escaped == 'Z')
                    {
                        _position += 2;
                        node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.End, false);
                        return true;
                    }

                    if (escaped == 'z')
                    {
                        _position += 2;
                        node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.VeryEnd, false);
                        return true;
                    }

                    if (escaped == 'G')
                    {
                        // Contiguous matches pin to the search start (BCL startat).
                        _position += 2;
                        node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Contiguous, false);
                        return true;
                    }

                    Utf8SliceShorthand? shorthand = escaped switch
                    {
                        'd' => Utf8SliceShorthand.Digit,
                        'D' => Utf8SliceShorthand.NonDigit,
                        'w' => Utf8SliceShorthand.Word,
                        'W' => Utf8SliceShorthand.NonWord,
                        's' => Utf8SliceShorthand.Space,
                        'S' => Utf8SliceShorthand.NonSpace,
                        _ => null,
                    };
                    if (shorthand is not null)
                    {
                        _position += 2;
                        node = new Utf8SliceNode.ShorthandNode(shorthand.Value);
                        return true;
                    }

                    if (escaped == 'p' || escaped == 'P')
                    {
                        // Unicode property escapes lower to owned category
                        // atoms (BCL general categories/groups plus named
                        // blocks); malformed or unknown names decline so
                        // legacy construction throws BCL-exact errors, and
                        // blocks under IgnoreCase decline since
                        // case-equivalent range expansion lives in legacy.
                        if (!TryParseCategoryAtom(_pattern, _position, escaped == 'P', _matchIgnoreCase, out node, out var categoryNext))
                        {
                            return false;
                        }

                        _position = categoryNext;
                        _sawFold |= _matchIgnoreCase;
                        return true;
                    }

                    // Single-digit backreferences bind earlier groups only:
                    // a following digit makes the escape multi-character
                    // (octal or two-digit reference in BCL), and undefined
                    // numbers stay legacy-exact (BCL throws or octal-matches).
                    if (escaped >= '1' && escaped <= '9' &&
                        (_position + 2 >= _pattern.Length || !char.IsAsciiDigit(_pattern[_position + 2])) &&
                        escaped - '0' <= _groups)
                    {
                        _position += 2;
                        _sawFold |= _matchIgnoreCase;
                        node = new Utf8SliceNode.BackrefNode(escaped - '0' - 1, _matchIgnoreCase);
                        return true;
                    }

                    // Hex, unicode, and NUL-led octal escapes with ASCII results
                    // lower to literals; malformed or non-ASCII results decline to
                    // legacy-exact routing (BCL throws on the former).
                    if (escaped == 'x' || escaped == 'u' || escaped == '0')
                    {
                        if (!TryParseAsciiNumericEscape(_pattern, _position + 1, false, out var number, out var next))
                        {
                            return false;
                        }

                        _position = next;
                        _sawFold |= _matchIgnoreCase;
                        node = new Utf8SliceNode.LiteralNode((byte)number, _matchIgnoreCase);
                        return true;
                    }

                    if (!IsEscapablePunctuation(escaped))
                    {
                        return false;
                    }

                    _position += 2;
                    _sawFold |= _matchIgnoreCase;
                    node = new Utf8SliceNode.LiteralNode((byte)escaped, _matchIgnoreCase);
                    return true;
                case ')':
                case '|':
                case '*':
                case '+':
                case '?':
                case '{':
                case '}':
                case ']':
                    return false;
                case '[':
                    return TryParseClass(out node);
                case '^':
                    _position++;
                    node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Beginning, _matchMultiline);
                    return true;
                case '$':
                    _position++;
                    node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.End, _matchMultiline);
                    return true;
                case '.':
                    _position++;
                    node = new Utf8SliceNode.DotNode(_matchNewline);
                    return true;
                default:
                    _position++;
                    _sawFold |= _matchIgnoreCase;
                    node = new Utf8SliceNode.LiteralNode((byte)character, _matchIgnoreCase);
                    return true;
            }
        }

        private static bool IsEscapablePunctuation(char character)
        {
            return character is '.' or '$' or '^' or '{' or '}' or '[' or ']' or '(' or ')' or '|' or '*' or '+' or '?' or '\\';
        }

        // ASCII character classes, mirroring the pinned runtime parser:
        // literals (with `]`-first and `-`/POSIX-edge rules), ascending
        // ranges (escapes allowed as bounds), supported single-char
        // escapes, and shorthand members (always members, never bounds).
        // Anything Unicode, categorical, or structural (categories,
        // hex/octal/control escapes, subtraction, reversed ranges,
        // shorthand upper bounds) declines to null so legacy routing
        // preserves exact behavior including construction failures.
        // `[` followed by `:`, `.`, or `=` also
        // declines: those open POSIX, collating, or equivalence classes in
        // sibling flavors while BCL reads plain literals, and a shared
        // program cannot serve both readings.
        private bool TryParseClass(out Utf8SliceNode? node)
        {
            node = null;
            _position++;

            // Single-shorthand classes fold to the shorthand atom they denote.
            var shorthandOffset = 0;
            if (_position + shorthandOffset < _pattern.Length && _pattern[_position + shorthandOffset] == '^')
            {
                shorthandOffset++;
            }

            if (_position + shorthandOffset + 2 < _pattern.Length &&
                _pattern[_position + shorthandOffset] == '\\' &&
                _pattern[_position + shorthandOffset + 2] == ']')
            {
                Utf8SliceShorthand? shorthand = _pattern[_position + shorthandOffset + 1] switch
                {
                    'd' => Utf8SliceShorthand.Digit,
                    'D' => Utf8SliceShorthand.NonDigit,
                    'w' => Utf8SliceShorthand.Word,
                    'W' => Utf8SliceShorthand.NonWord,
                    's' => Utf8SliceShorthand.Space,
                    'S' => Utf8SliceShorthand.NonSpace,
                    _ => (Utf8SliceShorthand?)null,
                };
                if (shorthand is not null)
                {
                    if (shorthandOffset == 1)
                    {
                        shorthand = (Utf8SliceShorthand)((int)shorthand.Value ^ 1);
                    }

                    _position += shorthandOffset + 3;
                    node = new Utf8SliceNode.ShorthandNode(shorthand.Value);
                    return true;
                }
            }

            // Single-category classes fold to the category atom they denote;
            // anything else naming a category stays legacy-exact (mixed
            // members would need hybrid category operands, and BCL reads a
            // category in bound position as a construction throw).
            var categoryOffset = 0;
            if (_position + categoryOffset < _pattern.Length && _pattern[_position + categoryOffset] == '^')
            {
                categoryOffset++;
            }

            if (_position + categoryOffset + 3 < _pattern.Length &&
                _pattern[_position + categoryOffset] == '\\' &&
                (_pattern[_position + categoryOffset + 1] == 'p' || _pattern[_position + categoryOffset + 1] == 'P') &&
                _pattern[_position + categoryOffset + 2] == '{' &&
                TryParseCategoryAtom(_pattern, _position + categoryOffset, (categoryOffset == 1) != (_pattern[_position + categoryOffset + 1] == 'P'), _matchIgnoreCase, out var categoryNode, out var categoryNext) &&
                categoryNext < _pattern.Length && _pattern[categoryNext] == ']')
            {
                _position = categoryNext + 1;
                node = categoryNode;
                return true;
            }

            var negated = false;
            if (!AtEnd && _pattern[_position] == '^')
            {
                negated = true;
                _position++;
            }

            var bits = new bool[128];
            var memberBits = 0;
            var categoryMembers = new List<Utf8SliceNode.CategorySetMember>();
            var lowerBound = -1;
            var inRange = false;
            var firstChar = true;
            var closed = false;
            for (; !AtEnd; firstChar = false)
            {
                var translatedChar = false;
                var character = _pattern[_position];
                _position++;
                if (character == ']' && !firstChar)
                {
                    closed = true;
                    break;
                }

                int value;
                if (character == '\\' && !AtEnd)
                {
                    var escaped = _pattern[_position];
                    if (escaped is 'd' or 'D' or 's' or 'S' or 'w' or 'W')
                    {
                        // Mixed-class shorthand members (BCL uniform rule):
                        // a shorthand is always a member, never a bound; a
                        // following dash stays literal through the existing
                        // dash logic below. In upper-bound position (after
                        // `X-`) BCL throws, so decline to legacy-exact.
                        if (inRange)
                        {
                            return false;
                        }

                        Utf8SliceShorthand shorthand = escaped switch
                        {
                            'd' => Utf8SliceShorthand.Digit,
                            'D' => Utf8SliceShorthand.NonDigit,
                            'w' => Utf8SliceShorthand.Word,
                            'W' => Utf8SliceShorthand.NonWord,
                            's' => Utf8SliceShorthand.Space,
                            _ => Utf8SliceShorthand.NonSpace,
                        };
                        memberBits |= 1 << (int)shorthand;
                        switch (escaped)
                        {
                            case 'd':
                                for (var digit = '0'; digit <= '9'; digit++)
                                {
                                    bits[digit] = true;
                                }

                                break;
                            case 'D':
                                for (var nonDigit = 0; nonDigit < 128; nonDigit++)
                                {
                                    if (nonDigit < '0' || nonDigit > '9')
                                    {
                                        bits[nonDigit] = true;
                                    }
                                }

                                break;
                            case 'w':
                                for (var word = 0; word < 128; word++)
                                {
                                    if ((word >= '0' && word <= '9') || (word >= 'A' && word <= 'Z') || word == '_' || (word >= 'a' && word <= 'z'))
                                    {
                                        bits[word] = true;
                                    }
                                }

                                break;
                            case 'W':
                                for (var nonWord = 0; nonWord < 128; nonWord++)
                                {
                                    if ((nonWord < '0' || nonWord > '9') && (nonWord < 'A' || nonWord > 'Z') && nonWord != '_' && (nonWord < 'a' || nonWord > 'z'))
                                    {
                                        bits[nonWord] = true;
                                    }
                                }

                                break;
                            case 's':
                                for (var space = 0x09; space <= 0x0D; space++)
                                {
                                    bits[space] = true;
                                }

                                bits[0x20] = true;
                                break;
                            default:
                                for (var nonSpace = 0; nonSpace < 128; nonSpace++)
                                {
                                    if ((nonSpace < 0x09 || nonSpace > 0x0D) && nonSpace != 0x20)
                                    {
                                        bits[nonSpace] = true;
                                    }
                                }

                                break;
                        }

                        _position++;
                        continue;
                    }

                    if (escaped is 'p' or 'P')
                    {
                        // Categories are always members, never bounds (BCL
                        // uniform rule, mirroring shorthands); in
                        // upper-bound position BCL throws, so decline to
                        // legacy-exact. The shared helper also declines
                        // blocks under IgnoreCase.
                        if (inRange)
                        {
                            return false;
                        }

                        if (!TryParseCategoryAtom(_pattern, _position - 1, escaped == 'P', _matchIgnoreCase, out var categoryAtom, out var categoryMemberNext) ||
                            categoryAtom is not Utf8SliceNode.CategoryNode categoryMember)
                        {
                            return false;
                        }

                        categoryMembers.Add(new Utf8SliceNode.CategorySetMember(
                            new Utf8SliceCategoryTable(categoryMember.Mask, categoryMember.Ranges),
                            escaped == 'P'));
                        _position = categoryMemberNext;
                        continue;
                    }

                    if (escaped == '-')
                    {
                        _position++;
                        if (inRange)
                        {
                            if (lowerBound > '-')
                            {
                                return false;
                            }

                            for (var member = lowerBound; member <= '-'; member++)
                            {
                                bits[member] = true;
                            }

                            inRange = false;
                            lowerBound = -1;
                        }
                        else
                        {
                            bits['-'] = true;
                        }

                        continue;
                    }

                    switch (escaped)
                    {
                        case 'a':
                            value = 7;
                            break;
                        case 'b':
                            value = 8;
                            break;
                        case 'e':
                            value = 27;
                            break;
                        case 'f':
                            value = 12;
                            break;
                        case 'n':
                            value = 10;
                            break;
                        case 'r':
                            value = 13;
                            break;
                        case 't':
                            value = 9;
                            break;
                        case 'v':
                            value = 11;
                            break;
                        case 'x':
                        case 'u':
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                            // Hex, unicode, and octal escapes (bare octal included)
                            // resolve here; the value flows into member/range logic
                            // below. Anything unusable declines to legacy-exact.
                            if (!TryParseAsciiNumericEscape(_pattern, _position, true, out value, out var next))
                            {
                                return false;
                            }

                            // The shared tail advances past one escape char, so step
                            // back to land after multi-char escapes.
                            _position = next - 1;
                            break;
                        default:
                            if ((escaped >= 'a' && escaped <= 'z') || (escaped >= 'A' && escaped <= 'Z') || (escaped >= '0' && escaped <= '9'))
                            {
                                return false;
                            }

                            if (escaped > 127)
                            {
                                return false;
                            }

                            value = escaped;
                            break;
                    }

                    _position++;
                    translatedChar = true;
                }
                else
                {
                    if (character > 127)
                    {
                        return false;
                    }

                    value = character;
                }

                if (inRange)
                {
                    inRange = false;
                    if (value == '[' && !translatedChar && !firstChar)
                    {
                        return false;
                    }

                    if (lowerBound > value)
                    {
                        return false;
                    }

                    for (var member = lowerBound; member <= value; member++)
                    {
                        bits[member] = true;
                    }

                    lowerBound = -1;
                }
                else if (_position + 1 < _pattern.Length && _pattern[_position] == '-' && _pattern[_position + 1] != ']')
                {
                    lowerBound = value;
                    inRange = true;
                    _position++;
                }
                else if (value == '-' && !translatedChar && !AtEnd && _pattern[_position] == '[' && !firstChar)
                {
                    return false;
                }
                else
                {
                    if (value == '[' && !translatedChar && !AtEnd &&
                        (_pattern[_position] == ':' || _pattern[_position] == '.' || _pattern[_position] == '='))
                    {
                        return false;
                    }

                    bits[value] = true;
                }
            }

            if (!closed)
            {
                return false;
            }

            // Case-insensitive classes carry both cases (input scalars fold
            // before the mask test, so folded members must be present too).
            if (_matchIgnoreCase)
            {
                _sawFold = true;
                for (var member = 0; member < 128; member++)
                {
                    if (bits[member] && ((member >= 65 && member <= 90) || (member >= 97 && member <= 122)))
                    {
                        bits[member ^ 32] = true;
                    }
                }
            }

            var mask = new byte[16];
            for (var member = 0; member < 128; member++)
            {
                if (bits[member])
                {
                    mask[member >> 3] |= (byte)(1 << (member & 7));
                }
            }

            Utf8SliceShorthand[] members;
            if (memberBits == 0)
            {
                members = [];
            }
            else
            {
                var collected = new Utf8SliceShorthand[6];
                var collectedCount = 0;
                for (var bit = 0; bit < 6; bit++)
                {
                    if ((memberBits & (1 << bit)) != 0)
                    {
                        collected[collectedCount++] = (Utf8SliceShorthand)bit;
                    }
                }

                members = collected[..collectedCount];
            }

            if (categoryMembers.Count == 0)
            {
                node = new Utf8SliceNode.SetNode(mask, negated, members, _matchIgnoreCase);
            }
            else
            {
                node = new Utf8SliceNode.CategorySetNode(mask, negated, members, categoryMembers.ToArray(), _matchIgnoreCase);
            }

            return true;
        }

        private bool AtEnd => _position >= _pattern.Length;
    }
}
