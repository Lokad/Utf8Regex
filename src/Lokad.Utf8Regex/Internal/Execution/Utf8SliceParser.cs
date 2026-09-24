using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Recognizes the owned R02a slice: ASCII-only patterns built from
/// literals, concatenation, ordered alternation, greedy/lazy repetition,
/// numbered and named groups (BCL unnamed-first numbering), balancing
/// captures, transparent non-capturing groups and inline comments, (R03) dots
/// with Singleline-scoped newline matching, anchors with Multiline-scoped line
/// edges, ASCII character classes (literals, ranges, supported escapes, and
/// shorthand members), word boundaries, shorthand classes, backreferences,
/// and zero-width lookarounds (dots, negated classes, and negated shorthands
/// decline astral inputs at routing).
/// Anything else (conditionals, duplicate or explicitly-numbered names,
/// categories, case/direction-sensitive options, captures or pops inside
/// lookbehind bodies of balancing shapes) declines to <c>null</c> so the existing
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
                case Utf8SliceNode.BalanceNode balance:
                    return new Utf8SliceNode.BalanceNode(
                        finals[balance.PopSlot] - 1,
                        balance.CaptureSlot < 0 ? -1 : finals[balance.CaptureSlot] - 1,
                        RenumberCaptures(balance.Body, finals));
                default:
                    return node;
            }
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

        // Balancing stacks live in attempt-local chunks while nested
        // lookbehind runs share live slots and heads: a capture or pop
        // inside a lookbehind body would alias shared balance heads through
        // the nested tables, so such mixes decline to legacy-exact routing.
        // (Inline lookahead shares the same tables and stays owned.)
        if (parser.SawBalancing && parser.SawLookbehindGroupWrite)
        {
            return null;
        }

        return Utf8SliceLowerer.Lower(RenumberCaptures(root, finals), groupNames, parser.SawBalancing);
    }

    private sealed class Parser
    {
        private readonly string _pattern;
        private readonly bool _matchNewline;
        private readonly bool _matchMultiline;
        private int _position;
        private int _groups;
        private readonly List<(int Slot, string? Name)> _groupDecls = new();
        private readonly Dictionary<string, int> _groupSlots = new(StringComparer.Ordinal);
        private bool _sawBalancing;
        private bool _sawFiniteMaximumLazyRepeat;
        private int _lookbehindDepth;
        private bool _sawLookbehindGroupWrite;

        internal Parser(string pattern, bool matchNewline, bool matchMultiline)
        {
            _pattern = pattern;
            _matchNewline = matchNewline;
            _matchMultiline = matchMultiline;
        }

        internal int Position => _position;
        internal int GroupCount => _groups;
        internal List<(int Slot, string? Name)> GroupDecls => _groupDecls;
        internal bool SawBalancing => _sawBalancing;
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
                            _lookbehindDepth++;
                            var parsedBehind = TryParseAlternation(depth + 1, out var lookbehind);
                            _lookbehindDepth--;
                            if (!parsedBehind || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            node = new Utf8SliceNode.LookbehindNode(lookbehind ?? new Utf8SliceNode.EmptyNode(), behindPositive);
                            return true;
                        }

                        if (marker == ':')
                        {
                            // Non-capturing groups are transparent: the body
                            // lowers as-is with no slot and no group number.
                            _position += 3;
                            if (!TryParseAlternation(depth + 1, out var plainBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            node = plainBody ?? new Utf8SliceNode.EmptyNode();
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
                                if (!TryParseAlternation(depth + 1, out var balanceBody) || AtEnd || _pattern[_position] != ')')
                                {
                                    return false;
                                }

                                _position++;
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
                            if (!TryParseAlternation(depth + 1, out var namedBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            node = new Utf8SliceNode.CaptureNode(namedSlot, namedBody ?? new Utf8SliceNode.EmptyNode());
                            return true;
                        }
                    }

                    _position++;
                    var slot = _groups++;
                    _groupDecls.Add((slot, null));
                    if (_lookbehindDepth > 0)
                    {
                        _sawLookbehindGroupWrite = true;
                    }
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

            var negated = false;
            if (!AtEnd && _pattern[_position] == '^')
            {
                negated = true;
                _position++;
            }

            var bits = new bool[128];
            var memberBits = 0;
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

            node = new Utf8SliceNode.SetNode(mask, negated, members);
            return true;
        }

        private bool AtEnd => _position >= _pattern.Length;
    }
}
