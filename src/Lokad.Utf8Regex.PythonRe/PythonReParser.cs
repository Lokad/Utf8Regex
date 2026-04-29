using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

internal sealed class PythonReParser
{
    private const string PatternBadEscapeLetters = "ceghijklmopqyCEFGHIJKLMNOPQRTVXY";
    private const string CharacterClassBadEscapeLetters = "ceghijklmopqyzABCEFGHIJKLMOPQRTVXYZ";
    private const string AsciiWordClass = "[A-Za-z0-9_]";
    private const string AsciiNonWordClass = "[^A-Za-z0-9_]";
    private const string AsciiDigitClass = "[0-9]";
    private const string AsciiNonDigitClass = "[^0-9]";
    private const string AsciiSpaceClass = @"[\t\n\r\f\v ]";
    private const string AsciiNonSpaceClass = @"[^\t\n\r\f\v ]";
    private const string AsciiWordBoundary = @"(?:(?<![A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<=[A-Za-z0-9_])(?![A-Za-z0-9_]))";
    private const string AsciiNonWordBoundary = @"(?:(?<=[A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<![A-Za-z0-9_])(?![A-Za-z0-9_]))";
    private const string OctalDigits = "01234567";
    private const string HexDigits = "0123456789abcdefABCDEF";
    private readonly string _pattern;
    private readonly Dictionary<string, int> _namedGroups = new(StringComparer.Ordinal);
    private readonly HashSet<string> _openNamedGroups = new(StringComparer.Ordinal);
    private readonly HashSet<int> _openGroupNumbers = [];
    private int _position;
    private int _groupCount;
    private PythonReCompileOptions _leadingInlineOptions;

    public PythonReParser(string pattern)
    {
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
    }

    public PythonReParseResult Parse(PythonReCompileOptions options)
    {
        ConsumeLeadingInlineFlags(ref options);
        var node = ParseAlternation(options);
        if (!IsAtEnd)
        {
            if (Current == ')')
            {
                throw Error("unbalanced parenthesis");
            }

            throw Error("unexpected character");
        }

        return new PythonReParseResult(node, options, _groupCount, new Dictionary<string, int>(_namedGroups, StringComparer.Ordinal));
    }

    private void ConsumeLeadingInlineFlags(ref PythonReCompileOptions options)
    {
        while (TryConsumeLeadingInlineFlagGroup(out var add))
        {
            options |= add;
            _leadingInlineOptions |= add;
        }
    }

    private bool TryConsumeLeadingInlineFlagGroup(out PythonReCompileOptions add)
    {
        add = PythonReCompileOptions.None;
        if (!_pattern.AsSpan(_position).StartsWith("(?", StringComparison.Ordinal))
        {
            return false;
        }

        var probe = _position + 2;
        if (probe >= _pattern.Length || !IsInlineFlagChar(_pattern[probe]))
        {
            return false;
        }

        while (probe < _pattern.Length && IsInlineFlagChar(_pattern[probe]))
        {
            probe++;
        }

        if (probe >= _pattern.Length || _pattern[probe] != ')')
        {
            return false;
        }

        _position += 2;
        add = ParseFlagSet(allowEmpty: false, FlagParseContext.Added);
        _position++;
        return true;
    }

    private PythonReNode ParseAlternation(PythonReCompileOptions options)
    {
        var branches = new List<PythonReNode>();
        branches.Add(ParseSequence(options));
        while (TryConsume('|'))
        {
            branches.Add(ParseSequence(options));
        }

        return branches.Count == 1
            ? branches[0]
            : new PythonReAlternationNode(branches);
    }

    private PythonReNode ParseSequence(PythonReCompileOptions options)
    {
        var elements = new List<PythonReNode>();
        while (true)
        {
            SkipIgnoredPatternTrivia(options);
            if (IsAtEnd || Current == ')' || Current == '|')
            {
                break;
            }

            elements.Add(ParseQuantifiedAtom(options));
        }

        return elements.Count switch
        {
            0 => new PythonReSequenceNode([]),
            1 => elements[0],
            _ => new PythonReSequenceNode(elements),
        };
    }

    private PythonReNode ParseQuantifiedAtom(PythonReCompileOptions options)
    {
        var atom = ParseAtom(options);
        if (IsAtEnd)
        {
            return atom;
        }

        int min;
        int? max;
        if (TryConsume('*'))
        {
            min = 0;
            max = null;
        }
        else if (TryConsume('+'))
        {
            min = 1;
            max = null;
        }
        else if (TryConsume('?'))
        {
            min = 0;
            max = 1;
        }
        else if (TryConsume('{'))
        {
            min = ParseNumber();
            max = min;
            if (TryConsume(','))
            {
                max = TryPeekDigit() ? ParseNumber() : null;
            }

            if (!TryConsume('}'))
            {
                throw Error("missing }");
            }

            if (max.HasValue && max.Value < min)
            {
                throw Error("numbers out of order in quantifier");
            }
        }
        else
        {
            return atom;
        }

        var flavor = PythonReQuantifierFlavor.Greedy;
        if (TryConsume('?'))
        {
            flavor = PythonReQuantifierFlavor.Reluctant;
        }
        else if (TryConsume('+'))
        {
            flavor = PythonReQuantifierFlavor.Possessive;
        }

        if (!IsAtEnd && Current is '*' or '+' or '?' or '{')
        {
            throw new PythonRePatternException("multiple repeat", _position);
        }

        return new PythonReQuantifierNode(atom, min, max, flavor);
    }

    private PythonReNode ParseAtom(PythonReCompileOptions options)
    {
        if (!IsAtEnd && Current is '*' or '+' or '?' or '{')
        {
            throw new PythonRePatternException("nothing to repeat", _position);
        }

        if (TryConsume('('))
        {
            return ParseGroup(options);
        }

        if (TryConsume('['))
        {
            return ParseCharacterClass(options);
        }

        if (TryConsume('\\'))
        {
            return ParseEscape(_position - 1, options);
        }

        if (TryConsume('.'))
        {
            return new PythonReRawNode(".", PythonReRawKind.Dot);
        }

        if (TryConsume('^'))
        {
            return new PythonReRawNode("^", PythonReRawKind.StartAnchor);
        }

        if (TryConsume('$'))
        {
            return new PythonReRawNode("$", PythonReRawKind.EndAnchor);
        }

        return new PythonReLiteralNode(Consume());
    }

    private PythonReNode ParseGroup(PythonReCompileOptions options)
    {
        var groupStart = _position - 1;
        if (!TryConsume('?'))
        {
            var groupNumber = _groupCount + 1;
            _groupCount = groupNumber;
            _openGroupNumbers.Add(groupNumber);
            var inner = ParseAlternation(options);
            _openGroupNumbers.Remove(groupNumber);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.Capturing, inner);
        }

        if (IsAtEnd)
        {
            throw new PythonRePatternException("unexpected end of pattern", _position);
        }

        if (TryConsume('#'))
        {
            while (!IsAtEnd && !TryConsume(')'))
            {
                _position++;
            }

            if (IsAtEnd && (_position == 0 || _pattern[_position - 1] != ')'))
            {
                throw new PythonRePatternException("missing ), unterminated comment", groupStart);
            }

            return new PythonReSequenceNode([]);
        }

        if (TryConsume(':'))
        {
            var inner = ParseAlternation(options);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.NonCapturing, inner);
        }

        if (TryConsume('>'))
        {
            var inner = ParseAlternation(options);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.Atomic, inner);
        }

        if (TryConsume('='))
        {
            var inner = ParseAlternation(options);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.PositiveLookahead, inner);
        }

        if (TryConsume('!'))
        {
            var inner = ParseAlternation(options);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.NegativeLookahead, inner);
        }

        if (TryConsumeExact("<="))
        {
            var inner = ParseAlternation(options);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.PositiveLookbehind, inner);
        }

        if (TryConsumeExact("<!"))
        {
            var inner = ParseAlternation(options);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.NegativeLookbehind, inner);
        }

        if (TryConsumeExact("P<"))
        {
            var groupNumber = _groupCount + 1;
            _groupCount = groupNumber;
            var name = ParseGroupName('>', "missing >, unterminated name");
            if (!_namedGroups.TryAdd(name, groupNumber))
            {
                throw Error($"redefinition of group name '{name}'");
            }

            _openNamedGroups.Add(name);
            _openGroupNumbers.Add(groupNumber);
            var inner = ParseAlternation(options);
            _openNamedGroups.Remove(name);
            _openGroupNumbers.Remove(groupNumber);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.NamedCapturing, inner, name);
        }

        if (TryConsumeExact("P="))
        {
            var referenceStart = _position;
            var name = ParseGroupName(')', "missing ), unterminated name");
            if (_openNamedGroups.Contains(name))
            {
                throw new PythonRePatternException("cannot refer to an open group", referenceStart);
            }

            return new PythonReNamedBackreferenceNode(name);
        }

        if (TryConsume('('))
        {
            var condition = ParseConditionalReference();
            ExpectGroupEnd(groupStart, "missing ), unterminated name");
            var yesBranch = ParseConditionalBranch(options);
            PythonReNode? noBranch = null;
            if (TryConsume('|'))
            {
                noBranch = ParseConditionalBranch(options);
                if (!IsAtEnd && Current == '|')
                {
                    throw new PythonRePatternException("conditional backref with more than two branches", _position);
                }
            }

            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReConditionalNode(condition, yesBranch, noBranch);
        }

        if (TryConsume('-'))
        {
            var removedOnlyOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Removed);
            if (!IsAtEnd && char.IsAsciiLetter(Current) && Current != ':')
            {
                throw new PythonRePatternException("unknown flag", _position);
            }

            if (TryConsume(':'))
            {
                var inner = ParseAlternation(ApplyScopedFlags(options, PythonReCompileOptions.None, removedOnlyOptions));
                ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
                return new PythonReGroupNode(PythonReGroupKind.ScopedFlags, inner, null, PythonReCompileOptions.None, removedOnlyOptions);
            }

            throw new PythonRePatternException("missing :", _position);
        }

        if (IsAtEnd)
        {
            throw new PythonRePatternException("missing -, : or )", _position);
        }

        if (Current == 'P')
        {
            if (_position + 1 >= _pattern.Length)
            {
                throw new PythonRePatternException("unexpected end of pattern", _position + 1);
            }

            throw new PythonRePatternException(GetUnknownExtensionMessage(), _position - 1);
        }

        if (char.IsAsciiLetter(Current) && !IsInlineFlagChar(Current))
        {
            throw new PythonRePatternException("unknown extension ?" + Current, _position - 1);
        }

        if (Current == '<')
        {
            if (_position + 1 >= _pattern.Length)
            {
                throw new PythonRePatternException("unexpected end of pattern", _position + 1);
            }

            if (_pattern[_position + 1] == '>')
            {
                throw new PythonRePatternException("unknown extension ?<>", _position - 1);
            }
        }

        var addOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Added);
        if (!IsAtEnd && Current != ':' && Current != ')' && Current != '-' && !char.IsAsciiLetter(Current))
        {
            throw new PythonRePatternException("missing -, : or )", _position);
        }

        var removeOptions = PythonReCompileOptions.None;
        if (TryConsume('-'))
        {
            removeOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Removed);
            if (!IsAtEnd && char.IsAsciiLetter(Current) && Current != ':')
            {
                throw new PythonRePatternException("unknown flag", _position);
            }
        }

        if (TryConsume(':'))
        {
            var inner = ParseAlternation(ApplyScopedFlags(options, addOptions, removeOptions));
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            return new PythonReGroupNode(PythonReGroupKind.ScopedFlags, inner, null, addOptions, removeOptions);
        }

        if (TryConsume(')'))
        {
            throw new PythonRePatternException("global flags not at the start of the expression", groupStart);
        }

        if (IsAtEnd)
        {
            throw new PythonRePatternException("missing -, : or )", _position);
        }

        if (char.IsAsciiLetter(Current))
        {
            throw new PythonRePatternException("unknown flag", _position);
        }

        throw Error("unknown group extension");
    }

    private PythonReConditionalReference ParseConditionalReference()
    {
        if (TryPeekDigit())
        {
            var referenceStart = _position;
            var number = ParseNumber();
            if (number <= 0)
            {
                throw new PythonRePatternException("bad group number", referenceStart);
            }

            if (IsAtEnd)
            {
                throw new PythonRePatternException("missing ), unterminated name", referenceStart);
            }

            if (Current != ')')
            {
                while (!IsAtEnd && Current != ')')
                {
                    _position++;
                }

                var invalidName = _pattern[referenceStart.._position];
                throw new PythonRePatternException($"bad character in group name '{invalidName}'", referenceStart);
            }

            return PythonReConditionalReference.ForGroupNumber(number);
        }

        var start = _position;
        while (!IsAtEnd && Current != ')')
        {
            _position++;
        }

        if (_position == start)
        {
            throw Error("missing group name");
        }

        var name = _pattern[start.._position];
        if (!PythonReGroupNameValidator.IsValid(name))
        {
            throw new PythonRePatternException($"bad character in group name '{name}'", start);
        }

        return PythonReConditionalReference.ForGroupName(name);
    }

    private PythonReNode ParseConditionalBranch(PythonReCompileOptions options)
    {
        var elements = new List<PythonReNode>();
        while (true)
        {
            SkipIgnoredPatternTrivia(options);
            if (IsAtEnd || Current == ')' || Current == '|')
            {
                break;
            }

            elements.Add(ParseQuantifiedAtom(options));
        }

        return elements.Count switch
        {
            0 => new PythonReSequenceNode([]),
            1 => elements[0],
            _ => new PythonReSequenceNode(elements),
        };
    }

    private PythonReNode ParseCharacterClass(PythonReCompileOptions options)
    {
        var classStart = _position - 1;
        var isNegated = TryConsume('^');
        var items = new List<PythonReCharacterClassItem>();
        var allowLiteralClosingBracket = true;
        while (!IsAtEnd)
        {
            if (Current == ']' && !allowLiteralClosingBracket)
            {
                _position++;
                return new PythonReCharacterClassNode(isNegated, items);
            }

            var parsedItem = ParseCharacterClassItem(options, allowLiteralClosingBracket);
            var item = parsedItem.Item;
            allowLiteralClosingBracket = false;

            if (item is PythonReCharacterClassLiteralItem literal &&
                !IsAtEnd &&
                Current == '-' &&
                _position + 1 < _pattern.Length &&
                _pattern[_position + 1] != ']')
            {
                var rangePosition = _position;
                _position++;
                var endParsedItem = ParseCharacterClassItem(options, allowLiteralClosingBracket: false);
                var endItem = endParsedItem.Item;
                if (endItem is not PythonReCharacterClassLiteralItem endLiteral)
                {
                    throw new PythonRePatternException(
                        $"bad character range {DescribeCharacterClassItem(literal)}-{DescribeCharacterClassItem(endItem)}",
                        parsedItem.Position);
                }

                if (literal.Scalar > endLiteral.Scalar)
                {
                    throw new PythonRePatternException(
                        $"bad character range {DescribeCharacterClassItem(literal)}-{DescribeCharacterClassItem(endLiteral)}",
                        parsedItem.Position);
                }

                items.Add(new PythonReCharacterClassRangeItem(literal.Scalar, endLiteral.Scalar));
                continue;
            }

            if (item is not PythonReCharacterClassLiteralItem &&
                !IsAtEnd &&
                Current == '-' &&
                _position + 1 < _pattern.Length &&
                _pattern[_position + 1] != ']')
            {
                var rangePosition = _position;
                _position++;
                var endItem = ParseCharacterClassItem(options, allowLiteralClosingBracket: false).Item;
                throw new PythonRePatternException(
                    $"bad character range {DescribeCharacterClassItem(item)}-{DescribeCharacterClassItem(endItem)}",
                    parsedItem.Position);
            }

            items.Add(item);
        }

        throw new PythonRePatternException("unterminated character set", classStart);
    }

    private PythonReNode ParseEscape(int escapeStart, PythonReCompileOptions options)
    {
        if (IsAtEnd)
        {
            throw new PythonRePatternException("bad escape (end of pattern)", escapeStart);
        }

        var ch = Consume();
        return ch switch
        {
            'w' when IsAsciiMode(options) => new PythonReRawNode(AsciiWordClass, PythonReRawKind.Escape),
            'W' when IsAsciiMode(options) => new PythonReRawNode(AsciiNonWordClass, PythonReRawKind.Escape),
            'd' when IsAsciiMode(options) => new PythonReRawNode(AsciiDigitClass, PythonReRawKind.Escape),
            'D' when IsAsciiMode(options) => new PythonReRawNode(AsciiNonDigitClass, PythonReRawKind.Escape),
            's' when IsAsciiMode(options) => new PythonReRawNode(AsciiSpaceClass, PythonReRawKind.Escape),
            'S' when IsAsciiMode(options) => new PythonReRawNode(AsciiNonSpaceClass, PythonReRawKind.Escape),
            'b' when IsAsciiMode(options) => new PythonReRawNode(AsciiWordBoundary, PythonReRawKind.Escape),
            'B' when IsAsciiMode(options) => new PythonReRawNode(AsciiNonWordBoundary, PythonReRawKind.Escape),
            'A' or 'Z' or 'z' or 'b' or 'B' or 'd' or 'D' or 's' or 'S' or 'w' or 'W' or '\\' or '.' or '^' or '$' or '*' or '+' or '?' or '{' or '}' or '[' or ']' or '(' or ')' or '|' =>
                new PythonReRawNode("\\" + ch, PythonReRawKind.Escape),
            'x' => CreateEscapedLiteralNode(ParseHexEscapeValue(escapeStart, 2, 'x')),
            'u' => CreateEscapedLiteralNode(ParseHexEscapeValue(escapeStart, 4, 'u')),
            'U' => CreateEscapedLiteralNode(ParseUnicodeScalarEscapeValue(escapeStart)),
            'N' => throw new PythonRePatternException(@"\N{...} named Unicode escapes are intentionally unsupported in PythonRe to avoid a large embedded Unicode-name table.", escapeStart),
            '0' => CreateEscapedLiteralNode(ParseOctalEscapeValue(escapeStart, ch)),
            >= '1' and <= '9' => ParseNumericEscapeOrBackreference(ch, escapeStart),
            _ when PatternBadEscapeLetters.IndexOf(ch) >= 0 => throw new PythonRePatternException($@"bad escape \{ch}", escapeStart),
            _ => new PythonReRawNode("\\" + ch, PythonReRawKind.Escape),
        };
    }

    private static PythonReNode CreateEscapedLiteralNode(string value)
        => new PythonReRawNode(Regex.Escape(value), PythonReRawKind.Escape);

    private PythonReNode ParseNumericEscapeOrBackreference(char firstDigit, int escapeStart)
    {
        if (TryPeekDigit())
        {
            var secondDigit = Consume();
            if (IsOctalDigit(firstDigit) &&
                IsOctalDigit(secondDigit) &&
                !IsAtEnd &&
                IsOctalDigit(Current))
            {
                var thirdDigit = Consume();
                return CreateEscapedLiteralNode(ParseOctalEscapeValue(escapeStart, firstDigit, secondDigit, thirdDigit));
            }

            return ParseNumericBackreference(firstDigit, secondDigit, escapeStart);
        }

        return ParseNumericBackreference(firstDigit, null, escapeStart);
    }

    private PythonReNode ParseNumericBackreference(char firstDigit, char? secondDigit, int escapeStart)
    {
        var value = firstDigit - '0';
        if (secondDigit.HasValue)
        {
            value = checked(value * 10 + (secondDigit.Value - '0'));
        }

        if (_openGroupNumbers.Contains(value))
        {
            throw new PythonRePatternException("cannot refer to an open group", escapeStart);
        }

        if (value > _groupCount)
        {
            throw new PythonRePatternException($"invalid group reference {value}", escapeStart + 1);
        }

        return new PythonReNumericBackreferenceNode(value);
    }

    private PythonReParsedCharacterClassItem ParseCharacterClassItem(PythonReCompileOptions options, bool allowLiteralClosingBracket)
    {
        var escapeStart = _position;
        var ch = Consume();
        if (ch == '\\')
        {
            if (IsAtEnd)
            {
                throw new PythonRePatternException("bad escape (end of pattern)", escapeStart);
            }

            var escaped = Consume();
            if (CharacterClassBadEscapeLetters.IndexOf(escaped) >= 0)
            {
                throw new PythonRePatternException($@"bad escape \{escaped}", escapeStart);
            }

            if (escaped == 'N')
            {
                throw new PythonRePatternException(@"\N{...} named Unicode escapes are intentionally unsupported in PythonRe to avoid a large embedded Unicode-name table.", escapeStart);
            }

            var escapedLiteral = ParseEscapedCharacterClassLiteralScalar(escaped, escapeStart);
            if (escapedLiteral.HasValue)
            {
                return new PythonReParsedCharacterClassItem(new PythonReCharacterClassLiteralItem(escapedLiteral.Value), escapeStart);
            }

            if (TryGetAsciiCharacterClassEscapeFragment(escaped, options, out var asciiFragment))
            {
                return new PythonReParsedCharacterClassItem(new PythonReCharacterClassPredefinedItem(asciiFragment), escapeStart);
            }

            if (escaped is 'w' or 'W' or 'd' or 'D' or 's' or 'S')
            {
                return new PythonReParsedCharacterClassItem(new PythonReCharacterClassPredefinedItem(@"\" + escaped), escapeStart);
            }

            return new PythonReParsedCharacterClassItem(new PythonReCharacterClassLiteralItem(escaped), escapeStart);
        }

        if (ch == ']' && allowLiteralClosingBracket)
        {
            return new PythonReParsedCharacterClassItem(new PythonReCharacterClassLiteralItem(ch), escapeStart);
        }

        if (char.IsHighSurrogate(ch) &&
            !IsAtEnd &&
            char.IsLowSurrogate(Current))
        {
            var low = Consume();
            return new PythonReParsedCharacterClassItem(new PythonReCharacterClassLiteralItem(char.ConvertToUtf32(ch, low)), escapeStart);
        }

        return new PythonReParsedCharacterClassItem(new PythonReCharacterClassLiteralItem(ch), escapeStart);
    }

    private int? ParseEscapedCharacterClassLiteralScalar(char escaped, int escapeStart)
    {
        switch (escaped)
        {
            case 'x':
                return ParseHexEscapeScalarValue(escapeStart, 2, 'x');
            case 'u':
                return ParseHexEscapeScalarValue(escapeStart, 4, 'u');
            case 'U':
                return ParseUnicodeScalarEscapeIntValue(escapeStart);
            case >= '0' and <= '7':
                return ParseOctalEscapeIntValue(escapeStart, escaped);
            case '8' or '9':
                throw new PythonRePatternException($@"bad escape \{escaped}", escapeStart);
            default:
                return null;
        }
    }

    private string ParseHexEscapeValue(int escapeStart, int digits, char kind)
    {
        return char.ConvertFromUtf32(ParseHexEscapeScalarValue(escapeStart, digits, kind));
    }

    private int ParseHexEscapeScalarValue(int escapeStart, int digits, char kind)
    {
        var hex = ReadHexDigits(escapeStart, digits, kind);
        return Convert.ToInt32(hex, 16);
    }

    private string ParseUnicodeScalarEscapeValue(int escapeStart)
    {
        return char.ConvertFromUtf32(ParseUnicodeScalarEscapeIntValue(escapeStart));
    }

    private int ParseUnicodeScalarEscapeIntValue(int escapeStart)
    {
        var hex = ReadHexDigits(escapeStart, 8, 'U');
        var scalar = Convert.ToInt32(hex, 16);
        if (scalar > 0x10FFFF)
        {
            throw new PythonRePatternException($@"bad escape \U{hex}", escapeStart);
        }

        return scalar;
    }

    private string ReadHexDigits(int escapeStart, int digits, char kind)
    {
        var builder = new StringBuilder(digits);
        while (builder.Length < digits && !IsAtEnd && HexDigits.IndexOf(Current) >= 0)
        {
            builder.Append(Consume());
        }

        if (builder.Length != digits)
        {
            throw new PythonRePatternException($@"incomplete escape \{kind}{builder}", escapeStart);
        }

        return builder.ToString();
    }

    private string ParseOctalEscapeValue(int escapeStart, params char[] digits)
    {
        return char.ConvertFromUtf32(ParseOctalEscapeIntValue(escapeStart, digits));
    }

    private int ParseOctalEscapeIntValue(int escapeStart, params char[] digits)
    {
        var builder = new StringBuilder(3);
        foreach (var digit in digits)
        {
            builder.Append(digit);
        }

        while (builder.Length < 3 && !IsAtEnd && IsOctalDigit(Current))
        {
            builder.Append(Consume());
        }

        var value = Convert.ToInt32(builder.ToString(), 8);
        if (value > 0xFF)
        {
            throw new PythonRePatternException($@"octal escape value \{builder} outside of range 0-0o377", escapeStart);
        }

        return value;
    }

    private static bool IsOctalDigit(char ch) => OctalDigits.IndexOf(ch) >= 0;

    private string ParseGroupName(char terminator, string unterminatedMessage)
    {
        if (IsAtEnd)
        {
            throw Error("missing group name");
        }

        var start = _position;
        while (!IsAtEnd && Current != terminator)
        {
            if (terminator == '>' && Current == ')')
            {
                if (_position == start)
                {
                    throw new PythonRePatternException("missing group name", start);
                }

                throw new PythonRePatternException(unterminatedMessage, start);
            }

            _position++;
        }

        if (IsAtEnd)
        {
            if (_position == start)
            {
                throw Error("missing group name");
            }

            throw new PythonRePatternException(unterminatedMessage, start);
        }

        var name = _pattern[start.._position];
        _position++;
        if (name.Length == 0)
        {
            throw new PythonRePatternException("missing group name", start);
        }

        if (!PythonReGroupNameValidator.IsValid(name))
        {
            throw new PythonRePatternException($"bad character in group name '{name}'", start);
        }

        return name;
    }

    private PythonReCompileOptions ParseFlagSet(bool allowEmpty, FlagParseContext context)
    {
        var start = _position;
        var options = PythonReCompileOptions.None;
        while (!IsAtEnd)
        {
            PythonReCompileOptions next = Current switch
            {
                'i' => PythonReCompileOptions.IgnoreCase,
                'm' => PythonReCompileOptions.Multiline,
                's' => PythonReCompileOptions.DotAll,
                'x' => PythonReCompileOptions.Verbose,
                'a' => PythonReCompileOptions.Ascii,
                'L' => throw CreateInlineFlagException('L', context),
                'u' => PythonReCompileOptions.Unicode,
                _ => PythonReCompileOptions.None,
            };

            if (next == PythonReCompileOptions.None)
            {
                break;
            }

            if (context == FlagParseContext.Removed &&
                next is PythonReCompileOptions.Ascii or PythonReCompileOptions.Unicode)
            {
                throw CreateInlineFlagException(Current, context);
            }

            if (next is PythonReCompileOptions.Ascii or PythonReCompileOptions.Unicode)
            {
                var hasAsciiOrUnicode = (options & (PythonReCompileOptions.Ascii | PythonReCompileOptions.Unicode)) != 0;
                if (hasAsciiOrUnicode && (options & next) == 0)
                {
                    throw new PythonRePatternException("bad inline flags: flags 'a' and 'u' are incompatible", _position + 1);
                }
            }

            options |= next;
            _position++;
        }

        if (!allowEmpty && _position == start)
        {
            if (!IsAtEnd && char.IsAsciiLetter(Current))
            {
                throw new PythonRePatternException("unknown flag", _position);
            }

            throw Error("missing flag");
        }

        return options;
    }

    private PythonRePatternException CreateInlineFlagException(char flag, FlagParseContext context)
    {
        return context switch
        {
            FlagParseContext.Removed => new PythonRePatternException("bad inline flags: cannot turn off flags 'a', 'u' and 'L'", _position + 1),
            _ => new PythonRePatternException("bad inline flags: flags 'a', 'u' and 'L' are incompatible", _position + 1),
        };
    }

    private string GetUnknownExtensionMessage()
    {
        if (_position >= _pattern.Length)
        {
            return "unexpected end of pattern";
        }

        if (_position < _pattern.Length && _pattern[_position] == 'P')
        {
            var suffix = _position + 1 < _pattern.Length ? _pattern[_position + 1].ToString() : string.Empty;
            return "unknown extension ?P" + suffix;
        }

        return "unknown extension ?" + _pattern[_position];
    }

    private static bool IsInlineFlagChar(char ch) => ch is 'i' or 'm' or 's' or 'x' or 'a' or 'L' or 'u';

    private static bool IsAsciiMode(PythonReCompileOptions options)
        => (options & PythonReCompileOptions.Ascii) != 0;

    private void SkipIgnoredPatternTrivia(PythonReCompileOptions options)
    {
        if ((options & PythonReCompileOptions.Verbose) == 0)
        {
            return;
        }

        while (!IsAtEnd)
        {
            if (char.IsWhiteSpace(Current))
            {
                _position++;
                continue;
            }

            if (Current == '#')
            {
                _position++;
                while (!IsAtEnd && Current != '\n')
                {
                    _position++;
                }

                continue;
            }

            break;
        }
    }

    private static PythonReCompileOptions ApplyScopedFlags(
        PythonReCompileOptions options,
        PythonReCompileOptions addOptions,
        PythonReCompileOptions removeOptions)
    {
        if ((addOptions & PythonReCompileOptions.Ascii) != 0)
        {
            options &= ~PythonReCompileOptions.Unicode;
        }

        if ((addOptions & PythonReCompileOptions.Unicode) != 0)
        {
            options &= ~PythonReCompileOptions.Ascii;
        }

        return (options | addOptions) & ~removeOptions;
    }

    private static bool TryGetAsciiCharacterClassEscapeFragment(char escaped, PythonReCompileOptions options, out string fragment)
    {
        if (!IsAsciiMode(options))
        {
            fragment = string.Empty;
            return false;
        }

        switch (escaped)
        {
            case 'w':
                fragment = "A-Za-z0-9_";
                return true;
            case 'W':
                fragment = "^A-Za-z0-9_";
                return true;
            case 'd':
                fragment = "0-9";
                return true;
            case 'D':
                fragment = "^0-9";
                return true;
            case 's':
                fragment = @"\t\n\r\f\v ";
                return true;
            case 'S':
                fragment = @"^\t\n\r\f\v ";
                return true;
            default:
                fragment = string.Empty;
                return false;
        }
    }

    private int ParseNumber()
    {
        if (!TryPeekDigit())
        {
            throw Error("expected number");
        }

        var value = 0;
        while (TryPeekDigit())
        {
            value = checked(value * 10 + (Consume() - '0'));
        }

        return value;
    }

    private bool TryPeekDigit() => !IsAtEnd && char.IsAsciiDigit(Current);

    private bool IsAtEnd => _position >= _pattern.Length;

    private char Current => _pattern[_position];

    private char Consume() => _pattern[_position++];

    private void Expect(char ch)
    {
        if (!TryConsume(ch))
        {
            throw Error($"missing {ch}");
        }
    }

    private void ExpectGroupEnd(int groupStart, string message)
    {
        if (!TryConsume(')'))
        {
            throw new PythonRePatternException(message, groupStart);
        }
    }

    private bool TryConsume(char ch)
    {
        if (!IsAtEnd && Current == ch)
        {
            _position++;
            return true;
        }

        return false;
    }

    private static string EscapeCharacterClassLiteral(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        foreach (var ch in value)
        {
            if (ch is '\\' or '-' or ']' or '^')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string DescribeCharacterClassItem(PythonReCharacterClassItem item)
    {
        return item switch
        {
            PythonReCharacterClassLiteralItem literal => char.ConvertFromUtf32(literal.Scalar),
            PythonReCharacterClassRangeItem range => char.ConvertFromUtf32(range.StartScalar) + "-" + char.ConvertFromUtf32(range.EndScalar),
            PythonReCharacterClassPredefinedItem predefined => predefined.RegexClassFragment.StartsWith("^", StringComparison.Ordinal)
                ? @"\" + char.ToUpperInvariant(predefined.RegexClassFragment[1])
                : predefined.RegexClassFragment.StartsWith(@"\", StringComparison.Ordinal)
                    ? predefined.RegexClassFragment
                    : @"\" + InferAsciiEscapeName(predefined.RegexClassFragment),
            _ => "?",
        };
    }

    private static char InferAsciiEscapeName(string fragment)
    {
        return fragment switch
        {
            "A-Za-z0-9_" => 'w',
            "^A-Za-z0-9_" => 'W',
            "0-9" => 'd',
            "^0-9" => 'D',
            @"\t\n\r\f\v " => 's',
            @"^\t\n\r\f\v " => 'S',
            _ => '?',
        };
    }

    private bool TryConsumeExact(string text)
    {
        if (_pattern.AsSpan(_position).StartsWith(text, StringComparison.Ordinal))
        {
            _position += text.Length;
            return true;
        }

        return false;
    }

    private PythonRePatternException Error(string message) => new(message, _position);
}

internal readonly record struct PythonReParseResult(
    PythonReNode Root,
    PythonReCompileOptions Options,
    int CaptureGroupCount,
    IReadOnlyDictionary<string, int> NamedGroups);

internal enum FlagParseContext
{
    Added,
    Removed,
}

internal readonly record struct PythonReParsedCharacterClassItem(PythonReCharacterClassItem Item, int Position);
