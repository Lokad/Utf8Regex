using System.Collections;
using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal sealed partial class RegexParser
{
    private readonly string _pattern;
    private readonly CultureInfo _culture;
    private RegexOptions _options;
    private readonly Stack<RegexOptions> _optionsStack = new();
    private ParserStateFrame? _stack;
    private RegexNode? _group;
    private RegexNode? _alternation;
    private RegexNode? _concatenation;
    private RegexNode? _unit;
    private List<RegexReplacementToken>? _replacementTokens;
    private StringBuilder? _replacementLiteral;
    private int[] _replacementGroupNumbers = [];
    private string[] _replacementGroupNames = [];
    private bool _resolveReplacementGroups;
    private int[] _groupNumbers = [];
    private string[] _groupNames = [];
    private int[] _implicitCaptureNumbers = [];
    private Hashtable? _captureNumberSparseMapping;
    private Hashtable? _captureNameToNumberMapping;
    private int _captureCount;

    private readonly record struct ReplacementScanResult(
        bool Success,
        int NextPosition,
        RegexReplacementToken? Token = null,
        bool LiteralizeOriginalText = false,
        int LiteralStart = -1,
        string? LiteralText = null);

    private RegexParser(string pattern, RegexOptions options, CultureInfo culture)
    {
        _pattern = pattern;
        _options = options;
        _culture = culture;
    }

    private sealed class ParserStateFrame
    {
        public required ParserStateFrame? Previous { get; init; }

        public required RegexNode? Group { get; init; }

        public required RegexNode? Alternation { get; init; }

        public required RegexNode? Concatenation { get; init; }

        public required RegexNode? Unit { get; init; }
    }

    public static CultureInfo GetTargetCulture(RegexOptions options) =>
        (options & RegexOptions.CultureInvariant) != 0 ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;

    public static RegexOptions ParseOptionsInPattern(string pattern, RegexOptions options)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var index = 0;
        while (TryParseLeadingOptionsGroup(pattern, ref index, ref options))
        {
        }

        return options;
    }

    public static RegexTree Parse(string pattern, RegexOptions options, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(culture);

        var parser = new RegexParser(pattern, options, culture);
        if (parser.TryParseCore(out var tree, out var error, out var errorOffset))
        {
            return tree;
        }

        throw new RegexParseException(
            error,
            errorOffset,
            "The current native-subset parser supports only the implemented runtime front-end subset.");
    }

    public static bool TryParse(string pattern, RegexOptions options, CultureInfo culture, out RegexTree tree)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(culture);

        return new RegexParser(pattern, options, culture).TryParseCore(out tree, out _, out _);
    }

    public static int MapCaptureNumber(int capnum, Hashtable? caps)
    {
        return capnum == -1
            ? -1
            : caps is not null
                ? (int)caps[capnum]!
                : capnum;
    }

    public static string GroupNameFromNumber(Hashtable? caps, string[]? capsList, int capsize, int number)
    {
        if (capsList is null)
        {
            return (uint)number < (uint)capsize
                ? ((uint)number).ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        var mapped = number;
        if (caps is not null)
        {
            if (caps[number] is not int sparseIndex)
            {
                return string.Empty;
            }

            mapped = sparseIndex;
        }

        if ((uint)mapped < (uint)capsList.Length)
        {
            return capsList[mapped];
        }

        return string.Empty;
    }

    public static int GroupNumberFromName(Hashtable? caps, string[]? capsList, int capsize, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (caps is not null)
        {
            return caps[name] is int result ? result : -1;
        }

        if (uint.TryParse(name, out var slot) && slot < capsize)
        {
            return (int)slot;
        }

        if (capsList is not null)
        {
            for (var i = 0; i < capsList.Length; i++)
            {
                if (string.Equals(capsList[i], name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private bool TryParseCore(
        out RegexTree tree,
        out RegexParseError error,
        out int errorOffset)
    {
        _scanPattern = NormalizeLeadingGlobalOptions(_pattern, ref _options);
        _scanOptions = _options;
        CountCaptures();

        tree = ParsePatternTree()!;
        if (tree is null)
        {
            error = _scanError;
            errorOffset = _scanErrorOffset >= 0 ? _scanErrorOffset : 0;
            return false;
        }

        if (!TryValidateReferences(tree, out error, out errorOffset))
        {
            tree = null!;
            return false;
        }

        error = RegexParseError.InvalidPattern;
        errorOffset = 0;
        return true;
    }

    private void CountCaptures()
    {
        (_groupNumbers, _groupNames, _implicitCaptureNumbers) = CaptureMetadataScanner.Analyze(_scanPattern, _scanOptions, _culture);
        _captureNumberSparseMapping = BuildSparseCaptureNumberMapping(_groupNumbers);
        _captureNameToNumberMapping = BuildCaptureNameToNumberMapping(_groupNumbers, _groupNames);
        _captureCount = _captureNumberSparseMapping is null
            ? (_groupNumbers.Length == 0 ? 0 : _groupNumbers[^1] + 1)
            : _groupNumbers.Length;
    }

    private static Hashtable? BuildSparseCaptureNumberMapping(ReadOnlySpan<int> groupNumbers)
    {
        if (groupNumbers.Length == 0 || groupNumbers[^1] + 1 == groupNumbers.Length)
        {
            return null;
        }

        var mapping = new Hashtable();
        for (var i = 0; i < groupNumbers.Length; i++)
        {
            mapping[groupNumbers[i]] = i;
        }

        return mapping;
    }

    private static Hashtable? BuildCaptureNameToNumberMapping(ReadOnlySpan<int> groupNumbers, ReadOnlySpan<string> groupNames)
    {
        var hasNamedCaptures = false;
        for (var i = 0; i < groupNumbers.Length; i++)
        {
            if (!string.Equals(groupNames[i], groupNumbers[i].ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                hasNamedCaptures = true;
                break;
            }
        }

        if (!hasNamedCaptures)
        {
            return null;
        }

        var mapping = new Hashtable(StringComparer.Ordinal);
        for (var i = 0; i < groupNumbers.Length; i++)
        {
            mapping[groupNames[i]] = groupNumbers[i];
        }

        return mapping;
    }

    private bool TryValidateReferences(RegexTree tree, out RegexParseError error, out int errorOffset)
    {
        var validNumbers = new HashSet<int>(_groupNumbers);
        var validNames = new HashSet<string>(_groupNames, StringComparer.Ordinal);
        return TryValidateNode(tree.Root, validNumbers, validNames, out error, out errorOffset);
    }

    private static bool TryValidateNode(
        RegexNode node,
        HashSet<int> validNumbers,
        HashSet<string> validNames,
        out RegexParseError error,
        out int errorOffset)
    {
        if (node.Kind is RegexNodeKind.Backreference or RegexNodeKind.BackreferenceConditional)
        {
            if (node.Str is { Length: > 0 } referenceName)
            {
                if (!validNames.Contains(referenceName))
                {
                    error = RegexParseError.UndefinedNamedReference;
                    errorOffset = node.Position;
                    return false;
                }
            }
            else if (node.M > 0 && !validNumbers.Contains(node.M))
            {
                error = RegexParseError.UndefinedNumberedReference;
                errorOffset = node.Position;
                return false;
            }
        }
        else if (node.Kind == RegexNodeKind.Capture && (node.N != -1 || node.Str2 is { Length: > 0 }))
        {
            if (node.Str2 is { Length: > 0 } balanceReferenceName)
            {
                if (!validNames.Contains(balanceReferenceName))
                {
                    error = RegexParseError.UndefinedNamedReference;
                    errorOffset = node.Position;
                    return false;
                }
            }
            else if (node.N > 0 && !validNumbers.Contains(node.N))
            {
                error = RegexParseError.UndefinedNumberedReference;
                errorOffset = node.Position;
                return false;
            }
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryValidateNode(node.Child(i), validNumbers, validNames, out error, out errorOffset))
            {
                return false;
            }
        }

        error = RegexParseError.InvalidPattern;
        errorOffset = 0;
        return true;
    }

    public static RegexReplacementPattern ParseReplacement(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        return new RegexParser(replacement, RegexOptions.None, CultureInfo.InvariantCulture).ScanReplacementPattern();
    }

    public static RegexReplacementPattern ParseReplacement(
        string replacement,
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var parser = new RegexParser(replacement, RegexOptions.None, CultureInfo.InvariantCulture)
        {
            _replacementGroupNumbers = [.. validGroupNumbers],
            _replacementGroupNames = [.. validGroupNames],
            _resolveReplacementGroups = true,
        };
        return parser.ScanReplacementPattern();
    }

    private static bool TryParseLeadingOptionsGroup(string pattern, ref int index, ref RegexOptions options)
    {
        if (index + 3 >= pattern.Length || pattern[index] != '(' || pattern[index + 1] != '?')
        {
            return false;
        }

        var scan = index + 2;
        var enable = true;
        var changed = false;
        while (scan < pattern.Length)
        {
            var ch = pattern[scan];
            if (ch == '-')
            {
                enable = false;
                scan++;
                continue;
            }

            if (ch == ')' || ch == ':')
            {
                index = scan + 1;
                return changed;
            }

            if (!TryMapInlineOption(ch, out var option))
            {
                return false;
            }

            options = enable ? options | option : options & ~option;
            changed = true;
            scan++;
        }

        return false;
    }

    private static bool TryMapInlineOption(char ch, out RegexOptions option)
    {
        option = ch switch
        {
            'i' => RegexOptions.IgnoreCase,
            'm' => RegexOptions.Multiline,
            'n' => RegexOptions.ExplicitCapture,
            's' => RegexOptions.Singleline,
            'x' => RegexOptions.IgnorePatternWhitespace,
            _ => RegexOptions.None,
        };

        return option != RegexOptions.None;
    }

    public static string NormalizeLeadingGlobalOptions(string pattern, ref RegexOptions options)
    {
        var index = 0;
        var consumed = 0;
        while (index + 3 < pattern.Length &&
               pattern[index] == '(' &&
               pattern[index + 1] == '?' &&
               TryConsumeLeadingGlobalOptionsGroup(pattern, ref index, ref options))
        {
            consumed = index;
        }

        return consumed == 0 ? pattern : pattern[consumed..];
    }

    private static bool TryConsumeLeadingGlobalOptionsGroup(string pattern, ref int index, ref RegexOptions options)
    {
        var scan = index + 2;
        var enable = true;
        var changed = false;
        while (scan < pattern.Length)
        {
            var ch = pattern[scan];
            if (ch == '-')
            {
                enable = false;
                scan++;
                continue;
            }

            if (ch == ')')
            {
                index = scan + 1;
                return changed;
            }

            if (ch == ':')
            {
                return false;
            }

            if (!TryMapInlineOption(ch, out var option))
            {
                return false;
            }

            options = enable ? options | option : options & ~option;
            changed = true;
            scan++;
        }

        return false;
    }

    private RegexReplacementPattern ScanReplacementPattern()
    {
        _pos = 0;
        _replacementTokens = [];
        _replacementLiteral = new StringBuilder();

        while (_pos < _pattern.Length)
        {
            var start = _pos;
            _pos = _pattern.IndexOf('$', _pos);
            if (_pos < 0)
            {
                _pos = _pattern.Length;
            }

            AppendReplacementLiteral(start, _pos - start);
            if (_pos < _pattern.Length)
            {
                var result = ScanDollar();
                if (result.LiteralText is { Length: > 0 } literalText)
                {
                    _replacementLiteral!.Append(literalText);
                }
                else if (result.LiteralizeOriginalText)
                {
                    var literalStart = result.LiteralStart >= 0 ? result.LiteralStart : _pos;
                    AppendReplacementLiteral(literalStart, result.NextPosition - literalStart);
                }
                else if (result.Token is RegexReplacementToken token)
                {
                    FlushReplacementLiteral();
                    _replacementTokens!.Add(token);
                }

                _pos = result.NextPosition;
            }
        }

        FlushReplacementLiteral();
        return new RegexReplacementPattern([.. _replacementTokens]);
    }

    private ReplacementScanResult ScanDollar()
    {
        var tokenStart = _pos;
        if (++_pos >= _pattern.Length)
        {
            return new ReplacementScanResult(
                Success: true,
                NextPosition: tokenStart + 1,
                LiteralText: "$");
        }

        if (_pattern[_pos] == '$')
        {
            return new ReplacementScanResult(
                Success: true,
                NextPosition: tokenStart + 2,
                LiteralText: "$");
        }

        var result = TryScanReplacementToken(tokenStart);
        if (result.Success)
        {
            return result;
        }

        return new ReplacementScanResult(
            Success: true,
            NextPosition: _pos,
            LiteralText: "$");
    }

    private ReplacementScanResult TryScanReplacementToken(int tokenStart)
    {
        if (TryParseSpecialReplacementToken(_pattern[_pos], out var specialKind))
        {
            return new ReplacementScanResult(
                Success: true,
                NextPosition: tokenStart + 2,
                Token: new RegexReplacementToken(specialKind));
        }

        var numeric = TryScanNumericReplacement(tokenStart);
        if (numeric.Success)
        {
            return numeric;
        }

        return TryScanNamedReplacement(tokenStart);
    }

    private ReplacementScanResult TryScanNumericReplacement(int tokenStart)
    {
        var scan = _pos;
        var isBraced = TryScanReplacementBracePrefix(ref scan);
        if (scan >= _pattern.Length || !char.IsAsciiDigit(_pattern[scan]))
        {
            return default;
        }

        var digitStart = scan;
        while (scan < _pattern.Length && char.IsAsciiDigit(_pattern[scan]))
        {
            scan++;
        }

        if (!int.TryParse(_pattern.AsSpan(digitStart, scan - digitStart), NumberStyles.None, CultureInfo.InvariantCulture, out var groupNumber))
        {
            return default;
        }

        if (isBraced)
        {
            if (scan >= _pattern.Length || _pattern[scan] != '}')
            {
                return default;
            }

            scan++;
        }

        if (_resolveReplacementGroups && !Contains(_replacementGroupNumbers, groupNumber))
        {
            return new ReplacementScanResult(
                Success: true,
                NextPosition: scan,
                LiteralizeOriginalText: true,
                LiteralStart: tokenStart);
        }

        return new ReplacementScanResult(
            Success: true,
            NextPosition: scan,
            Token: new RegexReplacementToken(RegexReplacementTokenKind.Group, groupNumber: groupNumber, isBraceEnclosed: isBraced));
    }

    private ReplacementScanResult TryScanNamedReplacement(int tokenStart)
    {
        if (_pattern[_pos] != '{')
        {
            return default;
        }

        var nameStart = _pos + 1;
        if (nameStart >= _pattern.Length || !RegexCharClass.IsWordChar(_pattern[nameStart]))
        {
            return default;
        }

        var endBrace = _pattern.IndexOf('}', nameStart);
        if (endBrace <= nameStart)
        {
            return default;
        }

        var groupName = _pattern[nameStart..endBrace];
        if (_resolveReplacementGroups)
        {
            var resolvedGroupNumber = ResolveReplacementGroupName(groupName);
            if (resolvedGroupNumber >= 0)
            {
                return new ReplacementScanResult(
                    Success: true,
                    NextPosition: endBrace + 1,
                    Token: new RegexReplacementToken(RegexReplacementTokenKind.Group, groupNumber: resolvedGroupNumber, isBraceEnclosed: true));
            }

            return new ReplacementScanResult(
                Success: true,
                NextPosition: endBrace + 1,
                LiteralizeOriginalText: true,
                LiteralStart: tokenStart);
        }

        return new ReplacementScanResult(
            Success: true,
            NextPosition: endBrace + 1,
            Token: new RegexReplacementToken(RegexReplacementTokenKind.Group, literal: groupName, isBraceEnclosed: true));
    }

    private bool TryScanReplacementBracePrefix(ref int scan)
    {
        if (_pattern[scan] != '{')
        {
            return false;
        }

        if (scan + 1 >= _pattern.Length)
        {
            return false;
        }

        scan++;
        return true;
    }

    private void AppendReplacementLiteral(int start, int length)
    {
        if (length > 0)
        {
            _replacementLiteral!.Append(_pattern, start, length);
        }
    }

    private static bool TryParseSpecialReplacementToken(char marker, out RegexReplacementTokenKind kind)
    {
        kind = marker switch
        {
            '&' => RegexReplacementTokenKind.WholeMatch,
            '`' => RegexReplacementTokenKind.LeftPortion,
            '\'' => RegexReplacementTokenKind.RightPortion,
            '+' => RegexReplacementTokenKind.LastGroup,
            '_' => RegexReplacementTokenKind.WholeString,
            _ => default,
        };

        return marker is '&' or '`' or '\'' or '+' or '_';
    }

    private void FlushReplacementLiteral()
    {
        if (_replacementLiteral is null || _replacementLiteral.Length == 0)
        {
            return;
        }

        _replacementTokens!.Add(new RegexReplacementToken(RegexReplacementTokenKind.Literal, _replacementLiteral.ToString()));
        _replacementLiteral.Clear();
    }

    private int ResolveReplacementGroupName(string groupName)
    {
        for (var i = 0; i < _replacementGroupNames.Length && i < _replacementGroupNumbers.Length; i++)
        {
            if (string.Equals(_replacementGroupNames[i], groupName, StringComparison.Ordinal))
            {
                return _replacementGroupNumbers[i];
            }
        }

        return -1;
    }

    private static bool Contains(ReadOnlySpan<int> values, int value)
    {
        foreach (var item in values)
        {
            if (item == value)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class CaptureMetadataScanner
    {
        private readonly string _pattern;
        private readonly Dictionary<int, int> _slots = new();
        private readonly Dictionary<string, int> _captureNames = new(StringComparer.Ordinal);
        private readonly List<string> _captureNameList = [];
        private readonly Stack<RegexOptions> _optionsStack = new();
        private RegexOptions _options;
        private int _autocap = 1;
        private int _pos;
        private bool _ignoreNextParen;

        private CaptureMetadataScanner(string pattern, RegexOptions options)
        {
            _pattern = pattern;
            _options = options;
        }

        private readonly List<int> _implicitCaptureNumbers = [];

        public static (int[] GroupNumbers, string[] GroupNames, int[] ImplicitCaptureNumbers) Analyze(string pattern, RegexOptions options, CultureInfo culture)
        {
            _ = culture;
            return new CaptureMetadataScanner(pattern, options).Scan();
        }

        private (int[] GroupNumbers, string[] GroupNames, int[] ImplicitCaptureNumbers) Scan()
        {
            NoteCaptureSlot(0, 0);

            while (_pos < _pattern.Length)
            {
                var pos = _pos;
                var ch = _pattern[_pos++];
                switch (ch)
                {
                    case '\\':
                        SkipEscape();
                        break;

                    case '#':
                        if ((_options & RegexOptions.IgnorePatternWhitespace) != 0)
                        {
                            SkipLineComment();
                        }
                        break;

                    case '[':
                        SkipCharClass();
                        break;

                    case ')':
                        if (_optionsStack.Count != 0)
                        {
                            _options = _optionsStack.Pop();
                        }
                        break;

                    case '(':
                        if (_pos + 1 < _pattern.Length && _pattern[_pos] == '?' && _pattern[_pos + 1] == '#')
                        {
                            SkipInlineComment();
                            break;
                        }

                        _optionsStack.Push(_options);
                        if (_pos < _pattern.Length && _pattern[_pos] == '?')
                        {
                            _pos++;

                            if (_pos + 1 < _pattern.Length && (_pattern[_pos] == '<' || _pattern[_pos] == '\''))
                            {
                                var delimiter = _pattern[_pos] == '<' ? '>' : '\'';
                                _pos++;
                                if (TryScanCaptureSpecifier(delimiter, out var captureNumber, out var captureName))
                                {
                                    if (captureNumber > 0)
                                    {
                                        NoteCaptureSlot(captureNumber, pos);
                                    }
                                    else if (captureName is not null)
                                    {
                                        NoteCaptureName(captureName, pos);
                                    }
                                }
                            }
                            else
                            {
                                ScanOptions();

                                if (_pos < _pattern.Length)
                                {
                                    if (_pattern[_pos] == ')')
                                    {
                                        _pos++;
                                        _optionsStack.Pop();
                                    }
                                    else if (_pattern[_pos] == '(')
                                    {
                                        _ignoreNextParen = true;
                                        break;
                                    }
                                }
                            }
                        }
                        else if ((_options & RegexOptions.ExplicitCapture) == 0 && !_ignoreNextParen)
                        {
                            NoteCaptureSlot(_autocap, pos);
                            _implicitCaptureNumbers.Add(_autocap);
                            _autocap++;
                        }

                        _ignoreNextParen = false;
                        break;
                }
            }

            AssignNameSlots();

            var groupNumbers = _slots.Keys.OrderBy(static n => n).ToArray();
            var groupNames = new string[groupNumbers.Length];
            for (var i = 0; i < groupNumbers.Length; i++)
            {
                var number = groupNumbers[i];
                var name = number.ToString(CultureInfo.InvariantCulture);
                groupNames[i] = _captureNames.FirstOrDefault(pair => pair.Value == number).Key ?? name;
            }

            return (groupNumbers, groupNames, [.. _implicitCaptureNumbers]);
        }

        private void SkipEscape()
        {
            if (_pos >= _pattern.Length)
            {
                return;
            }

            var ch = _pattern[_pos++];
            switch (ch)
            {
                case 'x':
                    _pos = Math.Min(_pos + 2, _pattern.Length);
                    break;
                case 'u':
                    _pos = Math.Min(_pos + 4, _pattern.Length);
                    break;
                case 'c':
                    _pos = Math.Min(_pos + 1, _pattern.Length);
                    break;
                case 'p':
                case 'P':
                    if (_pos < _pattern.Length && _pattern[_pos] == '{')
                    {
                        _pos++;
                        while (_pos < _pattern.Length && _pattern[_pos] != '}')
                        {
                            _pos++;
                        }

                        if (_pos < _pattern.Length)
                        {
                            _pos++;
                        }
                    }
                    break;
                case 'k':
                    if (_pos < _pattern.Length && _pattern[_pos] is '<' or '\'')
                    {
                        var delimiter = _pattern[_pos++] == '<' ? '>' : '\'';
                        while (_pos < _pattern.Length && _pattern[_pos] != delimiter)
                        {
                            _pos++;
                        }

                        if (_pos < _pattern.Length)
                        {
                            _pos++;
                        }
                    }
                    break;
                default:
                    while (_pos < _pattern.Length && char.IsAsciiDigit(_pattern[_pos]))
                    {
                        _pos++;
                    }
                    break;
            }
        }

        private void SkipCharClass()
        {
            var escaped = false;
            while (_pos < _pattern.Length)
            {
                var ch = _pattern[_pos++];
                if (escaped)
                {
                    escaped = false;
                    if (ch == 'p' || ch == 'P')
                    {
                        if (_pos < _pattern.Length && _pattern[_pos] == '{')
                        {
                            _pos++;
                            while (_pos < _pattern.Length && _pattern[_pos] != '}')
                            {
                                _pos++;
                            }

                            if (_pos < _pattern.Length)
                            {
                                _pos++;
                            }
                        }
                    }

                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == ']')
                {
                    return;
                }
            }
        }

        private void SkipInlineComment()
        {
            _pos += 2;
            while (_pos < _pattern.Length && _pattern[_pos] != ')')
            {
                _pos++;
            }

            if (_pos < _pattern.Length)
            {
                _pos++;
            }
        }

        private void SkipLineComment()
        {
            while (_pos < _pattern.Length && _pattern[_pos] is not '\r' and not '\n')
            {
                _pos++;
            }
        }

        private void ScanOptions()
        {
            var enable = true;
            while (_pos < _pattern.Length)
            {
                var ch = _pattern[_pos];
                if (ch == '-')
                {
                    enable = false;
                    _pos++;
                    continue;
                }

                if (ch is ':' or ')' or '(')
                {
                    return;
                }

                if (!TryMapInlineOption(ch, out var option))
                {
                    return;
                }

                _options = enable ? _options | option : _options & ~option;
                _pos++;
            }
        }

        private bool TryScanCaptureSpecifier(char delimiter, out int captureNumber, out string? captureName)
        {
            captureNumber = 0;
            captureName = null;

            if (_pos >= _pattern.Length)
            {
                return false;
            }

            if (_pattern[_pos] == '-')
            {
                _pos++;
                return TryConsumeCaptureToken(delimiter);
            }

            if (!TryScanCaptureToken('-', delimiter, out var primaryToken))
            {
                return false;
            }

            if (primaryToken is null)
            {
                return false;
            }

            if (char.IsAsciiDigit(primaryToken[0]))
            {
                if (!int.TryParse(primaryToken, NumberStyles.None, CultureInfo.InvariantCulture, out captureNumber))
                {
                    return false;
                }
            }
            else
            {
                captureName = primaryToken;
            }

            if (_pos < _pattern.Length && _pattern[_pos] == '-')
            {
                _pos++;
                return TryConsumeCaptureToken(delimiter) && TryConsumeDelimiter(delimiter);
            }

            return TryConsumeDelimiter(delimiter);
        }

        private bool TryConsumeCaptureToken(char delimiter) => TryScanCaptureToken(delimiter, out _);

        private bool TryScanCaptureToken(char delimiter, out string? token) =>
            TryScanCaptureTokenCore(stopChar: null, delimiter, out token);

        private bool TryScanCaptureToken(char stopChar, char delimiter, out string? token) =>
            TryScanCaptureTokenCore(stopChar, delimiter, out token);

        private bool TryScanCaptureTokenCore(char? stopChar, char delimiter, out string? token)
        {
            token = null;
            var start = _pos;
            while (_pos < _pattern.Length)
            {
                var ch = _pattern[_pos];
                if (ch == delimiter || (stopChar is char stop && ch == stop))
                {
                    break;
                }

                _pos++;
            }

            if (_pos >= _pattern.Length || start == _pos)
            {
                return false;
            }

            token = _pattern[start.._pos];
            return true;
        }

        private bool TryConsumeDelimiter(char delimiter)
        {
            if (_pos >= _pattern.Length || _pattern[_pos] != delimiter)
            {
                return false;
            }

            _pos++;
            return true;
        }

        private void NoteCaptureSlot(int number, int position)
        {
            _slots.TryAdd(number, position);
        }

        private void NoteCaptureName(string name, int position)
        {
            if (!_captureNames.ContainsKey(name))
            {
                _captureNames.Add(name, position);
                _captureNameList.Add(name);
            }
        }

        private void AssignNameSlots()
        {
            foreach (var name in _captureNameList)
            {
                while (_slots.ContainsKey(_autocap))
                {
                    _autocap++;
                }

                _captureNames[name] = _autocap;
                NoteCaptureSlot(_autocap, _captureNames[name]);
                _autocap++;
            }
        }
    }
}
