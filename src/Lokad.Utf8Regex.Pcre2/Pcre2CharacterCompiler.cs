using System.Buffers;
using System.Globalization;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2CharacterSyntaxTree : IPcre2SyntaxTree
{
    internal Pcre2CharacterSyntaxTree(Pcre2SyntaxNode[] nodes)
    {
        Nodes = nodes;
    }

    public Pcre2SyntaxNodeKind RootKind => Pcre2SyntaxNodeKind.CharacterProgram;

    internal Pcre2SyntaxNode[] Nodes { get; }
}

internal sealed class Pcre2CharacterProgram
{
    internal Pcre2CharacterProgram(
        Pcre2CharacterToken[] tokens,
        Pcre2CompileRequest request,
        byte? leadingAsciiByte,
        bool leadingExtendedGraphemeCluster,
        bool usesCodeUnit)
    {
        Tokens = tokens;
        Request = request;
        LeadingAsciiByte = leadingAsciiByte;
        LeadingExtendedGraphemeCluster = leadingExtendedGraphemeCluster;
        UsesCodeUnit = usesCodeUnit;
    }

    internal Pcre2CharacterToken[] Tokens { get; }

    internal Pcre2CompileRequest Request { get; }

    internal byte? LeadingAsciiByte { get; }

    internal bool LeadingExtendedGraphemeCluster { get; }

    internal bool UsesCodeUnit { get; }

    internal bool CanCountSingleCharacterClassDirectly =>
        Tokens is [{ Kind: Pcre2CharacterTokenKind.CharacterClass }] &&
        (Request.Options &
            (Pcre2CompileOptions.Anchored |
             Pcre2CompileOptions.EndAnchored |
             Pcre2CompileOptions.FirstLine)) == 0;
}

internal interface IPcre2CharacterCompileOutcome
{
}

internal sealed class Pcre2NotCharacterOutcome : IPcre2CharacterCompileOutcome
{
    internal static Pcre2NotCharacterOutcome Instance { get; } = new();

    private Pcre2NotCharacterOutcome()
    {
    }
}

internal sealed class Pcre2CompiledCharacterOutcome : IPcre2CharacterCompileOutcome
{
    internal Pcre2CompiledCharacterOutcome(Pcre2CharacterSyntaxTree syntaxTree, Pcre2CharacterProgram program)
    {
        SyntaxTree = syntaxTree;
        Program = program;
    }

    internal Pcre2CharacterSyntaxTree SyntaxTree { get; }

    internal Pcre2CharacterProgram Program { get; }
}

[Flags]
internal enum Pcre2CharacterOptions : byte
{
    None = 0,
    Caseless = 1,
    Multiline = 2,
    DotAll = 4,
    Extended = 8,
    ExtendedMore = 16,
}

internal enum Pcre2CharacterTokenKind : byte
{
    Literal = 0,
    CharacterClass = 1,
    Any = 2,
    AnyNotNewline = 3,
    NewlineSequence = 4,
    BeginningOfLine = 5,
    EndOfLine = 6,
    BeginningOfSubject = 7,
    EndOfSubjectOrFinalNewline = 8,
    EndOfSubject = 9,
    FirstMatchingPosition = 10,
    WordBoundary = 11,
    NonWordBoundary = 12,
    ExtendedGraphemeCluster = 13,
    CodeUnit = 14,
}

internal readonly struct Pcre2CharacterToken
{
    internal Pcre2CharacterToken(
        Pcre2CharacterTokenKind kind,
        Rune literal,
        Pcre2CharacterClass characterClass,
        Pcre2CharacterOptions options,
        int patternOffset,
        int patternLength)
    {
        Kind = kind;
        Literal = literal;
        CharacterClass = characterClass;
        Options = options;
        PatternOffset = patternOffset;
        PatternLength = patternLength;
    }

    internal Pcre2CharacterTokenKind Kind { get; }

    internal Rune Literal { get; }

    internal Pcre2CharacterClass CharacterClass { get; }

    internal Pcre2CharacterOptions Options { get; }

    internal int PatternOffset { get; }

    internal int PatternLength { get; }

    internal static Pcre2CharacterToken Create(
        Pcre2CharacterTokenKind kind,
        Pcre2CharacterOptions options,
        int patternOffset,
        int patternLength) =>
        new(kind, default, Pcre2CharacterClass.Empty, options, patternOffset, patternLength);

    internal static Pcre2CharacterToken CreateLiteral(
        Rune literal,
        Pcre2CharacterOptions options,
        int patternOffset,
        int patternLength) =>
        new(Pcre2CharacterTokenKind.Literal, literal, Pcre2CharacterClass.Empty, options, patternOffset, patternLength);

    internal static Pcre2CharacterToken CreateClass(
        Pcre2CharacterClass characterClass,
        Pcre2CharacterOptions options,
        int patternOffset,
        int patternLength) =>
        new(Pcre2CharacterTokenKind.CharacterClass, default, characterClass, options, patternOffset, patternLength);
}

internal enum Pcre2CharacterClassTermKind : byte
{
    Range = 0,
    Digit = 1,
    Space = 2,
    Word = 3,
    HorizontalSpace = 4,
    VerticalSpace = 5,
    Property = 6,
    Posix = 7,
}

internal readonly record struct Pcre2ScalarRange(int Low, int High)
{
    internal bool Contains(int value) => value >= Low && value <= High;
}

internal readonly record struct Pcre2CharacterClassTerm(
    Pcre2CharacterClassTermKind Kind,
    Pcre2ScalarRange Range,
    string Name,
    bool Negated)
{
    internal static Pcre2CharacterClassTerm CreateRange(int low, int high) =>
        new(Pcre2CharacterClassTermKind.Range, new Pcre2ScalarRange(low, high), string.Empty, false);

    internal static Pcre2CharacterClassTerm CreatePredicate(Pcre2CharacterClassTermKind kind, bool negated) =>
        new(kind, default, string.Empty, negated);

    internal static Pcre2CharacterClassTerm CreateNamed(Pcre2CharacterClassTermKind kind, string name, bool negated) =>
        new(kind, default, name, negated);
}

internal sealed class Pcre2CharacterClass
{
    internal static Pcre2CharacterClass Empty { get; } = new([], false, AsciiCharClass.Empty);

    internal Pcre2CharacterClass(
        Pcre2CharacterClassTerm[] terms,
        bool negated,
        AsciiCharClass asciiSet)
    {
        Terms = terms;
        Negated = negated;
        AsciiSet = asciiSet;
    }

    internal Pcre2CharacterClassTerm[] Terms { get; }

    internal bool Negated { get; }

    internal AsciiCharClass AsciiSet { get; }

    internal bool Matches(Rune scalar, bool ucp, bool caseless)
    {
        if (scalar.IsAscii && !caseless)
        {
            return AsciiSet.Contains((byte)scalar.Value);
        }

        return MatchesSemantic(scalar, ucp, caseless);
    }

    internal bool MatchesSemantic(Rune scalar, bool ucp, bool caseless)
    {
        var matched = MatchesPositive(scalar, ucp, caseless);
        return Negated ? !matched : matched;
    }

    private bool MatchesPositive(Rune scalar, bool ucp, bool caseless)
    {
        foreach (var term in Terms)
        {
            var matched = term.Kind switch
            {
                Pcre2CharacterClassTermKind.Range => MatchesRange(term.Range, scalar, caseless),
                Pcre2CharacterClassTermKind.Digit => Pcre2CharacterSemantics.IsDigit(scalar, ucp),
                Pcre2CharacterClassTermKind.Space => Pcre2CharacterSemantics.IsSpace(scalar, ucp),
                Pcre2CharacterClassTermKind.Word => Pcre2CharacterSemantics.IsWord(scalar, ucp),
                Pcre2CharacterClassTermKind.HorizontalSpace => Pcre2CharacterSemantics.IsHorizontalSpace(scalar),
                Pcre2CharacterClassTermKind.VerticalSpace => Pcre2CharacterSemantics.IsVerticalSpace(scalar),
                Pcre2CharacterClassTermKind.Property => Pcre2CharacterSemantics.MatchesProperty(scalar, term.Name, caseless),
                Pcre2CharacterClassTermKind.Posix => Pcre2CharacterSemantics.MatchesPosix(scalar, term.Name, ucp, caseless),
                _ => false,
            };
            if (term.Negated ? !matched : matched)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesRange(Pcre2ScalarRange range, Rune scalar, bool caseless)
    {
        if (range.Contains(scalar.Value))
        {
            return true;
        }

        if (!caseless)
        {
            return false;
        }

        return range.Contains(Rune.ToLowerInvariant(scalar).Value) ||
            range.Contains(Rune.ToUpperInvariant(scalar).Value);
    }
}

internal static class Pcre2CharacterCompiler
{
    internal static IPcre2CharacterCompileOutcome Compile(Pcre2CompileRequest request)
    {
        var parser = new Pcre2CharacterParser(request);
        return parser.TryParse(out var tokens, out var nodes)
            ? new Pcre2CompiledCharacterOutcome(
                new Pcre2CharacterSyntaxTree(nodes),
                new Pcre2CharacterProgram(
                    tokens,
                    request,
                    GetLeadingAsciiByte(tokens),
                    tokens is [{ Kind: Pcre2CharacterTokenKind.ExtendedGraphemeCluster }, ..],
                    tokens.Any(static token => token.Kind == Pcre2CharacterTokenKind.CodeUnit)))
            : Pcre2NotCharacterOutcome.Instance;
    }

    internal static bool TryCompileSingleToken(
        Pcre2CompileRequest request,
        int patternOffset,
        Pcre2CharacterOptions options,
        out Pcre2CharacterToken token,
        out int patternLength)
    {
        var parser = new Pcre2CharacterParser(request, patternOffset, options);
        return parser.TryParseOne(out token, out patternLength);
    }

    private static byte? GetLeadingAsciiByte(Pcre2CharacterToken[] tokens)
    {
        if (tokens.Length == 0 ||
            tokens[0].Kind != Pcre2CharacterTokenKind.Literal ||
            !tokens[0].Literal.IsAscii ||
            (tokens[0].Options & Pcre2CharacterOptions.Caseless) != 0)
        {
            return null;
        }

        return (byte)tokens[0].Literal.Value;
    }
}

internal sealed class Pcre2CharacterParser
{
    private readonly Pcre2CompileRequest _request;
    private readonly string _pattern;
    private readonly List<Pcre2CharacterToken> _tokens = [];
    private readonly List<Pcre2SyntaxNode> _nodes = [];
    private int _offset;
    private Pcre2CharacterOptions _options;
    private bool _requiresCharacterProgram;

    internal Pcre2CharacterParser(Pcre2CompileRequest request)
        : this(request, 0, GetInitialOptions(request.Options))
    {
    }

    internal Pcre2CharacterParser(
        Pcre2CompileRequest request,
        int patternOffset,
        Pcre2CharacterOptions options)
    {
        _request = request;
        _pattern = request.Pattern;
        _offset = patternOffset;
        _options = options;
        _requiresCharacterProgram = (request.Options &
            (Pcre2CompileOptions.Caseless |
             Pcre2CompileOptions.Extended |
             Pcre2CompileOptions.ExtendedMore |
             Pcre2CompileOptions.FirstLine)) != 0;
    }

    internal bool TryParse(out Pcre2CharacterToken[] tokens, out Pcre2SyntaxNode[] nodes)
    {
        while (_offset < _pattern.Length)
        {
            if (TrySkipExtendedTrivia())
            {
                continue;
            }

            var start = _offset;
            var ch = _pattern[_offset];
            if (ch == '(')
            {
                if (!TryParseGlobalOptions(start))
                {
                    return Fail(out tokens, out nodes);
                }

                continue;
            }

            if (ch is ')' or '|' or '*' or '+' or '?' or '{')
            {
                return Fail(out tokens, out nodes);
            }

            if (ch == '[')
            {
                if (!TryParseClass(start, out var characterClass))
                {
                    return Fail(out tokens, out nodes);
                }

                Add(Pcre2CharacterToken.CreateClass(characterClass, _options, start, _offset - start));
                continue;
            }

            if (ch == '\\')
            {
                if (!TryParseEscape(start))
                {
                    return Fail(out tokens, out nodes);
                }

                continue;
            }

            _offset++;
            switch (ch)
            {
                case '.':
                    Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.Any, _options, start, 1));
                    break;
                case '^':
                    Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.BeginningOfLine, _options, start, 1));
                    break;
                case '$':
                    Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.EndOfLine, _options, start, 1));
                    break;
                default:
                    _offset = start;
                    if (!TryReadPatternRune(out var literal, out var length))
                    {
                        return Fail(out tokens, out nodes);
                    }

                    Add(Pcre2CharacterToken.CreateLiteral(literal, _options, start, length));
                    break;
            }
        }

        tokens = [.. _tokens];
        nodes = [.. _nodes];
        return _requiresCharacterProgram ||
            tokens.Any(static token => token.Kind != Pcre2CharacterTokenKind.Literal) ||
            tokens.Length == 0;
    }

    internal bool TryParseOne(out Pcre2CharacterToken token, out int patternLength)
    {
        var start = _offset;
        if (_offset >= _pattern.Length)
        {
            token = default;
            patternLength = 0;
            return false;
        }

        var ch = _pattern[_offset];
        if (ch == '[')
        {
            if (!TryParseClass(start, out var characterClass))
            {
                token = default;
                patternLength = 0;
                return false;
            }

            token = Pcre2CharacterToken.CreateClass(characterClass, _options, start, _offset - start);
        }
        else if (ch == '\\')
        {
            if (!TryParseEscape(start) || _tokens.Count != 1)
            {
                token = default;
                patternLength = 0;
                return false;
            }

            token = _tokens[0];
        }
        else
        {
            _offset++;
            token = ch switch
            {
                '.' => Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.Any, _options, start, 1),
                '^' => Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.BeginningOfLine, _options, start, 1),
                '$' => Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.EndOfLine, _options, start, 1),
                _ => CreateLiteralToken(start),
            };
        }

        patternLength = _offset - start;
        return true;

        Pcre2CharacterToken CreateLiteralToken(int literalStart)
        {
            _offset = literalStart;
            if (!TryReadPatternRune(out var literal, out var length))
            {
                return default;
            }

            return Pcre2CharacterToken.CreateLiteral(literal, _options, literalStart, length);
        }
    }

    private bool TryParseGlobalOptions(int start)
    {
        if (_offset + 2 >= _pattern.Length || _pattern[_offset + 1] != '?')
        {
            return false;
        }

        var cursor = _offset + 2;
        var resetAll = false;
        if (cursor < _pattern.Length && _pattern[cursor] == '^')
        {
            _options = Pcre2CharacterOptions.None;
            resetAll = true;
            cursor++;
        }

        var unset = false;
        var hyphenSeen = false;
        while (cursor < _pattern.Length && _pattern[cursor] != ')')
        {
            if (_pattern[cursor] == '-')
            {
                if (hyphenSeen || resetAll)
                {
                    return false;
                }

                unset = true;
                hyphenSeen = true;
                cursor++;
                continue;
            }

            var option = _pattern[cursor] switch
            {
                'i' => Pcre2CharacterOptions.Caseless,
                'm' => Pcre2CharacterOptions.Multiline,
                's' => Pcre2CharacterOptions.DotAll,
                'x' => Pcre2CharacterOptions.Extended,
                'n' or 'U' => Pcre2CharacterOptions.None,
                _ => Pcre2CharacterOptions.None,
            };
            if (option == Pcre2CharacterOptions.None && _pattern[cursor] is not ('n' or 'U'))
            {
                return false;
            }

            if (option == Pcre2CharacterOptions.Extended)
            {
                if (unset)
                {
                    _options &= ~(Pcre2CharacterOptions.Extended | Pcre2CharacterOptions.ExtendedMore);
                }
                else if ((_options & Pcre2CharacterOptions.Extended) != 0)
                {
                    _options |= Pcre2CharacterOptions.ExtendedMore;
                }
                else
                {
                    _options |= Pcre2CharacterOptions.Extended;
                }
            }
            else
            {
                _options = unset ? _options & ~option : _options | option;
            }

            cursor++;
        }

        if (cursor >= _pattern.Length || _pattern[cursor] != ')')
        {
            return false;
        }

        _offset = cursor + 1;
        _requiresCharacterProgram = true;
        _nodes.Add(new Pcre2SyntaxNode(Pcre2SyntaxNodeKind.CharacterProgram, start, _offset - start));
        return true;
    }

    private bool TryParseEscape(int start)
    {
        _requiresCharacterProgram = true;
        _offset++;
        if (_offset >= _pattern.Length)
        {
            return false;
        }

        var escape = _pattern[_offset++];
        switch (escape)
        {
            case 'A':
                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.BeginningOfSubject, _options, start, 2));
                return true;
            case 'Z':
                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline, _options, start, 2));
                return true;
            case 'z':
                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.EndOfSubject, _options, start, 2));
                return true;
            case 'G':
                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.FirstMatchingPosition, _options, start, 2));
                return true;
            case 'b':
                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.WordBoundary, _options, start, 2));
                return true;
            case 'B':
                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.NonWordBoundary, _options, start, 2));
                return true;
            case 'R':
                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.NewlineSequence, _options, start, 2));
                return true;
            case 'X':
                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.ExtendedGraphemeCluster, _options, start, 2));
                return true;
            case 'C':
                if (_request.Settings.BackslashC != Pcre2BackslashCPolicy.Allow)
                {
                    return false;
                }

                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.CodeUnit, _options, start, 2));
                return true;
            case 'N':
                if (_offset < _pattern.Length && _pattern[_offset] == '{')
                {
                    return TryParseNamedCodePoint(start);
                }

                Add(Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.AnyNotNewline, _options, start, 2));
                return true;
            case 'd':
            case 'D':
                AddClass(Pcre2CharacterClassTermKind.Digit, escape == 'D', start);
                return true;
            case 's':
            case 'S':
                AddClass(Pcre2CharacterClassTermKind.Space, escape == 'S', start);
                return true;
            case 'w':
            case 'W':
                AddClass(Pcre2CharacterClassTermKind.Word, escape == 'W', start);
                return true;
            case 'h':
            case 'H':
                AddClass(Pcre2CharacterClassTermKind.HorizontalSpace, escape == 'H', start);
                return true;
            case 'v':
            case 'V':
                AddClass(Pcre2CharacterClassTermKind.VerticalSpace, escape == 'V', start);
                return true;
            case 'p':
            case 'P':
                if (!TryReadPropertyName(out var property, out var propertyNegated))
                {
                    return false;
                }

                AddClass(Pcre2CharacterClassTerm.CreateNamed(
                    Pcre2CharacterClassTermKind.Property,
                    property,
                    (escape == 'P') != propertyNegated), start);
                return true;
            case 'Q':
                return TryParseQuotedLiterals(start);
            case 'a':
                AddLiteral(new Rune('\a'), start);
                return true;
            case 'e':
                AddLiteral(new Rune(0x1B), start);
                return true;
            case 'f':
                AddLiteral(new Rune('\f'), start);
                return true;
            case 'n':
                AddLiteral(new Rune('\n'), start);
                return true;
            case 'r':
                AddLiteral(new Rune('\r'), start);
                return true;
            case 't':
                AddLiteral(new Rune('\t'), start);
                return true;
            case 'c':
                if (_offset >= _pattern.Length)
                {
                    return false;
                }

                var control = _pattern[_offset++];
                if (control is < (char)32 or > (char)126)
                {
                    return false;
                }

                control = control is >= 'a' and <= 'z' ? (char)(control - 0x20) : control;
                AddLiteral(new Rune(control ^ 0x40), start);
                return true;
            case 'x':
                return TryParseHexEscape(start);
            case 'o':
                return TryParseBracedNumber(start, 8);
            case '0':
                return TryParseZeroOctal(start);
            case 'E':
                return true;
            case '\\':
            case '.':
            case '^':
            case '$':
            case '[':
            case ']':
            case '(':
            case ')':
            case '{':
            case '}':
            case '*':
            case '+':
            case '?':
            case '|':
            case '-':
            case '#':
                AddLiteral(new Rune(escape), start);
                return true;
            default:
                return false;
        }
    }

    private bool TryParseClass(int start, out Pcre2CharacterClass characterClass)
    {
        _offset++;
        var negated = _offset < _pattern.Length && _pattern[_offset] == '^';
        if (negated)
        {
            _offset++;
        }

        var terms = new List<Pcre2CharacterClassTerm>();
        var first = true;
        while (_offset < _pattern.Length)
        {
            if ((_options & Pcre2CharacterOptions.ExtendedMore) != 0 && _pattern[_offset] is ' ' or '\t')
            {
                _offset++;
                continue;
            }

            if (_pattern[_offset] == ']' && !first)
            {
                _offset++;
                characterClass = CreateClass(terms, negated);
                return true;
            }

            first = false;
            if (TryParsePosixTerm(out var posixTerm))
            {
                terms.Add(posixTerm);
                continue;
            }

            if (!TryParseClassTerm(out var term, out var scalar))
            {
                characterClass = Pcre2CharacterClass.Empty;
                return false;
            }

            if ((_options & Pcre2CharacterOptions.ExtendedMore) != 0)
            {
                while (_offset < _pattern.Length && _pattern[_offset] is ' ' or '\t')
                {
                    _offset++;
                }
            }

            var rangeEndOffset = _offset + 1;
            if ((_options & Pcre2CharacterOptions.ExtendedMore) != 0)
            {
                while (rangeEndOffset < _pattern.Length && _pattern[rangeEndOffset] is ' ' or '\t')
                {
                    rangeEndOffset++;
                }
            }

            if (scalar is { } low &&
                _offset < _pattern.Length - 1 &&
                _pattern[_offset] == '-' &&
                rangeEndOffset < _pattern.Length &&
                _pattern[rangeEndOffset] != ']')
            {
                _offset = rangeEndOffset;
                if (!TryParseClassTerm(out _, out var high) || high is null || high.Value.Value < low.Value)
                {
                    characterClass = Pcre2CharacterClass.Empty;
                    return false;
                }

                terms.Add(Pcre2CharacterClassTerm.CreateRange(low.Value, high.Value.Value));
            }
            else
            {
                terms.Add(term);
            }
        }

        characterClass = Pcre2CharacterClass.Empty;
        return false;
    }

    private bool TryParseClassTerm(out Pcre2CharacterClassTerm term, out Rune? scalar)
    {
        scalar = null;
        if (_pattern[_offset] != '\\')
        {
            if (!TryReadPatternRune(out var literal, out _))
            {
                term = default;
                return false;
            }

            scalar = literal;
            term = Pcre2CharacterClassTerm.CreateRange(literal.Value, literal.Value);
            return true;
        }

        _offset++;
        if (_offset >= _pattern.Length)
        {
            term = default;
            return false;
        }

        var escape = _pattern[_offset++];
        var kind = escape switch
        {
            'd' or 'D' => Pcre2CharacterClassTermKind.Digit,
            's' or 'S' => Pcre2CharacterClassTermKind.Space,
            'w' or 'W' => Pcre2CharacterClassTermKind.Word,
            'h' or 'H' => Pcre2CharacterClassTermKind.HorizontalSpace,
            'v' or 'V' => Pcre2CharacterClassTermKind.VerticalSpace,
            _ => Pcre2CharacterClassTermKind.Range,
        };
        if (kind != Pcre2CharacterClassTermKind.Range)
        {
            term = Pcre2CharacterClassTerm.CreatePredicate(kind, char.IsUpper(escape));
            return true;
        }

        if (escape is 'p' or 'P')
        {
            if (!TryReadPropertyName(out var property, out var propertyNegated))
            {
                term = default;
                return false;
            }

            term = Pcre2CharacterClassTerm.CreateNamed(
                Pcre2CharacterClassTermKind.Property,
                property,
                (escape == 'P') != propertyNegated);
            return true;
        }

        if (escape == 'x')
        {
            if (!TryReadHexScalar(out var hexadecimal))
            {
                term = default;
                return false;
            }

            scalar = hexadecimal;
            term = Pcre2CharacterClassTerm.CreateRange(hexadecimal.Value, hexadecimal.Value);
            return true;
        }

        if (escape == 'o')
        {
            if (!TryReadBracedScalar(8, out var octal))
            {
                term = default;
                return false;
            }

            scalar = octal;
            term = Pcre2CharacterClassTerm.CreateRange(octal.Value, octal.Value);
            return true;
        }

        if (escape == '0')
        {
            var begin = _offset - 1;
            var end = begin;
            while (end < _pattern.Length && end - begin < 3 && _pattern[end] is >= '0' and <= '7')
            {
                end++;
            }

            if (!TryParseScalarNumber(_pattern.AsSpan(begin, end - begin), 8, out var zeroOctal))
            {
                term = default;
                return false;
            }

            _offset = end;
            scalar = zeroOctal;
            term = Pcre2CharacterClassTerm.CreateRange(zeroOctal.Value, zeroOctal.Value);
            return true;
        }

        if (escape == 'c')
        {
            if (_offset >= _pattern.Length || _pattern[_offset] is < (char)32 or > (char)126)
            {
                term = default;
                return false;
            }

            var control = _pattern[_offset++];
            control = control is >= 'a' and <= 'z' ? (char)(control - 0x20) : control;
            var controlScalar = new Rune(control ^ 0x40);
            scalar = controlScalar;
            term = Pcre2CharacterClassTerm.CreateRange(controlScalar.Value, controlScalar.Value);
            return true;
        }

        if (escape == 'N' && _offset < _pattern.Length && _pattern[_offset] == '{')
        {
            if (!TryReadNamedCodePoint(out var namedScalar))
            {
                term = default;
                return false;
            }

            scalar = namedScalar;
            term = Pcre2CharacterClassTerm.CreateRange(namedScalar.Value, namedScalar.Value);
            return true;
        }

        var simpleValue = escape switch
        {
            'b' => new Rune('\b'),
            'n' => new Rune('\n'),
            'r' => new Rune('\r'),
            't' => new Rune('\t'),
            'f' => new Rune('\f'),
            'a' => new Rune('\a'),
            'e' => new Rune(0x1B),
            _ => (Rune?)null,
        };
        if (simpleValue is { } simpleScalar)
        {
            scalar = simpleScalar;
            term = Pcre2CharacterClassTerm.CreateRange(simpleScalar.Value, simpleScalar.Value);
            return true;
        }

        if (escape is 'B' or 'R' or 'X' or 'N' or 'Q' or 'E' || char.IsAsciiLetterOrDigit(escape))
        {
            term = default;
            return false;
        }

        var value = new Rune(escape);
        scalar = value;
        term = Pcre2CharacterClassTerm.CreateRange(value.Value, value.Value);
        return true;
    }

    private bool TryParsePosixTerm(out Pcre2CharacterClassTerm term)
    {
        term = default;
        if (_offset + 3 >= _pattern.Length || _pattern[_offset] != '[' || _pattern[_offset + 1] != ':')
        {
            return false;
        }

        var end = _pattern.IndexOf(":]", _offset + 2, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        var nameStart = _offset + 2;
        var negated = nameStart < end && _pattern[nameStart] == '^';
        if (negated)
        {
            nameStart++;
        }

        var name = Pcre2CharacterSemantics.NormalizePropertyName(_pattern[nameStart..end]);
        if (!Pcre2CharacterSemantics.IsKnownPosixName(name))
        {
            return false;
        }

        _offset = end + 2;
        term = Pcre2CharacterClassTerm.CreateNamed(Pcre2CharacterClassTermKind.Posix, name, negated);
        return true;
    }

    private Pcre2CharacterClass CreateClass(List<Pcre2CharacterClassTerm> terms, bool negated)
    {
        var positiveTerms = terms.ToArray();
        var ucp = (_request.Options & Pcre2CompileOptions.Ucp) != 0;
        var semanticClass = new Pcre2CharacterClass(positiveTerms, negated, AsciiCharClass.Empty);
        var ascii = Pcre2AsciiByteSetFactory.CreatePredicate(
            value => semanticClass.MatchesSemantic(new Rune(value), ucp, caseless: false),
            negated: false);
        return new Pcre2CharacterClass(positiveTerms, negated, ascii);
    }

    private void AddClass(Pcre2CharacterClassTermKind kind, bool negated, int start) =>
        AddClass(Pcre2CharacterClassTerm.CreatePredicate(kind, negated), start);

    private void AddClass(Pcre2CharacterClassTerm term, int start)
    {
        var characterClass = CreateClass([term], negated: false);
        Add(Pcre2CharacterToken.CreateClass(characterClass, _options, start, _offset - start));
    }

    private bool TryReadPropertyName(out string property, out bool negated)
    {
        property = string.Empty;
        negated = false;
        if (_offset < _pattern.Length && _pattern[_offset] != '{')
        {
            var shorthand = _pattern[_offset];
            var normalizedShorthand = Pcre2CharacterSemantics.NormalizePropertyName(shorthand.ToString());
            if (!char.IsAsciiLetter(shorthand) || !Pcre2CharacterSemantics.IsKnownProperty(normalizedShorthand))
            {
                return false;
            }

            property = normalizedShorthand;
            _offset++;
            return true;
        }

        if (_offset >= _pattern.Length)
        {
            return false;
        }

        var end = _pattern.IndexOf('}', _offset + 1);
        if (end < 0)
        {
            return false;
        }

        var propertyStart = _offset + 1;
        if (propertyStart < end && _pattern[propertyStart] == '^')
        {
            negated = true;
            propertyStart++;
        }

        property = Pcre2CharacterSemantics.NormalizePropertyName(_pattern[propertyStart..end]);
        if (!Pcre2CharacterSemantics.IsKnownProperty(property))
        {
            return false;
        }

        _offset = end + 1;
        return true;
    }

    private bool TryParseQuotedLiterals(int start)
    {
        while (_offset < _pattern.Length)
        {
            if (_offset + 1 < _pattern.Length && _pattern[_offset] == '\\' && _pattern[_offset + 1] == 'E')
            {
                _offset += 2;
                return true;
            }

            var literalStart = _offset;
            if (!TryReadPatternRune(out var literal, out var length))
            {
                return false;
            }

            Add(Pcre2CharacterToken.CreateLiteral(literal, _options, literalStart, length));
        }

        return _offset > start + 2;
    }

    private bool TryParseHexEscape(int start)
    {
        if (!TryReadHexScalar(out var scalar))
        {
            return false;
        }

        AddLiteral(scalar, start);
        return true;
    }

    private bool TryParseNamedCodePoint(int start)
    {
        if (!TryReadNamedCodePoint(out var scalar))
        {
            return false;
        }

        AddLiteral(scalar, start);
        return true;
    }

    private bool TryReadNamedCodePoint(out Rune scalar)
    {
        var end = _pattern.IndexOf('}', _offset + 1);
        const string Prefix = "U+";
        if (end < 0 ||
            !_pattern.AsSpan(_offset + 1, end - _offset - 1).StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !TryParseScalarNumber(_pattern.AsSpan(_offset + 3, end - _offset - 3), 16, out scalar))
        {
            scalar = default;
            return false;
        }

        _offset = end + 1;
        return true;
    }

    private bool TryReadHexScalar(out Rune scalar)
    {
        if (_offset < _pattern.Length && _pattern[_offset] == '{')
        {
            return TryReadBracedScalar(16, out scalar);
        }

        var begin = _offset;
        var end = begin;
        while (end < _pattern.Length && end - begin < 2 && IsDigitForRadix(_pattern[end], 16))
        {
            end++;
        }

        if (end == begin || !TryParseScalarNumber(_pattern.AsSpan(begin, end - begin), 16, out scalar))
        {
            scalar = default;
            return false;
        }

        _offset = end;
        return true;
    }

    private bool TryReadBracedScalar(int radix, out Rune scalar)
    {
        if (_offset >= _pattern.Length || _pattern[_offset] != '{')
        {
            scalar = default;
            return false;
        }

        var end = _pattern.IndexOf('}', _offset + 1);
        if (end < 0 || !TryParseScalarNumber(_pattern.AsSpan(_offset + 1, end - _offset - 1), radix, out scalar))
        {
            scalar = default;
            return false;
        }

        _offset = end + 1;
        return true;
    }

    private bool TryParseBracedNumber(int start, int radix)
    {
        if (!TryReadBracedScalar(radix, out var scalar))
        {
            return false;
        }

        AddLiteral(scalar, start);
        return true;
    }

    private bool TryParseZeroOctal(int start)
    {
        var begin = _offset - 1;
        var end = begin;
        while (end < _pattern.Length && end - begin < 3 && _pattern[end] is >= '0' and <= '7')
        {
            end++;
        }

        if (!TryParseScalarNumber(_pattern.AsSpan(begin, end - begin), 8, out var scalar))
        {
            return false;
        }

        _offset = end;
        AddLiteral(scalar, start);
        return true;
    }

    private static bool TryParseScalarNumber(ReadOnlySpan<char> text, int radix, out Rune scalar)
    {
        scalar = default;
        if (text.IsEmpty)
        {
            return false;
        }

        var value = 0;
        foreach (var ch in text)
        {
            var digit = ch switch
            {
                >= '0' and <= '9' => ch - '0',
                >= 'a' and <= 'f' => ch - 'a' + 10,
                >= 'A' and <= 'F' => ch - 'A' + 10,
                _ => -1,
            };
            if (digit < 0 || digit >= radix)
            {
                return false;
            }

            value = checked(value * radix + digit);
        }

        if (!Rune.IsValid(value))
        {
            return false;
        }

        scalar = new Rune(value);
        return true;
    }

    private static bool IsDigitForRadix(char value, int radix)
    {
        var digit = value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => -1,
        };
        return digit >= 0 && digit < radix;
    }

    private bool TrySkipExtendedTrivia()
    {
        if ((_options & (Pcre2CharacterOptions.Extended | Pcre2CharacterOptions.ExtendedMore)) == 0)
        {
            return false;
        }

        var moved = false;
        while (_offset < _pattern.Length)
        {
            if (IsExtendedPatternWhitespace(_pattern[_offset]))
            {
                _offset++;
                moved = true;
                continue;
            }

            if (_pattern[_offset] != '#')
            {
                break;
            }

            moved = true;
            while (_offset < _pattern.Length && _pattern[_offset] is not ('\r' or '\n'))
            {
                _offset++;
            }
        }

        return moved;
    }

    private static bool IsExtendedPatternWhitespace(char value) => value is
        ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

    private bool TryReadPatternRune(out Rune rune, out int length)
    {
        var status = Rune.DecodeFromUtf16(_pattern.AsSpan(_offset), out rune, out length);
        if (status != OperationStatus.Done)
        {
            return false;
        }

        _offset += length;
        return true;
    }

    private void AddLiteral(Rune literal, int start) =>
        Add(Pcre2CharacterToken.CreateLiteral(literal, _options, start, _offset - start));

    private void Add(Pcre2CharacterToken token)
    {
        _tokens.Add(token);
        _nodes.Add(new Pcre2SyntaxNode(Pcre2SyntaxNodeKind.CharacterProgram, token.PatternOffset, token.PatternLength));
    }

    private static bool Fail(out Pcre2CharacterToken[] tokens, out Pcre2SyntaxNode[] nodes)
    {
        tokens = [];
        nodes = [];
        return false;
    }

    private static Pcre2CharacterOptions GetInitialOptions(Pcre2CompileOptions options)
    {
        var result = Pcre2CharacterOptions.None;
        result |= (options & Pcre2CompileOptions.Caseless) != 0 ? Pcre2CharacterOptions.Caseless : 0;
        result |= (options & Pcre2CompileOptions.Multiline) != 0 ? Pcre2CharacterOptions.Multiline : 0;
        result |= (options & Pcre2CompileOptions.DotAll) != 0 ? Pcre2CharacterOptions.DotAll : 0;
        result |= (options & Pcre2CompileOptions.Extended) != 0 ? Pcre2CharacterOptions.Extended : 0;
        result |= (options & Pcre2CompileOptions.ExtendedMore) != 0 ? Pcre2CharacterOptions.ExtendedMore : 0;
        return result;
    }
}

internal readonly record struct Pcre2CharacterMatch(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int ConsumedStartOffsetInBytes,
    int ConsumedEndOffsetInBytes,
    bool MatchBoundaryWasReset)
{
    internal static Pcre2CharacterMatch NoMatch => default;

    internal static Pcre2CharacterMatch Create(int start, int end) => new(true, start, end, start, end, false);
}

internal static class Pcre2CharacterRunner
{
    internal static int CountSingleCharacterClass(
        Pcre2CharacterProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start)
    {
        var token = program.Tokens[0];
        var ucp = (program.Request.Options & Pcre2CompileOptions.Ucp) != 0;
        var caseless = (token.Options & Pcre2CharacterOptions.Caseless) != 0;
        var count = 0;
        var index = start.Value;
        while (TryDecode(input, index, out var scalar, out var width))
        {
            if (token.CharacterClass.Matches(scalar, ucp, caseless))
            {
                count++;
            }

            index += width;
        }

        return count;
    }

    internal static Pcre2CharacterMatch Match(
        Pcre2CharacterProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
    {
        var bytes = input.Bytes;
        var anchored = (program.Request.Options & Pcre2CompileOptions.Anchored) != 0 ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0;
        var candidate = start.Value;
        while (candidate <= bytes.Length)
        {
            budget.ChargeCandidate();
            if (TryMatchAt(program, bytes, candidate, start.Value, matchOptions, out var end) &&
                ((program.Request.Options & Pcre2CompileOptions.EndAnchored) == 0 || end == bytes.Length) &&
                ((matchOptions & Pcre2MatchOptions.EndAnchored) == 0 || end == bytes.Length) &&
                ((matchOptions & Pcre2MatchOptions.NotEmpty) == 0 || end != candidate) &&
                ((matchOptions & Pcre2MatchOptions.NotEmptyAtStart) == 0 || candidate != start.Value || end != candidate))
            {
                return Pcre2CharacterMatch.Create(candidate, end);
            }

            if (anchored || !TryAdvanceCandidate(
                    program,
                    ref input,
                    candidate,
                    !budget.RequiresCandidateMetering,
                    out candidate))
            {
                break;
            }
        }

        return Pcre2CharacterMatch.NoMatch;
    }

    private static bool TryMatchAt(
        Pcre2CharacterProgram program,
        ReadOnlySpan<byte> input,
        int candidate,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        out int end)
    {
        var index = candidate;
        foreach (var token in program.Tokens)
        {
            if (!TryMatchToken(
                    token,
                    program.Request,
                    input,
                    index,
                    firstMatchingPosition,
                    matchOptions,
                    out index))
            {
                end = 0;
                return false;
            }
        }

        end = index;
        return true;
    }

    internal static bool TryMatchToken(
        Pcre2CharacterToken token,
        Pcre2CompileRequest request,
        ReadOnlySpan<byte> input,
        int index,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        out int nextIndex)
    {
        nextIndex = index;
        switch (token.Kind)
        {
            case Pcre2CharacterTokenKind.Literal:
                if (!TryDecode(input, index, out var literalSubject, out var literalWidth) ||
                    !Pcre2CharacterSemantics.Equals(token.Literal, literalSubject, (token.Options & Pcre2CharacterOptions.Caseless) != 0))
                {
                    return false;
                }

                nextIndex += literalWidth;
                return true;

            case Pcre2CharacterTokenKind.CharacterClass:
                if (!TryDecode(input, index, out var classSubject, out var classWidth) ||
                    !token.CharacterClass.Matches(
                        classSubject,
                        (request.Options & Pcre2CompileOptions.Ucp) != 0,
                        (token.Options & Pcre2CharacterOptions.Caseless) != 0))
                {
                    return false;
                }

                nextIndex += classWidth;
                return true;

            case Pcre2CharacterTokenKind.Any:
                if (!TryDecode(input, index, out _, out var anyWidth) ||
                    (token.Options & Pcre2CharacterOptions.DotAll) == 0 &&
                    Pcre2CharacterSemantics.TryGetNewlineWidth(input, index, request.Settings.Newline, out _))
                {
                    return false;
                }

                nextIndex += anyWidth;
                return true;

            case Pcre2CharacterTokenKind.AnyNotNewline:
                if (!TryDecode(input, index, out _, out var notNewlineWidth) ||
                    Pcre2CharacterSemantics.TryGetNewlineWidth(input, index, request.Settings.Newline, out _))
                {
                    return false;
                }

                nextIndex += notNewlineWidth;
                return true;

            case Pcre2CharacterTokenKind.NewlineSequence:
                if (!Pcre2CharacterSemantics.TryGetBsrWidth(input, index, request.Settings.Bsr, out var newlineWidth))
                {
                    return false;
                }

                nextIndex += newlineWidth;
                return true;

            case Pcre2CharacterTokenKind.ExtendedGraphemeCluster:
                if (!Pcre2GraphemeClusterSemantics.TryGetWidth(input, index, out var graphemeWidth))
                {
                    return false;
                }

                nextIndex += graphemeWidth;
                return true;

            case Pcre2CharacterTokenKind.CodeUnit:
                if (index >= input.Length)
                {
                    return false;
                }

                nextIndex++;
                return true;

            case Pcre2CharacterTokenKind.BeginningOfLine:
                return Pcre2CharacterSemantics.IsBeginningOfLine(input, index, token.Options, matchOptions, request.Settings.Newline);

            case Pcre2CharacterTokenKind.EndOfLine:
                return Pcre2CharacterSemantics.IsEndOfLine(input, index, token.Options, matchOptions, request.Options, request.Settings.Newline);

            case Pcre2CharacterTokenKind.BeginningOfSubject:
                return index == 0;

            case Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline:
                return Pcre2CharacterSemantics.IsEndOrBeforeFinalNewline(input, index, request.Settings.Newline);

            case Pcre2CharacterTokenKind.EndOfSubject:
                return index == input.Length;

            case Pcre2CharacterTokenKind.FirstMatchingPosition:
                return index == firstMatchingPosition;

            case Pcre2CharacterTokenKind.WordBoundary:
            case Pcre2CharacterTokenKind.NonWordBoundary:
                var boundary = Pcre2CharacterSemantics.IsWordBoundary(
                    input,
                    index,
                    (request.Options & Pcre2CompileOptions.Ucp) != 0);
                return boundary == (token.Kind == Pcre2CharacterTokenKind.WordBoundary);

            default:
                return false;
        }
    }

    internal static bool TryAdvanceCandidate(
        Pcre2CharacterProgram program,
        ref Utf8ValidatedInput input,
        int candidate,
        bool allowAcceleration,
        out int next)
    {
        if (program.LeadingExtendedGraphemeCluster &&
            (program.Request.Options & Pcre2CompileOptions.FirstLine) == 0 &&
            Pcre2GraphemeClusterSemantics.TryGetWidth(input.Bytes, candidate, out var width))
        {
            next = candidate + width;
            return true;
        }

        return TryAdvanceCandidate(
            program.Request,
            program.LeadingAsciiByte,
            ref input,
            candidate,
            allowAcceleration,
            out next);
    }

    internal static bool TryAdvanceCandidate(
        Pcre2CompileRequest request,
        byte? leadingAsciiByte,
        ref Utf8ValidatedInput input,
        int candidate,
        bool allowAcceleration,
        out int next)
    {
        if ((request.Options & Pcre2CompileOptions.FirstLine) != 0 &&
            Pcre2CharacterSemantics.TryGetNewlineWidth(input.Bytes, candidate, request.Settings.Newline, out _))
        {
            next = 0;
            return false;
        }

        if (allowAcceleration &&
            (request.Options & Pcre2CompileOptions.FirstLine) == 0 &&
            leadingAsciiByte is { } leading &&
            candidate < input.Bytes.Length)
        {
            var relative = input.Bytes[(candidate + 1)..].IndexOf(leading);
            if (relative < 0)
            {
                next = 0;
                return false;
            }

            next = candidate + relative + 1;
            return true;
        }

        var candidatePosition = new Utf8BytePosition(candidate);
        if (!input.IsScalarBoundary(candidatePosition))
        {
            next = candidate + 1;
            return next <= input.Bytes.Length;
        }

        if (!input.TryAdvanceScalar(candidatePosition, out var nextPosition))
        {
            next = 0;
            return false;
        }

        next = nextPosition.Value;
        return true;
    }

    private static bool TryDecode(ReadOnlySpan<byte> input, int index, out Rune rune, out int width)
    {
        if ((uint)index >= (uint)input.Length)
        {
            rune = default;
            width = 0;
            return false;
        }

        return Rune.DecodeFromUtf8(input[index..], out rune, out width) == OperationStatus.Done;
    }
}

internal static class Pcre2GraphemeClusterSemantics
{
    internal static bool TryGetWidth(ReadOnlySpan<byte> input, int offset, out int width)
    {
        if (!TryDecode(input, offset, out var previous, out var previousWidth))
        {
            width = 0;
            return false;
        }

        var end = offset + previousWidth;
        var regionalIndicatorCount = IsRegionalIndicator(previous) ? 1 : 0;
        var lastNonExtendWasExtendedPictographic = IsExtendedPictographic(previous);
        var zwjFollowsExtendedPictographic = false;
        var indicConsonantSeen = IsIndicConsonant(previous);
        var indicLinkerSeen = false;

        while (TryDecode(input, end, out var next, out var nextWidth))
        {
            var join = previous.Value == '\r' && next.Value == '\n' ||
                !IsControl(previous) && !IsControl(next) &&
                (IsHangulContinuation(previous, next) ||
                 IsExtend(next) ||
                 IsSpacingMark(next) ||
                 IsPrepend(previous) ||
                 previous.Value == 0x200D && zwjFollowsExtendedPictographic && IsExtendedPictographic(next) ||
                 IsRegionalIndicator(previous) && IsRegionalIndicator(next) && regionalIndicatorCount % 2 != 0 ||
                 indicConsonantSeen && indicLinkerSeen && IsIndicConsonant(next));
            if (!join)
            {
                break;
            }

            end += nextWidth;
            if (IsRegionalIndicator(next))
            {
                regionalIndicatorCount++;
            }
            else if (!IsExtend(next))
            {
                regionalIndicatorCount = 0;
            }

            if (next.Value == 0x200D)
            {
                zwjFollowsExtendedPictographic = lastNonExtendWasExtendedPictographic;
            }
            else if (!IsExtend(next))
            {
                lastNonExtendWasExtendedPictographic = IsExtendedPictographic(next);
                zwjFollowsExtendedPictographic = false;
            }

            if (IsIndicLinker(next) && indicConsonantSeen)
            {
                indicLinkerSeen = true;
            }
            else if (IsIndicConsonant(next))
            {
                indicConsonantSeen = true;
                indicLinkerSeen = false;
            }
            else if (!IsExtend(next) && next.Value != 0x200D)
            {
                indicConsonantSeen = false;
                indicLinkerSeen = false;
            }

            previous = next;
        }

        width = end - offset;
        return true;
    }

    private static bool IsControl(Rune rune)
    {
        if (rune.Value is 0x200C or 0x200D || IsPrepend(rune) || IsTagCharacter(rune))
        {
            return false;
        }

        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.Control or
            UnicodeCategory.Format or
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator or
            UnicodeCategory.Surrogate;
    }

    private static bool IsExtend(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark ||
        rune.Value is 0x200C or 0x200D or (>= 0x1F3FB and <= 0x1F3FF) ||
        IsTagCharacter(rune);

    private static bool IsSpacingMark(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == UnicodeCategory.SpacingCombiningMark;

    private static bool IsPrepend(Rune rune) => rune.Value is
        (>= 0x0600 and <= 0x0605) or 0x06DD or 0x070F or 0x0890 or 0x0891 or
        0x08E2 or 0x0D4E or 0x110BD or 0x110CD or
        (>= 0x111C2 and <= 0x111C3) or 0x113D1 or 0x1193F or 0x11941 or
        0x11A3A or (>= 0x11A84 and <= 0x11A89) or 0x11D46;

    private static bool IsHangulContinuation(Rune previous, Rune next) =>
        IsHangulL(previous) && (IsHangulL(next) || IsHangulV(next) || IsHangulLv(next) || IsHangulLvt(next)) ||
        (IsHangulLv(previous) || IsHangulV(previous)) && (IsHangulV(next) || IsHangulT(next)) ||
        (IsHangulLvt(previous) || IsHangulT(previous)) && IsHangulT(next);

    private static bool IsHangulL(Rune rune) => rune.Value is
        (>= 0x1100 and <= 0x115F) or (>= 0xA960 and <= 0xA97C);

    private static bool IsHangulV(Rune rune) => rune.Value is
        (>= 0x1160 and <= 0x11A7) or (>= 0xD7B0 and <= 0xD7C6);

    private static bool IsHangulT(Rune rune) => rune.Value is
        (>= 0x11A8 and <= 0x11FF) or (>= 0xD7CB and <= 0xD7FB);

    private static bool IsHangulLv(Rune rune) =>
        rune.Value is >= 0xAC00 and <= 0xD7A3 && (rune.Value - 0xAC00) % 28 == 0;

    private static bool IsHangulLvt(Rune rune) =>
        rune.Value is >= 0xAC00 and <= 0xD7A3 && (rune.Value - 0xAC00) % 28 != 0;

    private static bool IsRegionalIndicator(Rune rune) =>
        rune.Value is >= 0x1F1E6 and <= 0x1F1FF;

    private static bool IsExtendedPictographic(Rune rune) => rune.Value is
        0x00A9 or 0x00AE or 0x203C or 0x2049 or 0x2122 or 0x2139 or
        (>= 0x2194 and <= 0x2199) or (>= 0x21A9 and <= 0x21AA) or
        (>= 0x231A and <= 0x231B) or 0x2328 or 0x2388 or 0x23CF or
        (>= 0x23E9 and <= 0x23F3) or (>= 0x23F8 and <= 0x23FA) or
        (>= 0x24C2 and <= 0x24C2) or (>= 0x25AA and <= 0x25AB) or
        (>= 0x25B6 and <= 0x25B6) or (>= 0x25C0 and <= 0x25C0) or
        (>= 0x25FB and <= 0x25FE) or (>= 0x2600 and <= 0x27BF) or
        (>= 0x2934 and <= 0x2935) or (>= 0x2B05 and <= 0x2B07) or
        (>= 0x2B1B and <= 0x2B1C) or 0x2B50 or 0x2B55 or
        (>= 0x3030 and <= 0x3030) or 0x303D or 0x3297 or 0x3299 or
        (>= 0x1F000 and <= 0x1FAFF);

    private static bool IsIndicConsonant(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter;

    private static bool IsIndicLinker(Rune rune) => rune.Value is
        0x094D or 0x09CD or 0x0A4D or 0x0ACD or 0x0B4D or 0x0BCD or 0x0C4D or
        0x0CCD or 0x0D3B or 0x0D3C or 0x0D4D or 0x0DCA or 0x0E3A or 0x0F84 or
        0x1039 or 0x103A or 0x1714 or 0x1734 or 0x17D2 or 0x1A60 or 0x1B44 or
        0x1BAA or 0x1BAB or 0xA806 or 0xA82C or 0xA8C4 or 0xA953 or 0xA9C0 or
        0xAAF6 or 0xABED or 0x10A3F or 0x11046 or 0x11070 or 0x11133 or
        0x111C0 or 0x11235 or 0x112EA or 0x1134D or 0x11442 or 0x114C2 or
        0x115BF or 0x1163F or 0x116B6 or 0x1172B or 0x11839 or 0x1193D or
        0x119E0 or 0x11A34 or 0x11A47 or 0x11A99 or 0x11C3F or 0x11D44 or
        0x11D45 or 0x11D97 or 0x16AF0 or 0x16B44;

    private static bool IsTagCharacter(Rune rune) =>
        rune.Value is >= 0xE0020 and <= 0xE007F;

    private static bool TryDecode(ReadOnlySpan<byte> input, int offset, out Rune rune, out int width)
    {
        if ((uint)offset >= (uint)input.Length)
        {
            rune = default;
            width = 0;
            return false;
        }

        return Rune.DecodeFromUtf8(input[offset..], out rune, out width) == OperationStatus.Done;
    }
}

internal static class Pcre2CharacterSemantics
{
    internal static bool Equals(Rune left, Rune right, bool caseless)
    {
        if (left == right)
        {
            return true;
        }

        return caseless &&
            (Rune.ToLowerInvariant(left) == Rune.ToLowerInvariant(right) ||
             Rune.ToUpperInvariant(left) == Rune.ToUpperInvariant(right));
    }

    internal static bool IsDigit(Rune scalar, bool ucp) =>
        scalar.IsAscii
            ? Utf8AsciiBytePredicates.IsDigit((byte)scalar.Value)
            : ucp && Rune.GetUnicodeCategory(scalar) == UnicodeCategory.DecimalDigitNumber;

    internal static bool IsSpace(Rune scalar, bool ucp)
    {
        if (scalar.IsAscii)
        {
            return Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)scalar.Value);
        }

        return ucp && (IsSeparator(Rune.GetUnicodeCategory(scalar)) || IsHorizontalSpace(scalar) || IsVerticalSpace(scalar));
    }

    internal static bool IsWord(Rune scalar, bool ucp)
    {
        if (scalar.IsAscii)
        {
            return Utf8AsciiBytePredicates.IsWord((byte)scalar.Value);
        }

        if (!ucp)
        {
            return false;
        }

        return Rune.GetUnicodeCategory(scalar) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber or
            UnicodeCategory.ConnectorPunctuation;
    }

    internal static bool IsHorizontalSpace(Rune scalar) => scalar.Value is
        0x0009 or 0x0020 or 0x00A0 or 0x1680 or 0x180E or
        (>= 0x2000 and <= 0x200A) or 0x202F or 0x205F or 0x3000;

    internal static bool IsVerticalSpace(Rune scalar) => scalar.Value is
        0x000A or 0x000B or 0x000C or 0x000D or 0x0085 or 0x2028 or 0x2029;

    internal static bool IsKnownProperty(string normalizedName) => normalizedName is
        "ANY" or
        "C" or "CC" or "CF" or "CN" or "CO" or "CS" or
        "L" or "LC" or "L&" or "LU" or "LL" or "LT" or "LM" or "LO" or
        "M" or "MC" or "ME" or "MN" or
        "N" or "ND" or "NL" or "NO" or
        "P" or "PC" or "PD" or "PE" or "PF" or "PI" or "PO" or "PS" or
        "S" or "SC" or "SK" or "SM" or "SO" or
        "Z" or "ZS" or "ZL" or "ZP" or
        "XAN" or "XPS" or "XSP" or "XWD";

    internal static bool MatchesProperty(Rune scalar, string normalizedName, bool caseless)
    {
        var category = Rune.GetUnicodeCategory(scalar);
        return normalizedName switch
        {
            "ANY" => true,
            "C" => IsOther(category),
            "CC" => category == UnicodeCategory.Control,
            "CF" => category == UnicodeCategory.Format,
            "CN" => category == UnicodeCategory.OtherNotAssigned,
            "CO" => category == UnicodeCategory.PrivateUse,
            "CS" => category == UnicodeCategory.Surrogate,
            "L" => IsLetterCategory(category),
            "LC" or "L&" => IsCasedLetter(category),
            "LU" => caseless ? IsCasedLetter(category) : category == UnicodeCategory.UppercaseLetter,
            "LL" => caseless ? IsCasedLetter(category) : category == UnicodeCategory.LowercaseLetter,
            "LT" => caseless ? IsCasedLetter(category) : category == UnicodeCategory.TitlecaseLetter,
            "LM" => category == UnicodeCategory.ModifierLetter,
            "LO" => category == UnicodeCategory.OtherLetter,
            "M" => IsMark(category),
            "MC" => category == UnicodeCategory.SpacingCombiningMark,
            "ME" => category == UnicodeCategory.EnclosingMark,
            "MN" => category == UnicodeCategory.NonSpacingMark,
            "N" => IsNumber(category),
            "ND" => category == UnicodeCategory.DecimalDigitNumber,
            "NL" => category == UnicodeCategory.LetterNumber,
            "NO" => category == UnicodeCategory.OtherNumber,
            "P" => IsPunctuation(category),
            "PC" => category == UnicodeCategory.ConnectorPunctuation,
            "PD" => category == UnicodeCategory.DashPunctuation,
            "PE" => category == UnicodeCategory.ClosePunctuation,
            "PF" => category == UnicodeCategory.FinalQuotePunctuation,
            "PI" => category == UnicodeCategory.InitialQuotePunctuation,
            "PO" => category == UnicodeCategory.OtherPunctuation,
            "PS" => category == UnicodeCategory.OpenPunctuation,
            "S" => IsSymbol(category),
            "SC" => category == UnicodeCategory.CurrencySymbol,
            "SK" => category == UnicodeCategory.ModifierSymbol,
            "SM" => category == UnicodeCategory.MathSymbol,
            "SO" => category == UnicodeCategory.OtherSymbol,
            "Z" => IsSeparator(category),
            "ZS" => category == UnicodeCategory.SpaceSeparator,
            "ZL" => category == UnicodeCategory.LineSeparator,
            "ZP" => category == UnicodeCategory.ParagraphSeparator,
            "XAN" => IsLetterCategory(category) || IsNumber(category),
            "XPS" or "XSP" => scalar.Value is >= 0x09 and <= 0x0D || IsSeparator(category),
            "XWD" => IsWord(scalar, ucp: true),
            _ => false,
        };
    }

    internal static bool IsKnownPosixName(string normalizedName) => normalizedName is
        "ALNUM" or "ALPHA" or "ASCII" or "BLANK" or "CNTRL" or "DIGIT" or
        "GRAPH" or "LOWER" or "PRINT" or "PUNCT" or "SPACE" or "UPPER" or
        "WORD" or "XDIGIT";

    internal static bool MatchesPosix(Rune scalar, string normalizedName, bool ucp, bool caseless)
    {
        if (!ucp && !scalar.IsAscii)
        {
            return false;
        }

        var value = scalar.Value;
        var category = Rune.GetUnicodeCategory(scalar);
        return normalizedName switch
        {
            "ALNUM" => ucp ? IsLetterCategory(category) || IsNumber(category) : Utf8AsciiBytePredicates.IsLetterOrDigit((byte)value),
            "ALPHA" => ucp ? IsLetterCategory(category) : Utf8AsciiBytePredicates.IsLetter((byte)value),
            "ASCII" => scalar.IsAscii,
            "BLANK" => ucp ? IsHorizontalSpace(scalar) : value is 0x09 or 0x20,
            "CNTRL" => category == UnicodeCategory.Control,
            "DIGIT" => IsDigit(scalar, ucp),
            "GRAPH" => ucp ? IsPosixGraph(scalar, category) : value is >= 0x21 and <= 0x7E,
            "LOWER" => ucp
                ? MatchesProperty(scalar, "LL", caseless)
                : Utf8AsciiBytePredicates.IsLetter((byte)value) && (caseless || value is >= 'a' and <= 'z'),
            "PRINT" => ucp ? IsPosixGraph(scalar, category) || category == UnicodeCategory.SpaceSeparator : value is >= 0x20 and <= 0x7E,
            "PUNCT" => ucp ? IsPunctuation(category) || value < 256 && IsSymbol(category) : value is >= 0x21 and <= 0x7E && !Utf8AsciiBytePredicates.IsLetterOrDigit((byte)value),
            "SPACE" => ucp ? MatchesProperty(scalar, "XPS", caseless: false) : IsSpace(scalar, ucp: false),
            "UPPER" => ucp
                ? MatchesProperty(scalar, "LU", caseless)
                : Utf8AsciiBytePredicates.IsLetter((byte)value) && (caseless || value is >= 'A' and <= 'Z'),
            "WORD" => IsWord(scalar, ucp),
            "XDIGIT" => scalar.IsAscii && Utf8AsciiBytePredicates.IsHexDigit((byte)value) ||
                ucp && value is (>= 0xFF10 and <= 0xFF19) or (>= 0xFF21 and <= 0xFF26) or (>= 0xFF41 and <= 0xFF46),
            _ => false,
        };
    }

    internal static string NormalizePropertyName(string value)
    {
        Span<char> normalized = value.Length <= 128 ? stackalloc char[value.Length] : new char[value.Length];
        var length = 0;
        foreach (var character in value)
        {
            if (character is '_' or '-' or ' ' or '\t' or '\n' or '\v' or '\f' or '\r')
            {
                continue;
            }

            normalized[length++] = char.ToUpperInvariant(character);
        }

        return new string(normalized[..length]);
    }

    internal static bool IsWordBoundary(ReadOnlySpan<byte> input, int offset, bool ucp)
    {
        var previousIsWord = Utf8ScalarNeighbors.TryGetPrevious(input, offset, out var previous) && IsWord(previous, ucp);
        var nextIsWord = Utf8ScalarNeighbors.TryGetNext(input, offset, out var next) && IsWord(next, ucp);
        return previousIsWord != nextIsWord;
    }

    internal static bool IsBeginningOfLine(
        ReadOnlySpan<byte> input,
        int offset,
        Pcre2CharacterOptions options,
        Pcre2MatchOptions matchOptions,
        Pcre2NewlineConvention newline)
    {
        if (offset == 0)
        {
            return (matchOptions & Pcre2MatchOptions.NotBol) == 0;
        }

        if (offset == input.Length)
        {
            return false;
        }

        return (options & Pcre2CharacterOptions.Multiline) != 0 && IsImmediatelyAfterNewline(input, offset, newline);
    }

    internal static bool IsEndOfLine(
        ReadOnlySpan<byte> input,
        int offset,
        Pcre2CharacterOptions options,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileOptions compileOptions,
        Pcre2NewlineConvention newline)
    {
        if (offset == input.Length)
        {
            return (matchOptions & Pcre2MatchOptions.NotEol) == 0;
        }

        if ((options & Pcre2CharacterOptions.Multiline) != 0 && TryGetNewlineWidth(input, offset, newline, out _))
        {
            return true;
        }

        return (compileOptions & Pcre2CompileOptions.DollarEndOnly) == 0 &&
            IsEndOrBeforeFinalNewline(input, offset, newline);
    }

    internal static bool IsEndOrBeforeFinalNewline(ReadOnlySpan<byte> input, int offset, Pcre2NewlineConvention newline)
    {
        if (offset == input.Length)
        {
            return true;
        }

        return TryGetNewlineWidth(input, offset, newline, out var width) && offset + width == input.Length;
    }

    internal static bool TryGetNewlineWidth(
        ReadOnlySpan<byte> input,
        int offset,
        Pcre2NewlineConvention convention,
        out int width)
    {
        convention = convention == Pcre2NewlineConvention.Default ? Pcre2NewlineConvention.Lf : convention;
        width = 0;
        if ((uint)offset >= (uint)input.Length)
        {
            return false;
        }

        if (convention is Pcre2NewlineConvention.Crlf or Pcre2NewlineConvention.Any or Pcre2NewlineConvention.AnyCrlf &&
            input[offset] == (byte)'\r' && offset + 1 < input.Length && input[offset + 1] == (byte)'\n')
        {
            width = 2;
            return true;
        }

        if (!TryDecode(input, offset, out var scalar, out var scalarWidth))
        {
            return false;
        }

        var matches = convention switch
        {
            Pcre2NewlineConvention.Cr => scalar.Value == '\r',
            Pcre2NewlineConvention.Lf => scalar.Value == '\n',
            Pcre2NewlineConvention.Crlf => false,
            Pcre2NewlineConvention.AnyCrlf => scalar.Value is '\r' or '\n',
            Pcre2NewlineConvention.Any => IsVerticalSpace(scalar),
            Pcre2NewlineConvention.Nul => scalar.Value == 0,
            _ => false,
        };
        width = matches ? scalarWidth : 0;
        return matches;
    }

    internal static bool TryGetBsrWidth(ReadOnlySpan<byte> input, int offset, Pcre2BsrConvention convention, out int width)
    {
        convention = convention == Pcre2BsrConvention.Default ? Pcre2BsrConvention.Unicode : convention;
        if (offset < input.Length - 1 && input[offset] == (byte)'\r' && input[offset + 1] == (byte)'\n')
        {
            width = 2;
            return true;
        }

        if (!TryDecode(input, offset, out var scalar, out width))
        {
            width = 0;
            return false;
        }

        var matched = convention == Pcre2BsrConvention.AnyCrlf
            ? scalar.Value is '\r' or '\n'
            : IsVerticalSpace(scalar);
        if (!matched)
        {
            width = 0;
        }

        return matched;
    }

    private static bool IsImmediatelyAfterNewline(ReadOnlySpan<byte> input, int offset, Pcre2NewlineConvention newline)
    {
        var maxWidth = Math.Min(4, offset);
        for (var width = 1; width <= maxWidth; width++)
        {
            var start = offset - width;
            if (TryGetNewlineWidth(input, start, newline, out var actualWidth) && actualWidth == width)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryDecode(ReadOnlySpan<byte> input, int offset, out Rune scalar, out int width)
    {
        if ((uint)offset < (uint)input.Length)
        {
            return Rune.DecodeFromUtf8(input[offset..], out scalar, out width) == OperationStatus.Done;
        }

        scalar = default;
        width = 0;
        return false;
    }

    private static bool IsLetterCategory(UnicodeCategory category) => category is
        UnicodeCategory.UppercaseLetter or
        UnicodeCategory.LowercaseLetter or
        UnicodeCategory.TitlecaseLetter or
        UnicodeCategory.ModifierLetter or
        UnicodeCategory.OtherLetter;

    private static bool IsCasedLetter(UnicodeCategory category) => category is
        UnicodeCategory.UppercaseLetter or
        UnicodeCategory.LowercaseLetter or
        UnicodeCategory.TitlecaseLetter;

    private static bool IsMark(UnicodeCategory category) => category is
        UnicodeCategory.NonSpacingMark or
        UnicodeCategory.SpacingCombiningMark or
        UnicodeCategory.EnclosingMark;

    private static bool IsNumber(UnicodeCategory category) => category is
        UnicodeCategory.DecimalDigitNumber or
        UnicodeCategory.LetterNumber or
        UnicodeCategory.OtherNumber;

    private static bool IsPunctuation(UnicodeCategory category) => category is
        UnicodeCategory.ConnectorPunctuation or
        UnicodeCategory.DashPunctuation or
        UnicodeCategory.OpenPunctuation or
        UnicodeCategory.ClosePunctuation or
        UnicodeCategory.InitialQuotePunctuation or
        UnicodeCategory.FinalQuotePunctuation or
        UnicodeCategory.OtherPunctuation;

    private static bool IsSymbol(UnicodeCategory category) => category is
        UnicodeCategory.MathSymbol or
        UnicodeCategory.CurrencySymbol or
        UnicodeCategory.ModifierSymbol or
        UnicodeCategory.OtherSymbol;

    private static bool IsSeparator(UnicodeCategory category) => category is
        UnicodeCategory.SpaceSeparator or
        UnicodeCategory.LineSeparator or
        UnicodeCategory.ParagraphSeparator;

    private static bool IsOther(UnicodeCategory category) => category is
        UnicodeCategory.Control or
        UnicodeCategory.Format or
        UnicodeCategory.Surrogate or
        UnicodeCategory.PrivateUse or
        UnicodeCategory.OtherNotAssigned;

    private static bool IsPosixGraph(Rune scalar, UnicodeCategory category) =>
        (IsLetterCategory(category) || IsMark(category) || IsNumber(category) ||
         IsPunctuation(category) || IsSymbol(category) || category == UnicodeCategory.Format) &&
        scalar.Value is not (0x061C or 0x180E or >= 0x2066 and <= 0x2069);
}
