using System.Globalization;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Authoritative owned grammar for planning and scalar UTF-8 VM lowering.
// Lexical options, class subtraction, captures and references are resolved
// once. The VM consumes this tree and its capture catalog without reparsing.
// Syntax failures return owned diagnostics. Class payloads use full scalar
// endpoints and integer extents, with admitted storage and no 16-bit length cap.
// Parsing lives in static locals over an explicit state record, so the debt
// ceiling sees a single entry point.
internal static class Utf8OwnedTreeParser
{
    private const int Infinite = 2147483647;

    public static Utf8OwnedRegexTree? TryParse(string pattern, RegexOptions options)
        => TryParse(pattern, options, out _);

    internal static Utf8OwnedRegexTree? TryParse(string pattern, RegexOptions options, out Utf8PatternDiagnostic diagnostic)
    {
        diagnostic = default;
        using var construction = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(construction.Budget);
        using var frames = Utf8ConstructionFrames<OwnedParseFrame>.Create(construction.Budget,
            new(OwnedParseProduction.Alternation, 0));
        // Admit the root before ParserState acquires its remaining collection
        // storage. Context is a value, rather than a separately acquired object.
        var context = new OwnedParseContext(new ParserState(pattern, options, storage));
        try
        {
            while (frames.Count != 0 && !context.Failed)
            {
                construction.Budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                bool suspended;
                OwnedParseFrame child;
                try { suspended = Advance(ref context, ref frame, out child); }
                finally { frames[index] = frame; }
                if (suspended) frames.Push(child);
                else frames.Pop();
            }
            frames.Dispose(); // Release continuation-array scratch before numbering.
            var state = context.State;
            var root = context.Node;
            if (context.Failed || root is null || state.Position != pattern.Length)
            {
                diagnostic = state.Failure.Error != Utf8PatternError.None ? state.Failure
                    : new(state.Position < pattern.Length && pattern[state.Position] == ')'
                        ? Utf8PatternError.UnmatchedClosingParenthesis : Utf8PatternError.InvalidSyntax, state.Position);
                return null;
            }

            if ((options & RegexOptions.IgnoreCase) == 0 &&
                state.LeadingContribution != RegexOptions.None &&
                (state.LeadingGroup is null || !ReferenceEquals(root, state.LeadingGroup)))
            {
                // A leading scope that does not cover the whole pattern must not leak
                // its options onto the top composite. Per-node lexical options
                // remain authoritative for scalar execution.
                state.TreeSeen &= ~state.LeadingContribution;
            }

            var treeOptions = options | state.TreeSeen;
            if (root.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate)
            {
                var top = Utf8TreeNode.Create(root.Kind, treeOptions);
                top.Ch = root.Ch;
                top.Str = root.Str;
                top.M = root.M;
                top.N = root.N;
                top.Children = root.Children;
                if (top.Children is Utf8ConstructionBuffer<Utf8TreeNode> topChildren)
                {
                    foreach (var topChild in topChildren)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        topChild.Parent = top;
                    }
                }

                root = top;
            }

            bool numbered;
            int captureCount;
            string[] captureNames;
            IReadOnlyDictionary<string, int>? captureNameToNumber;
            IReadOnlyDictionary<int, int>? captureSparseMapping;
            try
            {
                numbered = FinishNumbering(ref state, ref root, out captureCount, out captureNames,
                    out captureNameToNumber, out captureSparseMapping);
            }
            finally { context.State = state; }
            if (!numbered)
            {
                diagnostic = state.Failure.Error != Utf8PatternError.None ? state.Failure
                    : new(Utf8PatternError.UndefinedCapture, state.Position);
                return null;
            }

            var tree = Utf8TreeNode.Create(Utf8TreeNodeKind.Capture, treeOptions & ~RegexOptions.IgnoreCase);
            tree.M = 0;
            tree.N = -1;
            tree.Children = root;
            root.Parent = tree;
            var culture = state.ConstructionCulture;
            FreezeChildren(storage, tree);
            var result = Utf8OwnedRegexTree.Create(tree, captureCount, captureNames, captureNameToNumber,
                captureSparseMapping, treeOptions, culture, state.CaptureCatalog,
                state.UsesLiteralClassBracketSyntax, state.UsesIgnoreCase);
            storage.Complete();
            construction.Complete();
            return result;

        }
        finally
        {
            context.State.GroupDefinitions?.Dispose();
            context.State.BackrefFixups?.Dispose();
            context.State.RequiredSlots?.Dispose();
            context.State.CapturePositions?.Dispose();
        }

        static bool Advance(ref OwnedParseContext context, ref OwnedParseFrame frame, out OwnedParseFrame child)
        {
            return frame.Production switch
            {
                OwnedParseProduction.Alternation => ParseAlternation(ref context, ref frame, out child),
                OwnedParseProduction.Concatenation => ParseConcatenation(ref context, ref frame, out child),
                OwnedParseProduction.Group => ParseGroup(ref context, ref frame, out child),
                OwnedParseProduction.NamedGroup => ParseNamedGroup(ref context, ref frame, out child),
                OwnedParseProduction.Lookaround => ParseLookaround(ref context, ref frame, out child),
                OwnedParseProduction.Conditional => TryTakeConditional(ref context, ref frame, out child),
                _ => throw new InvalidOperationException("Unknown owned grammar frame."),
            };
        }

        static bool ParseAlternation(ref OwnedParseContext context, ref OwnedParseFrame frame, out OwnedParseFrame child)
        {
            child = default;
            var state = context.State;
            if (frame.Nodes is null)
            {
                frame.Nodes = state.Storage.Buffer<Utf8TreeNode>();
                frame.EntryOptions = state.Options;
            }
            var branches = frame.Nodes;
            if (frame.Stage == 1)
            {
                var branch = context.Node;
                if (branch is null) { context.Fail(state); return false; }
                branches.Add(branch);
                if (state.AtEnd || state.Pattern[state.Position] != '|')
                {
                    context.Complete(state, branches.Count == 1 ? branches[0] : Alternate(frame.EntryOptions, branches));
                    return false;
                }
                state.Position++;
            }
            Utf8ConstructionScope.ChargeWork(1);
            if (frame.Depth == 0) state.Depth0Branch = branches.Count;
            context.State = state;
            frame.Stage = 1;
            child = new(OwnedParseProduction.Concatenation, frame.Depth);
            return true;
        }

        static bool ParseConcatenation(ref OwnedParseContext context, ref OwnedParseFrame frame, out OwnedParseFrame child)
        {
            child = default;
            var state = context.State;
            var depth = frame.Depth;
            Utf8TreeNode? node = null;
            if (frame.Nodes is null)
            {
                frame.Nodes = state.Storage.Buffer<Utf8TreeNode>();
                frame.Run = state.Storage.Text();
                frame.EntryOptions = state.Options;
            }
            var children = frame.Nodes;
            var run = frame.Run ?? throw new InvalidOperationException("Missing concatenation text storage.");
            var concatenationEntryOptions = frame.EntryOptions;
            while (frame.Stage == 1 || !state.AtEnd)
            {
                Utf8TreeNode atom;
                if (frame.Stage == 1)
                {
                    frame.Stage = 0;
                    var group = context.Node;
                    if (group is null) { context.Fail(state); return false; }
                    atom = group;
                }
                else
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    var character = state.Pattern[state.Position];
                    if (character == '|' || character == ')')
                    {
                        break;
                    }

                    if ((state.Options & RegexOptions.IgnorePatternWhitespace) != 0)
                    {
                        if (character == '#')
                        {
                            while (!state.AtEnd && state.Pattern[state.Position] != (char)10)
                            {
                                Utf8ConstructionScope.ChargeWork(1);
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

                    state.UsesIgnoreCase |= (state.Options & RegexOptions.IgnoreCase) != 0;

                    if (character == '\\')
                    {
                        if (TryAnchorEscape(ref state, out var anchor) && anchor is not null)
                        {
                            FlushRun(state, children, run);
                            var anchorAtom = anchor;
                            if (!ParseQuantifier(ref state, ref anchorAtom, out var decayedAnchor))
                            {
                                context.Fail(state);
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
                                context.Fail(state);
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
                            var tentative = Utf8TreeNode.Create(Utf8TreeNodeKind.Backreference, state.Options);
                            tentative.N = -1;
                            var backrefAtom = tentative;
                            if (!ParseQuantifier(ref state, ref backrefAtom, out var decayedBackref))
                            {
                                context.Fail(state);
                                return false;
                            }

                            if (decayedBackref)
                            {
                                run.Append('{');
                            }

                            if (backrefAtom.Kind == Utf8TreeNodeKind.Empty)
                            {
                                state.AddBackrefFixup(new OwnedBackrefFixup(backrefAtom, backrefDigits, state.Position, null, false, false, null));
                            }
                            else if (ReferenceEquals(backrefAtom, tentative))
                            {
                                state.AddBackrefFixup(new OwnedBackrefFixup(tentative, backrefDigits, state.Position, null, false, false, null));
                            }
                            else if (backrefAtom.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop &&
                                backrefAtom.ChildCount == 1 &&
                                ReferenceEquals(backrefAtom.Child(0), tentative))
                            {
                                state.AddBackrefFixup(new OwnedBackrefFixup(tentative, backrefDigits, state.Position, null, false, false, backrefAtom));
                            }

                            children.Add(backrefAtom);
                            continue;
                        }

                        var backrefName = TryScanNamedBackref(ref state);
                        if (backrefName is not null)
                        {
                            var referenceEnd = state.Position;
                            FlushRun(state, children, run);
                            var namedBackref = Utf8TreeNode.Create(Utf8TreeNodeKind.Backreference, state.Options);
                            namedBackref.Str = backrefName;
                            namedBackref.N = 0;
                            var namedAtom = namedBackref;
                            if (!ParseQuantifier(ref state, ref namedAtom, out var decayedNamed))
                            {
                                context.Fail(state);
                                return false;
                            }

                            if (decayedNamed)
                            {
                                run.Append('{');
                            }

                            if (namedAtom.Kind == Utf8TreeNodeKind.Empty)
                            {
                                state.AddBackrefFixup(new OwnedBackrefFixup(namedAtom, null, referenceEnd, backrefName, false, false, null));
                                children.Add(namedAtom);
                                continue;
                            }

                            state.AddBackrefFixup(new OwnedBackrefFixup(namedBackref, null, referenceEnd, backrefName, false, false, null));
                            children.Add(namedAtom);
                            continue;
                        }

                        if (!DecodeEscape(ref state, out var escaped))
                        {
                            context.Fail(state);
                            return false;
                        }

                        if (char.IsSurrogate(escaped))
                        {
                            if (char.IsHighSurrogate(escaped) && TryTakeLowSurrogate(ref state, out var low))
                            {
                                run.Append(escaped).Append(low);
                                continue;
                            }
                            FlushRun(state, children, run);
                            var impossible = Utf8TreeNode.Create(Utf8TreeNodeKind.Nothing, state.Options);
                            if (!ParseQuantifier(ref state, ref impossible, out _))
                            {
                                context.Fail(state);
                                return false;
                            }
                            children.Add(impossible);
                            continue;
                        }

                        if ((state.Options & RegexOptions.IgnoreCase) != 0 &&
                            Utf8OwnedCaseFold.TryGetFold(escaped, state.CaseBehavior, out var escapeLower, out var escapeUpper))
                        {
                            FlushRun(state, children, run);
                            var escapedAtom = FoldedSet(state.Options, escapeLower, escapeUpper);
                            if (!ParseQuantifier(ref state, ref escapedAtom, out var decayedEscaped))
                            {
                                context.Fail(state);
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
                        if (char.IsSurrogate(character))
                        {
                            state.Position++;
                            if (char.IsHighSurrogate(character) && TryTakeLowSurrogate(ref state, out var low))
                            {
                                run.Append(character).Append(low);
                                continue;
                            }
                            FlushRun(state, children, run);
                            var impossible = Utf8TreeNode.Create(Utf8TreeNodeKind.Nothing, state.Options);
                            if (!ParseQuantifier(ref state, ref impossible, out _))
                            {
                                context.Fail(state);
                                return false;
                            }
                            children.Add(impossible);
                            continue;
                        }

                        if ((state.Options & RegexOptions.IgnoreCase) != 0 &&
                            Utf8OwnedCaseFold.TryGetFold(character, state.CaseBehavior, out var foldLower, out var foldUpper))
                        {
                            FlushRun(state, children, run);
                            state.Position++;
                            var foldedAtom = FoldedSet(state.Options, foldLower, foldUpper);
                            if (!ParseQuantifier(ref state, ref foldedAtom, out var decayedFolded))
                            {
                                context.Fail(state);
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
                        // A literal supplementary scalar is one quantified atom.
                        var lastWidth = run.Length >= 2 && char.IsHighSurrogate(run[run.Length - 2]) &&
                            char.IsLowSurrogate(run[run.Length - 1]) ? 2 : 1;
                        var last = run[run.Length - 1];
                        var supplementaryLiteral = lastWidth == 2 ? run.ToText(run.Length - 2, 2) : null;
                        run.Truncate(run.Length - lastWidth);
                        FlushRun(state, children, run);
                        var unit = supplementaryLiteral is not null ? Multi(state, supplementaryLiteral) : One(state, last);
                        if (!ParseQuantifier(ref state, ref unit, out var decayedUnit))
                        {
                            context.Fail(state);
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
                            context.Fail(state);
                            return false;
                        }

                        continue;
                    }

                    if (character == '(' &&
                        state.Position + 2 < state.Pattern.Length &&
                        state.Pattern[state.Position + 1] == '?'
                        && (IsScopeFlag(state.Pattern[state.Position + 2]) || state.Pattern[state.Position + 2] is '-' or '+'))
                    {
                        if (state.ExpressionBranchDepth == depth)
                        {
                            context.Fail(state);
                            return false;
                        }
                        var toggleStart = state.Position;
                        FlushRun(state, children, run);
                        if (TryTakeToggle(ref state, depth))
                        {
                            continue;
                        }

                        state.Position = toggleStart;
                    }

                    FlushRun(state, children, run);
                    if (character == '^')
                    {
                        var anchorKind = (state.Options & RegexOptions.Multiline) == 0 ?
                            Utf8TreeNodeKind.Beginning :
                            Utf8TreeNodeKind.Bol;
                        atom = Utf8TreeNode.Create(anchorKind, state.Options);
                        state.Position++;
                    }
                    else if (character == '$')
                    {
                        var anchorKind = (state.Options & RegexOptions.Multiline) == 0 ?
                            Utf8TreeNodeKind.EndZ :
                            Utf8TreeNodeKind.Eol;
                        atom = Utf8TreeNode.Create(anchorKind, state.Options);
                        state.Position++;
                    }
                    else if (character == '.')
                    {
                        atom = CreateDotNode(state.Options);
                        state.Position++;
                    }
                    else if (character == '[')
                    {
                        if (!ParseClass(ref state, out var classNode) || classNode is null)
                        {
                            context.Fail(state);
                            return false;
                        }

                        atom = classNode;
                    }
                    else if (character == '{')
                    {
                        // A leading brace that cannot complete decays (BCL shapes like
                        // brace-a or brace-12); complete or invalid shapes still decline.
                        if (!ParseBrace(ref state, out _, out _, out _, out var decayedBrace) && decayedBrace)
                        {
                            run.Append('{');
                            continue;
                        }

                        state.Fail(Utf8PatternError.MissingQuantifierOperand);
                        context.Fail(state);
                        return false;
                    }
                    else if (character != '(')
                    {
                        state.Fail(IsQuantifierStart(character) ? Utf8PatternError.MissingQuantifierOperand : Utf8PatternError.InvalidSyntax);
                        context.Fail(state);
                        return false;
                    }
                    else
                    {
                        context.State = state;
                        frame.Stage = 1;
                        child = new(OwnedParseProduction.Group, depth);
                        return true;
                    }
                }

                if (!ParseQuantifier(ref state, ref atom, out var decayedAtom))
                {
                    context.Fail(state);
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
                node = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, concatenationEntryOptions);
                context.Complete(state, node);
                return false;
            }

            node = children.Count == 1 ? children[0] : Concatenate(concatenationEntryOptions, children);
            context.Complete(state, node);
            return false;
        }

        static void FlushRun(ParserState state, Utf8ConstructionBuffer<Utf8TreeNode> children, Utf8ConstructionText run)
        {
            if (run.Length == 0)
            {
                return;
            }

            Utf8ConstructionScope.ChargeWork((ulong)run.Length);
            children.Add(run.Length == 1 ? One(state, run[0]) : Multi(state, run.ToText()));
            run.Clear();
        }

        static bool TryTakeLowSurrogate(ref ParserState state, out char value)
        {
            value = default;
            if (state.AtEnd) return false;
            var probe = state;
            if (probe.Pattern[probe.Position] == '\\')
            {
                if (!DecodeEscape(ref probe, out value)) return false;
            }
            else value = probe.Pattern[probe.Position++];
            if (!char.IsLowSurrogate(value)) return false;
            state.Position = probe.Position;
            return true;
        }

        static bool DecodeEscape(ref ParserState state, out char value)
        {
            value = '\0';
            state.Position++;
            if (state.AtEnd)
            {
                return state.Fail(Utf8PatternError.InvalidEscape);
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
                        return state.Fail(Utf8PatternError.InvalidEscape);
                    }

                    var letter = state.Pattern[state.Position];
                    if (letter >= 'a' && letter <= 'z')
                    {
                        letter = (char)(letter - ('a' - 'A'));
                    }

                    if (letter < '@' || letter > '_')
                    {
                        return state.Fail(Utf8PatternError.InvalidEscape);
                    }

                    value = (char)(letter - '@');
                    state.Position++;
                    return true;
                case 'x':
                    state.Position++;
                    if (!TakeHex(ref state, 2, out var hex))
                    {
                        return state.Fail(Utf8PatternError.InvalidEscape);
                    }

                    value = (char)hex;
                    return true;
                case 'u':
                    state.Position++;
                    if (!TakeHex(ref state, 4, out var unicode))
                    {
                        return state.Fail(Utf8PatternError.InvalidEscape);
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
                        Utf8ConstructionScope.ChargeWork(1);
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
                    // Other characters spell themselves; a word character
                    // without an escape meaning declines exactly like the
                    // BCL oracle (UnrecognizedEscape).
                    if (IsWordChar(escaped) && ((state.Options & RegexOptions.ECMAScript) == 0 ||
                        escaped is 'p' or 'P' ||
                        escaped == 'k' && (state.Position + 1 >= state.Pattern.Length || state.Pattern[state.Position + 1] is not ('<' or '\''))))
                    {
                        return state.Fail(Utf8PatternError.InvalidEscape);
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
                    kind = (state.Options & RegexOptions.ECMAScript) != 0 ? Utf8TreeNodeKind.ECMABoundary : Utf8TreeNodeKind.Boundary;
                    break;
                case 'B':
                    kind = (state.Options & RegexOptions.ECMAScript) != 0 ? Utf8TreeNodeKind.NonECMABoundary : Utf8TreeNodeKind.NonBoundary;
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

            anchor = Utf8TreeNode.Create(kind, state.Options);
            state.Position += 2;
            return true;
        }

        static bool ParseClass(ref ParserState state, out Utf8TreeNode? node)
        {
            node = null;
            using var data = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            var remainingClosers = 0;
            // Subtraction is a single nested chain. Each segment is emitted
            // before its child, then all pending parent closers are consumed.
            // Class depth uses no recursive parser frames or group-depth cap.
            // .NET 10's recursive class scanner can overflow a worker stack
            // on these valid patterns; owned parsing retains their semantics.
            while (true)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (!ParseClassSegment(ref state, data, out var subtracted)) return false;
                if (!subtracted) break;
                remainingClosers++;
            }
            while (remainingClosers > 0)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (state.AtEnd || state.Pattern[state.Position] != ']')
                    return state.Fail(state.AtEnd ? Utf8PatternError.UnclosedClass : Utf8PatternError.InvalidSyntax);
                state.Position++;
                remainingClosers--;
            }

            // A negated complete scalar range is empty, including when followed
            // by subtraction. Keep parsing all segments before discarding them.
            if (data.Count >= 5 && data[0] == 1 && data[1] == 2 && data[2] == 0 &&
                data[3] == 0 && data[4] == 0x110000)
            {
                node = Utf8TreeNode.Create(Utf8TreeNodeKind.Nothing, state.Options & ~RegexOptions.IgnoreCase);
                return true;
            }
            node = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, state.Options & ~RegexOptions.IgnoreCase, Utf8ScalarClass.Create(data));
            node.MatchCaseInsensitive = (state.Options & RegexOptions.IgnoreCase) != 0;
            return true;
        }

        static bool ParseClassSegment(ref ParserState state, Utf8ConstructionBuffer<int> data, out bool subtracted)
        {
            subtracted = false;
            state.Position++;
            if (state.AtEnd)
            {
                return state.Fail(Utf8PatternError.UnclosedClass);
            }
            var negated = false;
            if (state.Pattern[state.Position] == '^')
            {
                negated = true;
                state.Position++;
                if (state.AtEnd)
                {
                    return state.Fail(Utf8PatternError.UnclosedClass);
                }
            }
            using var scalarRanges = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            using var categories = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            var pending = 0;
            var hasPending = false;
            var pendingIsPlain = false;
            var pendingAdded = false;
            var first = true;
            while (true)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (state.AtEnd)
                {
                    return state.Fail(Utf8PatternError.UnclosedClass);
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


                        if (!TryTakeRangeEnd(ref state, pending, scalarRanges))
                        {
                            return false;
                        }

                        hasPending = false;
                        pendingAdded = false;
                        first = false;
                        continue;
                    }

                    if (state.Position + 1 < state.Pattern.Length &&
                        state.Pattern[state.Position + 1] == '[' &&
                        (hasPending || categories.Count + scalarRanges.Count > 0))
                    {
                        if (hasPending && !pendingAdded)
                        {
                            if (!AddClassScalar(scalarRanges, pending))
                            {
                                return false;
                            }
                        }

                        AppendSegment(data, negated, scalarRanges, categories, (state.Options & RegexOptions.IgnoreCase) != 0, state.CaseBehavior);
                        subtracted = true;
                        state.Position++;
                        return true;
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
                        if (!AddClassScalar(scalarRanges, pending))
                        {
                            return false;
                        }
                    }

                    if (!AddClassScalar(scalarRanges, '-'))
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
                if (character == '\\' && TryTakeShorthandMembers(ref state, scalarRanges, categories))
                {
                    hasPending = false;
                    pendingIsPlain = false;
                    pendingAdded = false;
                    first = false;
                    continue;
                }
                if (!TryReadClassScalar(ref state, out var atom, out pendingIsPlain)) return false;
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
                    if (!AddClassScalar(scalarRanges, atom))
                    {
                        return false;
                    }
                    pending = atom;
                    hasPending = true;
                    pendingAdded = true;
                }
                first = false;
            }
            AppendSegment(data, negated, scalarRanges, categories, (state.Options & RegexOptions.IgnoreCase) != 0, state.CaseBehavior);
            return true;
        }

        static void AppendSegment(Utf8ConstructionBuffer<int> data, bool negated,
            Utf8ConstructionBuffer<int> scalarRanges, Utf8ConstructionBuffer<int> categories,
            bool fold, Utf8ScalarCaseBehavior behavior)
        {
            var owner = Utf8ConstructionScope.CurrentBudget;
            // These unions cover every scalar under any case fold. Recognize
            // them before enumerating folded ranges or allocating merge buffers.
            if ((categories.Count == 2 && categories[0] != 0 && categories[0] == -categories[1]) ||
                (scalarRanges.Count == 2 && scalarRanges[0] == 0 && scalarRanges[1] == 0x110000))
            {
                data.Add(negated ? 1 : 0); data.Add(2); data.Add(0);
                data.Add(0); data.Add(0x110000);
                return;
            }
            using var ranges = Utf8ConstructionBuffer<(int Low, int High)>.Create(owner);
            for (var index = 0; index < scalarRanges.Count; index += 2)
                {
                Utf8ConstructionScope.ChargeWork(1);
                ranges.Add((scalarRanges[index], scalarRanges[index + 1]));
            }
            if (fold)
            {
                var count = ranges.Count;
                for (var index = 0; index < count; index++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    var range = ranges[index];
                    for (var value = range.Low; value < range.High; value++)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        if (value < 0x10000)
                        {
                            if (behavior != Utf8ScalarCaseBehavior.Invariant &&
                                ((value | 0x20) == 'i' || (value | 0x01) == '\u0131'))
                            {
                                foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents((char)value, behavior))
                                    {
                                    Utf8ConstructionScope.ChargeWork(1);
                                    ranges.Add((equivalent, equivalent + 1));
                                }
                                continue;
                            }
                            if (!Utf8OwnedCaseFold.TryGetFold((char)value, behavior, out var lower, out var upper)) continue;
                            ranges.Add((lower, lower + 1));
                            ranges.Add((upper, upper + 1));
                        }
                        else
                        {
                            var lower = Utf8SupplementaryCase.Lower(value);
                            var upper = Utf8SupplementaryCase.Upper(value);
                            if (lower != value) ranges.Add((lower, lower + 1));
                            if (upper != value) ranges.Add((upper, upper + 1));
                        }
                    }
                }
            }
            ranges.Sort(static (left, right) => left.Low.CompareTo(right.Low));
            using var merged = Utf8ConstructionBuffer<(int Low, int High)>.Create(owner);
            foreach (var range in ranges)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (merged.Count != 0 && range.Low < merged[merged.Count - 1].High)
                    merged[merged.Count - 1] = (merged[merged.Count - 1].Low, Math.Max(merged[merged.Count - 1].High, range.High));
                else merged.Add(range);
            }
            using var endpoints = Utf8ConstructionBuffer<int>.Create(owner);
            foreach (var range in merged)
            {
                owner.ChargeWork(1);
                endpoints.Add(range.Low); endpoints.Add(range.High);
            }
            data.Add(negated ? 1 : 0); data.Add(endpoints.Count); data.Add(categories.Count);
            data.AddRange(endpoints.Span); data.AddRange(categories.Span);
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
            using var ranges = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            using var categories = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            return TryTakeShorthandMembers(ref probe, ranges, categories);
        }


        static bool TryTakeRangeEnd(ref ParserState state, int start, Utf8ConstructionBuffer<int> ranges)
        {
            if (!IsRangeEndAhead(state, 1)) return false;
            var probe = state;
            probe.Position++;
            if (!TryReadClassScalar(ref probe, out var end, out _)) return false;
            if (end < start)
            {
                return state.Fail(Utf8PatternError.ReversedRange, probe.Position);
            }
            ranges.Add(start);
            ranges.Add(end + 1);
            state.Position = probe.Position;
            return true;
        }

        static bool IsRangeEndAhead(ParserState state, int offset)
        {
            var index = state.Position + offset;
            if (index >= state.Pattern.Length || state.Pattern[index] is ']' or '[') return false;
            if (state.Pattern[index] != '\\') return true;
            state.Position = index;
            return TryReadClassScalar(ref state, out _, out _);
        }

        static bool TryReadClassScalar(ref ParserState state, out int value, out bool isPlain)
        {
            value = 0;
            isPlain = true;
            if (state.AtEnd) return false;
            char first;
            if (state.Pattern[state.Position] == '\\')
            {
                if (!DecodeClassEscape(ref state, out first, out isPlain)) return false;
            }
            else
            {
                first = state.Pattern[state.Position++];
                if (first == '[' && !state.AtEnd && state.Pattern[state.Position] is ':' or '.' or '=')
                    state.UsesLiteralClassBracketSyntax = true;
            }
            value = first;
            if (char.IsHighSurrogate(first) && TryTakeLowSurrogate(ref state, out var second))
                value = char.ConvertToUtf32(first, second);
            return true;
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
                ((state.Options & RegexOptions.ECMAScript) == 0 || number < 32) &&
                state.Position + 1 + length < state.Pattern.Length &&
                state.Pattern[state.Position + 1 + length] >= '0' &&
                state.Pattern[state.Position + 1 + length] <= '7')
            {
                Utf8ConstructionScope.ChargeWork(1);
                number = number * 8 + (state.Pattern[state.Position + 1 + length] - '0');
                length++;
            }

            if (length == 0)
            {
                return false;
            }

            value = (char)(number & 0xFF);
            state.Position += 1 + length;
            return true;
        }

        static bool DecodeClassEscape(ref ParserState state, out char value, out bool isPlain)
        {
            isPlain = false;
            // Octal decoding applies to both class members and range bounds.
            // Outside classes the same digits may resolve as capture references.
            if (TryTakeClassOctal(ref state, out value))
            {
                isPlain = true;
                return true;
            }
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
                // character class: DecodeEscape rejects those letters and
                // the owned parser reports the construction failure.
                if (letter == '-')
                {
                    value = '-';
                    state.Position += 2;
                    isPlain = false;
                    return true;
                }

                isPlain = IsLimitedClassEscapeHead(letter);
            }
            if (!DecodeEscape(ref state, out value))
            {
                return false;
            }
            return true;
        }

        static bool IsLimitedClassEscapeHead(char letter)
        {
            // Subset TryScanCharClassLiteralEscape heads beyond the arms above:
            // single-char escapes with full range behavior, unlike shorthand or
            // punctuation resolutions that break ranges.
            return letter is '0' or 'x' or 'u' or 'c' or 'a' or 'e' or 'n' or 'r' or 't' or 'f' or 'v' || (letter >= '0' && letter <= '7');
        }
        static Utf8TreeNode CreateDotNode(RegexOptions options)
        {
            using var data = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            if ((options & RegexOptions.Singleline) != 0) data.AddRange([0, 2, 0, 0, 0x110000]);
            else data.AddRange([1, 2, 0, '\n', '\n' + 1]);
            return Utf8TreeNode.Create(Utf8TreeNodeKind.Set, options & ~RegexOptions.IgnoreCase, Utf8ScalarClass.Create(data));
        }

        static bool TryTakeShorthandAtom(ref ParserState state, out Utf8TreeNode? node)
        {
            node = null;
            using var ranges = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            using var categories = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            var ecmaShorthand = (state.Options & RegexOptions.ECMAScript) != 0 &&
                state.Position + 1 < state.Pattern.Length && state.Pattern[state.Position + 1] is 'd' or 'D' or 'w' or 'W' or 's' or 'S';
            if (!TryTakeShorthandMembers(ref state, ranges, categories)) return false;
            using var data = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            data.Add(0); data.Add(ranges.Count); data.Add(categories.Count);
            data.AddRange(ranges.Span); data.AddRange(categories.Span);
            node = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, state.Options & ~RegexOptions.IgnoreCase, Utf8ScalarClass.Create(data));
            // Standalone ECMAScript shorthands keep their fixed scalar set;
            // bracketed unions receive lexical class expansion.
            node.MatchCaseInsensitive = !ecmaShorthand && (state.Options & RegexOptions.IgnoreCase) != 0;
            return true;
        }

        static bool TryTakeShorthandMembers(ref ParserState state, Utf8ConstructionBuffer<int> ranges, Utf8ConstructionBuffer<int> categories)
        {
            // state.Position is at the backslash; nothing is consumed on failure.
            if (state.Position + 1 >= state.Pattern.Length)
            {
                return false;
            }

            if ((state.Options & RegexOptions.ECMAScript) != 0 &&
                TryEcmaShorthand(state.Pattern[state.Position + 1], ranges, categories))
            {
                state.Position += 2;
                return true;
            }

            switch (state.Pattern[state.Position + 1])
            {
                case 'd':
                    categories.Add((int)UnicodeCategory.DecimalDigitNumber + 1);
                    state.Position += 2;
                    return true;
                case 'D':
                    categories.Add(-((int)UnicodeCategory.DecimalDigitNumber + 1));
                    state.Position += 2;
                    return true;
                case 'w':
                    categories.AddRange([0, 2, 4, 5, 3, 1, 6, 9, 19, 0]);
                    state.Position += 2;
                    return true;
                case 'W':
                    categories.AddRange([0, -2, -4, -5, -3, -1, -6, -9, -19, 0]);
                    state.Position += 2;
                    return true;
                case 's':
                    categories.Add(100);
                    state.Position += 2;
                    return true;
                case 'S':
                    categories.Add(-100);
                    state.Position += 2;
                    return true;
                case 'p':
                    return TryTakeProperty(ref state, invert: false, ranges, categories);
                case 'P':
                    return TryTakeProperty(ref state, invert: true, ranges, categories);
                default:
                    return false;
            }
        }

        static bool TryEcmaShorthand(char head, Utf8ConstructionBuffer<int> ranges, Utf8ConstructionBuffer<int> categories)
        {
            ReadOnlySpan<int> members = head switch
            {
                'd' or 'D' => ['0', ':'],
                's' or 'S' => ['\t', '\x0e', ' ', '!'],
                'w' or 'W' => ['0', ':', 'A', '[', '_', '`', 'a', '{', 0x0130, 0x0131],
                _ => [],
            };
            if (members.IsEmpty) return false;
            if (head is 'd' or 's' or 'w') ranges.AddRange(members);
            else AppendRangePredicate(categories, members, true);
            return true;
        }

        static void AppendRangePredicate(Utf8ConstructionBuffer<int> categories, ReadOnlySpan<int> ranges, bool negated)
        {
            categories.Add(Utf8ScalarClass.RangePredicateCode);
            categories.Add(negated ? 1 : 0);
            categories.Add(ranges.Length);
            categories.AddRange(ranges);
        }

        static bool TryTakeProperty(ref ParserState state, bool invert, Utf8ConstructionBuffer<int> ranges, Utf8ConstructionBuffer<int> categories)
        {
            // state.Position is at the backslash of \p or \P.
            var open = state.Position + 2;
            if (open >= state.Pattern.Length || state.Pattern[open] != '{')
            {
                return state.Fail(Utf8PatternError.InvalidProperty);
            }

            // Charge only examined characters. Charging the remaining pattern
            // at every property made repeated short properties quadratic.
            var close = open + 1;
            while (close < state.Pattern.Length)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (state.Pattern[close] == '}') break;
                close++;
            }
            if (close == state.Pattern.Length)
            {
                return state.Fail(Utf8PatternError.InvalidProperty);
            }

            var name = Utf8ConstructionScope.CurrentBudget.CopyText(state.Pattern.AsSpan(open + 1, close - open - 1));
            if (!TryResolvePropertyMembers(name, invert, ranges, categories))
            {
                return state.Fail(Utf8PatternError.InvalidProperty);
            }

            state.Position = close + 1;
            return true;
        }

        static bool TryResolvePropertyMembers(string name, bool invert, Utf8ConstructionBuffer<int> ranges, Utf8ConstructionBuffer<int> categories)
        {
            if (TryResolveSingleCategory(name, out var single))
            {
                var encoded = single + 1;
                categories.Add(invert ? -encoded : encoded);
                return true;
            }

            if (TryResolveCategoryGroup(name, out var members))
            {
                // Zero frames distinguish groups from a standalone -1 (not Lu).
                // Negative members inside the frame intersect their exclusions.
                categories.Add(0);
                foreach (var member in members)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    var encoded = member + 1;
                    categories.Add(invert ? -encoded : encoded);
                }

                categories.Add(0);
                return true;
            }

            if (Utf8SliceCategories.TryResolve(name, out _, out var blockRanges, out var isBlock) && isBlock)
            {
                if (invert) AppendRangePredicate(categories, blockRanges, true);
                else ranges.AddRange(blockRanges);
                return true;
            }

            return false;
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

        static bool TryResolveCategoryGroup(string name, out ReadOnlySpan<int> members)
        {
            // Borrowed immutable category groups; integer predicates cover every scalar plane.
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

        static bool ParseNamedGroup(ref OwnedParseContext context, ref OwnedParseFrame frame, out OwnedParseFrame child)
        {
            child = default;
            var state = context.State;
            Utf8TreeNode? node = null;
            if (frame.Stage == 0)
            {
                var openPosition = state.Position;
                var open = state.Pattern[state.Position];
                var close = open == '<' ? '>' : '\'';
                state.Position++;
                var contentStart = state.Position;
                while (!state.AtEnd && state.Pattern[state.Position] != close)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    state.Position++;
                }

                if (state.AtEnd)
                {
                    context.Fail(state);
                    return false;
                }

                var content = state.Storage.Budget.CopyText(state.Pattern.AsSpan(contentStart, state.Position - contentStart));
                state.Position++;

                var isPop = false;
                var name = content;
                string? popName = null;
                Utf8ConstructionScope.ChargeWork((ulong)content.Length * 3);
                var separator = content.IndexOf('-');
                if (separator >= 0)
                {
                    name = state.Storage.Budget.CopyText(content.AsSpan(0, separator));
                    popName = state.Storage.Budget.CopyText(content.AsSpan(separator + 1));
                    isPop = name.Length == 0;
                    if (popName.Length == 0 || popName.Contains('-'))
                    {
                        state.Fail(Utf8PatternError.InvalidCaptureName, contentStart);
                        context.Fail(state);
                        return false;
                    }
                }
                else if (name.Length == 0 || name.Contains('-'))
                {
                    state.Fail(Utf8PatternError.InvalidCaptureName, contentStart);
                    context.Fail(state);
                    return false;
                }

                var namedEntryOptions = state.Options;

                frame.OpenPosition = openPosition;
                frame.Name = name;
                frame.PopName = popName;
                frame.IsPop = isPop;
                frame.EntryOptions = namedEntryOptions;
                frame.Stage = 1;
                context.State = state;
                child = new(OwnedParseProduction.Alternation, frame.Depth + 1);
                return true;
            }
            var body = context.Node;
            if (body is null)
            {
                context.Fail(state);
                return false;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                if (state.AtEnd) state.Fail(Utf8PatternError.UnclosedGroup);
                context.Fail(state);
                return false;
            }

            state.Position++;
            state.Options = frame.EntryOptions;
            var group = Utf8TreeNode.Create(Utf8TreeNodeKind.Capture, frame.EntryOptions);
            group.Children = body;
            body.Parent = group;
            node = group;

            if (frame.PopName is not null)
                state.AddBackrefFixup(new OwnedBackrefFixup(group, null, frame.OpenPosition, frame.PopName, true, false, null));

            if (frame.IsPop)
            {
                group.M = -1;
                group.N = -1;
                context.Complete(state, node);
                return false;
            }

            var groupName = frame.Name ?? throw new InvalidOperationException("Missing capture name.");
            if (IsAllAsciiDigits(groupName) && TryParseGroupNumber(groupName, out var declared))
            {
                if (declared == 0)
                {
                    state.Fail(Utf8PatternError.InvalidCaptureName, frame.OpenPosition + 1);
                    context.Fail(state);
                    return false;
                }

                if (groupName.Length > 1 && groupName[0] == '0')
                {
                    // A zero-padded spelling does not declare a number during
                    // .NET's capture scan, but captures into it when it is declared
                    // elsewhere. Keep that alias separate from numbered declarations.
                    group.Str = groupName;
                    group.M = declared;
                    group.N = -1;
                    node = group;
                    state.RequireSlot(declared);
                    context.Complete(state, node);
                    return false;
                }

                group.Str = groupName;
                group.M = declared;
                group.N = -1;

                if (declared != 0)
                {
                    state.AddGroupDefinition(new OwnedGroupDefinition(node: group, name: groupName, declared: declared, isAuto: false, openPosition: frame.OpenPosition));
                }

                context.Complete(state, node);
                return false;
            }

            if (IsAllAsciiDigits(groupName) || IsAsciiDigit(groupName[0]))
            {
                // BCL rejects every survivor of this branch: all-digit overflow
                // fails group-number construction, and digit-start names are
                // invalid group names. Only (?:)-style transparency above serves.
                state.Fail(Utf8PatternError.InvalidCaptureName, frame.OpenPosition + 1);
                context.Fail(state);
                return false;
            }

            if (!IsValidGroupName(groupName))
            {
                // BCL capture-name rule (machine-probed on .NET 10): every name
                // character is a Unicode word character (letters, decimal digits,
                // connector punctuation, nonspacing marks, ZWJ/ZWNJ). Anything
                // else fails construction with the owned parser error below.
                state.Fail(Utf8PatternError.InvalidCaptureName, frame.OpenPosition + 1);
                context.Fail(state);
                return false;
            }

            group.Str = groupName;
            group.M = -1;
            group.N = -1;

            state.AddGroupDefinition(new OwnedGroupDefinition(node: group, name: groupName, declared: -1, isAuto: true, openPosition: frame.OpenPosition));

            context.Complete(state, node);
            return false;
        }

        static bool IsAllAsciiDigits(string value)
        {
            if (value.Length == 0)
            {
                return false;
            }

            foreach (var character in value)
            {
                Utf8ConstructionScope.ChargeWork(1);
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
            // Unicode word character. ASCII-digit starts are canonical or
            // rejected above, so digits here only admit non-ASCII numerals
            // as name text.
            if (value.Length == 0)
            {
                return false;
            }

            foreach (var character in value)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (!IsWordChar(character))
                {
                    return false;
                }
            }

            return true;
        }

        static bool IsWordChar(char value)
        {
            // Machine-probed BCL word-character rule shared by capture names
            // and bare escapes: all letter kinds, decimal digits, connector
            // punctuation, nonspacing marks, and zero-width joiners.
            if (value == (char)0x200C || value == (char)0x200D)
            {
                return true;
            }

            switch (char.GetUnicodeCategory(value))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                    return true;
                default:
                    return false;
            }
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
                Utf8ConstructionScope.ChargeWork(1);
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
                Utf8ConstructionScope.ChargeWork(1);
                end++;
            }

            var digits = state.Storage.Budget.CopyText(state.Pattern.AsSpan(state.Position + 1, end - state.Position - 1));
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
            Utf8ConstructionScope.ChargeWork((ulong)(state.Pattern.Length - state.Position - 3));
            var end = state.Pattern.IndexOf(close, state.Position + 3);
            if (end < 0)
            {
                return null;
            }

            var name = state.Storage.Budget.CopyText(state.Pattern.AsSpan(state.Position + 3, end - state.Position - 3));
            // ECMAScript treats an invalid delimited reference spelling as
            // escaped 'k' followed by ordinary pattern text. Valid but
            // undefined references still fail during capture resolution.
            if (!IsValidGroupName(name) || IsAsciiDigit(name[0]) && !IsAllAsciiDigits(name)) return null;
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
            out IReadOnlyDictionary<string, int>? captureNameToNumber,
            out IReadOnlyDictionary<int, int>? captureSparseMapping)
        {
            captureCount = 0;
            captureNames = [];
            captureNameToNumber = null;
            captureSparseMapping = null;

            var budget = Utf8ConstructionScope.CurrentBudget;
            var storage = state.Storage;
            var groupDefinitions = state.GroupDefinitions;
            var definitionCount = groupDefinitions?.Count ?? 0;
            var definitions = budget.AllocateArray<Utf8CaptureDefinition>(definitionCount, false);
            Utf8CaptureCatalog catalog;
            try
            {
                if (groupDefinitions is not null)
                    for (var index = 0; index < definitionCount; index++)
                    {
                        budget.ChargeWork(1);
                        var definition = groupDefinitions[index];
                        definitions[index] = new Utf8CaptureDefinition(definition.Name, definition.Declared, definition.IsAuto, definition.OpenPosition);
                    }
                // Opening positions are used only by ECMAScript numeric references.
                if (definitionCount != 0 && (state.Options & RegexOptions.ECMAScript) != 0)
                    state.CapturePositions ??= Utf8ConstructionMap<int, int>.Create(budget, EqualityComparer<int>.Default);
                catalog = Utf8CaptureCatalog.Create(state.Groups, definitions, state.RequiredSlots is { Count: > 0 });
            }
            finally { budget.ReleaseArray(definitions, false); }
            state.CaptureCatalog = catalog;
            if (groupDefinitions is not null)
                for (var index = 0; index < definitionCount; index++)
                {
                    budget.ChargeWork(1);
                    var definition = groupDefinitions[index];
                    var slot = catalog.DefinitionNumbers[index];
                    if (definition.IsAuto) definition.Node.M = slot;
                    state.RecordCapturePosition(slot, definition.OpenPosition);
                }
            if (state.RequiredSlots is { } requiredSlots)
                foreach (var required in requiredSlots.Span)
                {
                    budget.ChargeWork(1);
                    if (catalog.OrdinalFromNumber(required) < 0)
                        return state.Fail(Utf8PatternError.UndefinedCapture);
                }
            captureCount = catalog.GroupNumbers.Length;
            captureNames = catalog.GroupNames;
            captureNameToNumber = catalog.NameToNumber;
            captureSparseMapping = catalog.SparseMapping;

            var morphed = false;
            if (state.BackrefFixups is { } fixups)
                foreach (var fixup in fixups.Span)
                {
                    budget.ChargeWork(1);
                    if (!ResolveFixup(storage, fixup, catalog, state.CapturePositions, ref root, ref morphed, state.CaseBehavior))
                        return state.Fail(Utf8PatternError.UndefinedCapture, fixup.ReferencePosition);
                }

            root = MergeLiteralRuns(storage, root);

            return true;
        }

        static bool ResolveFixup(Utf8ConstructionSession storage, OwnedBackrefFixup fixup, Utf8CaptureCatalog catalog,
            Utf8ConstructionMap<int, int>? capturePositions, ref Utf8TreeNode root, ref bool morphed, Utf8ScalarCaseBehavior behavior)
        {
            if (fixup.Digits is not null && !fixup.IsConditional && !fixup.IsPop &&
                (fixup.Node.Options & RegexOptions.ECMAScript) != 0)
                return ResolveEcmaNumeric(storage, fixup, fixup.Digits, capturePositions,
                    catalog.GroupNumbers[^1] == int.MaxValue ? int.MaxValue : catalog.GroupNumbers[^1] + 1, ref root, ref morphed);
            if (fixup.Node.Kind == Utf8TreeNodeKind.Empty && (fixup.Digits is not null || fixup.Name is not null))
            {
                return ResolveEmptyFixup(storage, fixup, catalog, ref root, ref morphed, behavior);
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
                    if (!TryParseGroupNumber(popName, out var popNumber) || catalog.OrdinalFromNumber(popNumber) < 0)
                    {
                        return false;
                    }

                    fixup.Node.N = popNumber;
                    return true;
                }

                var popSlot = catalog.GroupNumberFromName(popName);
                if (popSlot < 0)
                {
                    return false;
                }

                fixup.Node.N = popSlot;
                return true;
            }

            if (fixup.Name is not null)
            {
                var lookupName = fixup.Name;
                if (IsAllAsciiDigits(lookupName))
                {
                    if (!TryParseGroupNumber(lookupName, out var numericName) || catalog.OrdinalFromNumber(numericName) < 0) return false;
                    fixup.Node.M = numericName;
                    return true;
                }

                // Numeric reference syntax is handled separately above.
                var namedSlot = catalog.GroupNumberFromName(lookupName);
                if (namedSlot < 0)
                {
                    // Undefined conditional names are expression tests. Their
                    // implicit assertion follows the enclosing direction;
                    // explicit lookahead and lookbehind set their own direction.
                    if (!fixup.IsConditional)
                    {
                        return false;
                    }

                    var testOptions = fixup.Node.Options;
                    var literal = lookupName.Length == 1
                        ? Utf8TreeNode.Create(Utf8TreeNodeKind.One, testOptions, lookupName[0])
                        : Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, testOptions, lookupName);
                    var assertion = Utf8TreeNode.Create(Utf8TreeNodeKind.PositiveLookaround, testOptions);
                    assertion.Children = literal;
                    literal.Parent = assertion;
                    var branches = storage.Buffer<Utf8TreeNode>();
                    branches.Add(assertion);
                    for (var branch = 0; branch < fixup.Node.ChildCount; branch++) {
                        Utf8ConstructionScope.ChargeWork(1);
                        branches.Add(fixup.Node.Child(branch));
                    }
                    fixup.Node.Kind = Utf8TreeNodeKind.ExpressionConditional;
                    fixup.Node.Children = branches;
                    foreach (var branch in branches) {
                        Utf8ConstructionScope.ChargeWork(1);
                        branch.Parent = fixup.Node;
                    }

                    return true;
                }

                fixup.Node.M = namedSlot;
                return true;
            }

            if (fixup.Digits is not null &&
                int.TryParse(fixup.Digits, out var backref) &&
                backref >= 1 &&
                catalog.OrdinalFromNumber(backref) >= 0)
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

            return ResolveOctalFallback(storage, fixup, ref root, ref morphed, behavior);
        }

        static bool ResolveEmptyFixup(Utf8ConstructionSession storage, OwnedBackrefFixup fixup, Utf8CaptureCatalog catalog, ref Utf8TreeNode root, ref bool morphed, Utf8ScalarCaseBehavior behavior)
        {
            if (fixup.Name is not null)
            {
                // Pruned ({0}) references still fail construction when unbound,
                // exactly like the BCL oracle; conditional fixups never reach
                // this path (their nodes are BackreferenceConditional).
                var lookupName = fixup.Name;
                if (IsAllAsciiDigits(lookupName))
                {
                    if (!TryParseGroupNumber(lookupName, out var numericName) || catalog.OrdinalFromNumber(numericName) < 0) return false;
                    fixup.Node.M = numericName;
                    return true;
                }

                if (catalog.GroupNumberFromName(lookupName) < 0)
                {
                    return false;
                }

                return true;
            }

            if (fixup.Digits is not null &&
                int.TryParse(fixup.Digits, out var backref) &&
                backref >= 1 &&
                catalog.OrdinalFromNumber(backref) >= 0)
            {
                return true;
            }

            // Single non-zero digits never spell octal: an unbound one fails
            // construction exactly like the BCL oracle, even under {0}.
            // "0" alone is NUL and flows to the octal path below.
            if (fixup.Digits is not null && fixup.Digits.Length == 1 && fixup.Digits[0] >= '1' && fixup.Digits[0] <= '9')
            {
                return false;
            }

            if (fixup.Digits is null || fixup.Digits.Length < 2)
            {
                return true;
            }

            if (!Utf8NumericEscape.TryReadBareOctal(fixup.Digits, out var value, out var length))
            {
                return false;
            }

            if (length == fixup.Digits.Length)
            {
                StripEmptyOptions(ref root, fixup.Node);
                return true;
            }

            return ResolveOctalRest(storage, fixup, ref root, ref morphed, length, value, behavior);
        }

        static void StripEmptyOptions(ref Utf8TreeNode root, Utf8TreeNode node)
        {
            if ((node.Options & RegexOptions.IgnoreCase) != 0)
            {
                var stripped = Utf8TreeNode.Create(Utf8TreeNodeKind.Empty, node.Options & ~RegexOptions.IgnoreCase);
                ReplaceNodeInParent(ref root, node, stripped);
            }
        }

        static bool ResolveEcmaNumeric(Utf8ConstructionSession storage, OwnedBackrefFixup fixup, string digits, Utf8ConstructionMap<int, int>? capturePositions, int captureScanLimit,
            ref Utf8TreeNode root, ref bool morphed)
        {
            var reference = -1;
            var consumed = 0;
            var number = 0;
            // Only preceding declarations bind, but .NET's ECMAScript grammar
            // consumes every candidate prefix through the capture-number ceiling,
            // even when it keeps an earlier binding. Its numeric scan also wraps
            // on overflow; preserve this interpreted lexical compatibility quirk.
            for (var index = 0; index < digits.Length; index++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                number = unchecked(number * 10 + digits[index] - '0');
                if (number > captureScanLimit) break;
                consumed = index + 1;
                if (capturePositions is not null && capturePositions.TryGetValue(number, out var position) && position < fixup.ReferencePosition)
                {
                    reference = number;
                }
            }
            Utf8TreeNode prefix;
            if (reference >= 0)
            {
                prefix = Utf8TreeNode.Create(Utf8TreeNodeKind.Backreference, fixup.Node.Options);
                prefix.M = reference;
                prefix.N = -1;
            }
            else
            {
                consumed = 0;
                var value = 0;
                while (consumed < digits.Length && consumed < 3 && value < 32 && digits[consumed] is >= '0' and <= '7')
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    value = value * 8 + digits[consumed] - '0';
                    consumed++;
                }
                if (consumed == 0) { consumed = 1; value = digits[0]; }
                prefix = Utf8TreeNode.Create(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, (char)value);
                prefix.MatchCaseInsensitive = fixup.Node.MatchCaseInsensitive;
            }
            if (consumed < digits.Length)
                return ResolveNumericRest(storage, fixup, ref root, ref morphed, consumed, prefix);
            if (fixup.Node.Kind == Utf8TreeNodeKind.Empty) return true;
            ReplaceNodeInParent(ref root, fixup.Node, prefix);
            morphed = true;
            return true;
        }

        static bool ResolveOctalFallback(Utf8ConstructionSession storage, OwnedBackrefFixup fixup, ref Utf8TreeNode root, ref bool morphed, Utf8ScalarCaseBehavior behavior)
        {
            // Single non-zero digits never spell octal; longer runs without a
            // matching slot fall back to up to three octal digits with no rest.
            if (fixup.Digits is null || fixup.Digits.Length < 2)
            {
                return false;
            }

            if (!Utf8NumericEscape.TryReadBareOctal(fixup.Digits, out var value, out var length))
            {
                return false;
            }

            if (length != fixup.Digits.Length)
            {
                return ResolveOctalRest(storage, fixup, ref root, ref morphed, length, value, behavior);
            }

            morphed = true;
            if (fixup.Wrapper is null)
            {
                if ((fixup.Node.Options & RegexOptions.IgnoreCase) != 0 &&
                    Utf8OwnedCaseFold.TryGetFold((char)value, behavior, out var octalLower, out var octalUpper))
                {
                    ReplaceNodeInParent(ref root, fixup.Node, FoldedSet(fixup.Node.Options, octalLower, octalUpper));
                    return true;
                }

                if ((fixup.Node.Options & RegexOptions.IgnoreCase) != 0)
                {
                    var stripped = Utf8TreeNode.Create(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, (char)value);
                    stripped.MatchCaseInsensitive = true;
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
                Utf8OwnedCaseFold.TryGetFold((char)value, behavior, out var wrapperLower, out var wrapperUpper))
            {
                var foldedLoop = FoldedSet(wrapper.Options, wrapperLower, wrapperUpper);
                foldedLoop.Kind = wrapper.Kind == Utf8TreeNodeKind.Lazyloop
                    ? Utf8TreeNodeKind.Setlazy
                    : Utf8TreeNodeKind.Setloop;
                foldedLoop.M = wrapper.M;
                foldedLoop.N = wrapper.N;
                foldedLoop.IsUnboundedRepeat = wrapper.IsUnboundedRepeat;
                ReplaceNodeInParent(ref root, wrapper, foldedLoop);
                return true;
            }

            if ((wrapper.Options & RegexOptions.IgnoreCase) != 0)
            {
                var strippedWrapper = Utf8TreeNode.Create(wrapper.Kind == Utf8TreeNodeKind.Lazyloop
                        ? Utf8TreeNodeKind.Onelazy
                        : Utf8TreeNodeKind.Oneloop, wrapper.Options & ~RegexOptions.IgnoreCase, (char)value);
                strippedWrapper.MatchCaseInsensitive = true;
                strippedWrapper.M = wrapper.M;
                strippedWrapper.N = wrapper.N;
                strippedWrapper.IsUnboundedRepeat = wrapper.IsUnboundedRepeat;
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

        static bool ResolveOctalRest(Utf8ConstructionSession storage, OwnedBackrefFixup fixup, ref Utf8TreeNode root, ref bool morphed, int prefixLength, int value, Utf8ScalarCaseBehavior behavior)
        {
            Utf8TreeNode morphedLeaf;
            if ((fixup.Node.Options & RegexOptions.IgnoreCase) != 0 &&
                Utf8OwnedCaseFold.TryGetFold((char)value, behavior, out var restLower, out var restUpper))
            {
                morphedLeaf = FoldedSet(fixup.Node.Options, restLower, restUpper);
            }
            else
            {
                morphedLeaf = Utf8TreeNode.Create(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, (char)value);
                morphedLeaf.MatchCaseInsensitive = fixup.Node.MatchCaseInsensitive;
                morphedLeaf.M = 0;
                morphedLeaf.N = 0;
            }

            return ResolveNumericRest(storage, fixup, ref root, ref morphed, prefixLength, morphedLeaf);
        }

        static bool ResolveNumericRest(Utf8ConstructionSession storage, OwnedBackrefFixup fixup, ref Utf8TreeNode root, ref bool morphed,
            int prefixLength, Utf8TreeNode prefix)
        {
            var restText = fixup.Digits.AsSpan(prefixLength);
            var beforeAnchor = storage.Buffer<Utf8TreeNode>();
            beforeAnchor.Add(prefix);
            for (var i = 0; i + 1 < restText.Length; i++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                beforeAnchor.Add(Utf8TreeNode.Create(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, restText[i]));
            }

            if (fixup.Wrapper is null)
            {
                // A discarded {0} removes only the final replayed digit,
                // retaining the octal prefix and every preceding literal digit.
                if (fixup.Node.Kind != Utf8TreeNodeKind.Empty)
                    beforeAnchor.Add(Utf8TreeNode.Create(Utf8TreeNodeKind.One, fixup.Node.Options & ~RegexOptions.IgnoreCase, restText[restText.Length - 1]));
                ReplaceNodeWithMany(storage, ref root, fixup.Node, beforeAnchor, null);
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
            var loopBody = Utf8TreeNode.Create(loopKind, loopOptions, loopChar);
            loopBody.M = restWrapper.M;
            loopBody.N = restWrapper.N;
            loopBody.IsUnboundedRepeat = restWrapper.IsUnboundedRepeat;
            ReplaceNodeWithMany(storage, ref root, restWrapper, beforeAnchor, loopBody);
            morphed = true;
            return true;
        }

        static void ReplaceNodeWithMany(Utf8ConstructionSession storage, ref Utf8TreeNode root, Utf8TreeNode oldNode, Utf8ConstructionBuffer<Utf8TreeNode> fresh, Utf8TreeNode? trailing)
        {
            var parent = oldNode.Parent;
            var sequence = storage.Buffer<Utf8TreeNode>();
            sequence.AddRange(fresh.Span);
            if (trailing is not null)
            {
                sequence.Add(trailing);
            }

            if (parent is not null && parent.Kind != Utf8TreeNodeKind.Concatenate)
            {
                // A capture, alternation arm or loop owns one expression at this
                // position. Keep replayed digits together instead of inserting
                // them as extra children of that enclosing operation.
                var expression = sequence.Count == 1 ? sequence[0]
                    : Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, oldNode.Options);
                if (sequence.Count > 1)
                {
                    expression.Children = sequence;
                    foreach (var child in sequence) {
                        Utf8ConstructionScope.ChargeWork(1);
                        child.Parent = expression;
                    }
                }
                ReplaceNodeInParent(ref root, oldNode, expression);
                return;
            }

            foreach (var node in sequence)
            {
                Utf8ConstructionScope.ChargeWork(1);
                node.Parent = parent;
            }

            if (parent is null)
            {
                if (sequence.Count == 1)
                {
                    root = sequence[0];
                    return;
                }

                var top = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, oldNode.Options);
                top.Children = sequence;
                foreach (var child in sequence) {
                    Utf8ConstructionScope.ChargeWork(1);
                    child.Parent = top;
                }
                root = top;
                return;
            }

            if (parent.Children is Utf8ConstructionBuffer<Utf8TreeNode> siblings)
            {
                for (var i = 0; i < siblings.Count; i++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    if (ReferenceEquals(siblings[i], oldNode))
                    {
                        siblings.RemoveAt(i);
                        siblings.InsertRange(i, sequence.Span);
                        return;
                    }
                }

                return;
            }

            var copied = storage.Buffer<Utf8TreeNode>();
            copied.AddRange(sequence.Span);
            parent.Children = copied;
        }

        static Utf8TreeNode MergeLiteralRuns(Utf8ConstructionSession storage, Utf8TreeNode node)
        {
            using var frames = Utf8ConstructionFrames<(Utf8TreeNode Node, int NextChild)>.Create(storage.Budget, (node, 0));
            while (frames.Count > 0)
            {
                storage.Budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                var current = frame.Node;
                var discardedLoop = current.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop or Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Onelazy or Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy &&
                    current.M == 0 && current.N == 0;
                if (!discardedLoop && frame.NextChild < current.ChildCount)
                {
                    frames[index] = (current, frame.NextChild + 1);
                    frames.Push((current.Child(frame.NextChild), 0));
                    continue;
                }
                frames.Pop();
                var merged = MergeNode(storage, current);
                if (frames.Count == 0) node = merged;
                else if (!ReferenceEquals(merged, current))
                {
                    var parent = frames[frames.Count - 1];
                    if (parent.Node.Children is Utf8ConstructionBuffer<Utf8TreeNode> children)
                        children[parent.NextChild - 1] = merged;
                    else parent.Node.Children = merged;
                    merged.Parent = parent.Node;
                }
            }
            return node;
        }

        static Utf8TreeNode MergeNode(Utf8ConstructionSession storage, Utf8TreeNode node)
        {
            if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop or Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Onelazy or Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy &&
                node.M == 0 &&
                node.N == 0)
            {
                return Utf8TreeNode.Create(Utf8TreeNodeKind.Empty, node.Options);
            }

            if (node.Kind == Utf8TreeNodeKind.Alternate) return node;

            if (node.Children is Utf8ConstructionBuffer<Utf8TreeNode> children)
            {
                if (node.Kind == Utf8TreeNodeKind.Concatenate && children.Count > 0)
                {
                    var removed = 0;
                    for (var i = 0; i < children.Count;)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
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
                            return Utf8TreeNode.Create(Utf8TreeNodeKind.Empty, node.Options);
                        }

                        if (children.Count == 1)
                        {
                            return children[0];
                        }
                    }
                }

                // Conditional branches stay unfused: the provider keeps yes/no as
                // parsed, so fusing them would invent Multi nodes it never builds.
                if (node.Kind is Utf8TreeNodeKind.BackreferenceConditional or Utf8TreeNodeKind.ExpressionConditional)
                {
                    return node;
                }

                var fused = storage.Buffer<Utf8TreeNode>();
                var pending = storage.Buffer<Utf8TreeNode>();
                var pendingOptions = RegexOptions.None;
                foreach (var child in children)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    if (TryMergeableText(child, out _, out _) && (pending.Count == 0 ||
                        child.Options == pendingOptions && child.MatchCaseInsensitive == pending[0].MatchCaseInsensitive))
                    {
                        if (pending.Count == 0)
                        {
                            pendingOptions = child.Options;
                        }

                        pending.Add(child);
                        continue;
                    }

                    FlushMergePending(storage, node, fused, pending);
                    fused.Add(child);
                }

                FlushMergePending(storage, node, fused, pending);
                if (fused.Count == children.Count)
                {
                    return node;
                }

                node.Children = fused;
                foreach (var child in fused)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    child.Parent = node;
                }

                if (node.Kind != Utf8TreeNodeKind.Concatenate)
                {
                    return node;
                }

                return fused.Count == 1 ? fused[0] : node;
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

            if (node.Kind == Utf8TreeNodeKind.Multi && node.Str is not null)
            {
                text = node.Str;
                return true;
            }

            return false;
        }

        static void FlushMergePending(Utf8ConstructionSession storage, Utf8TreeNode parent, Utf8ConstructionBuffer<Utf8TreeNode> fused, Utf8ConstructionBuffer<Utf8TreeNode> pending)
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

            var run = storage.Text();
            foreach (var literal in pending)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (literal.Kind == Utf8TreeNodeKind.One)
                {
                    run.Append(literal.Ch);
                }
                else
                {
                    Utf8ConstructionScope.ChargeWork((ulong)(literal.Str?.Length ?? 0));
                    run.Append(literal.Str);
                }
            }

            Utf8ConstructionScope.ChargeWork((ulong)run.Length);
            var merged = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, pending[0].Options, run.ToText());
            merged.MatchCaseInsensitive = pending[0].MatchCaseInsensitive;
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

        static bool ParseLookaround(ref OwnedParseContext context, ref OwnedParseFrame frame, out OwnedParseFrame child)
        {
            child = default;
            var state = context.State;
            Utf8TreeNode? node = null;
            if (frame.Stage == 0)
            {
                state.Position += frame.IsLookbehind ? 2 : 1;
                frame.SavedOptions = state.Options;
                frame.EntryOptions = frame.Kind == Utf8TreeNodeKind.Atomic
                    ? state.Options
                    : frame.IsLookbehind ? state.Options | RegexOptions.RightToLeft : state.Options & ~RegexOptions.RightToLeft;
                state.Options = frame.EntryOptions;
                context.State = state;
                frame.Stage = 1;
                child = new(OwnedParseProduction.Alternation, frame.Depth + 1);
                return true;
            }
            var body = context.Node;
            if (body is null)
            {
                state.Options = frame.SavedOptions;
                context.Fail(state);
                return false;
            }

            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                if (state.AtEnd) state.Fail(Utf8PatternError.UnclosedGroup);
                state.Options = frame.SavedOptions;
                context.Fail(state);
                return false;
            }

            state.Position++;
            var options = frame.EntryOptions;
            var assertion = Utf8TreeNode.Create(frame.Kind, options);
            assertion.Children = body;
            body.Parent = assertion;
            node = assertion;
            state.Options = frame.SavedOptions;
            context.Complete(state, node);
            return false;
        }

        static bool AddClassScalar(Utf8ConstructionBuffer<int> ranges, int value)
        {
            ranges.Add(value);
            ranges.Add(value + 1);
            return true;
        }
        static bool TryTakeToggle(ref ParserState state, int depth)
        {
            state.Position += 2;
            ParseOptionCluster(ref state, out var on, out var off);

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
            ParseOptionCluster(ref state, out scopeOn, out scopeOff);
            if (state.AtEnd || state.Pattern[state.Position] != ':')
            {
                return false;
            }
            state.Position++;
            scoped = (state.Options | scopeOn) & ~scopeOff;

            return true;
        }
        static void ParseOptionCluster(ref ParserState state, out RegexOptions on, out RegexOptions off)
        {
            on = RegexOptions.None;
            off = RegexOptions.None;
            var negate = false;
            while (!state.AtEnd)
            {
                Utf8ConstructionScope.ChargeWork(1);
                var character = state.Pattern[state.Position];
                if (character is '-' or '+') negate = character == '-';
                else if (IsScopeFlag(character))
                {
                    var flag = ScopeFlag(character);
                    if (negate) { off |= flag; on &= ~flag; }
                    else { on |= flag; off &= ~flag; }
                }
                else break;
                state.Position++;
            }
        }
        static bool IsIgnoredWhitespace(char character)
        {
            // .NET pattern whitespace is narrower than Unicode whitespace.
            // Vertical tab, NBSP and Unicode separators remain literal atoms.
            return character is ' ' or '\t' or '\n' or '\f' or '\r';
        }

        static void SkipPatternWhitespace(ref ParserState state)
        {
            if ((state.Options & RegexOptions.IgnorePatternWhitespace) == 0) return;
            while (!state.AtEnd)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (IsIgnoredWhitespace(state.Pattern[state.Position])) state.Position++;
                else if (state.Pattern[state.Position] == '#')
                {
                    while (!state.AtEnd && state.Pattern[state.Position] != '\n') {
                        Utf8ConstructionScope.ChargeWork(1);
                        state.Position++;
                    }
                }
                else break;
            }
        }

        static bool IsScopeFlag(char character) => character is 'i' or 'I' or 'm' or 'M' or 'n' or 'N' or 's' or 'S' or 'x' or 'X';

        static RegexOptions ScopeFlag(char character) => character switch
        {
            'i' or 'I' => RegexOptions.IgnoreCase,
            'm' or 'M' => RegexOptions.Multiline,
            'n' or 'N' => RegexOptions.ExplicitCapture,
            's' or 'S' => RegexOptions.Singleline,
            'x' or 'X' => RegexOptions.IgnorePatternWhitespace,
            _ => RegexOptions.None,
        };
        static bool IsCommentAhead(ParserState state)
        {
            return state.Position + 2 < state.Pattern.Length &&
                state.Pattern[state.Position + 1] == '?' &&
                state.Pattern[state.Position + 2] == '#';
        }
        static bool SkipComment(ref ParserState state)
        {
            Utf8ConstructionScope.ChargeWork((ulong)(state.Pattern.Length - state.Position - 3));
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
                Utf8ConstructionScope.ChargeWork(1);
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
            return Utf8TreeNode.Create(kind: Utf8TreeNodeKind.One, options: state.Options & ~RegexOptions.IgnoreCase,
                ch: character, str: null, m: 0, n: 0,
                children: null, parent: null, matchCaseInsensitive: (state.Options & RegexOptions.IgnoreCase) != 0);
        }

        static Utf8TreeNode Multi(ParserState state, string text)
        {
            return Utf8TreeNode.Create(kind: Utf8TreeNodeKind.Multi, options: state.Options & ~RegexOptions.IgnoreCase,
                ch: '\0', str: text, m: 0, n: 0,
                children: null, parent: null, matchCaseInsensitive: (state.Options & RegexOptions.IgnoreCase) != 0);
        }

        static Utf8TreeNode FoldedSet(RegexOptions options, char lower, char upper)
        {
            using var data = Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget);
            Span<int> ranges = stackalloc int[7] { 0, 4, 0, lower, (int)lower + 1, upper, (int)upper + 1 };
            data.AddRange(ranges);
            var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, options & ~RegexOptions.IgnoreCase, Utf8ScalarClass.Create(data));
            node.MatchCaseInsensitive = (options & RegexOptions.IgnoreCase) != 0;
            return node;
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

            if (parent.Children is Utf8ConstructionBuffer<Utf8TreeNode> siblings)
            {
                for (var i = 0; i < siblings.Count; i++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
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

        static Utf8TreeNode Concatenate(RegexOptions options, Utf8ConstructionBuffer<Utf8TreeNode> children)
        {
            var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, options);
            node.Children = children;
            foreach (var child in children)
            {
                Utf8ConstructionScope.ChargeWork(1);
                child.Parent = node;
            }

            return node;
        }

        static Utf8TreeNode Alternate(RegexOptions options, Utf8ConstructionBuffer<Utf8TreeNode> branches)
        {
            var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Alternate, options);
            node.Children = branches;
            foreach (var branch in branches)
            {
                Utf8ConstructionScope.ChargeWork(1);
                branch.Parent = node;
            }

            return node;
        }

        static bool TryTakeConditional(ref OwnedParseContext context, ref OwnedParseFrame frame, out OwnedParseFrame child)
        {
            child = default;
            var state = context.State;
            var depth = frame.Depth;
            if (frame.Stage == 0)
            {
                frame.EntryOptions = state.Options;
                frame.SavedExpressionDepth = state.ExpressionBranchDepth;
                var testParen = state.Position++;
                if (!state.AtEnd && IsAsciiDigit(state.Pattern[state.Position]))
                {
                    var start = state.Position;
                    while (!state.AtEnd && IsAsciiDigit(state.Pattern[state.Position]))
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        state.Position++;
                    }
                    frame.Digits = state.Storage.Budget.CopyText(state.Pattern.AsSpan(start, state.Position - start));
                    if (!TryParseGroupNumber(frame.Digits, out _) || state.AtEnd || state.Pattern[state.Position] != ')')
                    {
                        state.Fail(Utf8PatternError.InvalidConditional);
                        context.Fail(state);
                        return false;
                    }
                    state.Position++;
                }
                else
                {
                    var start = state.Position;
                    if (!state.AtEnd && IsAsciiConditionNameStart(state.Pattern[state.Position]))
                        while (!state.AtEnd && IsAsciiConditionNameChar(state.Pattern[state.Position]))
                        {
                            Utf8ConstructionScope.ChargeWork(1);
                            state.Position++;
                        }
                    if (state.Position > start && !state.AtEnd && state.Pattern[state.Position] == ')')
                    {
                        frame.Name = state.Storage.Budget.CopyText(state.Pattern.AsSpan(start, state.Position - start));
                        state.Position++;
                    }
                    else
                    {
                        state.Position = start;
                        if (!state.AtEnd && state.Pattern[state.Position] == '?')
                        {
                            if (state.Position + 1 >= state.Pattern.Length)
                            {
                                state.Fail(Utf8PatternError.InvalidConditional);
                                context.Fail(state);
                                return false;
                            }
                            var marker = state.Pattern[state.Position + 1];
                            if (marker is '#' or '\'' || IsScopeFlag(marker) || marker is '-' or '+' ||
                                marker == '<' && state.Position + 2 < state.Pattern.Length && state.Pattern[state.Position + 2] is not '=' and not '!')
                            {
                                state.Fail(Utf8PatternError.InvalidConditional);
                                context.Fail(state);
                                return false;
                            }
                            state.Position = testParen;
                            frame.Stage = 1;
                            child = new(OwnedParseProduction.Group, depth + 1);
                        }
                        else
                        {
                            frame.Stage = 2;
                            child = new(OwnedParseProduction.Alternation, depth + 1);
                        }
                        context.State = state;
                        return true;
                    }
                }
                frame.Stage = 3;
            }
            if (frame.Stage is 1 or 2)
            {
                var expression = context.Node;
                if (expression is null || frame.Stage == 2 && (state.AtEnd || state.Pattern[state.Position] != ')'))
                {
                    state.Fail(Utf8PatternError.InvalidConditional);
                    context.Fail(state);
                    return false;
                }
                if (frame.Stage == 2) state.Position++;
                if (frame.Stage == 2 || expression.Kind is not Utf8TreeNodeKind.PositiveLookaround and not Utf8TreeNodeKind.NegativeLookaround)
                {
                    var assertion = Utf8TreeNode.Create(Utf8TreeNodeKind.PositiveLookaround, frame.EntryOptions);
                    assertion.Children = expression;
                    expression.Parent = assertion;
                    expression = assertion;
                }
                if (frame.Stage == 1) state.IgnoreNextUnnamedGroup = true;
                frame.Expression = expression;
                state.Options = frame.EntryOptions;
                state.ExpressionBranchDepth = depth + 1;
                frame.Stage = 3;
            }
            if (frame.Stage == 3)
            {
                frame.YesIsEmpty = !state.AtEnd && state.Pattern[state.Position] is '|' or ')';
                if (frame.YesIsEmpty)
                    frame.YesBranch = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, frame.EntryOptions);
                else
                {
                    context.State = state;
                    frame.Stage = 4;
                    child = new(OwnedParseProduction.Concatenation, depth + 1);
                    return true;
                }
                frame.Stage = 5;
            }
            if (frame.Stage == 4)
            {
                frame.YesBranch = context.Node;
                if (frame.YesBranch is null) { context.Fail(state); return false; }
                frame.Stage = 5;
            }
            var yesBranch = frame.YesBranch;
            if (yesBranch is null) { context.Fail(state); return false; }
            if (frame.Stage == 5)
            {
                if (!state.AtEnd && state.Pattern[state.Position] == '|')
                {
                    state.Position++;
                    if (!state.AtEnd && state.Pattern[state.Position] == ')')
                    {
                        frame.Nodes = state.Storage.Buffer<Utf8TreeNode>();
                        frame.Nodes.Add(yesBranch);
                        frame.Nodes.Add(Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, frame.EntryOptions));
                    }
                    else
                    {
                        context.State = state;
                        frame.Stage = 6;
                        child = new(OwnedParseProduction.Concatenation, depth + 1);
                        return true;
                    }
                }
                else if (!state.AtEnd && state.Pattern[state.Position] == ')')
                {
                    frame.Nodes = state.Storage.Buffer<Utf8TreeNode>();
                    frame.Nodes.Add(yesBranch);
                    if (!frame.YesIsEmpty)
                        frame.Nodes.Add(Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, frame.EntryOptions));
                }
                else
                {
                    state.Fail(Utf8PatternError.InvalidConditional);
                    context.Fail(state);
                    return false;
                }
            }
            if (frame.Stage == 6)
            {
                var no = context.Node;
                if (no is null) { context.Fail(state); return false; }
                frame.Nodes = state.Storage.Buffer<Utf8TreeNode>();
                frame.Nodes.Add(yesBranch); frame.Nodes.Add(no);
            }
            if (state.AtEnd || state.Pattern[state.Position] != ')')
            {
                state.Fail(Utf8PatternError.InvalidConditional);
                context.Fail(state);
                return false;
            }
            state.Position++;
            state.Options = frame.EntryOptions;
            state.ExpressionBranchDepth = frame.SavedExpressionDepth;
            var branches = frame.Nodes ?? throw new InvalidOperationException("Missing conditional branches.");
            if (frame.Expression is not null) branches.Insert(0, frame.Expression);
            var conditional = Utf8TreeNode.Create(frame.Expression is null
                ? Utf8TreeNodeKind.BackreferenceConditional : Utf8TreeNodeKind.ExpressionConditional, frame.EntryOptions);
            if (frame.Name is not null) { conditional.Str = frame.Name; conditional.N = 0; }
            else conditional.N = -1;
            conditional.M = -1;
            conditional.Children = branches;
            foreach (var branch in branches)
            {
                Utf8ConstructionScope.ChargeWork(1);
                branch.Parent = conditional;
            }
            if (frame.Expression is null)
                state.AddBackrefFixup(new OwnedBackrefFixup(conditional, frame.Digits, 0, frame.Name, false, true, null));
            context.Complete(state, conditional);
            return false;
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

        static bool ParseGroup(ref OwnedParseContext context, ref OwnedParseFrame frame, out OwnedParseFrame child)
        {
            child = default;
            var state = context.State;
            var depth = frame.Depth;
            if (frame.Stage == 1)
            {
                // Delegated named/assertion/conditional productions already
                // completed both their state and their node.
                return false;
            }
            if (frame.Stage is 2 or 3)
            {
                var body = context.Node;
                if (body is null || state.AtEnd || state.Pattern[state.Position] != ')')
                {
                    if (state.AtEnd) state.Fail(Utf8PatternError.UnclosedGroup);
                    context.Fail(state);
                    return false;
                }
                state.Position++;
                var group = Utf8TreeNode.Create(frame.Kind, frame.EntryOptions);
                if (frame.Kind == Utf8TreeNodeKind.Capture) { group.M = frame.Slot; group.N = -1; }
                group.Children = body;
                body.Parent = group;
                if (frame.Stage == 2)
                {
                    if (frame.IsLeadingScope && frame.Contribution != RegexOptions.None) state.LeadingGroup = group;
                    if (frame.EnteredExplicit && state.ExplicitDepth > 0) state.ExplicitDepth--;
                    state.Options = frame.SavedOptions;
                }
                else state.Options = frame.EntryOptions;
                context.Complete(state, group);
                return false;
            }

            frame.OpenPosition = state.Position++;
            frame.Kind = Utf8TreeNodeKind.Capture;
            if (!state.AtEnd && state.Pattern[state.Position] == '?')
            {
                state.Position++;
                frame.Stage = 1;
                if (IsNamedGroupAhead(state)) child = new(OwnedParseProduction.NamedGroup, depth);
                else if (TryTakeLookaroundKind(state, out var kind, out var behind))
                    child = new(OwnedParseProduction.Lookaround, depth) { Kind = kind, IsLookbehind = behind };
                else if (!state.AtEnd && state.Pattern[state.Position] == '(')
                    child = new(OwnedParseProduction.Conditional, depth);
                else
                {
                    if (state.ExpressionBranchDepth == depth && !state.AtEnd &&
                        (IsScopeFlag(state.Pattern[state.Position]) || state.Pattern[state.Position] is '-' or '+'))
                    {
                        context.Fail(state);
                        return false;
                    }
                    if (!TryTakeOptionScope(ref state, out var scopedOptions, out var scopeOn, out var scopeOff))
                    {
                        context.Fail(state);
                        return false;
                    }
                    frame.IsLeadingScope = depth == 0 && state.Depth0Branch == 0 && state.Depth0Atom == 0;
                    frame.Contribution = scopeOn & ~scopeOff;
                    if (frame.IsLeadingScope)
                    {
                        state.TreeSeen = (state.TreeSeen | scopeOn) & ~scopeOff;
                        state.LeadingContribution = frame.Contribution;
                    }
                    frame.SavedOptions = state.Options;
                    state.Options = frame.EntryOptions = scopedOptions;
                    frame.EnteredExplicit = (scopedOptions & RegexOptions.ExplicitCapture) != 0;
                    if (frame.EnteredExplicit) state.ExplicitDepth++;
                    frame.Kind = Utf8TreeNodeKind.Group;
                    frame.Stage = 2;
                    child = new(OwnedParseProduction.Alternation, depth + 1);
                }
            }
            else
            {
                if ((state.Options & RegexOptions.ExplicitCapture) != 0)
                {
                    state.IgnoreNextUnnamedGroup = false;
                    frame.Kind = Utf8TreeNodeKind.Group;
                }
                else
                {
                    state.Groups++;
                    state.RecordCapturePosition(state.Groups, frame.OpenPosition);
                    if (state.IgnoreNextUnnamedGroup)
                    {
                        frame.Kind = Utf8TreeNodeKind.Group;
                        state.IgnoreNextUnnamedGroup = false;
                    }
                    else frame.Slot = ++state.ExecutionGroups;
                }
                frame.EntryOptions = state.Options;
                frame.Stage = 3;
                child = new(OwnedParseProduction.Alternation, depth + 1);
            }
            context.State = state;
            return true;
        }

        static bool ParseQuantifier(ref ParserState state, ref Utf8TreeNode atom, out bool decayedBrace)
        {
            decayedBrace = false;
            SkipPatternWhitespace(ref state);
            if (state.AtEnd)
            {
                return true;
            }

            var character = state.Pattern[state.Position];
            int minimum;
            int maximum;
            var maximumInfinite = false;
            if (character == '*')
            {
                minimum = 0;
                maximum = Infinite;
                maximumInfinite = true;
                state.Position++;
            }
            else if (character == '+')
            {
                minimum = 1;
                maximum = Infinite;
                maximumInfinite = true;
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
                if (!ParseBrace(ref state, out minimum, out maximum, out maximumInfinite, out var braceDecayed))
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

            SkipPatternWhitespace(ref state);
            var lazy = false;
            if (!state.AtEnd && state.Pattern[state.Position] == '?')
            {
                lazy = true;
                state.Position++;
            }

            atom = Quantify(state, atom, minimum, maximum, lazy);
            if (minimum != 1 || maximum != 1)
                atom.IsUnboundedRepeat = maximumInfinite;
            return true;
        }

        // Parses a '{m}', '{m,}' or '{m,n}' quantifier tail with the position on '{'.
        // Returns true with minimum/maximum set and the position past '}' for a complete
        // valid shape. A brace that cannot complete (non-digit opener, unclosed digits,
        // closed garbage) decays to a literal: the position rewinds past '{' and
        // decayToLiteral is set, exactly like BCL. Only a complete invalid shape
        // (reversed bounds, numeric overflow) rejects.
        static bool ParseBrace(ref ParserState state, out int minimum, out int maximum, out bool maximumInfinite, out bool decayToLiteral)
        {
            minimum = 0;
            maximum = 0;
            maximumInfinite = false;
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
                Utf8ConstructionScope.ChargeWork(1);
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
                        Utf8ConstructionScope.ChargeWork(1);
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
                return state.Fail(Utf8PatternError.InvalidQuantifier);
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
                return Utf8TreeNode.Create(Utf8TreeNodeKind.Empty, atom.Options);
            }

            if (atom.Kind == Utf8TreeNodeKind.One)
            {
                return Utf8TreeNode.Create(kind: lazy ? Utf8TreeNodeKind.Onelazy : Utf8TreeNodeKind.Oneloop, options: state.Options & ~RegexOptions.IgnoreCase,
                    ch: atom.Ch, str: null, m: minimum, n: maximum,
                    children: null, parent: null, matchCaseInsensitive: atom.MatchCaseInsensitive);
            }

            if (atom.Kind == Utf8TreeNodeKind.Set)
            {
                return Utf8TreeNode.CreateSetRepeat(atom, state.Options & ~RegexOptions.IgnoreCase, minimum, maximum, lazy);
            }

            // Loop wrappers carry the atom options, not the site options: a directly
            // wrapped scope marks the loop with its scoped options in the provider shape.
            // All other atoms carry entry options identical to the site options.
            var loop = Utf8TreeNode.Create(lazy ? Utf8TreeNodeKind.Lazyloop : Utf8TreeNodeKind.Loop, atom.Options, minimum, maximum);
            loop.Children = atom;
            atom.Parent = loop;
            return loop;
        }
    }

    private static void FreezeChildren(Utf8ConstructionSession storage, Utf8TreeNode root)
    {
        using var frames = Utf8ConstructionFrames<(Utf8TreeNode Node, int NextChild)>.Create(storage.Budget, (root, 0));
        while (frames.Count > 0)
        {
            storage.Budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var node = frame.Node;
            if (frame.NextChild == 0 && node.Children is Utf8ConstructionBuffer<Utf8TreeNode> children)
                node.Children = storage.Copy(children, true);
            if (frame.NextChild < node.ChildCount)
            {
                frames[index] = (node, frame.NextChild + 1);
                frames.Push((node.Child(frame.NextChild), 0));
            }
            else frames.Pop();
        }
    }

    private enum OwnedParseProduction { Alternation, Concatenation, Group, NamedGroup, Lookaround, Conditional }

    // Each suspended production occupies admitted value-array capacity. Payload
    // references borrow separately admitted parser nodes/text/scratch buffers.
    private struct OwnedParseFrame(OwnedParseProduction production, int depth)
    {
        internal OwnedParseProduction Production = production;
        internal int Depth = depth;
        internal int Stage, OpenPosition, Slot, SavedExpressionDepth;
        internal RegexOptions EntryOptions, SavedOptions, Contribution;
        internal Utf8TreeNodeKind Kind;
        internal bool IsLookbehind, IsPop, IsLeadingScope, EnteredExplicit, YesIsEmpty;
        internal string? Name, PopName, Digits;
        internal Utf8TreeNode? Expression, YesBranch;
        internal Utf8ConstructionBuffer<Utf8TreeNode>? Nodes;
        internal Utf8ConstructionText? Run;
    }

    private struct OwnedParseContext
    {
        internal ParserState State;
        internal Utf8TreeNode? Node;
        internal bool Failed;

        internal OwnedParseContext(ParserState state) => State = state;

        internal void Complete(ParserState state, Utf8TreeNode? node)
        {
            State = state;
            Node = node;
        }

        internal void Fail(ParserState state)
        {
            if (state.Failure.Error == Utf8PatternError.None)
                state.Failure = new(Utf8PatternError.InvalidSyntax, state.Position);
            State = state;
            Node = null;
            Failed = true;
        }
    }

    private struct ParserState
    {
        public readonly Utf8ConstructionSession Storage;
        public string Pattern;
        public RegexOptions Options;
        public CultureInfo ConstructionCulture;
        public Utf8ScalarCaseBehavior CaseBehavior;
        public int Position;
        public int Groups;
        public int ExecutionGroups;
        public Utf8ConstructionMap<int, int>? CapturePositions;
        public bool IgnoreNextUnnamedGroup;
        public RegexOptions TreeSeen;
        public int Depth0Branch;
        public int Depth0Atom;
        public RegexOptions LeadingContribution;
        public Utf8TreeNode? LeadingGroup;
        public int ExplicitDepth;
        public int ExpressionBranchDepth;
        public Utf8ConstructionBuffer<OwnedGroupDefinition>? GroupDefinitions;
        public Utf8ConstructionBuffer<OwnedBackrefFixup>? BackrefFixups;
        public Utf8ConstructionBuffer<int>? RequiredSlots;
        public Utf8CaptureCatalog? CaptureCatalog;
        public bool UsesLiteralClassBracketSyntax;
        public bool UsesIgnoreCase;
        public Utf8PatternDiagnostic Failure;

        public bool Fail(Utf8PatternError error) => Fail(error, Position);

        public bool Fail(Utf8PatternError error, int offset)
        {
            if (Failure.Error == Utf8PatternError.None) Failure = new(error, offset);
            return false;
        }

        public ParserState(string pattern, RegexOptions options, Utf8ConstructionSession storage)
        {
            Storage = storage;
            Pattern = pattern;
            Options = options;
            ConstructionCulture = Utf8OwnedOptionPrelude.GetTargetCulture(options);
            CaseBehavior = Utf8SliceCaseEquivalences.GetBehavior(ConstructionCulture, options);
            Position = 0;
            Groups = 0;
            ExecutionGroups = 0;
            CapturePositions = null;
            IgnoreNextUnnamedGroup = false;
            TreeSeen = RegexOptions.None;
            Depth0Branch = 0;
            Depth0Atom = 0;
            LeadingContribution = RegexOptions.None;
            LeadingGroup = null;
            ExplicitDepth = 0;
            ExpressionBranchDepth = -1;
            GroupDefinitions = null;
            BackrefFixups = null;
            RequiredSlots = null;
            CaptureCatalog = null;
            UsesLiteralClassBracketSyntax = false;
            UsesIgnoreCase = false;
            Failure = default;
        }

        public void AddGroupDefinition(OwnedGroupDefinition definition) =>
            (GroupDefinitions ??= Utf8ConstructionBuffer<OwnedGroupDefinition>.Create(Utf8ConstructionScope.CurrentBudget)).Add(definition);

        public void AddBackrefFixup(OwnedBackrefFixup fixup) =>
            (BackrefFixups ??= Utf8ConstructionBuffer<OwnedBackrefFixup>.Create(Utf8ConstructionScope.CurrentBudget)).Add(fixup);

        public void RequireSlot(int slot) =>
            (RequiredSlots ??= Utf8ConstructionBuffer<int>.Create(Utf8ConstructionScope.CurrentBudget)).Add(slot);

        public void RecordCapturePosition(int slot, int position)
        {
            if ((Options & RegexOptions.ECMAScript) == 0) return;
            var positions = CapturePositions ??= Utf8ConstructionMap<int, int>.Create(
                Utf8ConstructionScope.CurrentBudget, EqualityComparer<int>.Default);
            if (!positions.TryGetValue(slot, out var previous) || position < previous)
                positions[slot] = position;
        }

        public bool AtEnd => Position >= Pattern.Length;
    }

    // Transient declaration/fixup fields live in admitted value-array storage.
    // Nodes/text borrow the parser's separately admitted acquisitions.
    private readonly struct OwnedGroupDefinition(Utf8TreeNode node, string name, int declared, bool isAuto, int openPosition)
    {
        public readonly Utf8TreeNode Node = node;
        public readonly string Name = name;
        public readonly int Declared = declared;
        public readonly bool IsAuto = isAuto;
        public readonly int OpenPosition = openPosition;
    }

    private readonly struct OwnedBackrefFixup(Utf8TreeNode node, string? digits, int referencePosition,
        string? name, bool isPop, bool isConditional, Utf8TreeNode? wrapper)
    {
        public readonly Utf8TreeNode Node = node;
        public readonly string? Digits = digits;
        public readonly int ReferencePosition = referencePosition;
        public readonly string? Name = name;
        public readonly bool IsPop = isPop;
        public readonly bool IsConditional = isConditional;
        public readonly Utf8TreeNode? Wrapper = wrapper;
    }
}
