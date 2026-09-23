using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Recognizes the owned R02a slice: ASCII-only patterns built from
/// literals, concatenation, ordered alternation, greedy/lazy repetition,
/// numbered groups, (R03) dots with Singleline-scoped newline matching,
/// anchors with Multiline-scoped line edges, and ASCII character classes
/// (literals, ranges, and supported escapes; negated classes decline astral
/// inputs at routing).
/// Anything else (boundaries,
/// backreferences, named/non-capturing groups, shorthand escapes,
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
        return Utf8SliceLowerer.Lower(root);
    }

    private sealed class Parser
    {
        private readonly string _pattern;
        private readonly bool _matchNewline;
        private readonly bool _matchMultiline;
        private int _position;
        private int _groups;
        private bool _sawFiniteMaximumLazyRepeat;

        internal Parser(string pattern, bool matchNewline, bool matchMultiline)
        {
            _pattern = pattern;
            _matchNewline = matchNewline;
            _matchMultiline = matchMultiline;
        }

        internal int Position => _position;
        internal int GroupCount => _groups;
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
                            if (!TryParseAlternation(depth + 1, out var lookahead) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            node = new Utf8SliceNode.LookaheadNode(lookahead ?? new Utf8SliceNode.EmptyNode(), positive);
                            return true;
                        }

                        if (marker == '<' && _position + 3 < _pattern.Length &&
                            (_pattern[_position + 3] == '=' || _pattern[_position + 3] == '!'))
                        {
                            var behindPositive = _pattern[_position + 3] == '=';
                            _position += 4;
                            if (!TryParseAlternation(depth + 1, out var lookbehind) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            node = new Utf8SliceNode.LookbehindNode(lookbehind ?? new Utf8SliceNode.EmptyNode(), behindPositive);
                            return true;
                        }
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

                    // Single-digit backreferences bind earlier groups only:
                    // a following digit makes the escape multi-character
                    // (octal or two-digit reference in BCL), and undefined
                    // numbers stay legacy-exact (BCL throws or octal-matches).
                    if (escaped >= '1' && escaped <= '9' &&
                        (_position + 2 >= _pattern.Length || !char.IsAsciiDigit(_pattern[_position + 2])) &&
                        escaped - '0' <= _groups)
                    {
                        _position += 2;
                        node = new Utf8SliceNode.BackrefNode(escaped - '0' - 1);
                        return true;
                    }

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
                    node = new Utf8SliceNode.LiteralNode((byte)character);
                    return true;
            }
        }

        private static bool IsEscapablePunctuation(char character)
        {
            return character is '.' or '$' or '^' or '{' or '}' or '[' or ']' or '(' or ')' or '|' or '*' or '+' or '?' or '\\';
        }

        // ASCII character classes, mirroring the pinned runtime parser:
        // literals (with `]`-first and `-`/POSIX-edge rules), ascending
        // ranges (escapes allowed as bounds), and supported single-char
        // escapes. Anything Unicode, shorthand, or structural (categories,
        // hex/octal/control escapes, subtraction, reversed ranges) declines
        // to null so legacy routing preserves exact behavior including
        // construction failures. `[` followed by `:`, `.`, or `=` also
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

            var negated = false;
            if (!AtEnd && _pattern[_position] == '^')
            {
                negated = true;
                _position++;
            }

            var bits = new bool[128];
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
                    if (escaped is 'd' or 'D' or 's' or 'S' or 'w' or 'W' or 'p' or 'P')
                    {
                        return false;
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
                        case '0':
                            if (_position + 1 < _pattern.Length && char.IsAsciiDigit(_pattern[_position + 1]))
                            {
                                return false;
                            }

                            value = 0;
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

            var mask = new byte[16];
            for (var member = 0; member < 128; member++)
            {
                if (bits[member])
                {
                    mask[member >> 3] |= (byte)(1 << (member & 7));
                }
            }

            node = new Utf8SliceNode.SetNode(mask, negated);
            return true;
        }

        private bool AtEnd => _position >= _pattern.Length;
    }
}
