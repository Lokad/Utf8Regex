namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

using System.Globalization;

internal sealed partial class RegexParser
{
    private const int MaxExpandedAlternationBranches = 64;

    private string _scanPattern = string.Empty;
    private RegexOptions _scanOptions;
    private RegexCaseBehavior _caseBehavior;
    private RegexParseError _scanError;
    private int _pos;
    private int _scanErrorOffset;
    private int _nextImplicitCaptureIndex;
    private RegexOptions _currentOptions;
    private int _openGroupDepth;

    private RegexTree? ParsePatternTree()
    {
        _caseBehavior = default;
        _scanError = RegexParseError.InvalidPattern;
        _pos = 0;
        _scanErrorOffset = -1;
        _nextImplicitCaptureIndex = 0;
        _openGroupDepth = 0;
        _currentOptions = _scanOptions;
        _optionsStack.Clear();
        ResetParserState();

        return ParsePatternTreeCore();
    }

    private RegexTree? ParsePatternTreeCore()
    {
        PushGroup();
        StartGroup(new RegexNode(RegexNodeKind.Capture, _scanOptions & ~RegexOptions.IgnoreCase, 0, -1));

        var body = ScanRegex();
        if (body is null || _pos != _scanPattern.Length)
        {
            PopGroup();
            if (_pos < _scanPattern.Length && _scanPattern[_pos] == ')')
            {
                NoteScanError(RegexParseError.NotEnoughParentheses);
            }
            else
            {
                NoteScanError();
            }

            return null;
        }

        AddUnit(body);
        AddGroup();

        var root = _unit!;
        PopGroup();

        return new RegexTree(
            root,
            captureCount: _captureCount,
            captureNames: _groupNames,
            captureNameToNumberMapping: _captureNameToNumberMapping,
            captureNumberSparseMapping: _captureNumberSparseMapping,
            _scanOptions,
            culture: (_scanOptions & RegexOptions.CultureInvariant) != 0 ? CultureInfo.InvariantCulture : null,
            findOptimizations: new RegexFindOptimizations(root, _scanOptions));
    }

    private RegexNode? ScanRegex() => ScanRegex(stopAtAlternation: false);

    private void ResetParserState()
    {
        _stack = null;
        _group = null;
        _alternation = null;
        _concatenation = null;
        _unit = null;
    }

    private void PushParserState()
    {
        _stack = new ParserStateFrame
        {
            Previous = _stack,
            Group = _group,
            Alternation = _alternation,
            Concatenation = _concatenation,
            Unit = _unit,
        };
    }

    private void PopParserState()
    {
        if (_stack is null)
        {
            _group = null;
            _alternation = null;
            _concatenation = null;
            _unit = null;
            return;
        }

        var frame = _stack;
        _stack = frame.Previous;
        _group = frame.Group;
        _alternation = frame.Alternation;
        _concatenation = frame.Concatenation;
        _unit = frame.Unit;
    }

    private void PushGroup()
    {
        PushParserState();
        _openGroupDepth++;
    }

    private void PopGroup()
    {
        PopParserState();
        _openGroupDepth--;
    }

    private void StartGroup(RegexNode group)
    {
        _group = group;
        _alternation = new RegexNode(RegexNodeKind.Alternate, group.Options);
        _concatenation = new RegexNode(RegexNodeKind.Concatenate, group.Options);
        _unit = null;
    }

    private void AddUnit(RegexNode node)
    {
        _concatenation ??= new RegexNode(RegexNodeKind.Concatenate, node.Options);
        _concatenation.AddChild(node);
        _unit = node;
    }

    private void AddAlternate()
    {
        if (_concatenation is null)
        {
            return;
        }

        if (_group is not null && _group.Kind is RegexNodeKind.ExpressionConditional or RegexNodeKind.BackreferenceConditional)
        {
            _group.AddChild(_concatenation.Reduce());
            _concatenation = new RegexNode(RegexNodeKind.Concatenate, _group.Options);
            _unit = null;
            return;
        }

        _alternation ??= new RegexNode(RegexNodeKind.Alternate, _concatenation.Options);
        _alternation.AddChild(_concatenation.Reduce());
        _concatenation = new RegexNode(RegexNodeKind.Concatenate, _alternation.Options);
        _unit = null;
    }

    private void AddGroup()
    {
        if (_group is null)
        {
            return;
        }

        if (_group.Kind is RegexNodeKind.ExpressionConditional or RegexNodeKind.BackreferenceConditional)
        {
            _group.AddChild(_concatenation?.Reduce() ?? new RegexNode(RegexNodeKind.Empty, _group.Options));
            _unit = _group;
            return;
        }

        AddAlternate();
        var reduced = _alternation is null
            ? new RegexNode(RegexNodeKind.Empty, _group.Options)
            : _alternation.ChildCount == 1
                ? _alternation.Child(0)
                : _alternation.Reduce();
        _unit = _group.AddChild(reduced);
    }

    private void PushOptions(RegexOptions options)
    {
        _optionsStack.Push(_currentOptions);
        _currentOptions = options;
    }

    private void PopOptions()
    {
        _currentOptions = _optionsStack.Pop();
    }

    private RegexNode? ScanRegex(bool stopAtAlternation)
    {
        var baseGroupDepth = _openGroupDepth;
        PushParserState();
        _alternation = new RegexNode(RegexNodeKind.Alternate, _currentOptions);
        _concatenation = new RegexNode(RegexNodeKind.Concatenate, _currentOptions);
        _unit = null;

        while (true)
        {
            SkipPatternWhitespace(_currentOptions);
            if (_pos >= _scanPattern.Length)
            {
                break;
            }

            if (_scanPattern[_pos] == ')')
            {
                if (_openGroupDepth > baseGroupDepth)
                {
                    _pos++;
                    AddGroup();
                    var grouped = _unit!;
                    PopOptions();
                    PopGroup();

                    var closedGroup = ScanQuantifier(grouped);
                    if (closedGroup is null)
                    {
                        PopParserState();
                        return null;
                    }

                    AddUnit(closedGroup);
                    continue;
                }

                break;
            }

            if (_scanPattern[_pos] == '|')
            {
                if (stopAtAlternation)
                {
                    break;
                }

                _pos++;
                AddAlternate();
                continue;
            }

            var start = _pos;
            while (_pos < _scanPattern.Length)
            {
                var ch = _scanPattern[_pos];
                if (IsSpecial(ch) && (ch != '{' || IsTrueQuantifier()))
                {
                    break;
                }

                if ((_currentOptions & RegexOptions.IgnorePatternWhitespace) != 0 &&
                    (char.IsWhiteSpace(ch) || ch == '#'))
                {
                    break;
                }

                _pos++;
            }

            var end = _pos;
            SkipPatternWhitespace(_currentOptions);

            var atom = CreateLiteralRunNode(start, end, _currentOptions);
            if (atom is null)
            {
                if (_pos >= _scanPattern.Length)
                {
                    PopParserState();
                    return null;
                }

                var ch = _scanPattern[_pos++];
                atom = ch switch
                {
                    '*' or '+' or '?' => NoteQuantifierAfterNothing(),
                    '.' => new RegexNode(
                        RegexNodeKind.Set,
                        _currentOptions & ~RegexOptions.IgnoreCase,
                        (_currentOptions & RegexOptions.Singleline) != 0
                            ? RegexCharClass.AnyClass
                            : RegexCharClass.NotNewLineClass),
                    '^' => new RegexNode(
                        (_currentOptions & RegexOptions.Multiline) != 0
                            ? RegexNodeKind.Bol
                            : RegexNodeKind.Beginning,
                        _currentOptions),
                    '$' => new RegexNode(
                        (_currentOptions & RegexOptions.Multiline) != 0
                            ? RegexNodeKind.Eol
                            : RegexNodeKind.EndZ,
                        _currentOptions),
                    '[' => CreateCharClassNode(_currentOptions),
                    '\\' => ScanBackslash(scanOnly: false),
                    '(' => null,
                    _ => null,
                };

                if (ch == '(')
                {
                    var groupResult = ScanGroupOpen();
                    if (!groupResult.Success)
                    {
                        PopParserState();
                        return null;
                    }

                    if (groupResult.ContinueWithoutUnit)
                    {
                        continue;
                    }

                    atom = groupResult.Node;
                }
            }

            if (atom is null)
            {
                PopParserState();
                return null;
            }

            atom = ScanQuantifier(atom);
            if (atom is null)
            {
                PopParserState();
                return null;
            }

            AddUnit(atom);
        }

        if (_openGroupDepth > baseGroupDepth)
        {
            while (_openGroupDepth > baseGroupDepth)
            {
                PopOptions();
                PopGroup();
            }

            PopParserState();
            NoteScanError(RegexParseError.NotEnoughParentheses);
            return null;
        }

        AddAlternate();

        var alternation = _alternation!;
        var result = alternation.ChildCount == 1 ? alternation.Child(0) : alternation.Reduce();
        PopParserState();
        return result;
    }

    private RegexNode? ScanQuantifier(RegexNode atom)
    {
        if (_pos >= _scanPattern.Length)
        {
            return atom;
        }

        if (_scanPattern[_pos] == '?')
        {
            _pos++;
            return ApplyQuantifier(atom, 0, 1, _currentOptions, TryConsume('?'));
        }

        if (_scanPattern[_pos] is '*' or '+')
        {
            var quantifier = _scanPattern[_pos++];
            return CreateLoopNode(
                atom,
                minCount: quantifier == '+' ? 1 : 0,
                maxCount: int.MaxValue,
                lazy: TryConsume('?'),
                _currentOptions);
        }

        if (_scanPattern[_pos] != '{')
        {
            return atom;
        }

        var restorePos = _pos;
        if (!TryScanBoundedQuantifier(out var minCount, out var maxCount))
        {
            _pos = restorePos;
            return atom;
        }

        return ApplyQuantifier(atom, minCount, maxCount, _currentOptions, TryConsume('?'));
    }

    private readonly record struct GroupConstructScanResult(
        bool Success,
        bool IsNonCapturing,
        RegexOptions GroupOptions,
        RegexNodeKind GroupKind,
        string? CaptureName = null,
        string? BalanceCaptureName = null,
        RegexNode? GroupNode = null,
        RegexNode? ImmediateNode = null,
        bool ContinueWithoutUnit = false);

    private readonly record struct GroupOpenScanResult(
        bool Success,
        RegexNode? Node = null,
        bool ContinueWithoutUnit = false);

    private GroupOpenScanResult ScanGroupOpen()
    {
        var currentOptions = _currentOptions;
        var construct = ScanGroupConstruct(currentOptions);
        if (!construct.Success)
        {
            return default;
        }

        if (construct.ImmediateNode is not null)
        {
            return new GroupOpenScanResult(
                Success: true,
                Node: construct.ImmediateNode,
                ContinueWithoutUnit: construct.ContinueWithoutUnit);
        }

        if (construct.GroupNode is null && construct.ContinueWithoutUnit)
        {
            return new GroupOpenScanResult(Success: true, ContinueWithoutUnit: true);
        }

        var group = construct.GroupNode ?? BuildGroupNode(
            construct.GroupKind,
            construct.GroupOptions,
            construct.IsNonCapturing,
            construct.CaptureName,
            construct.BalanceCaptureName);
        if (group is null)
        {
            return default;
        }

        PushGroup();
        StartGroup(group);
        PushOptions(construct.GroupOptions);
        return new GroupOpenScanResult(Success: true, ContinueWithoutUnit: true);
    }

    private GroupConstructScanResult ScanGroupConstruct(RegexOptions currentOptions)
    {
        if (_pos >= _scanPattern.Length || _scanPattern[_pos] != '?')
        {
            return new GroupConstructScanResult(
                Success: true,
                IsNonCapturing: (currentOptions & RegexOptions.ExplicitCapture) != 0,
                GroupOptions: currentOptions,
                GroupKind: RegexNodeKind.Group);
        }

        if (_pos + 1 >= _scanPattern.Length)
        {
            return default;
        }

        if (TryScanCommentGroup(currentOptions, out var immediateNode))
        {
            return new GroupConstructScanResult(
                Success: immediateNode is not null,
                IsNonCapturing: true,
                GroupOptions: currentOptions,
                GroupKind: RegexNodeKind.Group,
                ImmediateNode: immediateNode,
                ContinueWithoutUnit: immediateNode is not null);
        }

        var structural = ScanStructuralGroupConstruct(currentOptions);
        if (structural.Success)
        {
            return structural;
        }

        NoteScanError(RegexParseError.InvalidGroupingConstruct);
        return default;
    }

    private bool TryScanCommentGroup(RegexOptions currentOptions, out RegexNode? node)
    {
        node = null;
        if (_scanPattern[_pos + 1] != '#')
        {
            return false;
        }

        _pos += 2;
        while (_pos < _scanPattern.Length && _scanPattern[_pos] != ')')
        {
            _pos++;
        }

        node = TryConsume(')')
            ? new RegexNode(RegexNodeKind.Empty, currentOptions)
            : null;
        return true;
    }

    private GroupConstructScanResult ScanStructuralGroupConstruct(RegexOptions currentOptions)
    {
        var isNonCapturing = (currentOptions & RegexOptions.ExplicitCapture) != 0;
        var groupOptions = currentOptions;
        var groupKind = RegexNodeKind.Group;
        string? captureName = null;
        string? balanceCaptureName = null;

        if (TryScanSimpleGroupHeader(ref isNonCapturing, ref groupKind))
        {
            return new GroupConstructScanResult(true, isNonCapturing, groupOptions, groupKind);
        }

        if (_scanPattern[_pos + 1] == '(')
        {
            var conditionalGroup = ScanConditionalGroup();
            if (conditionalGroup is null)
            {
                return default;
            }

            return new GroupConstructScanResult(
                Success: true,
                IsNonCapturing: true,
                GroupOptions: groupOptions,
                GroupKind: groupKind,
                GroupNode: conditionalGroup,
                ContinueWithoutUnit: true);
        }

        if (_scanPattern[_pos + 1] is '<' or '\'')
        {
            if (!TryScanCaptureSpecifier(out captureName, out balanceCaptureName))
            {
                return default;
            }

            return new GroupConstructScanResult(true, isNonCapturing, groupOptions, groupKind, captureName, balanceCaptureName);
        }

        if (!TryScanScopedOptions(ref groupOptions))
        {
            if (!TryScanStandaloneOptions(ref groupOptions))
            {
                return default;
            }

            _currentOptions = groupOptions;
            return new GroupConstructScanResult(
                Success: true,
                IsNonCapturing: true,
                GroupOptions: groupOptions,
                GroupKind: groupKind,
                ContinueWithoutUnit: true);
        }

        isNonCapturing = true;
        return new GroupConstructScanResult(true, isNonCapturing, groupOptions, groupKind);
    }

    private bool TryScanSimpleGroupHeader(ref bool isNonCapturing, ref RegexNodeKind groupKind)
    {
        if (_scanPattern[_pos + 1] == ':')
        {
            isNonCapturing = true;
            _pos += 2;
            return true;
        }

        if (_scanPattern[_pos + 1] == '>')
        {
            isNonCapturing = true;
            groupKind = RegexNodeKind.Atomic;
            _pos += 2;
            return true;
        }

        if (_scanPattern[_pos + 1] == '=')
        {
            isNonCapturing = true;
            groupKind = RegexNodeKind.PositiveLookaround;
            _pos += 2;
            return true;
        }

        if (_scanPattern[_pos + 1] == '!')
        {
            isNonCapturing = true;
            groupKind = RegexNodeKind.NegativeLookaround;
            _pos += 2;
            return true;
        }

        if (_pos + 2 < _scanPattern.Length && _scanPattern[_pos + 1] == '<' && _scanPattern[_pos + 2] is '=' or '!')
        {
            isNonCapturing = true;
            groupKind = _scanPattern[_pos + 2] == '='
                ? RegexNodeKind.PositiveLookaround
                : RegexNodeKind.NegativeLookaround;
            _pos += 3;
            return true;
        }

        return false;
    }

    private RegexNode? ScanConditionalGroup()
    {
        var currentOptions = _currentOptions;
        var conditionStart = _pos - 1;
        _pos += 2;

        RegexNode conditional;
        if (TryScanConditionalExpression(_currentOptions, out var conditionNode))
        {
            conditional = new RegexNode(RegexNodeKind.ExpressionConditional, currentOptions)
                .AddChild(conditionNode!);
        }
        else if (TryScanConditionalReference(out var captureNumber, out var captureName) &&
                 TryConsume(')'))
        {
            conditional = captureName is null
                ? new RegexNode(RegexNodeKind.BackreferenceConditional, currentOptions, captureNumber, -1)
                : new RegexNode(RegexNodeKind.BackreferenceConditional, currentOptions, captureName)
                {
                    M = ResolveCaptureNumber(captureName),
                };
            conditional.Position = conditionStart;
        }
        else
        {
            return null;
        }

        return conditional;
    }

    private RegexNode? ScanBackslash(bool scanOnly)
    {
        if (_pos >= _scanPattern.Length)
        {
            return null;
        }

        var escapeStart = _pos - 1;
        var escaped = _scanPattern[_pos++];
        if (escaped == '0')
        {
            var octalLiteral = ScanOctalEscape('0');
            return scanOnly ? null : CreateLiteralNode(octalLiteral, _currentOptions);
        }

        if (char.IsAsciiDigit(escaped))
        {
            return ScanNumericBackreference(scanOnly, escapeStart);
        }

        if (escaped == 'k')
        {
            return ScanNamedBackreference(scanOnly, escapeStart);
        }

        if (escaped is 'p' or 'P')
        {
            if (!TryScanCategoryEscape(escaped == 'P', _currentOptions, out var categoryNode))
            {
                return null;
            }

            return scanOnly ? null : categoryNode;
        }

        if (TryScanSpecialBackslashNode(escaped, _currentOptions, out var node))
        {
            return scanOnly ? null : node;
        }

        if (TryScanLiteralEscape(escaped, inCharClass: false, out var literal, out var literalStatus))
        {
            return scanOnly ? null : CreateLiteralNode(literal, _currentOptions);
        }

        if (literalStatus == EscapeLiteralStatus.InvalidHexDigits)
        {
            NoteScanError(RegexParseError.InsufficientOrInvalidHexDigits);
            return null;
        }

        if (literalStatus == EscapeLiteralStatus.InvalidControlEscape)
        {
            return null;
        }

        NoteScanError(RegexParseError.UnrecognizedEscape);
        return null;
    }

    private bool TryScanSpecialBackslashNode(char escaped, RegexOptions currentOptions, out RegexNode? node)
    {
        node = escaped switch
        {
            'A' => new RegexNode(RegexNodeKind.Beginning, currentOptions),
            'b' => new RegexNode(RegexNodeKind.Boundary, currentOptions),
            'B' => new RegexNode(RegexNodeKind.NonBoundary, currentOptions),
            'd' => new RegexNode(RegexNodeKind.Set, currentOptions & ~RegexOptions.IgnoreCase, RegexCharClass.DigitClass),
            'D' => new RegexNode(RegexNodeKind.Set, currentOptions & ~RegexOptions.IgnoreCase, RegexCharClass.NotDigitClass),
            'G' => new RegexNode(RegexNodeKind.Start, currentOptions),
            'w' => new RegexNode(RegexNodeKind.Set, currentOptions & ~RegexOptions.IgnoreCase, RegexCharClass.WordClass),
            'W' => new RegexNode(RegexNodeKind.Set, currentOptions & ~RegexOptions.IgnoreCase, RegexCharClass.NotWordClass),
            's' => new RegexNode(RegexNodeKind.Set, currentOptions & ~RegexOptions.IgnoreCase, RegexCharClass.SpaceClass),
            'S' => new RegexNode(RegexNodeKind.Set, currentOptions & ~RegexOptions.IgnoreCase, RegexCharClass.NotSpaceClass),
            'Z' => new RegexNode(RegexNodeKind.EndZ, currentOptions),
            'z' => new RegexNode(RegexNodeKind.End, currentOptions),
            _ => null,
        };

        return node is not null;
    }

    private RegexNode? CreateCharClassNode(RegexOptions currentOptions)
    {
        var charClass = ScanCharClass(
            caseInsensitive: (currentOptions & RegexOptions.IgnoreCase) != 0,
            scanOnly: false);
        return charClass is null
            ? null
            : new RegexNode(RegexNodeKind.Set, currentOptions & ~RegexOptions.IgnoreCase, charClass.ToStringClass());
    }

    private RegexCharClass? ScanCharClass(bool caseInsensitive, bool scanOnly)
    {
        char current;
        char previous = '\0';
        var inRange = false;
        var firstChar = true;
        var closed = false;
        var startingNewLevel = false;
        List<RegexCharClass?>? parents = null;

        RegexCharClass? charClass = scanOnly ? null : new RegexCharClass();
        if (_pos < _scanPattern.Length && _scanPattern[_pos] == '^')
        {
            _pos++;
            if (!scanOnly)
            {
                charClass!.Negate = true;
            }
        }

        for (; _pos < _scanPattern.Length; firstChar = false)
        {
            if (startingNewLevel)
            {
                startingNewLevel = false;
                firstChar = true;
                if (_pos < _scanPattern.Length && _scanPattern[_pos] == '^')
                {
                    _pos++;
                    if (!scanOnly)
                    {
                        charClass!.Negate = true;
                    }
                }
            }

            var translatedChar = false;
            current = _scanPattern[_pos++];
            if (TryCloseCharClassLevel(
                current,
                firstChar,
                scanOnly,
                caseInsensitive,
                ref charClass,
                ref parents,
                out closed))
            {
                if (_scanErrorOffset >= 0)
                {
                    return null;
                }

                if (closed)
                {
                    break;
                }

                continue;
            }

            if (current == '\\' && _pos < _scanPattern.Length)
            {
                var escapePos = _pos;
                if (TryScanCharClassLiteralEscape(out current))
                {
                    translatedChar = true;
                }
                else
                {
                    _pos = escapePos;
                    if (!TryAppendCharClassEscape(charClass, scanOnly))
                    {
                        NoteScanError(RegexParseError.UnrecognizedEscape);
                        return null;
                    }

                    continue;
                }
            }

            if (TryApplyCharClassRange(
                scanOnly,
                ref charClass,
                ref parents,
                ref previous,
                ref inRange,
                ref startingNewLevel,
                current,
                translatedChar,
                firstChar))
            {
                if (_scanErrorOffset >= 0)
                {
                    return null;
                }

                continue;
            }

            if (ShouldStartCharClassRange())
            {
                previous = current;
                inRange = true;
                _pos++;
                continue;
            }

            if (TryStartCharClassSubtraction(
                scanOnly,
                ref charClass,
                ref parents,
                ref previous,
                ref startingNewLevel,
                current,
                translatedChar,
                firstChar))
            {
                continue;
            }

            if (!scanOnly)
            {
                charClass!.AddChar(current);
            }
        }

        if (!closed)
        {
            NoteScanError(RegexParseError.UnterminatedBracket);
            return null;
        }

        return charClass;
    }

    private bool TryCloseCharClassLevel(
        char current,
        bool firstChar,
        bool scanOnly,
        bool caseInsensitive,
        ref RegexCharClass? charClass,
        ref List<RegexCharClass?>? parents,
        out bool closed)
    {
        closed = false;
        if (current != ']' || firstChar)
        {
            return false;
        }

        if (!scanOnly && caseInsensitive)
        {
            charClass!.AddCaseEquivalences(_culture);
        }

        if (parents is { Count: > 0 })
        {
            var parent = parents[^1];
            parents.RemoveAt(parents.Count - 1);
            if (!scanOnly)
            {
                parent!.AddSubtraction(charClass!);
            }

            if (_pos < _scanPattern.Length && _scanPattern[_pos] != ']')
            {
                NoteScanError(RegexParseError.ExclusionGroupNotLast);
                return true;
            }

            charClass = parent;
            return true;
        }

        closed = true;
        return true;
    }

    private bool TryApplyCharClassRange(
        bool scanOnly,
        ref RegexCharClass? charClass,
        ref List<RegexCharClass?>? parents,
        ref char previous,
        ref bool inRange,
        ref bool startingNewLevel,
        char current,
        bool translatedChar,
        bool firstChar)
    {
        if (!inRange)
        {
            return false;
        }

        inRange = false;
        if (current == '[' && !translatedChar && !firstChar)
        {
            if (!scanOnly)
            {
                charClass!.AddChar(previous);
            }

            (parents ??= []).Add(charClass);
            charClass = scanOnly ? null : new RegexCharClass();
            previous = '\0';
            startingNewLevel = true;
            return true;
        }

        if (current < previous)
        {
            NoteScanError(RegexParseError.ReversedCharacterRange);
            return true;
        }

        if (!scanOnly)
        {
            charClass!.AddRange(previous, current);
        }

        return true;
    }

    private bool ShouldStartCharClassRange() =>
        _pos + 1 < _scanPattern.Length &&
        _scanPattern[_pos] == '-' &&
        _scanPattern[_pos + 1] != ']';

    private bool TryStartCharClassSubtraction(
        bool scanOnly,
        ref RegexCharClass? charClass,
        ref List<RegexCharClass?>? parents,
        ref char previous,
        ref bool startingNewLevel,
        char current,
        bool translatedChar,
        bool firstChar)
    {
        if (_pos >= _scanPattern.Length ||
            current != '-' ||
            translatedChar ||
            _scanPattern[_pos] != '[' ||
            firstChar)
        {
            return false;
        }

        _pos++;
        (parents ??= []).Add(charClass);
        charClass = scanOnly ? null : new RegexCharClass();
        previous = '\0';
        startingNewLevel = true;
        return true;
    }

    private enum EscapeLiteralStatus
    {
        NotRecognized,
        Recognized,
        InvalidHexDigits,
        InvalidControlEscape,
    }

    private bool TryScanLiteralEscape(
        char escaped,
        bool inCharClass,
        out char literal,
        out EscapeLiteralStatus status)
    {
        literal = default;
        status = EscapeLiteralStatus.NotRecognized;

        if (inCharClass)
        {
            if (escaped == '0')
            {
                literal = ScanOctalEscape('0');
                status = EscapeLiteralStatus.Recognized;
                return true;
            }

            if (IsOctalDigit(escaped))
            {
                literal = ScanOctalEscape(escaped);
                status = EscapeLiteralStatus.Recognized;
                return true;
            }
        }

        switch (escaped)
        {
            case 'x':
                if (!TryScanHexEscape(out var hexValue))
                {
                    status = EscapeLiteralStatus.InvalidHexDigits;
                    return false;
                }

                literal = (char)hexValue;
                break;
            case 'u':
                if (!TryScanUnicodeEscape(out var unicodeValue))
                {
                    status = EscapeLiteralStatus.InvalidHexDigits;
                    return false;
                }

                literal = unicodeValue;
                break;
            case 'c':
                if (!TryScanControlEscape(out var controlValue))
                {
                    status = EscapeLiteralStatus.InvalidControlEscape;
                    return false;
                }

                literal = controlValue;
                break;
            case 'b' when inCharClass:
                literal = '\b';
                break;
            case 'B' when inCharClass:
            case 'A' when inCharClass:
            case 'G' when inCharClass:
            case 'Z' when inCharClass:
            case 'z' when inCharClass:
                literal = escaped;
                break;
            case 'a':
                literal = '\a';
                break;
            case 'e':
                literal = '\u001B';
                break;
            case 'n':
                literal = '\n';
                break;
            case 'r':
                literal = '\r';
                break;
            case 't':
                literal = '\t';
                break;
            case 'f':
                literal = '\f';
                break;
            case 'v':
                literal = '\v';
                break;
            case '\\':
            case '-':
            case ']':
            case '[':
                literal = escaped;
                break;
            case '^' when inCharClass:
                literal = escaped;
                break;
            case '.':
            case '$':
            case '^':
            case '{':
            case '(':
            case '|':
            case ')':
            case '*':
            case '+':
            case '?':
                literal = escaped;
                break;
            default:
                if (!char.IsAsciiLetterOrDigit(escaped))
                {
                    literal = escaped;
                    break;
                }

                return false;
        }

        status = EscapeLiteralStatus.Recognized;
        return true;
    }

    private bool TryAppendCharClassEscape(RegexCharClass? charClass, bool scanOnly)
    {
        if (_pos >= _scanPattern.Length)
        {
            return false;
        }

        var escaped = _scanPattern[_pos++];
        if (TryScanLiteralEscape(escaped, inCharClass: true, out var literal, out var literalStatus))
        {
            if (!scanOnly)
            {
                charClass!.AddChar(literal);
            }

            return true;
        }

        if (literalStatus == EscapeLiteralStatus.InvalidHexDigits)
        {
            NoteScanError(RegexParseError.InsufficientOrInvalidHexDigits);
            return false;
        }

        if (literalStatus == EscapeLiteralStatus.InvalidControlEscape)
        {
            return false;
        }

        if (escaped is 'p' or 'P')
        {
            if (!TryScanCategoryName(out var categoryName))
            {
                return false;
            }

            if (!scanOnly)
            {
                charClass!.AddCategoryFromName(categoryName, escaped == 'P', caseInsensitive: false, _scanPattern, _pos);
            }

            return true;
        }

        switch (escaped)
        {
            case 'd':
                if (!scanOnly)
                {
                    charClass!.AddDigit(ecma: false, negate: false, _scanPattern, _pos - 1);
                }

                return true;
            case 'D':
                if (!scanOnly)
                {
                    charClass!.AddDigit(ecma: false, negate: true, _scanPattern, _pos - 1);
                }

                return true;
            case 'w':
                if (!scanOnly)
                {
                    charClass!.AddWord(ecma: false, negate: false);
                }

                return true;
            case 'W':
                if (!scanOnly)
                {
                    charClass!.AddWord(ecma: false, negate: true);
                }

                return true;
            case 's':
                if (!scanOnly)
                {
                    charClass!.AddSpace(ecma: false, negate: false);
                }

                return true;
            case 'S':
                if (!scanOnly)
                {
                    charClass!.AddSpace(ecma: false, negate: true);
                }

                return true;
            default:
                return false;
        }
    }

    private RegexNode? ScanLiteral(char ch, RegexOptions currentOptions)
    {
        if (Utf8RegexSyntax.IsRegexMetaCharacter(ch))
        {
            return null;
        }

        return CreateLiteralNode(ch, currentOptions);
    }

    private RegexNode? CreateLiteralRunNode(int start, int end, RegexOptions currentOptions)
    {
        var length = end - start;
        if (length == 0)
        {
            return null;
        }

        if (_pos < _scanPattern.Length && IsQuantifier(_scanPattern[_pos]) && length > 1)
        {
            end--;
            length--;
            _pos = end;
        }

        if ((currentOptions & RegexOptions.IgnoreCase) == 0)
        {
            return length == 1
                ? new RegexNode(RegexNodeKind.One, currentOptions, _scanPattern[start])
                : new RegexNode(RegexNodeKind.Multi, currentOptions, _scanPattern.Substring(start, length));
        }

        var terms = new List<RegexNode>(length);
        for (var i = start; i < end; i++)
        {
            var term = ScanLiteral(_scanPattern[i], currentOptions);
            if (term is null)
            {
                return null;
            }

            AppendTerm(terms, term);
        }

        return BuildConcatenation(terms, currentOptions);
    }

    private RegexNode? ApplyQuantifier(
        RegexNode atom,
        int minCount,
        int maxCount,
        RegexOptions currentOptions,
        bool lazy)
    {
        if (minCount < 0 || minCount > maxCount)
        {
            return null;
        }

        _ = currentOptions;
        return atom.MakeQuantifier(lazy, minCount, maxCount);
    }

    private RegexNode CreateLoopNode(
        RegexNode atom,
        int minCount,
        int maxCount,
        bool lazy,
        RegexOptions currentOptions)
    {
        return atom.MakeQuantifier(lazy, minCount, maxCount);
    }

    private bool TryScanBoundedQuantifier(out int minCount, out int maxCount)
    {
        minCount = 0;
        maxCount = 0;
        if (!TryConsume('{'))
        {
            return false;
        }

        var minStart = _pos;
        while (_pos < _scanPattern.Length && char.IsAsciiDigit(_scanPattern[_pos]))
        {
            _pos++;
        }

        if (minStart == _pos || !int.TryParse(_scanPattern.AsSpan(minStart, _pos - minStart), NumberStyles.None, CultureInfo.InvariantCulture, out minCount))
        {
            return false;
        }

        maxCount = minCount;
        if (_pos < _scanPattern.Length && _scanPattern[_pos] == ',')
        {
            _pos++;
            var maxStart = _pos;
            while (_pos < _scanPattern.Length && char.IsAsciiDigit(_scanPattern[_pos]))
            {
                _pos++;
            }

            if (maxStart == _pos)
            {
                maxCount = int.MaxValue;
            }
            else if (!int.TryParse(_scanPattern.AsSpan(maxStart, _pos - maxStart), NumberStyles.None, CultureInfo.InvariantCulture, out maxCount))
            {
                return false;
            }
        }

        return TryConsume('}');
    }

    private bool TryScanHexEscape(out byte value)
    {
        value = 0;
        if (_pos + 1 >= _scanPattern.Length)
        {
            return false;
        }

        if (!byte.TryParse(
                _scanPattern.AsSpan(_pos, 2),
                System.Globalization.NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out value))
        {
            return false;
        }

        _pos += 2;
        return true;
    }

    private bool TryScanUnicodeEscape(out char value)
    {
        value = default;
        if (_pos + 3 >= _scanPattern.Length)
        {
            return false;
        }

        if (!ushort.TryParse(
                _scanPattern.AsSpan(_pos, 4),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var codeUnit))
        {
            return false;
        }

        _pos += 4;
        value = (char)codeUnit;
        return true;
    }

    private bool TryScanControlEscape(out char value)
    {
        value = default;
        if (_pos >= _scanPattern.Length)
        {
            return false;
        }

        var control = char.ToUpperInvariant(_scanPattern[_pos++]);
        if (control is < '@' or > '_')
        {
            return false;
        }

        value = (char)(control ^ 0x40);
        return true;
    }

    private char ScanOctalEscape(char firstDigit)
    {
        var value = firstDigit - '0';
        var digits = 1;
        while (digits < 3 && _pos < _scanPattern.Length && IsOctalDigit(_scanPattern[_pos]))
        {
            value = (value * 8) + (_scanPattern[_pos] - '0');
            _pos++;
            digits++;
        }

        return (char)value;
    }

    private static bool IsOctalDigit(char ch) => ch is >= '0' and <= '7';

    private bool TryScanCategoryEscape(bool negate, RegexOptions currentOptions, out RegexNode? node)
    {
        node = null;
        if (!TryScanCategoryName(out var categoryName))
        {
            return false;
        }

        var charClass = new RegexCharClass();
        charClass.AddCategoryFromName(categoryName, negate, caseInsensitive: false, _scanPattern, _pos);
        node = new RegexNode(RegexNodeKind.Set, currentOptions & ~RegexOptions.IgnoreCase, charClass.ToStringClass());
        return true;
    }

    private bool TryScanCategoryName(out string categoryName)
    {
        categoryName = string.Empty;
        if (!TryConsume('{'))
        {
            return false;
        }

        var start = _pos;
        while (_pos < _scanPattern.Length && _scanPattern[_pos] != '}')
        {
            _pos++;
        }

        if (_pos >= _scanPattern.Length || start == _pos)
        {
            return false;
        }

        categoryName = _scanPattern[start.._pos];
        _pos++;
        return true;
    }

    private bool TryConsume(char ch)
    {
        if (_pos < _scanPattern.Length && _scanPattern[_pos] == ch)
        {
            _pos++;
            return true;
        }

        return false;
    }

    private void SkipPatternWhitespace(RegexOptions currentOptions)
    {
        if ((currentOptions & RegexOptions.IgnorePatternWhitespace) == 0)
        {
            return;
        }

        while (_pos < _scanPattern.Length)
        {
            var ch = _scanPattern[_pos];
            if (char.IsWhiteSpace(ch))
            {
                _pos++;
                continue;
            }

            if (ch == '#')
            {
                _pos++;
                while (_pos < _scanPattern.Length && _scanPattern[_pos] is not '\r' and not '\n')
                {
                    _pos++;
                }

                continue;
            }

            break;
        }
    }

    private bool TryScanCaptureSpecifier(out string? captureName, out string? balanceCaptureName)
    {
        captureName = null;
        balanceCaptureName = null;
        var delimiter = _scanPattern[_pos + 1] == '<' ? '>' : '\'';
        _pos += 2;
        if (_pos >= _scanPattern.Length)
        {
            return false;
        }

        if (_scanPattern[_pos] == '-')
        {
            _pos++;
            if (!TryScanCaptureToken(delimiter, out balanceCaptureName))
            {
                return false;
            }

            return TryConsume(delimiter);
        }

        if (!TryScanCaptureToken('-', delimiter, out captureName))
        {
            return false;
        }

        if (_pos < _scanPattern.Length && _scanPattern[_pos] == '-')
        {
            _pos++;
            if (!TryScanCaptureToken(delimiter, out balanceCaptureName))
            {
                return false;
            }
        }

        if (_pos >= _scanPattern.Length || _scanPattern[_pos] != delimiter)
        {
            return false;
        }

        _pos++;
        return true;
    }

    private bool TryScanCaptureToken(char delimiter, out string? token)
    {
        return TryScanCaptureTokenCore(stopChar: null, delimiter, out token);
    }

    private bool TryScanCaptureToken(char stopChar, char delimiter, out string? token)
    {
        return TryScanCaptureTokenCore(stopChar, delimiter, out token);
    }

    private bool TryScanCaptureTokenCore(char? stopChar, char delimiter, out string? token)
    {
        token = null;
        var start = _pos;
        while (_pos < _scanPattern.Length)
        {
            var ch = _scanPattern[_pos];
            if (ch == delimiter || (stopChar is char stop && ch == stop))
            {
                break;
            }

            _pos++;
        }

        if (_pos >= _scanPattern.Length || start == _pos)
        {
            return false;
        }

        token = _scanPattern[start.._pos];
        return true;
    }

    private bool TryScanConditionalReference(out int captureNumber, out string? captureName)
    {
        captureNumber = -1;
        captureName = null;

        if (_pos >= _scanPattern.Length)
        {
            return false;
        }

        if (_scanPattern[_pos] is '<' or '\'')
        {
            var delimiter = _scanPattern[_pos] == '<' ? '>' : '\'';
            _pos++;
            var start = _pos;
            while (_pos < _scanPattern.Length && _scanPattern[_pos] != delimiter)
            {
                _pos++;
            }

            if (_pos >= _scanPattern.Length || start == _pos)
            {
                return false;
            }

            captureName = _scanPattern[start.._pos];
            _pos++;
            return true;
        }

        var tokenStart = _pos;
        while (_pos < _scanPattern.Length && _scanPattern[_pos] != ')')
        {
            _pos++;
        }

        if (_pos >= _scanPattern.Length || tokenStart == _pos)
        {
            return false;
        }

        var token = _scanPattern[tokenStart.._pos];
        if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out captureNumber))
        {
            return true;
        }

        captureName = token;
        return true;
    }

    private bool TryScanConditionalExpression(RegexOptions currentOptions, out RegexNode? conditionNode)
    {
        conditionNode = null;
        if (_pos + 1 >= _scanPattern.Length || _scanPattern[_pos] != '?')
        {
            return false;
        }

        RegexNodeKind groupKind;
        if (_scanPattern[_pos + 1] == '=')
        {
            groupKind = RegexNodeKind.PositiveLookaround;
            _pos += 2;
        }
        else if (_scanPattern[_pos + 1] == '!')
        {
            groupKind = RegexNodeKind.NegativeLookaround;
            _pos += 2;
        }
        else if (_pos + 2 < _scanPattern.Length && _scanPattern[_pos + 1] == '<' && _scanPattern[_pos + 2] is '=' or '!')
        {
            groupKind = _scanPattern[_pos + 2] == '='
                ? RegexNodeKind.PositiveLookaround
                : RegexNodeKind.NegativeLookaround;
            _pos += 3;
        }
        else
        {
            return false;
        }

        PushOptions(currentOptions);
        var body = ScanRegex(stopAtAlternation: false);
        PopOptions();
        if (body is null)
        {
            return false;
        }

        if (!TryConsume(')'))
        {
            NoteScanError(RegexParseError.NotEnoughParentheses);
            return false;
        }

        conditionNode = new RegexNode(groupKind, currentOptions).AddChild(body);
        return true;
    }

    private RegexNode? ScanNumericBackreference(bool scanOnly, int escapeStart)
    {
        var start = _pos - 1;
        var scan = _pos;
        while (scan < _scanPattern.Length && char.IsAsciiDigit(_scanPattern[scan]))
        {
            scan++;
        }

        if (!int.TryParse(_scanPattern.AsSpan(start, scan - start), NumberStyles.None, CultureInfo.InvariantCulture, out var captureNumber))
        {
            return null;
        }

        if (IsCaptureDefined(captureNumber))
        {
            _pos = scan;
            return scanOnly
                ? null
                : new RegexNode(RegexNodeKind.Backreference, _currentOptions, captureNumber, -1)
            {
                Position = escapeStart,
            };
        }

        if (scan - start > 1 && IsOctalDigit(_scanPattern[start]))
        {
            _pos = start + 1;
            var literal = ScanOctalEscape(_scanPattern[start]);
            return scanOnly ? null : CreateLiteralNode(literal, _currentOptions);
        }

        _pos = scan;
        return scanOnly
            ? null
            : new RegexNode(RegexNodeKind.Backreference, _currentOptions, captureNumber, -1)
        {
            Position = escapeStart,
        };
    }

    private RegexNode? ScanNamedBackreference(bool scanOnly, int escapeStart)
    {
        if (_pos >= _scanPattern.Length || _scanPattern[_pos] is not '<' and not '\'')
        {
            return null;
        }

        var delimiter = _scanPattern[_pos] == '<' ? '>' : '\'';
        _pos++;
        var nameStart = _pos;
        while (_pos < _scanPattern.Length && _scanPattern[_pos] != delimiter)
        {
            _pos++;
        }

        if (_pos >= _scanPattern.Length || nameStart == _pos)
        {
            return null;
        }

        var name = _scanPattern[nameStart.._pos];
        _pos++;
        return scanOnly
            ? null
            : new RegexNode(RegexNodeKind.Backreference, _currentOptions, name)
        {
            M = ResolveCaptureNumber(name),
            Position = escapeStart,
        };
    }

    private bool TryScanScopedOptions(ref RegexOptions groupOptions)
    {
        var scan = _pos + 1;
        var enable = true;
        var changed = false;
        while (scan < _scanPattern.Length)
        {
            var ch = _scanPattern[scan];
            if (ch == '-')
            {
                enable = false;
                scan++;
                continue;
            }

            if (ch == ':')
            {
                if (!changed)
                {
                    return false;
                }

                _pos = scan + 1;
                return true;
            }

            if (!TryMapInlineOption(ch, out var option))
            {
                return false;
            }

            groupOptions = enable ? groupOptions | option : groupOptions & ~option;
            changed = true;
            scan++;
        }

        return false;
    }

    private bool TryScanStandaloneOptions(ref RegexOptions groupOptions)
    {
        var scan = _pos + 1;
        var enable = true;
        var changed = false;
        while (scan < _scanPattern.Length)
        {
            var ch = _scanPattern[scan];
            if (ch == '-')
            {
                enable = false;
                scan++;
                continue;
            }

            if (ch == ')')
            {
                if (!changed)
                {
                    return false;
                }

                _pos = scan + 1;
                return true;
            }

            if (!TryMapInlineOption(ch, out var option))
            {
                return false;
            }

            groupOptions = enable ? groupOptions | option : groupOptions & ~option;
            changed = true;
            scan++;
        }

        return false;
    }

    private static RegexNode BuildConcatenation(List<RegexNode> terms, RegexOptions currentOptions)
    {
        if (terms.Count == 0)
        {
            return new RegexNode(RegexNodeKind.Empty, currentOptions);
        }

        if (terms.Count == 1)
        {
            return terms[0];
        }

        var concatenate = new RegexNode(RegexNodeKind.Concatenate, currentOptions);
        foreach (var term in terms)
        {
            concatenate.AddChild(term);
        }

        return concatenate.Reduce();
    }

    private static void AppendTerm(List<RegexNode> terms, RegexNode term)
    {
        if (term.Kind == RegexNodeKind.Empty)
        {
            return;
        }

        terms.Add(term);
    }

    private RegexNode CreateLiteralNode(char ch, RegexOptions currentOptions)
    {
        var culture = (currentOptions & RegexOptions.CultureInvariant) != 0
            ? CultureInfo.InvariantCulture
            : null;

        return RegexNode.CreateOneWithCaseConversion(ch, currentOptions, culture, ref _caseBehavior);
    }

    private RegexNode? BuildGroupNode(
        RegexNodeKind groupKind,
        RegexOptions groupOptions,
        bool isNonCapturing,
        string? captureName,
        string? balanceCaptureName)
    {
        if (isNonCapturing)
        {
            return new RegexNode(groupKind, groupOptions);
        }

        var captureNumber = -1;
        if (captureName is not null)
        {
            captureNumber = ResolveCaptureNumber(captureName);
        }
        else if (balanceCaptureName is null)
        {
            captureNumber = TakeNextImplicitCaptureNumber();
            if (captureNumber < 0)
            {
                return null;
            }
        }

        var balanceCaptureNumber = -1;
        if (balanceCaptureName is not null)
        {
            balanceCaptureNumber = ResolveCaptureNumber(balanceCaptureName);
            if (balanceCaptureNumber >= 0)
            {
                balanceCaptureName = null;
            }
        }

        return new RegexNode(RegexNodeKind.Capture, groupOptions, captureNumber, balanceCaptureNumber)
        {
            Str = captureName,
            Str2 = balanceCaptureName,
        };
    }

    private int ResolveCaptureNumber(string captureName)
    {
        if (_captureNameToNumberMapping?[captureName] is int mappedNumber)
        {
            return mappedNumber;
        }

        return int.TryParse(captureName, NumberStyles.None, CultureInfo.InvariantCulture, out var explicitNumber)
            ? explicitNumber
            : -1;
    }

    private bool IsCaptureDefined(int captureNumber) => Array.BinarySearch(_groupNumbers, captureNumber) >= 0;

    private int TakeNextImplicitCaptureNumber()
    {
        return (uint)_nextImplicitCaptureIndex < (uint)_implicitCaptureNumbers.Length
            ? _implicitCaptureNumbers[_nextImplicitCaptureIndex++]
            : -1;
    }

    private RegexNode? NoteQuantifierAfterNothing()
    {
        NoteScanError(RegexParseError.QuantifierAfterNothing, _pos - 1);
        return null;
    }

    private bool IsTrueQuantifier()
    {
        if (_pos >= _scanPattern.Length || _scanPattern[_pos] != '{')
        {
            return false;
        }

        var scan = _pos + 1;
        if (scan >= _scanPattern.Length || !char.IsAsciiDigit(_scanPattern[scan]))
        {
            return false;
        }

        while (scan < _scanPattern.Length && char.IsAsciiDigit(_scanPattern[scan]))
        {
            scan++;
        }

        if (scan >= _scanPattern.Length)
        {
            return false;
        }

        if (_scanPattern[scan] == '}')
        {
            return true;
        }

        if (_scanPattern[scan] != ',')
        {
            return false;
        }

        scan++;
        while (scan < _scanPattern.Length && char.IsAsciiDigit(_scanPattern[scan]))
        {
            scan++;
        }

        return scan < _scanPattern.Length && _scanPattern[scan] == '}';
    }

    private static bool IsSpecial(char ch)
    {
        return ch is '[' or ']' or '(' or ')' or '{' or '}' or '*' or '+' or '?' or '|' or '^' or '$' or '.' or '\\';
    }

    private static bool IsQuantifier(char ch)
    {
        return ch is '*' or '+' or '?' or '{';
    }

    private void NoteScanError()
    {
        NoteScanError(RegexParseError.InvalidPattern, _pos);
    }

    private void NoteScanError(RegexParseError error)
    {
        NoteScanError(error, _pos);
    }

    private void NoteScanError(RegexParseError error, int offset)
    {
        if (_scanErrorOffset < 0)
        {
            _scanError = error;
            _scanErrorOffset = Math.Min(offset, _scanPattern.Length);
        }
    }

    private bool TryScanCharClassLiteralEscape(out char ch)
    {
        ch = default;

        if (_pos >= _scanPattern.Length)
        {
            return false;
        }

        var escaped = _scanPattern[_pos++];
        switch (escaped)
        {
            case 'b':
                ch = '\b';
                return true;
            case 'B':
                ch = 'B';
                return true;
            case 'A':
            case 'G':
            case 'Z':
            case 'z':
                ch = escaped;
                return true;
            case '0':
                ch = ScanOctalEscape('0');
                return true;
            case 'x':
                if (!TryScanHexEscape(out var hexValue))
                {
                    NoteScanError(RegexParseError.InsufficientOrInvalidHexDigits);
                    return false;
                }

                ch = (char)hexValue;
                return true;
            case 'u':
                if (!TryScanUnicodeEscape(out var unicodeValue))
                {
                    NoteScanError(RegexParseError.InsufficientOrInvalidHexDigits);
                    return false;
                }

                ch = unicodeValue;
                return true;
            case 'c':
                if (!TryScanControlEscape(out var controlValue))
                {
                    return false;
                }

                ch = controlValue;
                return true;
            case 'a':
                ch = '\a';
                return true;
            case 'e':
                ch = '\u001B';
                return true;
            case 'n':
                ch = '\n';
                return true;
            case 'r':
                ch = '\r';
                return true;
            case 't':
                ch = '\t';
                return true;
            case 'f':
                ch = '\f';
                return true;
            case 'v':
                ch = '\v';
                return true;
            case '\\':
            case '-':
            case ']':
            case '[':
            case '^':
                ch = escaped;
                return true;
            default:
                if (IsOctalDigit(escaped))
                {
                    ch = ScanOctalEscape(escaped);
                    return true;
                }

                return false;
        }
    }

    private bool TryScanCharClassRangeEnd(out char ch)
    {
        ch = default;
        if (_pos >= _scanPattern.Length)
        {
            return false;
        }

        if (_scanPattern[_pos] != '\\')
        {
            ch = _scanPattern[_pos++];
            return true;
        }

        _pos++;
        return TryScanCharClassLiteralEscape(out ch);
    }
}
