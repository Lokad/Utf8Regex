using System.Collections;
using System.Collections;
using System.Globalization;
using Lokad.Utf8Regex.Internal.Execution;

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

    public static Utf8OwnedRegexTree? TryParse(string pattern, RegexOptions options)
    {
        var state = new ParserState(pattern, options);
        if (!ParseAlternation(ref state, 0, out var root) || root is null || state.Position != pattern.Length)
        {
            return null;
        }

        if ((options & RegexOptions.IgnoreCase) == 0 &&
            state.LeadingContribution != RegexOptions.None &&
            (state.LeadingGroup is null || !ReferenceEquals(root, state.LeadingGroup)))
        {
            // A leading scope that does not cover the whole pattern must not leak
            // its options onto the top composite: strip the non-covering contribution
            // instead of declining. Per-node scoping is already BCL-exact and the top
            // composite options are inert downstream, so serving is safe.
            state.TreeSeen &= ~state.LeadingContribution;
        }

        var treeOptions = options | state.TreeSeen;
        if (root.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate)
        {
            var top = new Utf8TreeNode(root.Kind, treeOptions);
            top.Ch = root.Ch;
            top.Str = root.Str;
            top.Str2 = root.Str2;
            top.M = root.M;
            top.N = root.N;
            top.Children = root.Children;
            if (top.Children is List<Utf8TreeNode> topChildren)
            {
                foreach (var topChild in topChildren)
                {
                    topChild.Parent = top;
                }
            }

            root = top;
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

        var tree = new Utf8TreeNode(Utf8TreeNodeKind.Capture, treeOptions & ~RegexOptions.IgnoreCase);
        tree.M = 0;
        tree.N = -1;
        tree.Children = root;
        root.Parent = tree;
        var culture = (options & RegexOptions.CultureInvariant) != 0
            ? Utf8OwnedOptionPrelude.GetTargetCulture(options)
            : null;
        return new Utf8OwnedRegexTree(
            tree,
            captureCount,
            captureNames,
            captureNameToNumber,
            captureSparseMapping,
            treeOptions,
            culture);

        static bool ParseAlternation(ref ParserState state, int depth, out Utf8TreeNode? root)
        {
            root = null;
            if (depth > MaxNestingDepth)
            {
                return false;
            }

            var branches = new List<Utf8TreeNode>();
            var alternationEntryOptions = state.Options;
            while (true)
            {
                if (depth == 0)
                {
                    state.Depth0Branch = branches.Count;
                }

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

            root = branches.Count == 1 ? branches[0] : Alternate(alternationEntryOptions, branches);
            return true;
        }

        static bool ParseConcatenation(ref ParserState state, int depth, out Utf8TreeNode? node)
        {
            node = null;
            var children = new List<Utf8TreeNode>();
            var run = new StringBuilder();
            var concatenationEntryOptions = state.Options;
            while (!state.AtEnd)
            {
                var character = state.Pattern[state.Position];
                if (character == '|' || character == ')')
                {
                    break;
                }

                if ((state.Options & RegexOptions.IgnorePatternWhitespace) != 0)
                {
                    if (character == '#')
                    {
                        while (!state.AtEnd && state.Pattern[state.Position] != (char)10 && state.Pattern[state.Position] != (char)13)
                        {
                            state.Position++;
                        }

                        continue;
                    }

                    if (IsIgnoredWhitespace(character))
                    {
                        state.Position++;
                        continue;
                    }
                }

                if (depth == 0)
                {
                    state.Depth0Atom = children.Count + (run.Length > 0 ? 1 : 0);
                }

                if (character == '\\')
                {
                    if (TryAnchorEscape(ref state, out var anchor) && anchor is not null)
                    {
                        FlushRun(state, children, run);
                        var anchorAtom = anchor;
                        if (!ParseQuantifier(ref state, ref anchorAtom, out var decayedAnchor))
                        {
                            return false;
                        }

                        if (decayedAnchor)
                        {
                            run.Append('{');
                        }

                        children.Add(anchorAtom);
                        continue;
                    }

                    if (TryTakeShorthandAtom(ref state, out var shorthand) && shorthand is not null)
                    {
                        FlushRun(state, children, run);
                        var shorthandAtom = shorthand;
                        if (!ParseQuantifier(ref state, ref shorthandAtom, out var decayedShorthand))
                        {
                            return false;
                        }

                        if (decayedShorthand)
                        {
                            run.Append('{');
                        }

                        children.Add(shorthandAtom);
                        continue;
                    }


                    var backrefDigits = TryScanBackrefDigits(ref state);
                    if (backrefDigits is not null)
                    {
                        FlushRun(state, children, run);
                        var tentative = new Utf8TreeNode(Utf8TreeNodeKind.Backreference, state.Options);
                        tentative.N = -1;
                        var backrefAtom = tentative;
                        if (!ParseQuantifier(ref state, ref backrefAtom, out var decayedBackref))
                        {
                            return false;
                        }

                        if (decayedBackref)
                        {
                            run.Append('{');
                        }

                        if (backrefAtom.Kind == Utf8TreeNodeKind.Empty)
                        {
                            state.BackrefFixups.Add(new OwnedBackrefFixup { Node = backrefAtom, Digits = backrefDigits });
                        }
                        else if (ReferenceEquals(backrefAtom, tentative))
                        {
                            state.BackrefFixups.Add(new OwnedBackrefFixup { Node = tentative, Digits = backrefDigits });
                        }
                        else if (backrefAtom.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop &&
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
                        var namedBackref = new Utf8TreeNode(Utf8TreeNodeKind.Backreference, state.Options);
                        namedBackref.Str = backrefName;
                        namedBackref.N = 0;
                        var namedAtom = namedBackref;
                        if (!ParseQuantifier(ref state, ref namedAtom, out var decayedNamed))
                        {
                            return false;
                        }

                        if (decayedNamed)
                        {
                            run.Append('{');
                        }

                        if (namedAtom.Kind == Utf8TreeNodeKind.Empty)
                        {
                            state.BackrefFixups.Add(new OwnedBackrefFixup { Node = namedAtom, Name = backrefName });
                            children.Add(namedAtom);
                            continue;
                        }

                        state.BackrefFixups.Add(new OwnedBackrefFixup { Node = namedBackref, Name = backrefName });
                        children.Add(namedAtom);
                        continue;
                    }

                    if (!DecodeEscape(ref state, out var escaped))
                    {
                        return false;
                    }

                    if ((state.Options & RegexOptions.IgnoreCase) != 0 &&
                        Utf8OwnedCaseFold.TryGetFold(escaped, out var escapeLower, out var escapeUpper))
                    {
                        FlushRun(state, children, run);
                        var escapedAtom = FoldedSet(state.Options, escapeLower, escapeUpper);
                        if (!ParseQuantifier(ref state, ref escapedAtom, out var decayedEscaped))
                        {
                            return false;
                        }

                        if (decayedEscaped)
                        {
                            run.Append('{');
                        }

                        children.Add(escapedAtom);
                        continue;
                    }

                    run.Append(escaped);
                    continue;
                }

                if (IsLiteralChar(character))
                {
                    if ((state.Options & RegexOptions.IgnoreCase) != 0 &&
                        Utf8OwnedCaseFold.TryGetFold(character, out var foldLower, out var foldUpper))
                    {
                        FlushRun(state, children, run);
                        state.Position++;
                        var foldedAtom = FoldedSet(state.Options, foldLower, foldUpper);
                        if (!ParseQuantifier(ref state, ref foldedAtom, out var decayedFolded))
                        {
                            return false;
                        }

                        if (decayedFolded)
                        {
                            run.Append('{');
                        }

                        children.Add(foldedAtom);
                        continue;
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
                    if (!ParseQuantifier(ref state, ref unit, out var decayedUnit))
                    {
                        return false;
                    }

                    if (decayedUnit)
                    {
                        run.Append('{');
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

                if (character == '(' &&
                    state.Position + 2 < state.Pattern.Length &&
                    state.Pattern[state.Position + 1] == '?'
                    && (IsScopeFlag(state.Pattern[state.Position + 2]) || state.Pattern[state.Position + 2] == '-'))
                {
                    var toggleStart = state.Position;
                    if (TryTakeToggle(ref state, depth))
                    {
                        continue;
                    }

                    state.Position = toggleStart;
                }

                FlushRun(state, children, run);
                Utf8TreeNode atom;
                if (character == '^')
                {
                    var anchorKind = (state.Options & RegexOptions.Multiline) == 0 ?
                        Utf8TreeNodeKind.Beginning :
                        Utf8TreeNodeKind.Bol;
                    atom = new Utf8TreeNode(anchorKind, state.Options);
                    state.Position++;
                }
                else if (character == '$')
                {
                    var anchorKind = (state.Options & RegexOptions.Multiline) == 0 ?
                        Utf8TreeNodeKind.EndZ :
                        Utf8TreeNodeKind.Eol;
                    atom = new Utf8TreeNode(anchorKind, state.Options);
                    state.Position++;
                }
                else if (character == '.' && (state.Options & RegexOptions.Singleline) == 0)
                {
                    atom = new Utf8TreeNode(Utf8TreeNodeKind.Set, state.Options & ~RegexOptions.IgnoreCase, DotText);
                    state.Position++;
                }
                else if (character == '.')
                {
                    atom = new Utf8TreeNode(Utf8TreeNodeKind.Set, state.Options & ~RegexOptions.IgnoreCase, SinglelineDotText);
                    state.Position++;
                }
                else if (character == '[')
                {
                    if (!ParseClass(ref state, out var classNode) || classNode is null)
                    {
                        return false;
                    }

                    atom = classNode;
                }
                else if (character == '{')
                {
                    // A leading brace that cannot complete decays (BCL shapes like
                    // brace-a or brace-12); complete or invalid shapes still decline.
                    if (!ParseBrace(ref state, out _, out _, out var decayedBrace) && decayedBrace)
                    {
                        run.Append('{');
                        continue;
                    }

                    return false;
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

                if (!ParseQuantifier(ref state, ref atom, out var decayedAtom))
                {
                    return false;
                }

                if (decayedAtom)
                {
                    run.Append('{');
                }

                children.Add(atom);
            }

            FlushRun(state, children, run);
            if (children.Count == 0)
            {
                node = new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, concatenationEntryOptions);
                return true;
            }

            node = children.Count == 1 ? children[0] : Concatenate(concatenationEntryOptions, children);
            return true;
        }

        static void FlushRun(ParserState state, List<Utf8TreeNode> children, StringBuilder run)
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

        static bool TryAnchorEscape(ref ParserState state, out Utf8TreeNode? anchor)
        {
            anchor = null;
            if (state.Position + 1 >= state.Pattern.Length)
            {
                return false;
            }

            Utf8TreeNodeKind kind;
            switch (state.Pattern[state.Position + 1])
            {
                case 'b':
                    kind = Utf8TreeNodeKind.Boundary;
                    break;
                case 'B':
                    kind = Utf8TreeNodeKind.NonBoundary;
                    break;
                case 'A':
                    kind = Utf8TreeNodeKind.Beginning;
                    break;
                case 'G':
                    kind = Utf8TreeNodeKind.Start;
                    break;
                case 'Z':
                    kind = Utf8TreeNodeKind.EndZ;
                    break;
                case 'z':
                    kind = Utf8TreeNodeKind.End;
                    break;
                default:
                    return false;
            }

            anchor = new Utf8TreeNode(kind, state.Options);
            state.Position += 2;
            return true;
        }

        static bool ParseClass(ref ParserState state, out Utf8TreeNode? node)
        {
            node = null;
            var wire = new StringBuilder();
            if (!ParseClassLevel(ref state, wire))
            {
                return false;
            }

            node = new Utf8TreeNode(Utf8TreeNodeKind.Set, state.Options & ~RegexOptions.IgnoreCase, wire.ToString());
            return true;
        }

        static bool ParseClassLevel(ref ParserState state, StringBuilder wire)
        {
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
            var subtracted = false;
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

                    if (state.Position + 1 < state.Pattern.Length &&
                        state.Pattern[state.Position + 1] == '[')
                    {
                        if (!subtracted && (hasPending || units.Count + categories.Count > 0))
                        {
                            if (hasPending && !pendingAdded)
                            {
                                if (!AddClassUnit(units, pending))
                                {
                                    return false;
                                }
                            }

                            AppendSegment(wire, negated, units, categories, (state.Options & RegexOptions.IgnoreCase) != 0);
                            subtracted = true;
                            state.Position++;
                            if (state.AtEnd || state.Pattern[state.Position] != '[')
                            {
                                return false;
                            }

                            if (!ParseClassLevel(ref state, wire))
                            {
                                return false;
                            }

                            if (state.AtEnd || state.Pattern[state.Position] != ']')
                            {
                                return false;
                            }

                            state.Position++;
                            return true;
                        }

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

                    if (TryTakeClassOctal(ref state, out atom))
                    {
                        pendingIsPlain = true;
                    }
                    else if (DecodeClassEscape(ref state, out atom, out var atomIsPlain))
                    {
                        pendingIsPlain = atomIsPlain;
                    }
                    else
                    {
                        return false;
                    }
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
            AppendSegment(wire, negated, units, categories, (state.Options & RegexOptions.IgnoreCase) != 0);
            return true;
        }

        static void AppendSegment(StringBuilder wire, bool negated, List<char> units, List<char> categories, bool fold)
        {
            if (!fold)
            {
                wire.Append(negated ? (char)1 : (char)0);
                wire.Append((char)units.Count);
                wire.Append((char)categories.Count);
                for (var i = 0; i < units.Count; i++)
                {
                    wire.Append(units[i]);
                }

                for (var i = 0; i < categories.Count; i++)
                {
                    wire.Append(categories[i]);
                }

                return;
            }

            var folded = new List<char>(units.Count + 4);
            folded.AddRange(units);
            for (var i = 0; i + 1 < units.Count; i += 2)
            {
                var rangeLow = units[i];
                var rangeHigh = units[i + 1];
                folded.Add(rangeLow);
                folded.Add(rangeHigh);
                for (var current = rangeLow; current < rangeHigh; current++)
                {
                    if (Utf8OwnedCaseFold.TryGetFold(current, out var foldLower, out var foldUpper))
                    {
                        folded.Add(foldLower);
                        folded.Add((char)(foldLower + 1));
                        folded.Add(foldUpper);
                        folded.Add((char)(foldUpper + 1));
                    }
                    else
                    {
                        folded.Add(current);
                        folded.Add((char)(current + 1));
                        folded.Add(current);
                        folded.Add((char)(current + 1));
                    }
                }
            }

            wire.Append(negated ? (char)1 : (char)0);
            wire.Append((char)folded.Count);
            wire.Append((char)categories.Count);
            for (var i = 0; i < folded.Count; i++)
            {
                wire.Append(folded[i]);
            }

            for (var i = 0; i < categories.Count; i++)
            {
                wire.Append(categories[i]);
            }
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
        static bool TryTakeClassOctal(ref ParserState state, out char value)
        {
            value = (char)0;
            if (state.Position + 1 >= state.Pattern.Length)
            {
                return false;
            }

            var head = state.Pattern[state.Position + 1];
            if (head < '1' || head > '9')
            {
                return false;
            }

            var length = 0;
            var number = 0;
            while (length < 3 &&
                state.Position + 1 + length < state.Pattern.Length &&
                state.Pattern[state.Position + 1 + length] >= '0' &&
                state.Pattern[state.Position + 1 + length] <= '7')
            {
                number = number * 8 + (state.Pattern[state.Position + 1 + length] - '0');
                length++;
            }

            if (length == 0)
            {
                return false;
            }

            value = (char)number;
            state.Position += 1 + length;
            return true;
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
                // Anchor escapes (A, B, G, Z, z) are BCL-invalid inside a
                // character class (UnrecognizedEscape): fall through to
                // DecodeEscape, which declines ASCII letters, so these
                // decline to the BCL fallback exactly like the oracle.
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
        static bool TryTakeShorthandAtom(ref ParserState state, out Utf8TreeNode? node)
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

        static Utf8TreeNode BuildSetNode(RegexOptions options, bool negated, List<char> units, List<char> categories)
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

            return new Utf8TreeNode(Utf8TreeNodeKind.Set, options, new string(text));
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
        static bool ParseNamedGroup(ref ParserState state, int depth, out Utf8TreeNode? node)
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

            var underExplicit = (state.Options & RegexOptions.ExplicitCapture) != RegexOptions.None;
            if (underExplicit && isPop)
            {
                return false;
            }

            var namedEntryOptions = state.Options;

            if (!ParseAlternation(ref state, depth + 1, out var body) || body is null)
            {
                return false;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                return false;
            }

            state.Position++;
            state.Options = namedEntryOptions;
            var group = new Utf8TreeNode(underExplicit ? Utf8TreeNodeKind.Group : Utf8TreeNodeKind.Capture, namedEntryOptions);
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
                if (declared == 0)
                {
                    return false;
                }

                if (name.Length > 1 && name[0] == '0')
                {
                    // BCL transparent-constrained definition: the content matches
                    // with no slot and no name (lowered exactly like (?:)), while
                    // the end pass requires the stripped slot to exist elsewhere.
                    var transparent = new Utf8TreeNode(Utf8TreeNodeKind.Group, namedEntryOptions);
                    transparent.Children = body;
                    body.Parent = transparent;
                    node = transparent;
                    state.RequiredSlots.Add(declared);
                    return true;
                }

                if (!underExplicit)
                {
                    group.Str = name;
                    group.M = declared;
                    group.N = -1;
                }

                if (declared != 0)
                {
                    state.GroupDefinitions.Add(new OwnedGroupDefinition { Node = group, Name = name, Declared = declared, IsAuto = false, Slot = declared });
                }

                return true;
            }

            if (IsAllAsciiDigits(name) || IsAsciiDigit(name[0]))
            {
                // BCL rejects every survivor of this branch: all-digit overflow
                // fails group-number construction, and digit-start names are
                // invalid group names. Only (?:)-style transparency above serves.
                return false;
            }

            if (!IsValidGroupName(name))
            {
                // BCL capture-name rule (machine-probed on .NET 10): every name
                // character is a Unicode word character (letters, decimal digits,
                // connector punctuation, nonspacing marks, ZWJ/ZWNJ). Anything
                // else declines to the BCL fallback exactly like the oracle.
                return false;
            }

            if (!underExplicit)
            {
                group.Str = name;
                group.M = -1;
                group.N = -1;
            }

            state.GroupDefinitions.Add(new OwnedGroupDefinition { Node = group, Name = name, Declared = -1, IsAuto = true, Slot = -1, UnderExplicit = underExplicit });

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

        static bool IsValidGroupName(string value)
        {
            // Machine-probed BCL capture-name rule: every character is a
            // Unicode word character (all letter kinds, decimal digits,
            // connector punctuation, nonspacing marks) or a zero-width
            // joiner. ASCII-digit starts are canonical or rejected above,
            // so digits here only admit non-ASCII numerals as name text.
            foreach (var character in value)
            {
                if (character == (char)0x200C || character == (char)0x200D)
                {
                    continue;
                }

                switch (char.GetUnicodeCategory(character))
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.OtherLetter:
                    case UnicodeCategory.NonSpacingMark:
                    case UnicodeCategory.DecimalDigitNumber:
                    case UnicodeCategory.ConnectorPunctuation:
                        break;
                    default:
                        return false;
                }
            }

            return value.Length != 0;
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
            ref Utf8TreeNode root,
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
                    if (!definition.UnderExplicit)
                    {
                        definition.Node.M = shared;
                    }

                    continue;
                }

                while (taken.Contains(nextFree))
                {
                    nextFree++;
                }

                definition.Slot = nextFree;
                taken.Add(nextFree);
                autoSlots[definition.Name] = nextFree;
                if (!definition.UnderExplicit)
                {
                    definition.Node.M = nextFree;
                }
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

            foreach (var required in state.RequiredSlots)
            {
                if (!slots.Contains(required))
                {
                    return false;
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
                if (!ResolveFixup(fixup, slots, slotByName, ref root, ref morphed))
                {
                    return false;
                }
            }

            root = MergeLiteralRuns(root);

            return true;
        }

        static bool ResolveFixup(OwnedBackrefFixup fixup, SortedSet<int> slots, Dictionary<string, int> slotByName, ref Utf8TreeNode root, ref bool morphed)
        {
            if (fixup.Node.Kind == Utf8TreeNodeKind.Empty && (fixup.Digits is not null || fixup.Name is not null))
            {
                return ResolveEmptyFixup(fixup, slots, slotByName, ref root);
            }

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
                var lookupName = fixup.Name;
                if (lookupName.Length > 1 && IsAllAsciiDigits(lookupName))
                {
                    lookupName = lookupName.TrimStart('0');
                }

                if (!slotByName.TryGetValue(lookupName, out var namedSlot))
                {
                    // BCL evaluates undefined conditional names as never-matching
                    // tests; only backreferences to undefined names fail construction.
                    // The node keeps its unbound marker, which no owned-tree reader
                    // consults for execution (slices parse raw patterns, fallbacks use
                    // BCL): matching always takes the else branch, exactly like BCL.
                    if (!fixup.IsConditional)
                    {
                        return false;
                    }

                    return true;
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

            if (fixup.IsConditional)
            {
                // BCL serves (?(0)...)/(?(00)...) as never-matching tests while
                // undefined nonzero numbers fail construction.
                if (fixup.Digits is not null &&
                    int.TryParse(fixup.Digits, out var conditionalNumber) &&
                    conditionalNumber == 0)
                {
                    return true;
                }

                return false;
            }

            return ResolveOctalFallback(fixup, ref root, ref morphed);
        }

        static bool ResolveEmptyFixup(OwnedBackrefFixup fixup, SortedSet<int> slots, Dictionary<string, int> slotByName, ref Utf8TreeNode root)
        {
            if (fixup.Name is not null)
            {
                if (!slotByName.TryGetValue(fixup.Name, out _))
                {
                    return true;
                }

                return true;
            }

            if (fixup.Digits is not null &&
                int.TryParse(fixup.Digits, out var backref) &&
                backref >= 1 &&
                slots.Contains(backref))
            {
                return true;
            }

            if (fixup.Digits is null || fixup.Digits.Length < 2)
            {
                return true;
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

            if (length == 0)
            {
                return false;
            }

            if (length == fixup.Digits.Length)
            {
                StripEmptyOptions(ref root, fixup.Node);
                return true;
            }

            Utf8TreeNode morphedLeaf;
            if ((fixup.Node.Options & RegexOptions.IgnoreCase) != 0 &&
                Utf8OwnedCaseFold.TryGetFold((char)value, out var emptyLower, out var emptyUpper))
            {
                morphedLeaf = FoldedSet(fixup.Node.Options, emptyLower, emptyUpper);
            }
            else
            {
                morphedLeaf = new Utf8TreeNode(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, (char)value);
                morphedLeaf.M = 0;
                morphedLeaf.N = 0;
            }

            ReplaceNodeInParent(ref root, fixup.Node, morphedLeaf);
            return true;
        }

        static void StripEmptyOptions(ref Utf8TreeNode root, Utf8TreeNode node)
        {
            if ((node.Options & RegexOptions.IgnoreCase) != 0)
            {
                var stripped = new Utf8TreeNode(Utf8TreeNodeKind.Empty, node.Options & ~RegexOptions.IgnoreCase);
                ReplaceNodeInParent(ref root, node, stripped);
            }
        }

        static bool ResolveOctalFallback(OwnedBackrefFixup fixup, ref Utf8TreeNode root, ref bool morphed)
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

            if (length == 0)
            {
                return false;
            }

            if (length != fixup.Digits.Length)
            {
                return ResolveOctalRest(fixup, ref root, ref morphed, length, value);
            }

            morphed = true;
            if (fixup.Wrapper is null)
            {
                if ((fixup.Node.Options & RegexOptions.IgnoreCase) != 0 &&
                    Utf8OwnedCaseFold.TryGetFold((char)value, out var octalLower, out var octalUpper))
                {
                    ReplaceNodeInParent(ref root, fixup.Node, FoldedSet(fixup.Node.Options, octalLower, octalUpper));
                    return true;
                }

                if ((fixup.Node.Options & RegexOptions.IgnoreCase) != 0)
                {
                    var stripped = new Utf8TreeNode(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, (char)value);
                    stripped.M = 0;
                    stripped.N = 0;
                    ReplaceNodeInParent(ref root, fixup.Node, stripped);
                    return true;
                }

                fixup.Node.Kind = Utf8TreeNodeKind.One;
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

            if ((wrapper.Options & RegexOptions.IgnoreCase) != 0 &&
                Utf8OwnedCaseFold.TryGetFold((char)value, out var wrapperLower, out var wrapperUpper))
            {
                var foldedLoop = FoldedSet(wrapper.Options, wrapperLower, wrapperUpper);
                foldedLoop.Kind = wrapper.Kind == Utf8TreeNodeKind.Lazyloop
                    ? Utf8TreeNodeKind.Setlazy
                    : Utf8TreeNodeKind.Setloop;
                foldedLoop.M = wrapper.M;
                foldedLoop.N = wrapper.N;
                ReplaceNodeInParent(ref root, wrapper, foldedLoop);
                return true;
            }

            if ((wrapper.Options & RegexOptions.IgnoreCase) != 0)
            {
                var strippedWrapper = new Utf8TreeNode(
                    wrapper.Kind == Utf8TreeNodeKind.Lazyloop
                        ? Utf8TreeNodeKind.Onelazy
                        : Utf8TreeNodeKind.Oneloop,
                    wrapper.Options & ~RegexOptions.IgnoreCase,
                    (char)value);
                strippedWrapper.M = wrapper.M;
                strippedWrapper.N = wrapper.N;
                ReplaceNodeInParent(ref root, wrapper, strippedWrapper);
                return true;
            }

            wrapper.Kind = wrapper.Kind == Utf8TreeNodeKind.Lazyloop
                ? Utf8TreeNodeKind.Onelazy
                : Utf8TreeNodeKind.Oneloop;
            wrapper.Ch = (char)value;
            wrapper.Children = null;
            return true;
        }

        static bool ResolveOctalRest(OwnedBackrefFixup fixup, ref Utf8TreeNode root, ref bool morphed, int prefixLength, int value)
        {
            Utf8TreeNode morphedLeaf;
            if ((fixup.Node.Options & RegexOptions.IgnoreCase) != 0 &&
                Utf8OwnedCaseFold.TryGetFold((char)value, out var restLower, out var restUpper))
            {
                morphedLeaf = FoldedSet(fixup.Node.Options, restLower, restUpper);
            }
            else
            {
                morphedLeaf = new Utf8TreeNode(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, (char)value);
                morphedLeaf.M = 0;
                morphedLeaf.N = 0;
            }

            var restText = fixup.Digits.Substring(prefixLength);
            var beforeAnchor = new List<Utf8TreeNode>(restText.Length);
            beforeAnchor.Add(morphedLeaf);
            for (var i = 0; i + 1 < restText.Length; i++)
            {
                beforeAnchor.Add(new Utf8TreeNode(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, restText[i]));
            }

            if (fixup.Wrapper is null)
            {
                beforeAnchor.Add(new Utf8TreeNode(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, restText[restText.Length - 1]));
                ReplaceNodeWithMany(ref root, fixup.Node, beforeAnchor, null);
                morphed = true;
                return true;
            }

            var restWrapper = fixup.Wrapper;
            if (restWrapper.ChildCount != 1 || !ReferenceEquals(restWrapper.Child(0), fixup.Node))
            {
                return false;
            }

            var loopChar = restText[restText.Length - 1];
            var loopKind = restWrapper.Kind == Utf8TreeNodeKind.Lazyloop
                ? Utf8TreeNodeKind.Onelazy
                : Utf8TreeNodeKind.Oneloop;
            var loopOptions = (restWrapper.Options & RegexOptions.IgnoreCase) != 0
                ? restWrapper.Options & ~RegexOptions.IgnoreCase
                : restWrapper.Options;
            var loopBody = new Utf8TreeNode(loopKind, loopOptions, loopChar);
            loopBody.M = restWrapper.M;
            loopBody.N = restWrapper.N;
            ReplaceNodeWithMany(ref root, restWrapper, beforeAnchor, loopBody);
            morphed = true;
            return true;
        }

        static void ReplaceNodeWithMany(ref Utf8TreeNode root, Utf8TreeNode oldNode, List<Utf8TreeNode> fresh, Utf8TreeNode? trailing)
        {
            var parent = oldNode.Parent;
            var sequence = new List<Utf8TreeNode>(fresh.Count + 1);
            sequence.AddRange(fresh);
            if (trailing is not null)
            {
                sequence.Add(trailing);
            }

            foreach (var node in sequence)
            {
                node.Parent = parent;
            }

            if (parent is null)
            {
                if (sequence.Count == 1)
                {
                    root = sequence[0];
                    return;
                }

                var top = new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, oldNode.Options);
                top.Children = sequence;
                root = top;
                return;
            }

            if (parent.Children is List<Utf8TreeNode> siblings)
            {
                for (var i = 0; i < siblings.Count; i++)
                {
                    if (ReferenceEquals(siblings[i], oldNode))
                    {
                        siblings.RemoveAt(i);
                        siblings.InsertRange(i, sequence);
                        return;
                    }
                }

                return;
            }

            parent.Children = new List<Utf8TreeNode>(sequence);
        }

        static Utf8TreeNode MergeLiteralRuns(Utf8TreeNode node)
        {
            if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop or Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Onelazy or Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy &&
                node.M == 0 &&
                node.N == 0)
            {
                return new Utf8TreeNode(Utf8TreeNodeKind.Empty, node.Options);
            }

            if (node.Kind == Utf8TreeNodeKind.Alternate && node.Children is List<Utf8TreeNode> branches)
            {
                for (var i = 0; i < branches.Count; i++)
                {
                    var mergedBranch = MergeLiteralRuns(branches[i]);
                    if (!ReferenceEquals(mergedBranch, branches[i]))
                    {
                        branches[i] = mergedBranch;
                        mergedBranch.Parent = node;
                    }
                }

                return node;
            }

            if (node.Children is List<Utf8TreeNode> children)
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

                if (node.Kind == Utf8TreeNodeKind.Concatenate && children.Count > 0)
                {
                    var removed = 0;
                    for (var i = 0; i < children.Count;)
                    {
                        if (children[i].Kind == Utf8TreeNodeKind.Empty)
                        {
                            children.RemoveAt(i);
                            removed++;
                        }
                        else
                        {
                            i++;
                        }
                    }

                    if (removed > 0)
                    {
                        if (children.Count == 0)
                        {
                            return new Utf8TreeNode(Utf8TreeNodeKind.Empty, node.Options);
                        }

                        if (children.Count == 1)
                        {
                            return children[0];
                        }
                    }
                }

                // Conditional branches stay unfused: the provider keeps yes/no as
                // parsed, so fusing them would invent Multi nodes it never builds.
                if (node.Kind == Utf8TreeNodeKind.BackreferenceConditional)
                {
                    return node;
                }

                var fused = new List<Utf8TreeNode>(children.Count);
                var pending = new List<Utf8TreeNode>();
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

                if (node.Kind != Utf8TreeNodeKind.Concatenate)
                {
                    return node;
                }

                return fused.Count == 1 ? fused[0] : node;
            }

            if (node.Children is Utf8TreeNode single)
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

        static bool TryMergeableText(Utf8TreeNode node, out char single, out string? text)
        {
            single = '\0';
            text = null;
            if (node.Kind == Utf8TreeNodeKind.One && node.Str is null)
            {
                single = node.Ch;
                return true;
            }

            if (node.Kind == Utf8TreeNodeKind.Multi && node.Str2 is null && node.Str is not null)
            {
                text = node.Str;
                return true;
            }

            return false;
        }

        static void FlushMergePending(Utf8TreeNode parent, List<Utf8TreeNode> fused, List<Utf8TreeNode> pending)
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
                if (literal.Kind == Utf8TreeNodeKind.One)
                {
                    run.Append(literal.Ch);
                }
                else
                {
                    run.Append(literal.Str);
                }
            }

            var merged = new Utf8TreeNode(Utf8TreeNodeKind.Multi, pending[0].Options, run.ToString());
            merged.Parent = parent;
            fused.Add(merged);
            pending.Clear();
        }
        static bool TryTakeLookaroundKind(ParserState state, out Utf8TreeNodeKind kind, out bool isLookbehind)
        {
            kind = Utf8TreeNodeKind.Concatenate;
            isLookbehind = false;
            if (state.AtEnd)
            {
                return false;
            }

            var marker = state.Pattern[state.Position];
            if (marker == '=')
            {
                kind = Utf8TreeNodeKind.PositiveLookaround;
                return true;
            }

            if (marker == '!')
            {
                kind = Utf8TreeNodeKind.NegativeLookaround;
                return true;
            }

            if (marker == '>')
            {
                kind = Utf8TreeNodeKind.Atomic;
                return true;
            }

            if (marker == '<' && state.Position + 1 < state.Pattern.Length)
            {
                var direction = state.Pattern[state.Position + 1];
                if (direction == '=')
                {
                    kind = Utf8TreeNodeKind.PositiveLookaround;
                    isLookbehind = true;
                    return true;
                }

                if (direction == '!')
                {
                    kind = Utf8TreeNodeKind.NegativeLookaround;
                    isLookbehind = true;
                    return true;
                }
            }

            return false;
        }

        static bool ParseLookaround(ref ParserState state, int depth, out Utf8TreeNode? node, Utf8TreeNodeKind kind, bool isLookbehind)
        {
            node = null;
            // state.Position is at the opener marker after "(?". Lookbehind
            // consumes its direction marker too and forces RightToLeft below.
            state.Position += isLookbehind ? 2 : 1;

            var savedOptions = state.Options;
            var bodyOptions = kind == Utf8TreeNodeKind.Atomic
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
            if (isLookbehind && ContainsLookbehindBackreference(body))
            {
                state.Options = savedOptions;
                return false;
            }

            var options = bodyOptions;
            var assertion = new Utf8TreeNode(kind, options);
            assertion.Children = body;
            body.Parent = assertion;
            node = assertion;
            state.Options = savedOptions;
            return true;
        }

        static bool ContainsLookbehindBackreference(Utf8TreeNode node)
        {
            // Captures inside lookbehind serve BCL-exactly (left-to-right numbering
            // with values populated); only backreferences stay declined since their
            // right-to-left capture timing has no owned execution proof.
            if (node.Kind == Utf8TreeNodeKind.Backreference)
            {
                return true;
            }

            for (var i = 0; i < node.ChildCount; i++)
            {
                if (ContainsLookbehindBackreference(node.Child(i)))
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
        static bool TryTakeToggle(ref ParserState state, int depth)
        {
            state.Position += 2;
            var on = RegexOptions.None;
            var off = RegexOptions.None;
            while (!state.AtEnd && IsScopeFlag(state.Pattern[state.Position]))
            {
                on |= ScopeFlag(state.Pattern[state.Position]);
                state.Position++;
            }

            while (!state.AtEnd && state.Pattern[state.Position] == '-')
            {
                state.Position++;
            }

            while (!state.AtEnd && IsScopeFlag(state.Pattern[state.Position]))
            {
                off |= ScopeFlag(state.Pattern[state.Position]);
                state.Position++;
            }

            while (!state.AtEnd && state.Pattern[state.Position] == '-')
            {
                state.Position++;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                return false;
            }

            state.Position++;
            state.Options = (state.Options | on) & ~off;
            if ((on & RegexOptions.ExplicitCapture) != RegexOptions.None)
            {
                state.ExplicitDepth++;
            }
            else if ((off & RegexOptions.ExplicitCapture) != RegexOptions.None && state.ExplicitDepth > 0)
            {
                state.ExplicitDepth--;
            }
            if (depth == 0 && state.Depth0Branch == 0 && state.Depth0Atom == 0)
            {
                state.TreeSeen = (state.TreeSeen | on) & ~off;
            }

            return true;
        }

        static bool TryTakeOptionScope(ref ParserState state, out RegexOptions scoped, out RegexOptions scopeOn, out RegexOptions scopeOff)
        {
            scoped = state.Options;
            scopeOn = RegexOptions.None;
            scopeOff = RegexOptions.None;
            while (!state.AtEnd && IsScopeFlag(state.Pattern[state.Position]))
            {
                scopeOn |= ScopeFlag(state.Pattern[state.Position]);
                state.Position++;
            }
            while (!state.AtEnd && state.Pattern[state.Position] == '-')
            {
                state.Position++;
            }
            while (!state.AtEnd && IsScopeFlag(state.Pattern[state.Position]))
            {
                scopeOff |= ScopeFlag(state.Pattern[state.Position]);
                state.Position++;
            }
            while (!state.AtEnd && state.Pattern[state.Position] == '-')
            {
                state.Position++;
            }
            if (state.AtEnd || state.Pattern[state.Position] != ':')
            {
                return false;
            }
            state.Position++;
            scoped = (state.Options | scopeOn) & ~scopeOff;

            return true;
        }
        static bool IsIgnoredWhitespace(char character)
        {
            return (character >= (char)9 && character <= (char)13) ||
                character == (char)32 ||
                character == (char)133 ||
                character == (char)160 ||
                character == (char)5760 ||
                (character >= (char)8192 && character <= (char)8202) ||
                character == (char)8232 ||
                character == (char)8233 ||
                character == (char)8239 ||
                character == (char)8287 ||
                character == (char)12288;
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
                character != '$';
        }

        static bool IsQuantifierStart(char character)
        {
            return character == '*' || character == '+' || character == '?' || character == '{';
        }

        static Utf8TreeNode One(ParserState state, char character)
        {
            return new Utf8TreeNode(Utf8TreeNodeKind.One, state.Options & ~RegexOptions.IgnoreCase, character);
        }

        static Utf8TreeNode Multi(ParserState state, string text)
        {
            return new Utf8TreeNode(Utf8TreeNodeKind.Multi, state.Options & ~RegexOptions.IgnoreCase, text);
        }

        static Utf8TreeNode FoldedSet(RegexOptions options, char lower, char upper)
        {
            var wire = new string(new char[]
            {
                (char)0, (char)4, (char)0, lower, (char)(lower + 1), upper, (char)(upper + 1),
            });

            return new Utf8TreeNode(Utf8TreeNodeKind.Set, options & ~RegexOptions.IgnoreCase, wire)
            {
                M = 0,
                N = 0,
            };
        }

        static void ReplaceNodeInParent(ref Utf8TreeNode root, Utf8TreeNode oldNode, Utf8TreeNode fresh)
        {
            var parent = oldNode.Parent;
            fresh.Parent = parent;
            if (parent is null)
            {
                root = fresh;
                return;
            }

            if (parent.Children is List<Utf8TreeNode> siblings)
            {
                for (var i = 0; i < siblings.Count; i++)
                {
                    if (ReferenceEquals(siblings[i], oldNode))
                    {
                        siblings[i] = fresh;
                        return;
                    }
                }

                return;
            }

            parent.Children = fresh;
        }

        static Utf8TreeNode Concatenate(RegexOptions options, List<Utf8TreeNode> children)
        {
            var node = new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, options);
            node.Children = children;
            foreach (var child in children)
            {
                child.Parent = node;
            }

            return node;
        }

        static Utf8TreeNode Alternate(RegexOptions options, List<Utf8TreeNode> branches)
        {
            var node = new Utf8TreeNode(Utf8TreeNodeKind.Alternate, options);
            node.Children = branches;
            foreach (var branch in branches)
            {
                branch.Parent = node;
            }

            return node;
        }

        static bool TryTakeConditional(ref ParserState state, int depth, out Utf8TreeNode? node)
        {
            node = null;
            // state.Position is at '(' after "(?".
            state.Position++;
            string? digits = null;
            string? name = null;
            if (!state.AtEnd && state.Pattern[state.Position] >= '0' && state.Pattern[state.Position] <= '9')
            {
                // Leading zeros ride the strict digit path: the end pass resolves
                // the stripped number, serves (?(0)...)/(?(00)...) as
                // never-matching tests, and declines undefined nonzero numbers.
                var digitsStart = state.Position;
                while (!state.AtEnd && state.Pattern[state.Position] >= '0' && state.Pattern[state.Position] <= '9')
                {
                    state.Position++;
                }

                digits = state.Pattern.Substring(digitsStart, state.Position - digitsStart);
                if (!TryParseGroupNumber(digits, out _))
                {
                    return false;
                }
            }
            else if (!state.AtEnd && IsAsciiConditionNameStart(state.Pattern[state.Position]))
            {
                var nameStart = state.Position;
                while (!state.AtEnd && IsAsciiConditionNameChar(state.Pattern[state.Position]))
                {
                    state.Position++;
                }

                name = state.Pattern.Substring(nameStart, state.Position - nameStart);
            }
            else
            {
                return false;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                return false;
            }

            state.Position++;
            var conditionalOptions = state.Options;
            var yesIsEmpty = !state.AtEnd && (state.Pattern[state.Position] == '|' || state.Pattern[state.Position] == ')');

            Utf8TreeNode yesBranch;
            if (yesIsEmpty)
            {
                yesBranch = new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, conditionalOptions);
            }
            else
            {
                if (!ParseConcatenation(ref state, depth + 1, out var parsedYes) || parsedYes is null)
                {
                    return false;
                }

                yesBranch = parsedYes;
            }

            List<Utf8TreeNode>? branches = null;
            if (!state.AtEnd && state.Pattern[state.Position] == '|')
            {
                state.Position++;
                Utf8TreeNode noBranch;
                if (!state.AtEnd && state.Pattern[state.Position] == ')')
                {
                    noBranch = new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, conditionalOptions);
                }
                else
                {
                    if (!ParseConcatenation(ref state, depth + 1, out var parsedNo) || parsedNo is null)
                    {
                        return false;
                    }

                    noBranch = parsedNo;
                }

                if (state.AtEnd || state.Pattern[state.Position] != ')')
                {
                    return false;
                }

                state.Position++;
                branches = new List<Utf8TreeNode> { yesBranch, noBranch };
            }
            else if (!state.AtEnd && state.Pattern[state.Position] == ')')
            {
                state.Position++;
                branches = yesIsEmpty
                    ? new List<Utf8TreeNode> { yesBranch }
                    : new List<Utf8TreeNode> { yesBranch, new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, conditionalOptions) };
            }
            else
            {
                return false;
            }

            var conditional = new Utf8TreeNode(Utf8TreeNodeKind.BackreferenceConditional, conditionalOptions);
            if (name is not null)
            {
                conditional.Str = name;
                conditional.N = 0;
            }
            else
            {
                conditional.N = -1;
            }

            conditional.M = -1;
            conditional.Children = branches;
            foreach (var branch in branches)
            {
                branch.Parent = conditional;
            }

            node = conditional;
            state.BackrefFixups.Add(new OwnedBackrefFixup { Node = conditional, Digits = digits, Name = name, IsConditional = true });
            return true;
        }

        static bool IsAsciiConditionNameStart(char character)
        {
            return (character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                character == '_';
        }

        static bool IsAsciiConditionNameChar(char character)
        {
            return IsAsciiConditionNameStart(character) || (character >= '0' && character <= '9');
        }

        static bool ParseGroup(ref ParserState state, int depth, out Utf8TreeNode? node)
        {
            node = null;
            state.Position++;
            var kind = Utf8TreeNodeKind.Capture;
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

                if (!state.AtEnd && state.Pattern[state.Position] == '(')
                {
                    return TryTakeConditional(ref state, depth, out node);
                }

                if (!TryTakeOptionScope(ref state, out var scopedOptions, out var scopeOn, out var scopeOff))
                {
                    return false;
                }

                var isLeadingScope = depth == 0 && state.Depth0Branch == 0 && state.Depth0Atom == 0;
                if (isLeadingScope)
                {
                    state.TreeSeen = (state.TreeSeen | scopeOn) & ~scopeOff;
                    state.LeadingContribution = scopeOn & ~scopeOff;
                }

                var savedOptions = state.Options;
                state.Options = scopedOptions;
                var scopeEnteredExplicit = (scopedOptions & RegexOptions.ExplicitCapture) != RegexOptions.None;
                if (scopeEnteredExplicit)
                {
                    state.ExplicitDepth++;
                }

                if (!ParseAlternation(ref state, depth + 1, out var scopeBody) || scopeBody is null)
                {
                    return false;
                }

                if (state.AtEnd || state.Pattern[state.Position] != ')')
                {
                    return false;
                }

                state.Position++;
                var scopeGroup = new Utf8TreeNode(Utf8TreeNodeKind.Group, scopedOptions);
                scopeGroup.Children = scopeBody;
                scopeBody.Parent = scopeGroup;
                node = scopeGroup;
                if (isLeadingScope && (scopeOn & ~scopeOff) != RegexOptions.None)
                {
                    state.LeadingGroup = scopeGroup;
                }

                if (scopeEnteredExplicit && state.ExplicitDepth > 0)
                {
                    state.ExplicitDepth--;
                }

                state.Options = savedOptions;
                return true;
            }
            else if (kind == Utf8TreeNodeKind.Capture &&
                (state.Options & RegexOptions.ExplicitCapture) != RegexOptions.None)
            {
                kind = Utf8TreeNodeKind.Group;
            }
            else
            {
                slot = ++state.Groups;
            }

            var groupEntryOptions = state.Options;

            if (!ParseAlternation(ref state, depth + 1, out var body) || body is null)
            {
                return false;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                return false;
            }

            state.Position++;
            state.Options = groupEntryOptions;
            var group = new Utf8TreeNode(kind, groupEntryOptions);
            if (kind == Utf8TreeNodeKind.Capture)
            {
                group.M = slot;
                group.N = -1;
            }

            group.Children = body;
            body.Parent = group;
            node = group;
            return true;
        }

        static bool ParseQuantifier(ref ParserState state, ref Utf8TreeNode atom, out bool decayedBrace)
        {
            decayedBrace = false;
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
                if (!ParseBrace(ref state, out minimum, out maximum, out var braceDecayed))
                {
                    if (braceDecayed)
                    {
                        decayedBrace = true;
                        return true;
                    }

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

        // Parses a '{m}', '{m,}' or '{m,n}' quantifier tail with the position on '{'.
        // Returns true with minimum/maximum set and the position past '}' for a complete
        // valid shape. A brace that cannot complete (non-digit opener, unclosed digits,
        // closed garbage) decays to a literal: the position rewinds past '{' and
        // decayToLiteral is set, exactly like BCL. Only a complete invalid shape
        // (reversed bounds, numeric overflow) rejects.
        static bool ParseBrace(ref ParserState state, out int minimum, out int maximum, out bool decayToLiteral)
        {
            minimum = 0;
            maximum = 0;
            decayToLiteral = false;
            var braceStart = state.Position;
            state.Position++;
            if (state.AtEnd || state.Pattern[state.Position] < '0' || state.Pattern[state.Position] > '9')
            {
                state.Position = braceStart + 1;
                decayToLiteral = true;
                return false;
            }

            var overflow = false;
            var minimumValue = 0L;
            while (!state.AtEnd && state.Pattern[state.Position] >= '0' && state.Pattern[state.Position] <= '9')
            {
                var next = minimumValue * 10 + (state.Pattern[state.Position] - '0');
                if (next > Infinite)
                {
                    overflow = true;
                }
                else
                {
                    minimumValue = next;
                }

                state.Position++;
            }

            var maximumValue = minimumValue;
            var maximumInfinite = false;
            var hasMaximum = false;
            if (!state.AtEnd && state.Pattern[state.Position] == ',')
            {
                state.Position++;
                if (!state.AtEnd && state.Pattern[state.Position] == '}')
                {
                    maximumInfinite = true;
                }
                else if (!state.AtEnd && state.Pattern[state.Position] >= '0' && state.Pattern[state.Position] <= '9')
                {
                    maximumValue = 0;
                    hasMaximum = true;
                    while (!state.AtEnd && state.Pattern[state.Position] >= '0' && state.Pattern[state.Position] <= '9')
                    {
                        var nextMaximum = maximumValue * 10 + (state.Pattern[state.Position] - '0');
                        if (nextMaximum > Infinite)
                        {
                            overflow = true;
                        }
                        else
                        {
                            maximumValue = nextMaximum;
                        }

                        state.Position++;
                    }
                }
                else
                {
                    state.Position = braceStart + 1;
                    decayToLiteral = true;
                    return false;
                }
            }

            if (state.AtEnd || state.Pattern[state.Position] != '}')
            {
                state.Position = braceStart + 1;
                decayToLiteral = true;
                return false;
            }

            if (overflow || (hasMaximum && maximumValue < minimumValue))
            {
                return false;
            }

            minimum = (int)minimumValue;
            maximum = maximumInfinite ? Infinite : (int)maximumValue;
            state.Position++;
            return true;
        }

        static Utf8TreeNode Quantify(ParserState state, Utf8TreeNode atom, int minimum, int maximum, bool lazy)
        {
            if (minimum == 1 && maximum == 1)
            {
                return atom;
            }

            if (maximum == 0)
            {
                return new Utf8TreeNode(Utf8TreeNodeKind.Empty, atom.Options);
            }

            if (atom.Kind == Utf8TreeNodeKind.One)
            {
                return new Utf8TreeNode(
                    lazy ? Utf8TreeNodeKind.Onelazy : Utf8TreeNodeKind.Oneloop,
                    state.Options & ~RegexOptions.IgnoreCase,
                    atom.Ch)
                {
                    M = minimum,
                    N = maximum,
                };
            }

            if (atom.Kind == Utf8TreeNodeKind.Set)
            {
                return new Utf8TreeNode(
                    lazy ? Utf8TreeNodeKind.Setlazy : Utf8TreeNodeKind.Setloop,
                    state.Options & ~RegexOptions.IgnoreCase,
                    atom.Str)
                {
                    M = minimum,
                    N = maximum,
                };
            }

            // Loop wrappers carry the atom options, not the site options: a directly
            // wrapped scope marks the loop with its scoped options in the provider shape.
            // All other atoms carry entry options identical to the site options.
            var loop = new Utf8TreeNode(
                lazy ? Utf8TreeNodeKind.Lazyloop : Utf8TreeNodeKind.Loop,
                atom.Options,
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
        public int Depth0Branch;
        public int Depth0Atom;
        public RegexOptions LeadingContribution;
        public Utf8TreeNode? LeadingGroup;
        public int ExplicitDepth;
        public List<OwnedGroupDefinition> GroupDefinitions;
        public List<OwnedBackrefFixup> BackrefFixups;
        public List<int> RequiredSlots;

        public ParserState(string pattern, RegexOptions options)
        {
            Pattern = pattern;
            Options = options;
            Position = 0;
            Groups = 0;
            TreeSeen = RegexOptions.None;
            Depth0Branch = 0;
            Depth0Atom = 0;
            LeadingContribution = RegexOptions.None;
            LeadingGroup = null;
            ExplicitDepth = 0;
            GroupDefinitions = new List<OwnedGroupDefinition>();
            BackrefFixups = new List<OwnedBackrefFixup>();
            RequiredSlots = new List<int>();
        }

        public bool AtEnd => Position >= Pattern.Length;
    }

    // A named or numbered group definition: unnamed groups never appear here.
    // Auto-named groups take a pending slot that the end pass assigns; explicit
    // numbers claim their literal slot (zero is a pure alias and is not recorded).
    private sealed class OwnedGroupDefinition
    {
        public required Utf8TreeNode Node { get; init; }
        public string Name = string.Empty;
        public int Declared = -1;
        public bool IsAuto;
        public bool UnderExplicit;
        public int Slot;
    }

    // A backreference or balance pop awaiting end-pass resolution, when group
    // numbering is final. Numeric tentatives carry their digit text and an
    // optional quantified wrapper for the octal outcome.
    private sealed class OwnedBackrefFixup
    {
        public required Utf8TreeNode Node { get; init; }
        public string? Digits;
        public string? Name;
        public bool IsPop;
        public bool IsConditional;
        public Utf8TreeNode? Wrapper;
    }
}