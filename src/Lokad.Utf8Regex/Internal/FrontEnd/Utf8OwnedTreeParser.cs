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
            var runStart = -1;
            while (!state.AtEnd)
            {
                var character = state.Pattern[state.Position];
                if (character == '|' || character == ')')
                {
                    break;
                }

                RuntimeFrontEnd.RegexNode unit;
                if (IsQuantifierStart(character) && runStart >= 0)
                {
                    unit = SplitLastChar(ref state, ref runStart, children);
                }
                else
                {
                    if (IsLiteralChar(character))
                    {
                        if (runStart < 0)
                        {
                            runStart = state.Position;
                        }

                        state.Position++;
                        continue;
                    }

                    FlushRun(ref state, children, ref runStart);
                    if (!ParseGroup(ref state, depth, out var group) || group is null)
                    {
                        return false;
                    }

                    unit = group;
                }

                if (!ParseQuantifier(ref state, ref unit))
                {
                    return false;
                }

                children.Add(unit);
            }

            FlushRun(ref state, children, ref runStart);
            if (children.Count == 0)
            {
                node = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, state.Options);
                return true;
            }

            node = children.Count == 1 ? children[0] : Concatenate(state, children);
            return true;
        }
        static void FlushRun(ref ParserState state, List<RuntimeFrontEnd.RegexNode> children, ref int runStart)
        {
            if (runStart < 0)
            {
                return;
            }

            children.Add(Run(state, state.Pattern.Substring(runStart, state.Position - runStart)));
            runStart = -1;
        }

        static RuntimeFrontEnd.RegexNode SplitLastChar(ref ParserState state, ref int runStart, List<RuntimeFrontEnd.RegexNode> children)
        {
            var last = state.Pattern[state.Position - 1];
            if (state.Position - runStart > 1)
            {
                children.Add(Run(state, state.Pattern.Substring(runStart, state.Position - runStart - 1)));
            }

            runStart = -1;
            return One(state, last);
        }

        static RuntimeFrontEnd.RegexNode Run(ParserState state, string text)
        {
            return text.Length == 1 ? One(state, text[0]) : Multi(state, text);
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
