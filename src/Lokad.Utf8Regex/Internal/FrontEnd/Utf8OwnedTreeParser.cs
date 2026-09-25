using System.Collections;
using System.Collections;
using System.Globalization;
using Lokad.Utf8Regex.Internal.Execution;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned parser strangler: literals, escapes, concatenation, alternation,
// numbered groups, named groups, backreferences, balance pops, octal escapes,
// quantifiers, dot, anchors, character classes, predefined shorthands,
// Unicode properties, scoped option groups, and comments lower to runtime
// tree nodes.
// Anything else declines (returns null) so the provider keeps exact behavior,
// including construction throws. Output nodes intentionally mirror the runtime
// tree shapes the differential battery pins; later stages widen the subset and
// shrink the provider caller. Recursion is depth-capped and brace numbers
// saturate at 32 bits; over-deep or overflowing shapes decline to legacy
// routing. Parent links are wired exactly like children. Dot declines under
// Singleline and ^/$ decline under Multiline, where the provider shape
// demonstrably differs; the differential battery pins the stable shapes.
// Parsing lives in static locals over an explicit state record, so the debt
// ceiling sees a single entry point.
internal static class Utf8OwnedTreeParser
{
    private const int MaxNestingDepth = 64;
    private const int Infinite = 2147483647;

    // Provider dot payloads probed per Singleline shape; set nodes never carry IgnoreCase.
    private const string DotText = "\u0001\u0002\u0000\u000a\u000b";
    private const string SinglelineDotText = "\u0000\u0001\u0000\u0000";

    // Probe-observed predefined shorthand payloads, verbatim: the six ASCII
    // shorthands land as category payloads, including the complement and
    // framing quirks the differential battery pins.
    private const string ShorthandDigitCats = "\u0009";
    private const string ShorthandNotDigitCats = "\uFFF7";
    private const string ShorthandWordCats = "\u0000\u0002\u0004\u0005\u0003\u0001\u0006\u0009\u0013\u0000";
    private const string ShorthandNotWordCats = "\u0000\uFFFE\uFFFC\uFFFB\uFFFD\uFFFF\uFFFA\uFFF7\uFFED\u0000";
    private const string ShorthandSpaceCats = "d";
    private const string ShorthandNotSpaceCats = "\uFF9C";

    public static RuntimeFrontEnd.RegexTree? TryParse(string pattern, RegexOptions options)
    {
        if ((options & RegexOptions.ExplicitCapture) != 0)
        {
            return null;
        }

        var state = new ParserState(pattern, options);
        if (!ParseAlternation(ref state, 0, out var root) || root is null || state.Position != pattern.Length)
        {
            return null;
        }

        if (!FinishNumbering(
            ref state,
            ref root,
            out var captureCount,
            out var captureNames,
            out var captureNameToNumber,
            out var captureSparseMapping))
        {
            return null;
        }

        var treeOptions = options | state.TreeSeen;
        var tree = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Capture, treeOptions & ~RegexOptions.IgnoreCase);
        tree.M = 0;
        tree.N = -1;
        tree.Children = root;
        root.Parent = tree;
        return new RuntimeFrontEnd.RegexTree(
            tree,
            captureCount,
            captureNames,
            captureNameToNumber,
            captureSparseMapping,
            treeOptions,
            RuntimeFrontEnd.RegexParser.GetTargetCulture(options),
            null);

        static bool ParseAlternation(ref ParserState state, int depth, out RuntimeFrontEnd.RegexNode? root)
        {
            root = null;
            if (depth > MaxNestingDepth)
            {
                return false;
            }

            var branches = new List<RuntimeFrontEnd.RegexNode>();
            while (true)
            {
                if (!ParseConcatenation(ref state, depth, out var branch) || branch is null)
                {
                    return false;
                }

                branches.Add(branch);
                if (state.AtEnd || state.Pattern[state.Position] != '|')
                {
                    break;
                }

                state.Position++;
            }

            root = branches.Count == 1 ? branches[0] : Alternate(state, branches);
            return true;
        }

        static bool ParseConcatenation(ref ParserState state, int depth, out RuntimeFrontEnd.RegexNode? node)
        {
            node = null;
            var children = new List<RuntimeFrontEnd.RegexNode>();
            var run = new StringBuilder();
            while (!state.AtEnd)
            {
                var character = state.Pattern[state.Position];
                if (character == '|' || character == ')')
                {
                    break;
                }

                if (character == '\\')
                {
                    if (TryAnchorEscape(ref state, out var anchor) && anchor is not null)
                    {
                        FlushRun(state, children, run);
                        var anchorAtom = anchor;
                        if (!ParseQuantifier(ref state, ref anchorAtom))
                        {
                            return false;
                        }

                        children.Add(anchorAtom);
                        continue;
                    }

                    if (TryTakeShorthandAtom(ref state, out var shorthand) && shorthand is not null)
                    {
                        FlushRun(state, children, run);
                        var shorthandAtom = shorthand;
                        if (!ParseQuantifier(ref state, ref shorthandAtom))
                        {
                            return false;
                        }

                        children.Add(shorthandAtom);
                        continue;
                    }

                    // Numeric backreferences stay out of IgnoreCase: a later octal
                    // outcome would carry unfolded options, and the provider folds.
                    if ((state.Options & RegexOptions.IgnoreCase) != 0)
                    {
                        return false;
                    }

                    var backrefDigits = TryScanBackrefDigits(ref state);
                    if (backrefDigits is not null)
                    {
                        FlushRun(state, children, run);
                        var tentative = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Backreference, state.Options);
                        tentative.N = -1;
                        var backrefAtom = tentative;
                        if (!ParseQuantifier(ref state, ref backrefAtom))
                        {
                            return false;
                        }

                        if (backrefAtom.Kind == RuntimeFrontEnd.RegexNodeKind.Empty)
                        {
                            return false;
                        }

                        if (ReferenceEquals(backrefAtom, tentative))
                        {
                            state.BackrefFixups.Add(new OwnedBackrefFixup { Node = tentative, Digits = backrefDigits });
                        }
                        else if (backrefAtom.Kind is RuntimeFrontEnd.RegexNodeKind.Loop or RuntimeFrontEnd.RegexNodeKind.Lazyloop &&
                            backrefAtom.ChildCount == 1 &&
                            ReferenceEquals(backrefAtom.Child(0), tentative))
                        {
                            state.BackrefFixups.Add(new OwnedBackrefFixup { Node = tentative, Digits = backrefDigits, Wrapper = backrefAtom });
                        }

                        children.Add(backrefAtom);
                        continue;
                    }

                    var backrefName = TryScanNamedBackref(ref state);
                    if (backrefName is not null)
                    {
                        FlushRun(state, children, run);
                        var namedBackref = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Backreference, state.Options);
                        namedBackref.Str = backrefName;
                        namedBackref.N = 0;
                        var namedAtom = namedBackref;
                        if (!ParseQuantifier(ref state, ref namedAtom))
                        {
                            return false;
                        }

                        if (namedAtom.Kind == RuntimeFrontEnd.RegexNodeKind.Empty)
                        {
                            return false;
                        }

                        state.BackrefFixups.Add(new OwnedBackrefFixup { Node = namedBackref, Name = backrefName });
                        children.Add(namedAtom);
                        continue;
                    }

                    if ((state.Options & RegexOptions.IgnoreCase) != 0)
                    {
                        return false;
                    }

                    if (!DecodeEscape(ref state, out var escaped))
                    {
                        return false;
                    }

                    run.Append(escaped);
                    continue;
                }

                if (IsLiteralChar(character))
                {
                    if ((state.Options & RegexOptions.IgnoreCase) != 0)
                    {
                        return false;
                    }

                    if ((state.Options & RegexOptions.IgnorePatternWhitespace) != 0 &&
                        (character == '#' || char.IsWhiteSpace(character)))
                    {
                        return false;
                    }

                    run.Append(character);
                    state.Position++;
                    continue;
                }

                if (IsQuantifierStart(character) && run.Length != 0)
                {
                    var last = run[run.Length - 1];
                    run.Length--;
                    FlushRun(state, children, run);
                    var unit = One(state, last);
                    if (!ParseQuantifier(ref state, ref unit))
                    {
                        return false;
                    }

                    children.Add(unit);
                    continue;
                }

                if (character == '(' && IsCommentAhead(state))
                {
                    if (!SkipComment(ref state))
                    {
                        return false;
                    }

                    continue;
                }

                FlushRun(state, children, run);
                RuntimeFrontEnd.RegexNode atom;
                if (character == '^')
                {
                    var anchorKind = (state.Options & RegexOptions.Multiline) == 0 ?
                        RuntimeFrontEnd.RegexNodeKind.Beginning :
                        RuntimeFrontEnd.RegexNodeKind.Bol;
                    atom = new RuntimeFrontEnd.RegexNode(anchorKind, state.Options);
                    state.Position++;
                }
                else if (character == '$')
                {
                    var anchorKind = (state.Options & RegexOptions.Multiline) == 0 ?
                        RuntimeFrontEnd.RegexNodeKind.EndZ :
                        RuntimeFrontEnd.RegexNodeKind.Eol;
                    atom = new RuntimeFrontEnd.RegexNode(anchorKind, state.Options);
                    state.Position++;
                }
                else if (character == '.' && (state.Options & RegexOptions.Singleline) == 0)
                {
                    atom = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Set, state.Options & ~RegexOptions.IgnoreCase, DotText);
                    state.Position++;
                }
                else if (character == '.')
                {
                    atom = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Set, state.Options & ~RegexOptions.IgnoreCase, SinglelineDotText);
                    state.Position++;
                }
                else if (character == '[' && (state.Options & RegexOptions.IgnoreCase) == 0)
                {
                    if (!ParseClass(ref state, out var classNode) || classNode is null)
                    {
                        return false;
                    }

                    atom = classNode;
                }
                else if (character != '(')
                {
                    return false;
                }
                else
                {
                    if (!ParseGroup(ref state, depth, out var group) || group is null)
                    {
                        return false;
                    }

                    atom = group;
                }

                if (!ParseQuantifier(ref state, ref atom))
                {
                    return false;
                }

                children.Add(atom);
            }

            FlushRun(state, children, run);
            if (children.Count == 0)
            {
                node = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, state.Options);
                return true;
            }

            node = children.Count == 1 ? children[0] : Concatenate(state, children);
            return true;
        }

        static void FlushRun(ParserState state, List<RuntimeFrontEnd.RegexNode> children, StringBuilder run)
        {
            if (run.Length == 0)
            {
                return;
            }

            children.Add(run.Length == 1 ? One(state, run[0]) : Multi(state, run.ToString()));
            run.Clear();
        }

        static bool DecodeEscape(ref ParserState state, out char value)
        {
            value = '\0';
            state.Position++;
            if (state.AtEnd)
            {
                return false;
            }

            var escaped = state.Pattern[state.Position];
            switch (escaped)
            {
                case 'a':
                    value = '\a';
                    state.Position++;
                    return true;
                case 'e':
                    value = (char)27;
                    state.Position++;
                    return true;
                case 'f':
                    value = '\f';
                    state.Position++;
                    return true;
                case 'n':
                    value = '\n';
                    state.Position++;
                    return true;
                case 'r':
                    value = '\r';
                    state.Position++;
                    return true;
                case 't':
                    value = '\t';
                    state.Position++;
                    return true;
                case 'v':
                    value = '\v';
                    state.Position++;
                    return true;
                case 'c':
                    state.Position++;
                    if (state.AtEnd)
                    {
                        return false;
                    }

                    var letter = state.Pattern[state.Position];
                    if (letter >= 'a' && letter <= 'z')
                    {
                        letter = (char)(letter - ('a' - 'A'));
                    }

                    if (letter < '@' || letter > '_')
                    {
                        return false;
                    }

                    value = (char)(letter - '@');
                    state.Position++;
                    return true;
                case 'x':
                    state.Position++;
                    if (!TakeHex(ref state, 2, out var hex))
                    {
                        return false;
                    }

                    value = (char)hex;
                    return true;
                case 'u':
                    state.Position++;
                    if (!TakeHex(ref state, 4, out var unicode))
                    {
                        return false;
                    }

                    value = (char)unicode;
                    return true;
                case '0':
                    // Octal: backslash-zero plus up to two more octal digits.
                    // A non-octal follower stays literal.
                    var octal = 0;
                    var octalDigits = 0;
                    while (octalDigits < 2 &&
                        state.Position + 1 < state.Pattern.Length &&
                        state.Pattern[state.Position + 1] >= '0' &&
                        state.Pattern[state.Position + 1] <= '7')
                    {
                        state.Position++;
                        octal = octal * 8 + (state.Pattern[state.Position] - '0');
                        octalDigits++;
                    }

                    value = (char)octal;
                    state.Position++;
                    return true;
                case '.':
                case '$':
                case '^':
                case '{':
                case '}':
                case '[':
                case ']':
                case '(':
                case ')':
                case '|':
                case '*':
                case '+':
                case '?':
                case '\\':
                    value = escaped;
                    state.Position++;
                    return true;
                default:
                    // Other characters spell themselves; only ASCII letters
                    // and digits without an escape meaning keep declining.
                    if ((escaped >= 'a' && escaped <= 'z') || (escaped >= 'A' && escaped <= 'Z') || (escaped >= '0' && escaped <= '9'))
                    {
                        return false;
                    }

                    value = escaped;
                    state.Position++;
                    return true;
            }
        }

        static bool TryAnchorEscape(ref ParserState state, out RuntimeFrontEnd.RegexNode? anchor)
        {
            anchor = null;
            if (state.Position + 1 >= state.Pattern.Length)
            {
                return false;
            }

            RuntimeFrontEnd.RegexNodeKind kind;
            switch (state.Pattern[state.Position + 1])
            {
                case 'b':
                    kind = RuntimeFrontEnd.RegexNodeKind.Boundary;
                    break;
                case 'B':
                    kind = RuntimeFrontEnd.RegexNodeKind.NonBoundary;
                    break;
                case 'A':
                    kind = RuntimeFrontEnd.RegexNodeKind.Beginning;
                    break;
                case 'G':
                    kind = RuntimeFrontEnd.RegexNodeKind.Start;
                    break;
                case 'Z':
                    kind = RuntimeFrontEnd.RegexNodeKind.EndZ;
                    break;
                case 'z':
                    kind = RuntimeFrontEnd.RegexNodeKind.End;
                    break;
                default:
                    return false;
            }

            anchor = new RuntimeFrontEnd.RegexNode(kind, state.Options);
            state.Position += 2;
            return true;
        }

        static bool ParseClass(ref ParserState state, out RuntimeFrontEnd.RegexNode? node)
        {
            node = null;
            state.Position++;
            if (state.AtEnd)
            {
                return false;
            }
            var negated = false;
            if (state.Pattern[state.Position] == '^')
            {
                negated = true;
                state.Position++;
                if (state.AtEnd)
                {
                    return false;
                }
            }
            var units = new List<char>();
            var categories = new List<char>();
            var pending = (char)0;
            var hasPending = false;
            var pendingIsPlain = false;
            var pendingAdded = false;
            var first = true;
            while (true)
            {
                if (state.AtEnd)
                {
                    return false;
                }
                var character = state.Pattern[state.Position];
                if (character == ']' && !first)
                {
                    state.Position++;
                    break;
                }
                if (character == '-')
                {
                    if (hasPending &&
                        pendingIsPlain &&
                        state.Position + 1 < state.Pattern.Length &&
                        state.Pattern[state.Position + 1] != ']' &&
                        state.Pattern[state.Position + 1] != '[')
                    {
                        if (ShorthandRangeEndAhead(ref state))
                        {
                            return false;
                        }

                        if (TryTakeComplexRangeEnd(ref state, units))
                        {
                            hasPending = false;
                            pendingAdded = false;
                            first = false;
                            if (state.AtEnd || state.Pattern[state.Position] != ']')
                            {
                                return false;
                            }

                            continue;
                        }

                        if (!TryTakeRangeEnd(ref state, pending, units))
                        {
                            return false;
                        }

                        hasPending = false;
                        pendingAdded = false;
                        first = false;
                        continue;
                    }

                    if (hasPending &&
                        state.Position + 1 < state.Pattern.Length &&
                        state.Pattern[state.Position + 1] == '[')
                    {
                        return false;
                    }

                    if (!hasPending &&
                        state.Position + 1 < state.Pattern.Length &&
                        state.Pattern[state.Position + 1] == '-' &&
                        IsRangeEndAhead(state, 2))
                    {
                        pending = '-';
                        hasPending = true;
                        pendingIsPlain = true;
                        pendingAdded = false;
                        state.Position++;
                        first = false;
                        continue;
                    }
                    if (hasPending && !pendingAdded)
                    {
                        if (!AddClassUnit(units, pending))
                        {
                            return false;
                        }
                    }

                    if (!AddClassUnit(units, '-'))
                    {
                        return false;
                    }
                    pending = '-';
                    hasPending = true;
                    pendingIsPlain = true;
                    pendingAdded = true;
                    state.Position++;
                    first = false;
                    continue;
                }
                char atom;
                if (character == '\\')
                {
                    if (TryTakeShorthandMembers(ref state, units, categories))
                    {
                        hasPending = false;
                        pendingIsPlain = false;
                        pendingAdded = false;
                        first = false;
                        continue;
                    }

                    if (!DecodeClassEscape(ref state, out atom, out var atomIsPlain))
                    {
                        return false;
                    }

                    pendingIsPlain = atomIsPlain;
                }
                else
                {
                    atom = character;
                    if (IsRejectedClassChar(atom))
                    {
                        return false;
                    }
                    state.Position++;
                    pendingIsPlain = true;
                }
                if (state.Position < state.Pattern.Length &&
                    state.Pattern[state.Position] == '-' &&
                    IsRangeEndAhead(state, 1))
                {
                    pending = atom;
                    hasPending = true;
                    pendingAdded = false;
                }
                else
                {
                    if (!AddClassUnit(units, atom))
                    {
                        return false;
                    }
                    pending = atom;
                    hasPending = true;
                    pendingAdded = true;
                }
                first = false;
            }
            var text = new char[units.Count + categories.Count + 3];
            text[0] = negated ? (char)1 : (char)0;
            text[1] = (char)units.Count;
            text[2] = (char)categories.Count;
            for (var i = 0; i < units.Count; i++)
            {
                text[i + 3] = units[i];
            }

            for (var i = 0; i < categories.Count; i++)
            {
                text[i + 3 + units.Count] = categories[i];
            }
            node = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Set, state.Options, new string(text));
            return true;
        }
        static bool ShorthandRangeEndAhead(ref ParserState state)
        {
            // state.Position is at '-'; true when a set or category escape
            // follows, which the provider rejects in upper-bound position.
            if (state.Position + 1 >= state.Pattern.Length || state.Pattern[state.Position + 1] != '\\')
            {
                return false;
            }

            var probe = state;
            probe.Position++;
            return TryTakeShorthandMembers(ref probe, new List<char>(), new List<char>());
        }

        static bool TryTakeComplexRangeEnd(ref ParserState state, List<char> units)
        {
            // state.Position is at '-'; consumes a complex-decodable end
            // (punctuation resolution) as a single, discarding the pending start
            // exactly like the provider, whose range never starts there.
            if (state.Position + 1 >= state.Pattern.Length || state.Pattern[state.Position + 1] != '\\')
            {
                return false;
            }

            var probe = state;
            probe.Position++;
            if (!DecodeClassEscape(ref probe, out var end, out var isPlain) || isPlain)
            {
                return false;
            }

            state.Position = probe.Position;
            if (!AddClassUnit(units, end))
            {
                return false;
            }

            return true;
        }

        static bool TryTakeRangeEnd(ref ParserState state, char start, List<char> units)
        {
            if (state.Position + 1 >= state.Pattern.Length)
            {
                return false;
            }
            var next = state.Pattern[state.Position + 1];
            if (next == ']' || next == '[')
            {
                return false;
            }
            char end;
            int consumed;
            if (next == '\\')
            {
                var probe = state;
                probe.Position++;
                if (!DecodeClassEscape(ref probe, out end, out _))
                {
                    return false;
                }
                consumed = probe.Position - state.Position;
            }
            else
            {
                end = next;
                if (IsRejectedClassChar(end))
                {
                    return false;
                }
                consumed = 2;
            }
            if (end < start)
            {
                return false;
            }
            units.Add(start);
            units.Add((char)(end + 1));
            state.Position += consumed;
            return true;
        }
        static bool IsRangeEndAhead(ParserState state, int offset)
        {
            var index = state.Position + offset;
            if (index >= state.Pattern.Length)
            {
                return false;
            }
            var candidate = state.Pattern[index];
            if (candidate == ']' || candidate == '[')
            {
                return false;
            }
            if (candidate == '\\')
            {
                var probe = state;
                probe.Position = index;
                return DecodeClassEscape(ref probe, out _, out _);
            }
            return !IsRejectedClassChar(candidate);
        }
        static bool DecodeClassEscape(ref ParserState state, out char value, out bool isPlain)
        {
            isPlain = false;
            if (state.Position + 1 < state.Pattern.Length)
            {
                var letter = state.Pattern[state.Position + 1];
                if (letter == 'b')
                {
                    value = (char)8;
                    state.Position += 2;
                    isPlain = true;
                    return true;
                }
                if (letter is 'B' or 'G' or 'Z' or 'z')
                {
                    value = letter;
                    state.Position += 2;
                    isPlain = true;
                    return true;
                }
                if (letter == 'A')
                {
                    value = 'A';
                    state.Position += 2;
                    isPlain = true;
                    return true;
                }
                if (letter == '-')
                {
                    value = '-';
                    state.Position += 2;
                    isPlain = true;
                    return true;
                }

                isPlain = IsLimitedClassEscapeHead(letter);
            }
            if (!DecodeEscape(ref state, out value))
            {
                return false;
            }
            return !IsRejectedClassChar(value);
        }

        static bool IsLimitedClassEscapeHead(char letter)
        {
            // Subset TryScanCharClassLiteralEscape heads beyond the arms above:
            // single-char escapes with full range behavior, unlike shorthand or
            // punctuation resolutions that break ranges.
            return letter is '0' or 'x' or 'u' or 'c' or 'a' or 'e' or 'n' or 'r' or 't' or 'f' or 'v' || (letter >= '0' && letter <= '7');
        }
        static bool TryTakeShorthandAtom(ref ParserState state, out RuntimeFrontEnd.RegexNode? node)
        {
            node = null;
            var units = new List<char>();
            var categories = new List<char>();
            if (!TryTakeShorthandMembers(ref state, units, categories))
            {
                return false;
            }

            node = BuildSetNode(state.Options & ~RegexOptions.IgnoreCase, negated: false, units, categories);
            return true;
        }

        static RuntimeFrontEnd.RegexNode BuildSetNode(RegexOptions options, bool negated, List<char> units, List<char> categories)
        {
            var text = new char[units.Count + categories.Count + 3];
            text[0] = negated ? (char)1 : (char)0;
            text[1] = (char)units.Count;
            text[2] = (char)categories.Count;
            for (var i = 0; i < units.Count; i++)
            {
                text[i + 3] = units[i];
            }

            for (var i = 0; i < categories.Count; i++)
            {
                text[i + 3 + units.Count] = categories[i];
            }

            return new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Set, options, new string(text));
        }

        static bool TryTakeShorthandMembers(ref ParserState state, List<char> units, List<char> categories)
        {
            // state.Position is at the backslash; nothing is consumed on failure.
            if (state.Position + 1 >= state.Pattern.Length)
            {
                return false;
            }

            switch (state.Pattern[state.Position + 1])
            {
                case 'd':
                    categories.AddRange(ShorthandDigitCats);
                    state.Position += 2;
                    return true;
                case 'D':
                    categories.AddRange(ShorthandNotDigitCats);
                    state.Position += 2;
                    return true;
                case 'w':
                    categories.AddRange(ShorthandWordCats);
                    state.Position += 2;
                    return true;
                case 'W':
                    categories.AddRange(ShorthandNotWordCats);
                    state.Position += 2;
                    return true;
                case 's':
                    categories.AddRange(ShorthandSpaceCats);
                    state.Position += 2;
                    return true;
                case 'S':
                    categories.AddRange(ShorthandNotSpaceCats);
                    state.Position += 2;
                    return true;
                case 'p':
                    return TryTakeProperty(ref state, invert: false, units, categories);
                case 'P':
                    return TryTakeProperty(ref state, invert: true, units, categories);
                default:
                    return false;
            }
        }

        static bool TryTakeProperty(ref ParserState state, bool invert, List<char> units, List<char> categories)
        {
            // state.Position is at the backslash of \p or \P.
            var open = state.Position + 2;
            if (open >= state.Pattern.Length || state.Pattern[open] != '{')
            {
                return false;
            }

            var close = state.Pattern.IndexOf('}', open + 1);
            if (close < 0)
            {
                return false;
            }

            var name = state.Pattern.Substring(open + 1, close - open - 1);
            if (!TryResolvePropertyMembers(name, invert, units, categories))
            {
                return false;
            }

            state.Position = close + 1;
            return true;
        }

        static bool TryResolvePropertyMembers(string name, bool invert, List<char> units, List<char> categories)
        {
            if (TryResolveSingleCategory(name, out var single))
            {
                var encoded = (char)(single + 1);
                categories.Add(invert ? ComplementCategory(encoded) : encoded);
                return true;
            }

            if (TryResolveCategoryGroup(name, out var members))
            {
                categories.Add(invert ? (char)0xFFFF : (char)0);
                foreach (var member in members)
                {
                    var encoded = (char)(member + 1);
                    categories.Add(invert ? ComplementCategory(encoded) : encoded);
                }

                categories.Add(invert ? (char)0xFFFF : (char)0);
                return true;
            }

            if (Utf8SliceCategories.TryResolve(name, out _, out var blockRanges, out var isBlock) && isBlock)
            {
                AddBlockRanges(units, blockRanges, invert);
                return true;
            }

            return false;
        }

        static char ComplementCategory(char encoded)
        {
            return (char)~encoded;
        }

        static bool TryResolveSingleCategory(string name, out int category)
        {
            category = (int)(name switch
            {
                "Lu" => UnicodeCategory.UppercaseLetter,
                "Ll" => UnicodeCategory.LowercaseLetter,
                "Lt" => UnicodeCategory.TitlecaseLetter,
                "Lm" => UnicodeCategory.ModifierLetter,
                "Lo" => UnicodeCategory.OtherLetter,
                "Mn" => UnicodeCategory.NonSpacingMark,
                "Mc" => UnicodeCategory.SpacingCombiningMark,
                "Me" => UnicodeCategory.EnclosingMark,
                "Nd" => UnicodeCategory.DecimalDigitNumber,
                "Nl" => UnicodeCategory.LetterNumber,
                "No" => UnicodeCategory.OtherNumber,
                "Zs" => UnicodeCategory.SpaceSeparator,
                "Zl" => UnicodeCategory.LineSeparator,
                "Zp" => UnicodeCategory.ParagraphSeparator,
                "Cc" => UnicodeCategory.Control,
                "Cf" => UnicodeCategory.Format,
                "Cs" => UnicodeCategory.Surrogate,
                "Co" => UnicodeCategory.PrivateUse,
                "Cn" => UnicodeCategory.OtherNotAssigned,
                "Pc" => UnicodeCategory.ConnectorPunctuation,
                "Pd" => UnicodeCategory.DashPunctuation,
                "Ps" => UnicodeCategory.OpenPunctuation,
                "Pe" => UnicodeCategory.ClosePunctuation,
                "Pi" => UnicodeCategory.InitialQuotePunctuation,
                "Pf" => UnicodeCategory.FinalQuotePunctuation,
                "Po" => UnicodeCategory.OtherPunctuation,
                "Sm" => UnicodeCategory.MathSymbol,
                "Sc" => UnicodeCategory.CurrencySymbol,
                "Sk" => UnicodeCategory.ModifierSymbol,
                "So" => UnicodeCategory.OtherSymbol,
                _ => (UnicodeCategory)(-1),
            });
            return category >= 0;
        }

        static bool TryResolveCategoryGroup(string name, out int[] members)
        {
            // Member order is probe-observed, not declaration order.
            switch (name)
            {
                case "L":
                    members = [(int)UnicodeCategory.LowercaseLetter, (int)UnicodeCategory.ModifierLetter, (int)UnicodeCategory.OtherLetter, (int)UnicodeCategory.TitlecaseLetter, (int)UnicodeCategory.UppercaseLetter];
                    return true;
                case "M":
                    members = [(int)UnicodeCategory.NonSpacingMark, (int)UnicodeCategory.SpacingCombiningMark, (int)UnicodeCategory.EnclosingMark];
                    return true;
                case "N":
                    members = [(int)UnicodeCategory.DecimalDigitNumber, (int)UnicodeCategory.LetterNumber, (int)UnicodeCategory.OtherNumber];
                    return true;
                case "Z":
                    members = [(int)UnicodeCategory.SpaceSeparator, (int)UnicodeCategory.LineSeparator, (int)UnicodeCategory.ParagraphSeparator];
                    return true;
                case "C":
                    members = [(int)UnicodeCategory.Control, (int)UnicodeCategory.Format, (int)UnicodeCategory.Surrogate, (int)UnicodeCategory.PrivateUse, (int)UnicodeCategory.OtherNotAssigned];
                    return true;
                case "P":
                    members = [(int)UnicodeCategory.ConnectorPunctuation, (int)UnicodeCategory.DashPunctuation, (int)UnicodeCategory.OpenPunctuation, (int)UnicodeCategory.ClosePunctuation, (int)UnicodeCategory.InitialQuotePunctuation, (int)UnicodeCategory.FinalQuotePunctuation, (int)UnicodeCategory.OtherPunctuation];
                    return true;
                case "S":
                    members = [(int)UnicodeCategory.MathSymbol, (int)UnicodeCategory.CurrencySymbol, (int)UnicodeCategory.ModifierSymbol, (int)UnicodeCategory.OtherSymbol];
                    return true;
                default:
                    members = [];
                    return false;
            }
        }

        static void AddBlockRanges(List<char> units, ushort[] blockRanges, bool invert)
        {
            if (!invert)
            {
                for (var i = 0; i < blockRanges.Length; i += 2)
                {
                    var first = blockRanges[i];
                    var last = i + 1 < blockRanges.Length ? blockRanges[i + 1] - 1 : 0xFFFF;
                    units.Add((char)first);
                    units.Add((char)(last + 1));
                }

                return;
            }

            var uncovered = 0;
            for (var i = 0; i < blockRanges.Length; i += 2)
            {
                var first = blockRanges[i];
                var last = i + 1 < blockRanges.Length ? blockRanges[i + 1] - 1 : 0xFFFF;
                if (uncovered < first)
                {
                    units.Add((char)uncovered);
                    units.Add((char)first);
                }

                uncovered = last + 1;
            }

            if (uncovered <= 0xFFFF)
            {
                units.Add((char)uncovered);
                units.Add(unchecked((char)(0xFFFF + 1)));
            }
        }
        static bool ParseNamedGroup(ref ParserState state, int depth, out RuntimeFrontEnd.RegexNode? node)
        {
            node = null;
            var open = state.Pattern[state.Position];
            var close = open == '<' ? '>' : '\'';
            state.Position++;
            var contentStart = state.Position;
            while (!state.AtEnd && state.Pattern[state.Position] != close)
            {
                state.Position++;
            }

            if (state.AtEnd)
            {
                return false;
            }

            var content = state.Pattern.Substring(contentStart, state.Position - contentStart);
            state.Position++;

            var isPop = false;
            var name = content;
            if (content.StartsWith('-'))
            {
                isPop = true;
                name = content.Substring(1);
                if (name.Length == 0 || name.Contains('-'))
                {
                    return false;
                }
            }
            else if (name.Length == 0 || name.Contains('-'))
            {
                return false;
            }

            if (!ParseAlternation(ref state, depth + 1, out var body) || body is null)
            {
                return false;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                return false;
            }

            state.Position++;
            var group = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Capture, state.Options);
            group.Children = body;
            body.Parent = group;
            node = group;

            if (isPop)
            {
                group.M = -1;
                group.N = -1;
                state.BackrefFixups.Add(new OwnedBackrefFixup { Node = group, Name = name, IsPop = true });
                return true;
            }

            if (IsAllAsciiDigits(name) && TryParseGroupNumber(name, out var declared))
            {
                group.Str = name;
                group.M = declared;
                group.N = -1;
                if (declared != 0)
                {
                    state.GroupDefinitions.Add(new OwnedGroupDefinition { Node = group, Name = name, Declared = declared, IsAuto = false, Slot = declared });
                }

                return true;
            }

            if (IsAllAsciiDigits(name) || IsAsciiDigit(name[0]))
            {
                group.Str = name;
                group.M = -1;
                group.N = -1;
                return true;
            }

            group.Str = name;
            group.M = -1;
            group.N = -1;
            state.GroupDefinitions.Add(new OwnedGroupDefinition { Node = group, Name = name, Declared = -1, IsAuto = true, Slot = -1 });
            return true;
        }

        static bool IsAllAsciiDigits(string value)
        {
            if (value.Length == 0)
            {
                return false;
            }

            foreach (var character in value)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            return true;
        }

        static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        static bool TryParseGroupNumber(string value, out int number)
        {
            number = 0;
            if (value.Length == 0)
            {
                return false;
            }

            var total = 0L;
            foreach (var character in value)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }

                total = total * 10 + (character - '0');
                if (total > int.MaxValue)
                {
                    return false;
                }
            }

            number = (int)total;
            return true;
        }

        static string? TryScanBackrefDigits(ref ParserState state)
        {
            if (state.Position + 1 >= state.Pattern.Length)
            {
                return null;
            }

            var first = state.Pattern[state.Position + 1];
            if (first < '1' || first > '9')
            {
                return null;
            }

            var end = state.Position + 2;
            while (end < state.Pattern.Length && state.Pattern[end] >= '0' && state.Pattern[end] <= '9')
            {
                end++;
            }

            var digits = state.Pattern.Substring(state.Position + 1, end - state.Position - 1);
            state.Position = end;
            return digits;
        }

        static string? TryScanNamedBackref(ref ParserState state)
        {
            if (state.Position + 2 >= state.Pattern.Length || state.Pattern[state.Position + 1] != 'k')
            {
                return null;
            }

            var open = state.Pattern[state.Position + 2];
            if (open != '<' && open != '\'')
            {
                return null;
            }
            var close = open == '<' ? '>' : '\'';
            var end = state.Pattern.IndexOf(close, state.Position + 3);
            if (end < 0)
            {
                return null;
            }

            var name = state.Pattern.Substring(state.Position + 3, end - state.Position - 3);
            state.Position = end + 1;
            return name;
        }

        static bool IsNamedGroupAhead(ParserState state)
        {
            // state.Position is at '?' after "(?". Angle and quote openers start
            // named, pop, or balance shapes, except lookbehind markers.
            if (state.AtEnd)
            {
                return false;
            }

            var opener = state.Pattern[state.Position];
            if (opener == '\'')
            {
                return true;
            }

            return opener == '<' && (state.Position + 1 >= state.Pattern.Length || (state.Pattern[state.Position + 1] != '=' && state.Pattern[state.Position + 1] != '!'));
        }

        static bool FinishNumbering(
            ref ParserState state,
            ref RuntimeFrontEnd.RegexNode root,
            out int captureCount,
            out string[] captureNames,
            out Hashtable? captureNameToNumber,
            out Hashtable? captureSparseMapping)
        {
            captureCount = 0;
            captureNames = [];
            captureNameToNumber = null;
            captureSparseMapping = null;

            var taken = new HashSet<int>();
            foreach (var definition in state.GroupDefinitions)
            {
                if (!definition.IsAuto && definition.Declared > 0)
                {
                    taken.Add(definition.Declared);
                }
            }

            for (var slot = 1; slot <= state.Groups; slot++)
            {
                taken.Add(slot);
            }

            var nextFree = 1;
            var autoSlots = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var definition in state.GroupDefinitions)
            {
                if (!definition.IsAuto)
                {
                    definition.Slot = definition.Declared;
                    continue;
                }

                if (autoSlots.TryGetValue(definition.Name, out var shared))
                {
                    definition.Slot = shared;
                    definition.Node.M = shared;
                    continue;
                }

                while (taken.Contains(nextFree))
                {
                    nextFree++;
                }

                definition.Slot = nextFree;
                taken.Add(nextFree);
                autoSlots[definition.Name] = nextFree;
                definition.Node.M = nextFree;
            }

            var slots = new SortedSet<int> { 0 };
            for (var slot = 1; slot <= state.Groups; slot++)
            {
                slots.Add(slot);
            }

            foreach (var definition in state.GroupDefinitions)
            {
                if (definition.Slot > 0)
                {
                    slots.Add(definition.Slot);
                }
            }

            var orderedSlots = new List<int>(slots);
            captureCount = orderedSlots.Count;

            var autoBySlot = new Dictionary<int, string>();
            foreach (var definition in state.GroupDefinitions)
            {
                if (definition.IsAuto && !autoBySlot.ContainsKey(definition.Slot))
                {
                    autoBySlot[definition.Slot] = definition.Name;
                }
            }

            captureNames = new string[orderedSlots.Count];
            for (var i = 0; i < orderedSlots.Count; i++)
            {
                var slot = orderedSlots[i];
                captureNames[i] = slot == 0 ? "0" : autoBySlot.TryGetValue(slot, out var name) ? name : slot.ToString();
            }

            var dense = orderedSlots.Count == 0 || orderedSlots[orderedSlots.Count - 1] == orderedSlots.Count - 1;
            if (!dense)
            {
                var sparse = new Hashtable();
                for (var i = 0; i < orderedSlots.Count; i++)
                {
                    sparse[orderedSlots[i]] = i;
                }

                captureSparseMapping = sparse;
            }

            var hasAuto = false;
            foreach (var definition in state.GroupDefinitions)
            {
                if (definition.IsAuto)
                {
                    hasAuto = true;
                    break;
                }
            }

            if (hasAuto)
            {
                var nameToNumber = new Hashtable { { "0", 0 } };
                for (var slot = 1; slot <= state.Groups; slot++)
                {
                    nameToNumber[slot.ToString()] = slot;
                }

                foreach (var definition in state.GroupDefinitions)
                {
                    if (definition.IsAuto)
                    {
                        nameToNumber[definition.Name] = definition.Slot;
                    }
                    else
                    {
                        nameToNumber[definition.Declared.ToString()] = definition.Declared;
                    }
                }

                captureNameToNumber = nameToNumber;
            }

            var slotByName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < orderedSlots.Count; i++)
            {
                slotByName[captureNames[i]] = orderedSlots[i];
            }

            var morphed = false;
            foreach (var fixup in state.BackrefFixups)
            {
                if (!ResolveFixup(fixup, slots, slotByName, ref morphed))
                {
                    return false;
                }
            }

            if (morphed)
            {
                root = MergeLiteralRuns(root);
            }

            return true;
        }

        static bool ResolveFixup(OwnedBackrefFixup fixup, SortedSet<int> slots, Dictionary<string, int> slotByName, ref bool morphed)
        {
            if (fixup.IsPop)
            {
                var popName = fixup.Name;
                if (popName is null)
                {
                    return false;
                }

                if (IsAllAsciiDigits(popName))
                {
                    if (!TryParseGroupNumber(popName, out var popNumber) || !slots.Contains(popNumber))
                    {
                        return false;
                    }

                    fixup.Node.N = popNumber;
                    return true;
                }

                if (!slotByName.TryGetValue(popName, out var popSlot))
                {
                    return false;
                }

                fixup.Node.N = popSlot;
                return true;
            }

            if (fixup.Name is not null)
            {
                if (!slotByName.TryGetValue(fixup.Name, out var namedSlot))
                {
                    return false;
                }

                fixup.Node.M = namedSlot;
                return true;
            }

            if (fixup.Digits is not null &&
                int.TryParse(fixup.Digits, out var backref) &&
                backref >= 1 &&
                slots.Contains(backref))
            {
                fixup.Node.M = backref;
                return true;
            }

            return ResolveOctalFallback(fixup, ref morphed);
        }

        static bool ResolveOctalFallback(OwnedBackrefFixup fixup, ref bool morphed)
        {
            // Single non-zero digits never spell octal; longer runs without a
            // matching slot fall back to up to three octal digits with no rest.
            if (fixup.Digits is null || fixup.Digits.Length < 2)
            {
                return false;
            }

            var length = 0;
            var value = 0;
            while (length < 3 && length < fixup.Digits.Length)
            {
                var digit = fixup.Digits[length] - '0';
                if (digit < 0 || digit > 7)
                {
                    break;
                }

                value = value * 8 + digit;
                length++;
            }

            if (length == 0 || length != fixup.Digits.Length)
            {
                return false;
            }

            morphed = true;
            if (fixup.Wrapper is null)
            {
                fixup.Node.Kind = RuntimeFrontEnd.RegexNodeKind.One;
                fixup.Node.Ch = (char)value;
                fixup.Node.M = 0;
                fixup.Node.N = 0;
                return true;
            }

            var wrapper = fixup.Wrapper;
            if (wrapper.ChildCount != 1 || !ReferenceEquals(wrapper.Child(0), fixup.Node))
            {
                return false;
            }

            wrapper.Kind = wrapper.Kind == RuntimeFrontEnd.RegexNodeKind.Lazyloop
                ? RuntimeFrontEnd.RegexNodeKind.Onelazy
                : RuntimeFrontEnd.RegexNodeKind.Oneloop;
            wrapper.Ch = (char)value;
            wrapper.Children = null;
            return true;
        }

        static RuntimeFrontEnd.RegexNode MergeLiteralRuns(RuntimeFrontEnd.RegexNode node)
        {
            if (node.Children is List<RuntimeFrontEnd.RegexNode> children)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    var merged = MergeLiteralRuns(children[i]);
                    if (!ReferenceEquals(merged, children[i]))
                    {
                        children[i] = merged;
                        merged.Parent = node;
                    }
                }

                var fused = new List<RuntimeFrontEnd.RegexNode>(children.Count);
                var pending = new List<RuntimeFrontEnd.RegexNode>();
                var pendingOptions = RegexOptions.None;
                foreach (var child in children)
                {
                    if (TryMergeableText(child, out _, out _) && (pending.Count == 0 || child.Options == pendingOptions))
                    {
                        if (pending.Count == 0)
                        {
                            pendingOptions = child.Options;
                        }

                        pending.Add(child);
                        continue;
                    }

                    FlushMergePending(node, fused, pending);
                    fused.Add(child);
                }

                FlushMergePending(node, fused, pending);
                if (fused.Count == children.Count)
                {
                    return node;
                }

                node.Children = fused;
                foreach (var child in fused)
                {
                    child.Parent = node;
                }

                return fused.Count == 1 ? fused[0] : node;
            }

            if (node.Children is RuntimeFrontEnd.RegexNode single)
            {
                var merged = MergeLiteralRuns(single);
                if (!ReferenceEquals(merged, single))
                {
                    node.Children = merged;
                    merged.Parent = node;
                }
            }

            return node;
        }

        static bool TryMergeableText(RuntimeFrontEnd.RegexNode node, out char single, out string? text)
        {
            single = '\0';
            text = null;
            if (node.Kind == RuntimeFrontEnd.RegexNodeKind.One && node.Str is null)
            {
                single = node.Ch;
                return true;
            }

            if (node.Kind == RuntimeFrontEnd.RegexNodeKind.Multi && node.Str2 is null && node.Str is not null)
            {
                text = node.Str;
                return true;
            }

            return false;
        }

        static void FlushMergePending(RuntimeFrontEnd.RegexNode parent, List<RuntimeFrontEnd.RegexNode> fused, List<RuntimeFrontEnd.RegexNode> pending)
        {
            if (pending.Count == 0)
            {
                return;
            }

            if (pending.Count == 1)
            {
                fused.Add(pending[0]);
                pending.Clear();
                return;
            }

            var run = new StringBuilder();
            foreach (var literal in pending)
            {
                if (literal.Kind == RuntimeFrontEnd.RegexNodeKind.One)
                {
                    run.Append(literal.Ch);
                }
                else
                {
                    run.Append(literal.Str);
                }
            }

            var merged = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Multi, pending[0].Options, run.ToString());
            merged.Parent = parent;
            fused.Add(merged);
            pending.Clear();
        }
        static bool TryTakeLookaroundKind(ParserState state, out RuntimeFrontEnd.RegexNodeKind kind, out bool isLookbehind)
        {
            kind = RuntimeFrontEnd.RegexNodeKind.Concatenate;
            isLookbehind = false;
            if (state.AtEnd)
            {
                return false;
            }

            var marker = state.Pattern[state.Position];
            if (marker == '=')
            {
                kind = RuntimeFrontEnd.RegexNodeKind.PositiveLookaround;
                return true;
            }

            if (marker == '!')
            {
                kind = RuntimeFrontEnd.RegexNodeKind.NegativeLookaround;
                return true;
            }

            if (marker == '>')
            {
                kind = RuntimeFrontEnd.RegexNodeKind.Atomic;
                return true;
            }

            if (marker == '<' && state.Position + 1 < state.Pattern.Length)
            {
                var direction = state.Pattern[state.Position + 1];
                if (direction == '=')
                {
                    kind = RuntimeFrontEnd.RegexNodeKind.PositiveLookaround;
                    isLookbehind = true;
                    return true;
                }

                if (direction == '!')
                {
                    kind = RuntimeFrontEnd.RegexNodeKind.NegativeLookaround;
                    isLookbehind = true;
                    return true;
                }
            }

            return false;
        }

        static bool ParseLookaround(ref ParserState state, int depth, out RuntimeFrontEnd.RegexNode? node, RuntimeFrontEnd.RegexNodeKind kind, bool isLookbehind)
        {
            node = null;
            // state.Position is at the opener marker after "(?". Lookbehind
            // consumes its direction marker too and forces RightToLeft below.
            state.Position += isLookbehind ? 2 : 1;
            if (isLookbehind && state.Pattern.IndexOf('>', state.Position) >= 0)
            {
                return false;
            }

            var savedOptions = state.Options;
            var bodyOptions = kind == RuntimeFrontEnd.RegexNodeKind.Atomic
                ? savedOptions
                : isLookbehind
                    ? savedOptions | RegexOptions.RightToLeft
                    : savedOptions & ~RegexOptions.RightToLeft;
            state.Options = bodyOptions;

            if (!ParseAlternation(ref state, depth + 1, out var body) || body is null)
            {
                state.Options = savedOptions;
                return false;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                state.Options = savedOptions;
                return false;
            }

            state.Position++;
            if (isLookbehind && ContainsCaptureOrBackreference(body))
            {
                state.Options = savedOptions;
                return false;
            }

            var options = bodyOptions;
            var assertion = new RuntimeFrontEnd.RegexNode(kind, options);
            assertion.Children = body;
            body.Parent = assertion;
            node = assertion;
            state.Options = savedOptions;
            return true;
        }

        static bool ContainsCaptureOrBackreference(RuntimeFrontEnd.RegexNode node)
        {
            if (node.Kind == RuntimeFrontEnd.RegexNodeKind.Capture ||
                node.Kind == RuntimeFrontEnd.RegexNodeKind.Backreference)
            {
                return true;
            }

            for (var i = 0; i < node.ChildCount; i++)
            {
                if (ContainsCaptureOrBackreference(node.Child(i)))
                {
                    return true;
                }
            }

            return false;
        }
        static bool AddClassUnit(List<char> units, char value)
        {
            if (IsRejectedClassChar(value))
            {
                return false;
            }
            units.Add(value);
            units.Add((char)(value + 1));
            return true;
        }
        static bool IsRejectedClassChar(char value)
        {
            return (value >= (char)0xD800 && value <= (char)0xDFFF) || value == (char)0xFFFF;
        }
        static bool TryTakeOptionScope(ref ParserState state, out RegexOptions scoped, out RegexOptions contribution)
        {
            scoped = state.Options;
            contribution = RegexOptions.None;
            var on = RegexOptions.None;
            var off = RegexOptions.None;
            var dashed = false;
            while (!state.AtEnd && IsScopeFlag(state.Pattern[state.Position]))
            {
                on |= ScopeFlag(state.Pattern[state.Position]);
                state.Position++;
            }
            if (!state.AtEnd && state.Pattern[state.Position] == '-')
            {
                dashed = true;
                state.Position++;
                while (!state.AtEnd && IsScopeFlag(state.Pattern[state.Position]))
                {
                    off |= ScopeFlag(state.Pattern[state.Position]);
                    state.Position++;
                }
                if (!state.AtEnd && state.Pattern[state.Position] == '-')
                {
                    return false;
                }
            }
            if (state.AtEnd || state.Pattern[state.Position] != ':')
            {
                return false;
            }
            if (dashed && on == RegexOptions.None && off == RegexOptions.None)
            {
                return false;
            }
            if (((on | off) & RegexOptions.ExplicitCapture) != RegexOptions.None)
            {
                return false;
            }
            state.Position++;
            scoped = (state.Options | on) & ~off;
            contribution = on & ~off;
            if (off != RegexOptions.None)
            {
                contribution &= ~RegexOptions.IgnoreCase;
            }

            return true;
        }
        static bool IsScopeFlag(char character)
        {
            return character == 'i' ||
                character == 'm' ||
                character == 'n' ||
                character == 's' ||
                character == 'x';
        }
        static RegexOptions ScopeFlag(char character)
        {
            switch (character)
            {
                case 'i':
                    return RegexOptions.IgnoreCase;
                case 'm':
                    return RegexOptions.Multiline;
                case 'n':
                    return RegexOptions.ExplicitCapture;
                case 's':
                    return RegexOptions.Singleline;
                case 'x':
                    return RegexOptions.IgnorePatternWhitespace;
                default:
                    return RegexOptions.None;
            }
        }
        static bool IsCommentAhead(ParserState state)
        {
            return state.Position + 2 < state.Pattern.Length &&
                state.Pattern[state.Position + 1] == '?' &&
                state.Pattern[state.Position + 2] == '#';
        }
        static bool SkipComment(ref ParserState state)
        {
            var end = state.Pattern.IndexOf(')', state.Position + 3);
            if (end < 0)
            {
                return false;
            }
            state.Position = end + 1;
            return true;
        }
        static bool TakeHex(ref ParserState state, int count, out int value)
        {
            value = 0;
            for (var i = 0; i < count; i++)
            {
                if (state.AtEnd)
                {
                    return false;
                }

                var digit = state.Pattern[state.Position];
                int nibble;
                if (digit >= '0' && digit <= '9')
                {
                    nibble = digit - '0';
                }
                else if (digit >= 'a' && digit <= 'f')
                {
                    nibble = digit - 'a' + 10;
                }
                else if (digit >= 'A' && digit <= 'F')
                {
                    nibble = digit - 'A' + 10;
                }
                else
                {
                    return false;
                }

                value = value * 16 + nibble;
                state.Position++;
            }

            return true;
        }

        static bool IsLiteralChar(char character)
        {
            return character != '\\' &&
                character != '.' &&
                character != '[' &&
                character != '{' &&
                character != '(' &&
                character != ')' &&
                character != '|' &&
                character != '*' &&
                character != '+' &&
                character != '?' &&
                character != '^' &&
                character != '$' &&
                character != ']' &&
                character != '}';
        }

        static bool IsQuantifierStart(char character)
        {
            return character == '*' || character == '+' || character == '?' || character == '{';
        }

        static RuntimeFrontEnd.RegexNode One(ParserState state, char character)
        {
            return new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, state.Options, character);
        }

        static RuntimeFrontEnd.RegexNode Multi(ParserState state, string text)
        {
            return new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Multi, state.Options, text);
        }

        static RuntimeFrontEnd.RegexNode Concatenate(ParserState state, List<RuntimeFrontEnd.RegexNode> children)
        {
            var node = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, state.Options);
            node.Children = children;
            foreach (var child in children)
            {
                child.Parent = node;
            }

            return node;
        }

        static RuntimeFrontEnd.RegexNode Alternate(ParserState state, List<RuntimeFrontEnd.RegexNode> branches)
        {
            var node = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Alternate, state.Options);
            node.Children = branches;
            foreach (var branch in branches)
            {
                branch.Parent = node;
            }

            return node;
        }

        static bool ParseGroup(ref ParserState state, int depth, out RuntimeFrontEnd.RegexNode? node)
        {
            node = null;
            state.Position++;
            var kind = RuntimeFrontEnd.RegexNodeKind.Capture;
            var slot = 0;
            if (!state.AtEnd && state.Pattern[state.Position] == '?')
            {
                state.Position++;
                if (IsNamedGroupAhead(state))
                {
                    return ParseNamedGroup(ref state, depth, out node);
                }

                if (TryTakeLookaroundKind(state, out var lookaroundKind, out var isLookbehind))
                {
                    return ParseLookaround(ref state, depth, out node, lookaroundKind, isLookbehind);
                }

                if (!TryTakeOptionScope(ref state, out var scopedOptions, out var contribution))
                {
                    return false;
                }

                if (depth == 0)
                {
                    state.TreeSeen |= contribution;
                }

                var savedOptions = state.Options;
                state.Options = scopedOptions;
                if (!ParseAlternation(ref state, depth + 1, out var scopeBody) || scopeBody is null)
                {
                    return false;
                }

                if (state.AtEnd || state.Pattern[state.Position] != ')')
                {
                    return false;
                }

                state.Position++;
                var scopeGroup = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Group, scopedOptions);
                scopeGroup.Children = scopeBody;
                scopeBody.Parent = scopeGroup;
                node = scopeGroup;
                state.Options = savedOptions;
                return true;
            }
            else
            {
                slot = ++state.Groups;
            }

            if (!ParseAlternation(ref state, depth + 1, out var body) || body is null)
            {
                return false;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                return false;
            }

            state.Position++;
            var group = new RuntimeFrontEnd.RegexNode(kind, state.Options);
            if (kind == RuntimeFrontEnd.RegexNodeKind.Capture)
            {
                group.M = slot;
                group.N = -1;
            }

            group.Children = body;
            body.Parent = group;
            node = group;
            return true;
        }

        static bool ParseQuantifier(ref ParserState state, ref RuntimeFrontEnd.RegexNode atom)
        {
            if (state.AtEnd)
            {
                return true;
            }

            var character = state.Pattern[state.Position];
            int minimum;
            int maximum;
            if (character == '*')
            {
                minimum = 0;
                maximum = Infinite;
                state.Position++;
            }
            else if (character == '+')
            {
                minimum = 1;
                maximum = Infinite;
                state.Position++;
            }
            else if (character == '?')
            {
                minimum = 0;
                maximum = 1;
                state.Position++;
            }
            else if (character == '{')
            {
                if (!ParseBrace(ref state, out minimum, out maximum))
                {
                    return false;
                }
            }
            else
            {
                return true;
            }

            var lazy = false;
            if (!state.AtEnd && state.Pattern[state.Position] == '?')
            {
                lazy = true;
                state.Position++;
            }

            atom = Quantify(state, atom, minimum, maximum, lazy);
            return true;
        }

        static bool ParseBrace(ref ParserState state, out int minimum, out int maximum)
        {
            minimum = 0;
            maximum = 0;
            state.Position++;
            if (!ParseNonNegative(ref state, out minimum))
            {
                return false;
            }

            maximum = minimum;
            if (!state.AtEnd && state.Pattern[state.Position] == ',')
            {
                state.Position++;
                maximum = Infinite;
                if (!state.AtEnd && state.Pattern[state.Position] != '}')
                {
                    if (!ParseNonNegative(ref state, out maximum))
                    {
                        return false;
                    }
                }
            }

            if (state.AtEnd || state.Pattern[state.Position] != '}' || maximum < minimum)
            {
                return false;
            }

            state.Position++;
            return true;
        }

        static bool ParseNonNegative(ref ParserState state, out int value)
        {
            value = 0;
            var digits = 0;
            while (!state.AtEnd && state.Pattern[state.Position] >= '0' && state.Pattern[state.Position] <= '9')
            {
                var next = (long)value * 10 + (state.Pattern[state.Position] - '0');
                if (next > Infinite)
                {
                    return false;
                }

                value = (int)next;
                digits++;
                state.Position++;
            }

            return digits != 0;
        }

        static RuntimeFrontEnd.RegexNode Quantify(ParserState state, RuntimeFrontEnd.RegexNode atom, int minimum, int maximum, bool lazy)
        {
            if (minimum == 1 && maximum == 1)
            {
                return atom;
            }

            if (maximum == 0)
            {
                return new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Empty, state.Options);
            }

            if (atom.Kind == RuntimeFrontEnd.RegexNodeKind.One)
            {
                return new RuntimeFrontEnd.RegexNode(
                    lazy ? RuntimeFrontEnd.RegexNodeKind.Onelazy : RuntimeFrontEnd.RegexNodeKind.Oneloop,
                    state.Options,
                    atom.Ch)
                {
                    M = minimum,
                    N = maximum,
                };
            }

            if (atom.Kind == RuntimeFrontEnd.RegexNodeKind.Set)
            {
                return new RuntimeFrontEnd.RegexNode(
                    lazy ? RuntimeFrontEnd.RegexNodeKind.Setlazy : RuntimeFrontEnd.RegexNodeKind.Setloop,
                    state.Options & ~RegexOptions.IgnoreCase,
                    atom.Str)
                {
                    M = minimum,
                    N = maximum,
                };
            }

            var loop = new RuntimeFrontEnd.RegexNode(
                lazy ? RuntimeFrontEnd.RegexNodeKind.Lazyloop : RuntimeFrontEnd.RegexNodeKind.Loop,
                state.Options,
                minimum,
                maximum);
            loop.Children = atom;
            atom.Parent = loop;
            return loop;
        }
    }

    private struct ParserState
    {
        public string Pattern;
        public RegexOptions Options;
        public int Position;
        public int Groups;
        public RegexOptions TreeSeen;
        public List<OwnedGroupDefinition> GroupDefinitions;
        public List<OwnedBackrefFixup> BackrefFixups;

        public ParserState(string pattern, RegexOptions options)
        {
            Pattern = pattern;
            Options = options;
            Position = 0;
            Groups = 0;
            TreeSeen = RegexOptions.None;
            GroupDefinitions = new List<OwnedGroupDefinition>();
            BackrefFixups = new List<OwnedBackrefFixup>();
        }

        public bool AtEnd => Position >= Pattern.Length;
    }

    // A named or numbered group definition: unnamed groups never appear here.
    // Auto-named groups take a pending slot that the end pass assigns; explicit
    // numbers claim their literal slot (zero is a pure alias and is not recorded).
    private sealed class OwnedGroupDefinition
    {
        public required RuntimeFrontEnd.RegexNode Node { get; init; }
        public string Name = string.Empty;
        public int Declared = -1;
        public bool IsAuto;
        public int Slot;
    }

    // A backreference or balance pop awaiting end-pass resolution, when group
    // numbering is final. Numeric tentatives carry their digit text and an
    // optional quantified wrapper for the octal outcome.
    private sealed class OwnedBackrefFixup
    {
        public required RuntimeFrontEnd.RegexNode Node { get; init; }
        public string? Digits;
        public string? Name;
        public bool IsPop;
        public RuntimeFrontEnd.RegexNode? Wrapper;
    }
}