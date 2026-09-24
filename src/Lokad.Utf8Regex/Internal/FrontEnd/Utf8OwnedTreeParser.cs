using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Stage 1 of the owned parser strangler: literals, concatenation,
// alternation, numbered groups, and quantifiers lower to runtime tree nodes.
// Anything else declines (returns null) so the vendored parser keeps exact
// behavior, including construction throws. Output nodes intentionally mirror
// the runtime tree shapes the differential battery pins; later stages widen
// the subset and shrink the vendored caller. Recursion is depth-capped and
// brace numbers saturate at 32 bits; over-deep or overflowing shapes decline
// to legacy routing. Parent links are wired exactly like children.
// Parsing lives in static locals over an explicit state record, so the debt
// ceiling sees a single entry point.
internal static class Utf8OwnedTreeParser
{
    private const int MaxNestingDepth = 64;
    private const int Infinite = 2147483647;

    public static RuntimeFrontEnd.RegexTree? TryParse(string pattern, RegexOptions options)
    {
        var state = new ParserState(pattern, options);
        if (!ParseAlternation(ref state, 0, out var root) || root is null || state.Position != pattern.Length)
        {
            return null;
        }

        var tree = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Capture, options);
        tree.M = 0;
        tree.N = -1;
        tree.Children = root;
        root.Parent = tree;
        var captureNames = new string[state.Groups + 1];
        for (var number = 0; number < captureNames.Length; number++)
        {
            captureNames[number] = number.ToString();
        }

        return new RuntimeFrontEnd.RegexTree(
            tree,
            state.Groups + 1,
            captureNames,
            null,
            null,
            options,
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
                    if (!DecodeEscape(ref state, out var escaped))
                    {
                        return false;
                    }

                    run.Append(escaped);
                    continue;
                }

                if (IsLiteralChar(character))
                {
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

                FlushRun(state, children, run);
                if (!ParseGroup(ref state, depth, out var group) || group is null)
                {
                    return false;
                }

                var atom = group;
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
                    if (state.Position + 1 < state.Pattern.Length)
                    {
                        var follow = state.Pattern[state.Position + 1];
                        if (follow >= '0' && follow <= '7')
                        {
                            return false;
                        }
                    }

                    value = '\0';
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
                    return false;
            }
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
                if (state.AtEnd || state.Pattern[state.Position] != ':')
                {
                    return false;
                }

                state.Position++;
                kind = RuntimeFrontEnd.RegexNodeKind.Group;
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

            if (atom.Kind == RuntimeFrontEnd.RegexNodeKind.Capture &&
                atom.ChildCount == 1 &&
                atom.Child(0).Kind == RuntimeFrontEnd.RegexNodeKind.One)
            {
                var inner = new RuntimeFrontEnd.RegexNode(
                    lazy ? RuntimeFrontEnd.RegexNodeKind.Onelazy : RuntimeFrontEnd.RegexNodeKind.Oneloop,
                    state.Options,
                    atom.Child(0).Ch)
                {
                    M = minimum,
                    N = maximum,
                };
                inner.Parent = atom;
                atom.Children = inner;
                return atom;
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

        public ParserState(string pattern, RegexOptions options)
        {
            Pattern = pattern;
            Options = options;
            Position = 0;
            Groups = 0;
        }

        public bool AtEnd => Position >= Pattern.Length;
    }
}
