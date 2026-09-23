using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Recognizes the owned R02a slice: ASCII-only patterns built from
/// literals, concatenation, ordered alternation, greedy/lazy repetition,
/// numbered groups, (R03) dots with Singleline-scoped newline matching,
/// and anchors with Multiline-scoped line edges.
/// Anything else (classes, anchors, boundaries,
/// lookarounds, backreferences, named/non-capturing groups, shorthand escapes,
/// case/direction-sensitive options) declines to <c>null</c> so the existing
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
        RegexOptions.IgnoreCase |
        RegexOptions.RightToLeft |
        RegexOptions.ECMAScript |
        RegexOptions.NonBacktracking |
        RegexOptions.IgnorePatternWhitespace |
        RegexOptions.ExplicitCapture;

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

        foreach (var character in pattern)
        {
            if (character > 127)
            {
                return null;
            }
        }

        var parser = new Parser(pattern, (options & RegexOptions.Singleline) != 0, (options & RegexOptions.Multiline) != 0);
        if (!parser.TryParseAlternation(0, out var root) || parser.Position != pattern.Length)
        {
            return null;
        }

        root ??= new Utf8SliceNode.EmptyNode();
        return Utf8SliceLowerer.Lower(root);
    }

    private sealed class Parser
    {
        private readonly string _pattern;
        private readonly bool _matchNewline;
        private readonly bool _matchMultiline;
        private int _position;
        private int _groups;

        internal Parser(string pattern, bool matchNewline, bool matchMultiline)
        {
            _pattern = pattern;
            _matchNewline = matchNewline;
            _matchMultiline = matchMultiline;
        }

        internal int Position => _position;
        internal int GroupCount => _groups;

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

                    _position++;
                    var slot = _groups++;
                    if (!TryParseAlternation(depth + 1, out var body) || AtEnd || _pattern[_position] != ')')
                    {
                        return false;
                    }

                    _position++;
                    node = new Utf8SliceNode.CaptureNode(slot, body ?? new Utf8SliceNode.EmptyNode());
                    return true;
                case '\\':
                    if (_position + 1 >= _pattern.Length)
                    {
                        return false;
                    }

                    var escaped = _pattern[_position + 1];
                    if (!IsEscapablePunctuation(escaped))
                    {
                        return false;
                    }

                    _position += 2;
                    node = new Utf8SliceNode.LiteralNode((byte)escaped);
                    return true;
                case ')':
                case '|':
                case '*':
                case '+':
                case '?':
                case '{':
                case '}':
                case '[':
                case ']':
                    return false;
                case '^':
                    _position++;
                    node = new Utf8SliceNode.AnchorNode(true, _matchMultiline);
                    return true;
                case '$':
                    _position++;
                    node = new Utf8SliceNode.AnchorNode(false, _matchMultiline);
                    return true;
                case '.':
                    _position++;
                    node = new Utf8SliceNode.DotNode(_matchNewline);
                    return true;
                default:
                    _position++;
                    node = new Utf8SliceNode.LiteralNode((byte)character);
                    return true;
            }
        }

        private static bool IsEscapablePunctuation(char character)
        {
            return character is '.' or '$' or '^' or '{' or '}' or '[' or ']' or '(' or ')' or '|' or '*' or '+' or '?' or '\\';
        }

        private bool AtEnd => _position >= _pattern.Length;
    }
}
