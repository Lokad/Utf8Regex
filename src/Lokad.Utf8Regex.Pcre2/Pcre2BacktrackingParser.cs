using System.Buffers;
using Lokad.Utf8Regex.Internal;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

// PLAN.md D09: backtracking parser, split from the compiler collection along the parse ownership boundary.
internal sealed class Pcre2BacktrackingParser
{
    private readonly Pcre2CompileRequest _request;
    private readonly string _pattern;
    private int _offset;
    private Pcre2CharacterOptions _options;
    private bool _ungreedy;
    private bool _noAutoCapture;
    private bool _allowDuplicateNames;
    private bool _sawControlFlow;
    private int _captureCount;
    private readonly List<Pcre2NameEntry> _nameEntries = [];
    private readonly Dictionary<int, string> _captureSlotNames = [];
    private readonly Dictionary<int, Pcre2CaptureBacktrackingNode> _captureDefinitions = [];

    internal Pcre2BacktrackingParser(Pcre2CompileRequest request)
    {
        _request = request;
        _pattern = request.Pattern;
        _options = Pcre2CharacterOptionsFactory.FromCompileOptions(request.Options);
        _ungreedy = (request.Options & Pcre2CompileOptions.Ungreedy) != 0;
        _noAutoCapture = (request.Options & Pcre2CompileOptions.NoAutoCapture) != 0;
        _allowDuplicateNames = request.Settings.AllowDuplicateNames;
    }

    internal int CaptureCount => _captureCount;

    internal bool RequiresProfileOptions { get; private set; }

    internal bool UsesBranchReset { get; private set; }
    internal bool HasExplicitCrOrLf { get; private set; }

    internal Pcre2NameEntry[] NameEntries => [.. _nameEntries];

    internal IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> CaptureDefinitions => _captureDefinitions;

    private Pcre2TokenBacktrackingNode CreateTokenNode(Pcre2CharacterToken token)
    {
        if (!HasExplicitCrOrLf && _request.Settings.Newline is
            Pcre2NewlineConvention.Crlf or Pcre2NewlineConvention.Any or Pcre2NewlineConvention.AnyCrlf)
            HasExplicitCrOrLf = Pcre2CharacterCompiler.HasExplicitCrOrLf(token);
        return new Pcre2TokenBacktrackingNode(token);
    }

    internal bool TryParse(out IPcre2BacktrackingNode root)
    {
        if (!TryParseAlternation(false, out root) || _offset != _pattern.Length ||
            (!_sawControlFlow && !RequiresProfileOptions))
        {
            root = Pcre2EmptyBacktrackingNode.Instance;
            return false;
        }

        return true;
    }

    private bool TryParseAlternation(bool expectClosingParenthesis, out IPcre2BacktrackingNode node)
    {
        var alternatives = new List<IPcre2BacktrackingNode>();
        while (true)
        {
            if (!TryParseSequence(expectClosingParenthesis, out var sequence))
            {
                node = Pcre2EmptyBacktrackingNode.Instance;
                return false;
            }

            alternatives.Add(sequence);
            Utf8ConstructionScope.ChargeWork(1);
            SkipExtendedTrivia();
            if (_offset >= _pattern.Length || _pattern[_offset] != '|')
            {
                break;
            }

            _sawControlFlow = true;
            _offset++;
        }

        node = alternatives.Count == 1
            ? alternatives[0]
            : new Pcre2AlternationBacktrackingNode([.. alternatives]);
        return true;
    }

    private bool TryParseSequence(bool expectClosingParenthesis, out IPcre2BacktrackingNode node)
    {
        var children = new List<IPcre2BacktrackingNode>();
        while (true)
        {
            Utf8ConstructionScope.ChargeWork(1);
            SkipExtendedTrivia();
            if (_offset >= _pattern.Length || _pattern[_offset] == '|' ||
                expectClosingParenthesis && _pattern[_offset] == ')')
            {
                break;
            }

            if (_pattern[_offset] == ')' || IsQuantifierStart(_pattern[_offset]))
            {
                node = Pcre2EmptyBacktrackingNode.Instance;
                return false;
            }

            if (TryParseGlobalOptions())
            {
                continue;
            }

            if (_pattern.AsSpan(_offset).StartsWith("\\Q", StringComparison.Ordinal))
            {
                if (!TryParseQuotedSequence(out var quotedChildren))
                {
                    node = Pcre2EmptyBacktrackingNode.Instance;
                    return false;
                }

                if (quotedChildren.Length == 0)
                {
                    continue;
                }

                for (var quotedIndex = 0; quotedIndex < quotedChildren.Length - 1; quotedIndex++)
                {
                    children.Add(quotedChildren[quotedIndex]);
                }
                var quotedTail = quotedChildren[^1];
                if (!TryParseQuantifier(quotedTail, out quotedTail))
                {
                    node = Pcre2EmptyBacktrackingNode.Instance;
                    return false;
                }

                children.Add(quotedTail);
                continue;
            }

            if (!TryParseAtom(out var child) || !TryParseQuantifier(child, out child))
            {
                node = Pcre2EmptyBacktrackingNode.Instance;
                return false;
            }

            children.Add(child);
        }

        node = children.Count switch
        {
            0 => Pcre2EmptyBacktrackingNode.Instance,
            1 => children[0],
            _ => new Pcre2SequenceBacktrackingNode([.. children]),
        };
        return true;
    }

    private bool TryParseAtom(out IPcre2BacktrackingNode node)
    {
        var budget = Pcre2BacktrackingCompiler.EnterDepth();
        try
        {
            if (_pattern.AsSpan(_offset).StartsWith("\\K", StringComparison.Ordinal))
            {
                _sawControlFlow = true;
                _offset += 2;
                node = Pcre2MatchBoundaryResetBacktrackingNode.Instance;
                return true;
            }

            if (TryParseBackreference(out node))
            {
                return true;
            }

            if (_pattern[_offset] == '(')
            {
                if (_pattern.AsSpan(_offset).StartsWith("(?>", StringComparison.Ordinal) ||
                    _pattern.AsSpan(_offset).StartsWith("(*atomic:", StringComparison.OrdinalIgnoreCase))
                {
                    return TryParseAtomicGroup(out node);
                }

                if (TryParseControlVerb(out node))
                {
                    return true;
                }

                if (TryParseConditional(out node))
                {
                    return true;
                }

                if (TryParseSubroutineCall(out node))
                {
                    return true;
                }

                if (_pattern.AsSpan(_offset).StartsWith("(?|", StringComparison.Ordinal))
                {
                    return TryParseBranchReset(out node);
                }

                if (TryParseAssertionOpening(out var assertionKind, out var assertionOpeningLength))
                {
                    _sawControlFlow = true;
                    _offset += assertionOpeningLength;
                    var outerOptions = _options;
                    var outerUngreedy = _ungreedy;
                    var outerNoAutoCapture = _noAutoCapture;
                    var outerAllowDuplicateNames = _allowDuplicateNames;
                    if (!TryParseAlternation(true, out var assertionBody) ||
                        _offset >= _pattern.Length ||
                        _pattern[_offset] != ')')
                    {
                        _options = outerOptions;
                        _ungreedy = outerUngreedy;
                        _noAutoCapture = outerNoAutoCapture;
                        _allowDuplicateNames = outerAllowDuplicateNames;
                        node = Pcre2EmptyBacktrackingNode.Instance;
                        return false;
                    }

                    _offset++;
                    _options = outerOptions;
                    _ungreedy = outerUngreedy;
                    _noAutoCapture = outerNoAutoCapture;
                    _allowDuplicateNames = outerAllowDuplicateNames;
                    node = new Pcre2AssertionBacktrackingNode(assertionKind, assertionBody);
                    return true;
                }

                if (TryParseScopedOptionOpening(
                        out var scopedOptions,
                        out var scopedUngreedy,
                        out var scopedNoAutoCapture,
                        out var scopedAllowDuplicateNames,
                        out var scopedOpeningLength))
                {
                    _sawControlFlow = true;
                    var outerOptions = _options;
                    var outerUngreedy = _ungreedy;
                    var outerNoAutoCapture = _noAutoCapture;
                    var outerAllowDuplicateNames = _allowDuplicateNames;
                    _options = scopedOptions;
                    _ungreedy = scopedUngreedy;
                    _noAutoCapture = scopedNoAutoCapture;
                    _allowDuplicateNames = scopedAllowDuplicateNames;
                    _offset += scopedOpeningLength;
                    if (!TryParseAlternation(true, out node) ||
                        _offset >= _pattern.Length ||
                        _pattern[_offset] != ')')
                    {
                        _options = outerOptions;
                        _ungreedy = outerUngreedy;
                        _noAutoCapture = outerNoAutoCapture;
                        _allowDuplicateNames = outerAllowDuplicateNames;
                        return false;
                    }

                    _offset++;
                    _options = outerOptions;
                    _ungreedy = outerUngreedy;
                    _noAutoCapture = outerNoAutoCapture;
                    _allowDuplicateNames = outerAllowDuplicateNames;
                    return true;
                }

                var explicitNoncapturing = _pattern.AsSpan(_offset).StartsWith("(?:", StringComparison.Ordinal);
                if (explicitNoncapturing || _noAutoCapture && !_pattern.AsSpan(_offset).StartsWith("(?", StringComparison.Ordinal))
                {
                    _sawControlFlow = true;
                    _offset += explicitNoncapturing ? 3 : 1;
                    var outerOptions = _options;
                    var outerUngreedy = _ungreedy;
                    var outerNoAutoCapture = _noAutoCapture;
                    var outerAllowDuplicateNames = _allowDuplicateNames;
                    if (!TryParseAlternation(true, out node) || _offset >= _pattern.Length || _pattern[_offset] != ')')
                    {
                        _options = outerOptions;
                        _ungreedy = outerUngreedy;
                        _noAutoCapture = outerNoAutoCapture;
                        _allowDuplicateNames = outerAllowDuplicateNames;
                        return false;
                    }

                    _offset++;
                    _options = outerOptions;
                    _ungreedy = outerUngreedy;
                    _noAutoCapture = outerNoAutoCapture;
                    _allowDuplicateNames = outerAllowDuplicateNames;
                    return true;
                }

                if (TryParseCaptureOpening(out var captureName, out var openingLength))
                {
                    _sawControlFlow = true;
                    var slot = ++_captureCount;
                    if (captureName.Length != 0)
                    {
                        if (!TryRegisterCaptureName(slot, captureName))
                        {
                            node = Pcre2EmptyBacktrackingNode.Instance;
                            return false;
                        }
                    }

                    _offset += openingLength;
                    var outerOptions = _options;
                    var outerUngreedy = _ungreedy;
                    var outerNoAutoCapture = _noAutoCapture;
                    var outerAllowDuplicateNames = _allowDuplicateNames;
                    if (!TryParseAlternation(true, out var body) || _offset >= _pattern.Length || _pattern[_offset] != ')')
                    {
                        _options = outerOptions;
                        _ungreedy = outerUngreedy;
                        _noAutoCapture = outerNoAutoCapture;
                        _allowDuplicateNames = outerAllowDuplicateNames;
                        node = Pcre2EmptyBacktrackingNode.Instance;
                        return false;
                    }

                    _offset++;
                    _options = outerOptions;
                    _ungreedy = outerUngreedy;
                    _noAutoCapture = outerNoAutoCapture;
                    _allowDuplicateNames = outerAllowDuplicateNames;
                    node = new Pcre2CaptureBacktrackingNode(slot, captureName, body);
                    _captureDefinitions.TryAdd(slot, (Pcre2CaptureBacktrackingNode)node);
                    return true;
                }

                if (_pattern.AsSpan(_offset).StartsWith("(?#", StringComparison.Ordinal))
                {
                    var end = _pattern.IndexOf(')', _offset + 3);
                    if (end < 0)
                    {
                        node = Pcre2EmptyBacktrackingNode.Instance;
                        return false;
                    }

                    _offset = end + 1;
                    node = Pcre2EmptyBacktrackingNode.Instance;
                    return true;
                }

                node = Pcre2EmptyBacktrackingNode.Instance;
                return false;
            }

            if (!Pcre2CharacterCompiler.TryCompileSingleToken(
                    _request,
                    _offset,
                    _options,
                    out var token,
                    out var patternLength) ||
                patternLength <= 0)
            {
                node = Pcre2EmptyBacktrackingNode.Instance;
                return false;
            }

            _offset += patternLength;
            node = CreateTokenNode(token);
            return true;

        }
        finally { budget.LeaveDepth(); }
    }

    private bool TryParseCaptureOpening(out string name, out int openingLength)
    {
        name = string.Empty;
        openingLength = 0;
        if (_pattern[_offset] != '(')
        {
            return false;
        }

        if (_offset + 1 >= _pattern.Length || _pattern[_offset + 1] != '?')
        {
            openingLength = 1;
            return !_noAutoCapture;
        }

        var nameStart = 0;
        char terminator;
        if (_pattern.AsSpan(_offset).StartsWith("(?<", StringComparison.Ordinal) &&
            _offset + 3 < _pattern.Length &&
            _pattern[_offset + 3] is not ('=' or '!'))
        {
            nameStart = _offset + 3;
            terminator = '>';
        }
        else if (_pattern.AsSpan(_offset).StartsWith("(?P<", StringComparison.Ordinal))
        {
            nameStart = _offset + 4;
            terminator = '>';
        }
        else if (_pattern.AsSpan(_offset).StartsWith("(?\'", StringComparison.Ordinal))
        {
            nameStart = _offset + 3;
            terminator = '\'';
        }
        else
        {
            return false;
        }

        var nameEnd = _pattern.IndexOf(terminator, nameStart);
        if (nameEnd <= nameStart || !IsValidCaptureName(_pattern.AsSpan(nameStart, nameEnd - nameStart)))
        {
            return false;
        }

        name = _pattern[nameStart..nameEnd];
        openingLength = nameEnd - _offset + 1;
        return true;
    }

    private bool TryParseBackreference(out IPcre2BacktrackingNode node)
    {
        node = Pcre2EmptyBacktrackingNode.Instance;
        if (_pattern[_offset] == '\\' && _offset + 1 < _pattern.Length)
        {
            var marker = _pattern[_offset + 1];
            if (marker is >= '1' and <= '9')
            {
                var cursor = _offset + 1;
                if (!TryReadReferenceNumber(ref cursor, false, out var number))
                {
                    return false;
                }

                if (number < 8 || marker is '8' or '9' || number <= _captureCount)
                {
                    _offset = cursor;
                    node = CreateBackreference(Pcre2BackreferenceTargetKind.Absolute, number, string.Empty);
                    return true;
                }

                var octalCursor = _offset + 1;
                var octalEnd = Math.Min(octalCursor + 3, _pattern.Length);
                var octal = 0;
                while (octalCursor < octalEnd && _pattern[octalCursor] is >= '0' and <= '7')
                {
                    octal = octal * 8 + _pattern[octalCursor++] - '0';
                }

                if (octal > byte.MaxValue)
                {
                    return false;
                }

                var patternLength = octalCursor - _offset;
                node = CreateTokenNode(Pcre2CharacterToken.CreateLiteral(
                    new Rune(octal),
                    _options));
                _offset = octalCursor;
                return true;
            }

            if (marker == 'g')
            {
                return TryParseGBackreference(out node);
            }

            if (marker == 'k')
            {
                return TryParseKBackreference(out node);
            }
        }

        if (_pattern.AsSpan(_offset).StartsWith("(?P=", StringComparison.Ordinal))
        {
            var nameStart = _offset + 4;
            var nameEnd = _pattern.IndexOf(')', nameStart);
            if (nameEnd <= nameStart || !IsValidCaptureName(_pattern.AsSpan(nameStart, nameEnd - nameStart)))
            {
                return false;
            }

            var name = _pattern[nameStart..nameEnd];
            _offset = nameEnd + 1;
            node = CreateBackreference(Pcre2BackreferenceTargetKind.Named, 0, name);
            return true;
        }

        return false;
    }

    private bool TryParseQuotedSequence(out IPcre2BacktrackingNode[] children)
    {
        var result = new List<IPcre2BacktrackingNode>();
        _offset += 2;
        while (_offset < _pattern.Length)
        {
            if (_pattern.AsSpan(_offset).StartsWith("\\E", StringComparison.Ordinal))
            {
                _offset += 2;
                break;
            }

            Utf8ConstructionScope.ChargeWork(1);
            var literalStart = _offset;
            if (Rune.DecodeFromUtf16(_pattern.AsSpan(_offset), out var literal, out var length) != OperationStatus.Done)
            {
                children = [];
                return false;
            }

            _offset += length;
            result.Add(CreateTokenNode(Pcre2CharacterToken.CreateLiteral(
                literal,
                _options)));
        }

        children = [.. result];
        return true;
    }

    private bool TryParseGBackreference(out IPcre2BacktrackingNode node)
    {
        node = Pcre2EmptyBacktrackingNode.Instance;
        var cursor = _offset + 2;
        if (cursor >= _pattern.Length)
        {
            return false;
        }

        if (_pattern[cursor] == '{')
        {
            var contentStart = ++cursor;
            var end = _pattern.IndexOf('}', contentStart);
            if (end < 0)
            {
                return false;
            }

            var content = _pattern.AsSpan(contentStart, end - contentStart);
            if (TryParseSignedReference(content, out var number))
            {
                _offset = end + 1;
                var kind = content[0] is '+' or '-'
                    ? Pcre2BackreferenceTargetKind.Relative
                    : Pcre2BackreferenceTargetKind.Absolute;
                node = CreateBackreference(kind, number, string.Empty);
                return true;
            }

            if (!IsValidCaptureName(content))
            {
                return false;
            }

            _offset = end + 1;
            node = CreateBackreference(Pcre2BackreferenceTargetKind.Named, 0, content.ToString());
            return true;
        }

        if (!TryReadReferenceNumber(ref cursor, true, out var relativeOrAbsolute))
        {
            return false;
        }

        var targetKind = _pattern[_offset + 2] is '+' or '-'
            ? Pcre2BackreferenceTargetKind.Relative
            : Pcre2BackreferenceTargetKind.Absolute;
        _offset = cursor;
        node = CreateBackreference(targetKind, relativeOrAbsolute, string.Empty);
        return true;
    }

    private bool TryParseKBackreference(out IPcre2BacktrackingNode node)
    {
        node = Pcre2EmptyBacktrackingNode.Instance;
        var cursor = _offset + 2;
        if (cursor >= _pattern.Length || _pattern[cursor] is not ('<' or '\'' or '{'))
        {
            return false;
        }

        var terminator = _pattern[cursor] switch
        {
            '<' => '>',
            '\'' => '\'',
            _ => '}',
        };
        var nameStart = cursor + 1;
        var nameEnd = _pattern.IndexOf(terminator, nameStart);
        if (nameEnd <= nameStart || !IsValidCaptureName(_pattern.AsSpan(nameStart, nameEnd - nameStart)))
        {
            return false;
        }

        var name = _pattern[nameStart..nameEnd];
        _offset = nameEnd + 1;
        node = CreateBackreference(Pcre2BackreferenceTargetKind.Named, 0, name);
        return true;
    }

    private Pcre2BackreferenceBacktrackingNode CreateBackreference(
        Pcre2BackreferenceTargetKind kind,
        int number,
        string name) =>
        new(new Pcre2BackreferenceTarget(kind, number, name, _captureCount), _options);

    private bool TryReadReferenceNumber(ref int cursor, bool allowSign, out int number)
    {
        var start = cursor;
        var sign = 1;
        if (allowSign && cursor < _pattern.Length && _pattern[cursor] is '+' or '-')
        {
            sign = _pattern[cursor++] == '-' ? -1 : 1;
        }

        var digitStart = cursor;
        number = 0;
        while (cursor < _pattern.Length && _pattern[cursor] is >= '0' and <= '9')
        {
            var digit = _pattern[cursor++] - '0';
            if (number > (int.MaxValue - digit) / 10)
            {
                cursor = start;
                number = 0;
                return false;
            }

            number = number * 10 + digit;
        }

        if (cursor == digitStart)
        {
            cursor = start;
            return false;
        }

        number *= sign;
        return true;
    }

    private static bool TryParseSignedReference(ReadOnlySpan<char> text, out int number)
    {
        number = 0;
        if (text.IsEmpty)
        {
            return false;
        }

        var sign = 1;
        var offset = 0;
        if (text[0] is '+' or '-')
        {
            sign = text[0] == '-' ? -1 : 1;
            offset = 1;
        }

        if (offset == text.Length)
        {
            return false;
        }

        for (; offset < text.Length; offset++)
        {
            if (text[offset] is not (>= '0' and <= '9'))
            {
                return false;
            }

            var digit = text[offset] - '0';
            if (number > (int.MaxValue - digit) / 10)
            {
                return false;
            }

            number = number * 10 + digit;
        }

        number *= sign;
        return true;
    }

    private static bool IsValidCaptureName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty || !Pcre2CaptureNameValidator.FitsUtf8Limit(name, 128))
        {
            return false;
        }

        var first = true;
        foreach (var scalar in name.EnumerateRunes())
        {
            var valid = scalar.Value == '_' || Rune.IsLetter(scalar) ||
                !first && Rune.GetUnicodeCategory(scalar) == System.Globalization.UnicodeCategory.DecimalDigitNumber;
            if (!valid)
            {
                return false;
            }

            first = false;
        }

        return true;
    }

    private bool TryParseQuantifier(IPcre2BacktrackingNode body, out IPcre2BacktrackingNode node)
    {
        node = body;
        SkipExtendedTrivia();
        if (_offset >= _pattern.Length)
        {
            return true;
        }

        var minimum = 0;
        var maximum = 0;
        var hasQuantifier = true;
        switch (_pattern[_offset])
        {
            case '*':
                maximum = int.MaxValue;
                _offset++;
                break;
            case '+':
                minimum = 1;
                maximum = int.MaxValue;
                _offset++;
                break;
            case '?':
                maximum = 1;
                _offset++;
                break;
            case '{':
                if (!TryParseBoundedQuantifier(out minimum, out maximum))
                {
                    hasQuantifier = false;
                }
                break;
            default:
                hasQuantifier = false;
                break;
        }

        if (!hasQuantifier)
        {
            return true;
        }

        _sawControlFlow = true;
        var preference = _ungreedy ? Pcre2RepeatPreference.Lazy : Pcre2RepeatPreference.Greedy;
        SkipExtendedTrivia();
        if (_offset < _pattern.Length && _pattern[_offset] == '?')
        {
            preference = preference == Pcre2RepeatPreference.Greedy
                ? Pcre2RepeatPreference.Lazy
                : Pcre2RepeatPreference.Greedy;
            _offset++;
        }
        else if (_offset < _pattern.Length && _pattern[_offset] == '+')
        {
            preference = Pcre2RepeatPreference.Possessive;
            _offset++;
        }

        node = new Pcre2RepeatBacktrackingNode(body, minimum, maximum, preference);
        return true;
    }

    private bool TryParseAtomicGroup(out IPcre2BacktrackingNode node)
    {
        _sawControlFlow = true;
        var longForm = _pattern.AsSpan(_offset).StartsWith("(*atomic:", StringComparison.OrdinalIgnoreCase);
        _offset += longForm ? 9 : 3;
        var outerOptions = _options;
        var outerUngreedy = _ungreedy;
        var outerNoAutoCapture = _noAutoCapture;
        var outerAllowDuplicateNames = _allowDuplicateNames;
        if (!TryParseAlternation(true, out var body) || _offset >= _pattern.Length || _pattern[_offset] != ')')
        {
            _options = outerOptions;
            _ungreedy = outerUngreedy;
            _noAutoCapture = outerNoAutoCapture;
            _allowDuplicateNames = outerAllowDuplicateNames;
            node = Pcre2EmptyBacktrackingNode.Instance;
            return false;
        }

        _offset++;
        _options = outerOptions;
        _ungreedy = outerUngreedy;
        _noAutoCapture = outerNoAutoCapture;
        _allowDuplicateNames = outerAllowDuplicateNames;
        node = new Pcre2AtomicBacktrackingNode(body);
        return true;
    }

    private bool TryParseControlVerb(out IPcre2BacktrackingNode node)
    {
        node = Pcre2EmptyBacktrackingNode.Instance;
        if (!_pattern.AsSpan(_offset).StartsWith("(*", StringComparison.Ordinal))
        {
            return false;
        }

        var end = _pattern.IndexOf(')', _offset + 2);
        if (end < 0)
        {
            return false;
        }

        var content = _pattern.AsSpan(_offset + 2, end - _offset - 2);
        var colon = content.IndexOf(':');
        var verbText = colon < 0 ? content : content[..colon];
        var name = colon < 0 ? ReadOnlySpan<char>.Empty : content[(colon + 1)..];
        Pcre2ControlVerbKind verbKind;
        if (verbText.IsEmpty)
        {
            verbKind = Pcre2ControlVerbKind.Mark;
        }
        else if (verbText.Equals("ACCEPT", StringComparison.OrdinalIgnoreCase))
        {
            verbKind = Pcre2ControlVerbKind.Accept;
        }
        else if (verbText.Equals("FAIL", StringComparison.OrdinalIgnoreCase) ||
            verbText.Equals("F", StringComparison.OrdinalIgnoreCase))
        {
            verbKind = Pcre2ControlVerbKind.Fail;
        }
        else if (verbText.Equals("MARK", StringComparison.OrdinalIgnoreCase))
        {
            verbKind = Pcre2ControlVerbKind.Mark;
        }
        else if (verbText.Equals("COMMIT", StringComparison.OrdinalIgnoreCase))
        {
            verbKind = Pcre2ControlVerbKind.Commit;
        }
        else if (verbText.Equals("PRUNE", StringComparison.OrdinalIgnoreCase))
        {
            verbKind = Pcre2ControlVerbKind.Prune;
        }
        else if (verbText.Equals("SKIP", StringComparison.OrdinalIgnoreCase))
        {
            verbKind = Pcre2ControlVerbKind.Skip;
        }
        else if (verbText.Equals("THEN", StringComparison.OrdinalIgnoreCase))
        {
            verbKind = Pcre2ControlVerbKind.Then;
        }
        else
        {
            return false;
        }

        if ((verbKind == Pcre2ControlVerbKind.Mark && name.IsEmpty) ||
            !Pcre2CaptureNameValidator.FitsUtf8Limit(name, 255))
        {
            return false;
        }

        _sawControlFlow = true;
        _offset = end + 1;
        node = new Pcre2ControlVerbBacktrackingNode(verbKind, name.ToString());
        return true;
    }

    private bool TryParseBoundedQuantifier(out int minimum, out int maximum)
    {
        var savedOffset = _offset;
        _offset++;
        SkipHorizontalSpace();
        if (!TryReadDecimal(out minimum))
        {
            _offset = savedOffset;
            maximum = 0;
            return false;
        }

        SkipHorizontalSpace();
        if (_offset < _pattern.Length && _pattern[_offset] == '}')
        {
            _offset++;
            maximum = minimum;
            return true;
        }

        if (_offset >= _pattern.Length || _pattern[_offset] != ',')
        {
            _offset = savedOffset;
            maximum = 0;
            return false;
        }

        _offset++;
        SkipHorizontalSpace();
        if (!TryReadDecimal(out maximum))
        {
            maximum = int.MaxValue;
        }

        SkipHorizontalSpace();
        if (_offset >= _pattern.Length || _pattern[_offset] != '}' || maximum < minimum)
        {
            _offset = savedOffset;
            minimum = 0;
            maximum = 0;
            return false;
        }

        _offset++;
        return true;
    }

    private bool TryReadDecimal(out int value)
    {
        value = 0;
        var start = _offset;
        while (_offset < _pattern.Length && _pattern[_offset] is >= '0' and <= '9')
        {
            var digit = _pattern[_offset++] - '0';
            if (value > (int.MaxValue - digit) / 10)
            {
                return false;
            }

            value = value * 10 + digit;
        }

        return _offset > start;
    }

    private bool TryParseSubroutineCall(out IPcre2BacktrackingNode node)
    {
        node = Pcre2EmptyBacktrackingNode.Instance;
        if (_pattern.AsSpan(_offset).StartsWith("(?R)", StringComparison.Ordinal) ||
            _pattern.AsSpan(_offset).StartsWith("(?0)", StringComparison.Ordinal))
        {
            _offset += 4;
            node = new Pcre2SubroutineCallBacktrackingNode(
                new Pcre2BackreferenceTarget(Pcre2BackreferenceTargetKind.Absolute, 0, string.Empty, _captureCount));
            return true;
        }

        if (_pattern.AsSpan(_offset).StartsWith("(?&", StringComparison.Ordinal) ||
            _pattern.AsSpan(_offset).StartsWith("(?P>", StringComparison.Ordinal))
        {
            var nameStart = _offset + (_pattern[_offset + 2] == '&' ? 3 : 4);
            var nameEnd = _pattern.IndexOf(')', nameStart);
            if (nameEnd <= nameStart || !IsValidCaptureName(_pattern.AsSpan(nameStart, nameEnd - nameStart)))
            {
                return false;
            }

            var name = _pattern[nameStart..nameEnd];
            _offset = nameEnd + 1;
            node = new Pcre2SubroutineCallBacktrackingNode(
                new Pcre2BackreferenceTarget(Pcre2BackreferenceTargetKind.Named, 0, name, _captureCount));
            return true;
        }

        if (!_pattern.AsSpan(_offset).StartsWith("(?", StringComparison.Ordinal) ||
            _offset + 3 >= _pattern.Length ||
            _pattern[_offset + 2] is not ('+' or '-' or >= '1' and <= '9'))
        {
            return false;
        }

        var cursor = _offset + 2;
        if (!TryReadReferenceNumber(ref cursor, true, out var number) ||
            cursor >= _pattern.Length ||
            _pattern[cursor] != ')')
        {
            return false;
        }

        var kind = _pattern[_offset + 2] is '+' or '-'
            ? Pcre2BackreferenceTargetKind.Relative
            : Pcre2BackreferenceTargetKind.Absolute;
        _offset = cursor + 1;
        node = new Pcre2SubroutineCallBacktrackingNode(
            new Pcre2BackreferenceTarget(kind, number, string.Empty, _captureCount));
        return true;
    }

    private bool TryParseConditional(out IPcre2BacktrackingNode node)
    {
        node = Pcre2EmptyBacktrackingNode.Instance;
        if (!_pattern.AsSpan(_offset).StartsWith("(?(", StringComparison.Ordinal))
        {
            return false;
        }

        _sawControlFlow = true;
        _offset += 3;
        if (_pattern.AsSpan(_offset).StartsWith("DEFINE)", StringComparison.Ordinal))
        {
            _offset += 7;
            var definitionOptions = _options;
            var definitionUngreedy = _ungreedy;
            var definitionNoAutoCapture = _noAutoCapture;
            var definitionAllowDuplicateNames = _allowDuplicateNames;
            if (!TryParseAlternation(true, out _) || _offset >= _pattern.Length || _pattern[_offset] != ')')
            {
                return false;
            }

            _offset++;
            _options = definitionOptions;
            _ungreedy = definitionUngreedy;
            _noAutoCapture = definitionNoAutoCapture;
            _allowDuplicateNames = definitionAllowDuplicateNames;
            return true;
        }

        var conditionKind = Pcre2BacktrackingConditionKind.CaptureSet;
        var target = default(Pcre2BackreferenceTarget);
        Pcre2AssertionBacktrackingNode? assertion = null;
        if (TryParseConditionalAssertion(out assertion))
        {
            conditionKind = Pcre2BacktrackingConditionKind.Assertion;
        }
        else if (!TryParseConditionalReference(out conditionKind, out target))
        {
            return false;
        }

        var outerOptions = _options;
        var outerUngreedy = _ungreedy;
        var outerNoAutoCapture = _noAutoCapture;
        var outerAllowDuplicateNames = _allowDuplicateNames;
        if (!TryParseSequence(true, out var yesBranch))
        {
            return false;
        }

        IPcre2BacktrackingNode noBranch = Pcre2EmptyBacktrackingNode.Instance;
        if (_offset < _pattern.Length && _pattern[_offset] == '|')
        {
            _offset++;
            if (!TryParseSequence(true, out noBranch))
            {
                return false;
            }
        }

        if (_offset >= _pattern.Length || _pattern[_offset] != ')')
        {
            return false;
        }

        _offset++;
        _options = outerOptions;
        _ungreedy = outerUngreedy;
        _noAutoCapture = outerNoAutoCapture;
        _allowDuplicateNames = outerAllowDuplicateNames;
        node = new Pcre2ConditionalBacktrackingNode(
            conditionKind,
            target,
            assertion,
            yesBranch,
            noBranch);
        return true;
    }

    private bool TryParseConditionalAssertion(out Pcre2AssertionBacktrackingNode? assertion)
    {
        assertion = null;
        Pcre2AssertionKind kind;
        var openingLength = 0;
        if (_pattern.AsSpan(_offset).StartsWith("?<=", StringComparison.Ordinal))
        {
            kind = Pcre2AssertionKind.PositiveLookbehind;
            openingLength = 3;
        }
        else if (_pattern.AsSpan(_offset).StartsWith("?<!", StringComparison.Ordinal))
        {
            kind = Pcre2AssertionKind.NegativeLookbehind;
            openingLength = 3;
        }
        else if (_pattern.AsSpan(_offset).StartsWith("?=", StringComparison.Ordinal))
        {
            kind = Pcre2AssertionKind.PositiveLookahead;
            openingLength = 2;
        }
        else if (_pattern.AsSpan(_offset).StartsWith("?!", StringComparison.Ordinal))
        {
            kind = Pcre2AssertionKind.NegativeLookahead;
            openingLength = 2;
        }
        else
        {
            return false;
        }

        var outerOptions = _options;
        var outerUngreedy = _ungreedy;
        var outerNoAutoCapture = _noAutoCapture;
        var outerAllowDuplicateNames = _allowDuplicateNames;
        _offset += openingLength;
        if (!TryParseAlternation(true, out var body) || _offset >= _pattern.Length || _pattern[_offset] != ')')
        {
            _options = outerOptions;
            _ungreedy = outerUngreedy;
            _noAutoCapture = outerNoAutoCapture;
            _allowDuplicateNames = outerAllowDuplicateNames;
            return false;
        }

        _offset++;
        _options = outerOptions;
        _ungreedy = outerUngreedy;
        _noAutoCapture = outerNoAutoCapture;
        _allowDuplicateNames = outerAllowDuplicateNames;
        assertion = new Pcre2AssertionBacktrackingNode(kind, body);
        return true;
    }

    private bool TryParseConditionalReference(
        out Pcre2BacktrackingConditionKind conditionKind,
        out Pcre2BackreferenceTarget target)
    {
        conditionKind = Pcre2BacktrackingConditionKind.CaptureSet;
        target = default;
        var contentStart = _offset;
        var contentEnd = _pattern.IndexOf(')', contentStart);
        if (contentEnd <= contentStart)
        {
            return false;
        }

        var content = _pattern.AsSpan(contentStart, contentEnd - contentStart);
        if (content[0] == 'R')
        {
            if (content.Length == 1)
            {
                conditionKind = Pcre2BacktrackingConditionKind.RecursionAny;
                _offset = contentEnd + 1;
                return true;
            }

            var recursionTarget = content[1..];
            if (recursionTarget[0] == '&')
            {
                recursionTarget = recursionTarget[1..];
                if (!IsValidCaptureName(recursionTarget))
                {
                    return false;
                }

                target = new Pcre2BackreferenceTarget(
                    Pcre2BackreferenceTargetKind.Named,
                    0,
                    recursionTarget.ToString(),
                    _captureCount);
            }
            else if (TryParseSignedReference(recursionTarget, out var recursionNumber) && recursionNumber > 0)
            {
                target = new Pcre2BackreferenceTarget(
                    Pcre2BackreferenceTargetKind.Absolute,
                    recursionNumber,
                    string.Empty,
                    _captureCount);
            }
            else
            {
                return false;
            }

            conditionKind = Pcre2BacktrackingConditionKind.RecursionSlot;
            _offset = contentEnd + 1;
            return true;
        }

        if (content[0] == '<' && content[^1] == '>' || content[0] == '\'' && content[^1] == '\'')
        {
            content = content[1..^1];
        }

        if (TryParseSignedReference(content, out var number) && number > 0)
        {
            target = new Pcre2BackreferenceTarget(
                Pcre2BackreferenceTargetKind.Absolute,
                number,
                string.Empty,
                _captureCount);
        }
        else if (IsValidCaptureName(content))
        {
            target = new Pcre2BackreferenceTarget(
                Pcre2BackreferenceTargetKind.Named,
                0,
                content.ToString(),
                _captureCount);
        }
        else
        {
            return false;
        }

        _offset = contentEnd + 1;
        return true;
    }

    private bool TryParseBranchReset(out IPcre2BacktrackingNode node)
    {
        UsesBranchReset = true;
        _sawControlFlow = true;
        _offset += 3;
        var captureBase = _captureCount;
        var maximumCaptureCount = captureBase;
        var alternatives = new List<IPcre2BacktrackingNode>();
        var outerOptions = _options;
        var outerUngreedy = _ungreedy;
        var outerNoAutoCapture = _noAutoCapture;
        var outerAllowDuplicateNames = _allowDuplicateNames;
        while (true)
        {
            _captureCount = captureBase;
            if (!TryParseSequence(true, out var alternative))
            {
                node = Pcre2EmptyBacktrackingNode.Instance;
                return false;
            }

            alternatives.Add(alternative);
            maximumCaptureCount = Math.Max(maximumCaptureCount, _captureCount);
            if (_offset >= _pattern.Length)
            {
                node = Pcre2EmptyBacktrackingNode.Instance;
                return false;
            }

            if (_pattern[_offset] == ')')
            {
                _offset++;
                _captureCount = maximumCaptureCount;
                _options = outerOptions;
                _ungreedy = outerUngreedy;
                _noAutoCapture = outerNoAutoCapture;
                _allowDuplicateNames = outerAllowDuplicateNames;
                node = alternatives.Count == 1
                    ? alternatives[0]
                    : new Pcre2AlternationBacktrackingNode([.. alternatives]);
                return true;
            }

            if (_pattern[_offset] != '|')
            {
                node = Pcre2EmptyBacktrackingNode.Instance;
                return false;
            }

            _offset++;
        }
    }

    private bool TryRegisterCaptureName(int slot, string name)
    {
        // Admit this duplicate-name scan as a block, rather than charging each
        // helper object or equality probe. Names have the profile's length cap.
        Utf8ConstructionScope.ChargeWork((ulong)_nameEntries.Count + (ulong)name.Length);
        if (_captureSlotNames.TryGetValue(slot, out var existingSlotName) &&
            !string.Equals(existingSlotName, name, StringComparison.Ordinal))
        {
            throw new Pcre2CompileException(
                "A branch-reset capture number cannot have more than one name.",
                Pcre2ErrorKind.InvalidAfterParensQuery);
        }

        var scanned = 0;
        foreach (var entry in _nameEntries)
        {
            if ((scanned++ & 63) == 0) Utf8ConstructionScope.ChargeWork(0);
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (entry.Number == slot)
            {
                return true;
            }

            if (!_allowDuplicateNames)
            {
                throw new Pcre2CompileException(
                    "Duplicate capture names require duplicate-name mode.",
                    Pcre2ErrorKind.InvalidAfterParensQuery);
            }
        }

        _captureSlotNames[slot] = name;
        _nameEntries.Add(new Pcre2NameEntry { Name = name, Number = slot });
        return true;
    }

    private bool TryParseAssertionOpening(out Pcre2AssertionKind assertionKind, out int openingLength)
    {
        if (_pattern.AsSpan(_offset).StartsWith("(?=", StringComparison.Ordinal))
        {
            assertionKind = Pcre2AssertionKind.PositiveLookahead;
            openingLength = 3;
            return true;
        }

        if (_pattern.AsSpan(_offset).StartsWith("(?!", StringComparison.Ordinal))
        {
            assertionKind = Pcre2AssertionKind.NegativeLookahead;
            openingLength = 3;
            return true;
        }

        if (_pattern.AsSpan(_offset).StartsWith("(?<=", StringComparison.Ordinal))
        {
            assertionKind = Pcre2AssertionKind.PositiveLookbehind;
            openingLength = 4;
            return true;
        }

        if (_pattern.AsSpan(_offset).StartsWith("(?<!", StringComparison.Ordinal))
        {
            assertionKind = Pcre2AssertionKind.NegativeLookbehind;
            openingLength = 4;
            return true;
        }

        assertionKind = default;
        openingLength = 0;
        return false;
    }

    private bool TryParseScopedOptionOpening(
        out Pcre2CharacterOptions options,
        out bool ungreedy,
        out bool noAutoCapture,
        out bool allowDuplicateNames,
        out int openingLength)
    {
        options = _options;
        ungreedy = _ungreedy;
        noAutoCapture = _noAutoCapture;
        allowDuplicateNames = _allowDuplicateNames;
        openingLength = 0;
        if (_offset + 3 > _pattern.Length || !_pattern.AsSpan(_offset).StartsWith("(?", StringComparison.Ordinal))
        {
            return false;
        }

        var cursor = _offset + 2;
        var enabling = true;
        var sawOption = false;
        var enabledX = false;
        var enabledMore = false;
        var disabledX = false;
        var reset = cursor < _pattern.Length && _pattern[cursor] == '^';
        if (reset)
        {
            options = Pcre2CharacterOptions.None;
            noAutoCapture = false;
            sawOption = true;
            cursor++;
        }
        while (cursor < _pattern.Length)
        {
            var ch = _pattern[cursor];
            if (ch == '-')
            {
                if (reset) return false;
                if (!enabling)
                    throw new Pcre2CompileException("Only one hyphen is allowed in an option directive.",
                        Pcre2ErrorKind.InvalidAfterParensQuery);
                enabling = false;
                sawOption = true;
                cursor++;
                continue;
            }

            if (ch == ':')
            {
                if (!sawOption)
                {
                    return false;
                }

                options = Pcre2CharacterOptionsFactory.ApplyExtendedOptions(options, enabledX, enabledMore, disabledX);
                RequiresProfileOptions |= enabledX || reset;
                openingLength = cursor - _offset + 1;
                return true;
            }

            if (ch == 'x')
            {
                var doubled = cursor + 1 < _pattern.Length && _pattern[cursor + 1] == 'x';
                if (enabling) { enabledX = true; enabledMore |= doubled; }
                else disabledX = true;
                sawOption = true;
                cursor += doubled ? 2 : 1;
                continue;
            }
            if (ch == 'J')
            {
                allowDuplicateNames = enabling;
                RequiresProfileOptions = true;
            }
            else if (!TryApplyOption(ref options, ref ungreedy, ref noAutoCapture, ch, enabling))
            {
                return false;
            }
            sawOption = true;
            cursor++;
        }

        return false;
    }

    private bool TryParseGlobalOptions()
    {
        if (_offset + 3 > _pattern.Length || _pattern[_offset] != '(' || _pattern[_offset + 1] != '?')
        {
            return false;
        }

        var cursor = _offset + 2;
        var enabling = true;
        var options = _options;
        var ungreedy = _ungreedy;
        var noAutoCapture = _noAutoCapture;
        var allowDuplicateNames = _allowDuplicateNames;
        var sawOption = cursor < _pattern.Length && _pattern[cursor] == ')';
        var enabledX = false;
        var enabledMore = false;
        var disabledX = false;
        var reset = cursor < _pattern.Length && _pattern[cursor] == '^';
        if (reset)
        {
            options = Pcre2CharacterOptions.None;
            noAutoCapture = false;
            sawOption = true;
            cursor++;
        }
        while (cursor < _pattern.Length)
        {
            var ch = _pattern[cursor];
            if (ch == '-')
            {
                if (reset) return false;
                if (!enabling)
                    throw new Pcre2CompileException("Only one hyphen is allowed in an option directive.",
                        Pcre2ErrorKind.InvalidAfterParensQuery);
                enabling = false;
                sawOption = true;
                cursor++;
                continue;
            }

            if (ch == ')')
            {
                if (!sawOption)
                {
                    return false;
                }

                // PCRE2 permits an empty option directive; Core grammar does not.
                if (cursor == _offset + 2)
                {
                    _sawControlFlow = true;
                    RequiresProfileOptions = true;
                }
                _options = Pcre2CharacterOptionsFactory.ApplyExtendedOptions(options, enabledX, enabledMore, disabledX);
                RequiresProfileOptions |= enabledX || reset;
                _ungreedy = ungreedy;
                _noAutoCapture = noAutoCapture;
                _allowDuplicateNames = allowDuplicateNames;
                _offset = cursor + 1;
                return true;
            }

            if (ch == 'x')
            {
                var doubled = cursor + 1 < _pattern.Length && _pattern[cursor + 1] == 'x';
                if (enabling) { enabledX = true; enabledMore |= doubled; }
                else disabledX = true;
                sawOption = true;
                cursor += doubled ? 2 : 1;
                continue;
            }
            if (!TryApplyOption(ref options, ref ungreedy, ref noAutoCapture, ch, enabling))
            {
                if (ch == 'J')
                {
                    allowDuplicateNames = enabling;
                    RequiresProfileOptions = true;
                    sawOption = true;
                    cursor++;
                    continue;
                }

                return false;
            }
            sawOption = true;
            cursor++;
        }

        return false;
    }

    private bool TryApplyOption(
        ref Pcre2CharacterOptions options,
        ref bool ungreedy,
        ref bool noAutoCapture,
        char option,
        bool enabling)
    {
        Pcre2CharacterOptions flag;
        switch (option)
        {
            case 'i':
                flag = Pcre2CharacterOptions.Caseless;
                break;
            case 'r':
                flag = Pcre2CharacterOptions.RestrictCaseless;
                RequiresProfileOptions = true;
                break;
            case 'm':
                flag = Pcre2CharacterOptions.Multiline;
                break;
            case 's':
                flag = Pcre2CharacterOptions.DotAll;
                break;
            case 'n':
                noAutoCapture = enabling;
                return true;
            case 'U':
                ungreedy = enabling;
                return true;
            default:
                return false;
        }

        options = enabling ? options | flag : options & ~flag;
        return true;
    }

    private void SkipExtendedTrivia()
    {
        while (_offset < _pattern.Length)
        {
            if ((_offset & 2047) == 0) Utf8ConstructionScope.ChargeWork(0);
            if (Pcre2CompileValidator.TrySkipParenthesizedComment(_pattern, ref _offset))
            {
                _sawControlFlow = true;
                continue;
            }
            if ((_options & (Pcre2CharacterOptions.Extended | Pcre2CharacterOptions.ExtendedMore)) == 0) return;
            if (_pattern[_offset] is ' ' or '\t' or '\n' or '\v' or '\f' or '\r')
            {
                _offset++;
                continue;
            }

            if (_pattern[_offset] != '#')
            {
                return;
            }

            Pcre2PatternCodeScan.TrySkipExtendedComment(_pattern, ref _offset, true, _request.Settings.Newline);
            _offset++;
        }
    }

    private void SkipHorizontalSpace()
    {
        while (_offset < _pattern.Length && _pattern[_offset] is ' ' or '\t')
        {
            _offset++;
        }
    }

    private static bool IsQuantifierStart(char value) => value is '*' or '+' or '?' or '{';

}
