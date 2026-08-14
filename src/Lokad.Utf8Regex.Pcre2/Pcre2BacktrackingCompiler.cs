using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal interface IPcre2BacktrackingCompileOutcome
{
}

internal sealed class Pcre2NotBacktrackingOutcome : IPcre2BacktrackingCompileOutcome
{
    internal static Pcre2NotBacktrackingOutcome Instance { get; } = new();

    private Pcre2NotBacktrackingOutcome()
    {
    }
}

internal sealed class Pcre2CompiledBacktrackingOutcome : IPcre2BacktrackingCompileOutcome
{
    internal Pcre2CompiledBacktrackingOutcome(
        Pcre2BacktrackingSyntaxTree syntaxTree,
        Pcre2BacktrackingProgram program)
    {
        SyntaxTree = syntaxTree;
        Program = program;
    }

    internal Pcre2BacktrackingSyntaxTree SyntaxTree { get; }

    internal Pcre2BacktrackingProgram Program { get; }
}

internal sealed class Pcre2BacktrackingSyntaxTree : IPcre2SyntaxTree
{
    internal Pcre2BacktrackingSyntaxTree(IPcre2BacktrackingNode root)
    {
        Root = root;
    }

    public Pcre2SyntaxNodeKind RootKind => Pcre2SyntaxNodeKind.BacktrackingProgram;

    internal IPcre2BacktrackingNode Root { get; }
}

internal interface IPcre2BacktrackingNode
{
    Pcre2BacktrackingNodeKind Kind { get; }
}

internal enum Pcre2BacktrackingNodeKind : byte
{
    Empty = 0,
    Token = 1,
    Sequence = 2,
    Alternation = 3,
    Repeat = 4,
    Capture = 5,
    Backreference = 6,
    Assertion = 7,
}

internal sealed class Pcre2EmptyBacktrackingNode : IPcre2BacktrackingNode
{
    internal static Pcre2EmptyBacktrackingNode Instance { get; } = new();

    private Pcre2EmptyBacktrackingNode()
    {
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Empty;
}

internal sealed class Pcre2TokenBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2TokenBacktrackingNode(Pcre2CharacterToken token)
    {
        Token = token;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Token;

    internal Pcre2CharacterToken Token { get; }
}

internal sealed class Pcre2SequenceBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2SequenceBacktrackingNode(IPcre2BacktrackingNode[] children)
    {
        Children = children;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Sequence;

    internal IPcre2BacktrackingNode[] Children { get; }
}

internal sealed class Pcre2AlternationBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2AlternationBacktrackingNode(IPcre2BacktrackingNode[] alternatives)
    {
        Alternatives = alternatives;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Alternation;

    internal IPcre2BacktrackingNode[] Alternatives { get; }
}

internal sealed class Pcre2RepeatBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2RepeatBacktrackingNode(
        IPcre2BacktrackingNode body,
        int minimum,
        int maximum,
        Pcre2RepeatPreference preference)
    {
        Body = body;
        Minimum = minimum;
        Maximum = maximum;
        Preference = preference;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Repeat;

    internal IPcre2BacktrackingNode Body { get; }

    internal int Minimum { get; }

    internal int Maximum { get; }

    internal Pcre2RepeatPreference Preference { get; }
}

internal sealed class Pcre2CaptureBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2CaptureBacktrackingNode(int slot, string name, IPcre2BacktrackingNode body)
    {
        Slot = slot;
        Name = name;
        Body = body;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Capture;

    internal int Slot { get; }

    internal string Name { get; }

    internal IPcre2BacktrackingNode Body { get; }
}

internal sealed class Pcre2BackreferenceBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2BackreferenceBacktrackingNode(
        Pcre2BackreferenceTarget target,
        Pcre2CharacterOptions options)
    {
        Target = target;
        Options = options;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Backreference;

    internal Pcre2BackreferenceTarget Target { get; }

    internal Pcre2CharacterOptions Options { get; }
}

internal enum Pcre2BackreferenceTargetKind : byte
{
    Absolute = 0,
    Relative = 1,
    Named = 2,
}

internal readonly record struct Pcre2BackreferenceTarget(
    Pcre2BackreferenceTargetKind Kind,
    int Number,
    string Name,
    int CaptureCountAtReference);

internal enum Pcre2AssertionKind : byte
{
    PositiveLookahead = 0,
    NegativeLookahead = 1,
    PositiveLookbehind = 2,
    NegativeLookbehind = 3,
}

internal sealed class Pcre2AssertionBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2AssertionBacktrackingNode(Pcre2AssertionKind assertionKind, IPcre2BacktrackingNode body)
    {
        AssertionKind = assertionKind;
        Body = body;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Assertion;

    internal Pcre2AssertionKind AssertionKind { get; }

    internal IPcre2BacktrackingNode Body { get; }
}

internal enum Pcre2RepeatPreference : byte
{
    Greedy = 0,
    Lazy = 1,
}

internal sealed class Pcre2BacktrackingProgram
{
    internal Pcre2BacktrackingProgram(
        Pcre2BacktrackingInstruction[] instructions,
        Pcre2CompileRequest request,
        int repeatCount,
        int captureSlotCount,
        bool hasBackreferences,
        bool hasCaptureWrites,
        Pcre2BacktrackingProgram[] assertionPrograms,
        string[] groupNames,
        Pcre2NameEntry[] nameEntries,
        int minimumByteLength,
        int minimumScalarLength,
        int maximumScalarLength,
        byte? leadingAsciiByte)
    {
        Instructions = instructions;
        Request = request;
        RepeatCount = repeatCount;
        CaptureSlotCount = captureSlotCount;
        HasBackreferences = hasBackreferences;
        HasCaptureWrites = hasCaptureWrites;
        AssertionPrograms = assertionPrograms;
        GroupNames = groupNames;
        NameEntries = nameEntries;
        MinimumByteLength = minimumByteLength;
        MinimumScalarLength = minimumScalarLength;
        MaximumScalarLength = maximumScalarLength;
        LeadingAsciiByte = leadingAsciiByte;
    }

    internal Pcre2BacktrackingInstruction[] Instructions { get; }

    internal Pcre2CompileRequest Request { get; }

    internal int RepeatCount { get; }

    internal int CaptureSlotCount { get; }

    internal bool HasBackreferences { get; }

    internal bool HasCaptureWrites { get; }

    internal Pcre2BacktrackingProgram[] AssertionPrograms { get; }

    internal string[] GroupNames { get; }

    internal Pcre2NameEntry[] NameEntries { get; }

    internal int MinimumByteLength { get; }

    internal int MinimumScalarLength { get; }

    internal int MaximumScalarLength { get; }

    internal byte? LeadingAsciiByte { get; }
}

internal enum Pcre2BacktrackingInstructionKind : byte
{
    Token = 0,
    Split = 1,
    Jump = 2,
    Repeat = 3,
    RepeatEnd = 4,
    RepeatExit = 5,
    Accept = 6,
    CaptureStart = 7,
    CaptureEnd = 8,
    Backreference = 9,
    Assertion = 10,
}

internal readonly struct Pcre2BacktrackingInstruction
{
    private Pcre2BacktrackingInstruction(
        Pcre2BacktrackingInstructionKind kind,
        Pcre2CharacterToken token,
        int primaryTarget,
        int secondaryTarget,
        int repeatId,
        int minimum,
        int maximum,
        Pcre2RepeatPreference preference)
    {
        Kind = kind;
        Token = token;
        PrimaryTarget = primaryTarget;
        SecondaryTarget = secondaryTarget;
        RepeatId = repeatId;
        Minimum = minimum;
        Maximum = maximum;
        Preference = preference;
    }

    internal Pcre2BacktrackingInstructionKind Kind { get; }

    internal Pcre2CharacterToken Token { get; }

    internal int PrimaryTarget { get; }

    internal int SecondaryTarget { get; }

    internal int RepeatId { get; }

    internal int CaptureSlot => RepeatId;

    internal int Minimum { get; }

    internal Pcre2CharacterOptions BackreferenceOptions => (Pcre2CharacterOptions)Minimum;

    internal int AssertionProgramId => RepeatId;

    internal Pcre2AssertionKind AssertionKind => (Pcre2AssertionKind)Minimum;

    internal int Maximum { get; }

    internal Pcre2RepeatPreference Preference { get; }

    internal static Pcre2BacktrackingInstruction CreateToken(Pcre2CharacterToken token) =>
        new(Pcre2BacktrackingInstructionKind.Token, token, 0, 0, 0, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateSplit(int primaryTarget, int secondaryTarget) =>
        new(Pcre2BacktrackingInstructionKind.Split, default, primaryTarget, secondaryTarget, 0, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateJump(int target) =>
        new(Pcre2BacktrackingInstructionKind.Jump, default, target, 0, 0, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateRepeat(
        int bodyTarget,
        int exitTarget,
        int repeatId,
        int minimum,
        int maximum,
        Pcre2RepeatPreference preference) =>
        new(Pcre2BacktrackingInstructionKind.Repeat, default, bodyTarget, exitTarget, repeatId, minimum, maximum, preference);

    internal static Pcre2BacktrackingInstruction CreateRepeatEnd(int repeatTarget) =>
        new(Pcre2BacktrackingInstructionKind.RepeatEnd, default, repeatTarget, 0, 0, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateRepeatExit(int repeatId) =>
        new(Pcre2BacktrackingInstructionKind.RepeatExit, default, 0, 0, repeatId, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateAccept() =>
        new(Pcre2BacktrackingInstructionKind.Accept, default, 0, 0, 0, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateCaptureStart(int slot) =>
        new(Pcre2BacktrackingInstructionKind.CaptureStart, default, 0, 0, slot, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateCaptureEnd(int slot) =>
        new(Pcre2BacktrackingInstructionKind.CaptureEnd, default, 0, 0, slot, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateBackreference(int slot, Pcre2CharacterOptions options) =>
        new(Pcre2BacktrackingInstructionKind.Backreference, default, 0, 0, slot, (int)options, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateAssertion(int assertionProgramId, Pcre2AssertionKind assertionKind) =>
        new(Pcre2BacktrackingInstructionKind.Assertion, default, 0, 0, assertionProgramId, (int)assertionKind, 0, Pcre2RepeatPreference.Greedy);
}

internal static class Pcre2BacktrackingCompiler
{
    internal static IPcre2BacktrackingCompileOutcome Compile(Pcre2CompileRequest request)
    {
        var parser = new Pcre2BacktrackingParser(request);
        if (!parser.TryParse(out var root))
        {
            return Pcre2NotBacktrackingOutcome.Instance;
        }

        var nameEntries = parser.NameEntries;
        var groupNames = parser.GroupNames;
        var lowerer = new Pcre2BacktrackingLowerer(parser.CaptureCount, nameEntries, request, groupNames);
        var program = lowerer.Lower(root);
        return new Pcre2CompiledBacktrackingOutcome(
            new Pcre2BacktrackingSyntaxTree(root),
            program);
    }
}

internal sealed class Pcre2BacktrackingParser
{
    private readonly Pcre2CompileRequest _request;
    private readonly string _pattern;
    private int _offset;
    private Pcre2CharacterOptions _options;
    private bool _ungreedy;
    private bool _noAutoCapture;
    private bool _sawControlFlow;
    private int _captureCount;
    private readonly List<Pcre2NameEntry> _nameEntries = [];
    private readonly HashSet<string> _captureNames = new(StringComparer.Ordinal);

    internal Pcre2BacktrackingParser(Pcre2CompileRequest request)
    {
        _request = request;
        _pattern = request.Pattern;
        _options = GetInitialOptions(request.Options);
        _ungreedy = (request.Options & Pcre2CompileOptions.Ungreedy) != 0;
        _noAutoCapture = (request.Options & Pcre2CompileOptions.NoAutoCapture) != 0;
    }

    internal int CaptureCount => _captureCount;

    internal Pcre2NameEntry[] NameEntries => [.. _nameEntries];

    internal string[] GroupNames
    {
        get
        {
            var names = new string[_captureCount + 1];
            for (var slot = 0; slot < names.Length; slot++)
            {
                names[slot] = slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            foreach (var entry in _nameEntries)
            {
                names[entry.Number] = entry.Name;
            }

            return names;
        }
    }

    internal bool TryParse(out IPcre2BacktrackingNode root)
    {
        if (!TryParseAlternation(false, out root) || _offset != _pattern.Length || !_sawControlFlow)
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
        if (TryParseBackreference(out node))
        {
            return true;
        }

        if (_pattern[_offset] == '(')
        {
            if (TryParseAssertionOpening(out var assertionKind, out var assertionOpeningLength))
            {
                _sawControlFlow = true;
                _offset += assertionOpeningLength;
                var outerOptions = _options;
                var outerUngreedy = _ungreedy;
                var outerNoAutoCapture = _noAutoCapture;
                if (!TryParseAlternation(true, out var assertionBody) ||
                    _offset >= _pattern.Length ||
                    _pattern[_offset] != ')')
                {
                    _options = outerOptions;
                    _ungreedy = outerUngreedy;
                    _noAutoCapture = outerNoAutoCapture;
                    node = Pcre2EmptyBacktrackingNode.Instance;
                    return false;
                }

                _offset++;
                _options = outerOptions;
                _ungreedy = outerUngreedy;
                _noAutoCapture = outerNoAutoCapture;
                node = new Pcre2AssertionBacktrackingNode(assertionKind, assertionBody);
                return true;
            }

            if (TryParseScopedOptionOpening(
                    out var scopedOptions,
                    out var scopedUngreedy,
                    out var scopedNoAutoCapture,
                    out var scopedOpeningLength))
            {
                _sawControlFlow = true;
                var outerOptions = _options;
                var outerUngreedy = _ungreedy;
                var outerNoAutoCapture = _noAutoCapture;
                _options = scopedOptions;
                _ungreedy = scopedUngreedy;
                _noAutoCapture = scopedNoAutoCapture;
                _offset += scopedOpeningLength;
                if (!TryParseAlternation(true, out node) ||
                    _offset >= _pattern.Length ||
                    _pattern[_offset] != ')')
                {
                    _options = outerOptions;
                    _ungreedy = outerUngreedy;
                    _noAutoCapture = outerNoAutoCapture;
                    return false;
                }

                _offset++;
                _options = outerOptions;
                _ungreedy = outerUngreedy;
                _noAutoCapture = outerNoAutoCapture;
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
                if (!TryParseAlternation(true, out node) || _offset >= _pattern.Length || _pattern[_offset] != ')')
                {
                    _options = outerOptions;
                    _ungreedy = outerUngreedy;
                    _noAutoCapture = outerNoAutoCapture;
                    return false;
                }

                _offset++;
                _options = outerOptions;
                _ungreedy = outerUngreedy;
                _noAutoCapture = outerNoAutoCapture;
                return true;
            }

            if (TryParseCaptureOpening(out var captureName, out var openingLength))
            {
                _sawControlFlow = true;
                var slot = ++_captureCount;
                if (captureName.Length != 0)
                {
                    if (!_captureNames.Add(captureName))
                    {
                        node = Pcre2EmptyBacktrackingNode.Instance;
                        return false;
                    }

                    _nameEntries.Add(new Pcre2NameEntry { Name = captureName, Number = slot });
                }

                _offset += openingLength;
                var outerOptions = _options;
                var outerUngreedy = _ungreedy;
                var outerNoAutoCapture = _noAutoCapture;
                if (!TryParseAlternation(true, out var body) || _offset >= _pattern.Length || _pattern[_offset] != ')')
                {
                    _options = outerOptions;
                    _ungreedy = outerUngreedy;
                    _noAutoCapture = outerNoAutoCapture;
                    node = Pcre2EmptyBacktrackingNode.Instance;
                    return false;
                }

                _offset++;
                _options = outerOptions;
                _ungreedy = outerUngreedy;
                _noAutoCapture = outerNoAutoCapture;
                node = new Pcre2CaptureBacktrackingNode(slot, captureName, body);
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
        node = new Pcre2TokenBacktrackingNode(token);
        return true;
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
                node = new Pcre2TokenBacktrackingNode(Pcre2CharacterToken.CreateLiteral(
                    new Rune(octal),
                    _options,
                    _offset,
                    patternLength));
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
        if (name.IsEmpty || name.Length > 128)
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
        if (_offset < _pattern.Length && _pattern[_offset] == '?')
        {
            preference = preference == Pcre2RepeatPreference.Greedy
                ? Pcre2RepeatPreference.Lazy
                : Pcre2RepeatPreference.Greedy;
            _offset++;
        }
        else if (_offset < _pattern.Length && _pattern[_offset] == '+')
        {
            return false;
        }

        node = new Pcre2RepeatBacktrackingNode(body, minimum, maximum, preference);
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
        out int openingLength)
    {
        options = _options;
        ungreedy = _ungreedy;
        noAutoCapture = _noAutoCapture;
        openingLength = 0;
        if (_offset + 3 > _pattern.Length || !_pattern.AsSpan(_offset).StartsWith("(?", StringComparison.Ordinal))
        {
            return false;
        }

        var cursor = _offset + 2;
        var enabling = true;
        var sawOption = false;
        while (cursor < _pattern.Length)
        {
            var ch = _pattern[cursor];
            if (ch == '-')
            {
                enabling = false;
                cursor++;
                continue;
            }

            if (ch == ':')
            {
                if (!sawOption)
                {
                    return false;
                }

                openingLength = cursor - _offset + 1;
                return true;
            }

            if (!TryApplyOption(ref options, ref ungreedy, ref noAutoCapture, ch, enabling))
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
        var sawOption = false;
        while (cursor < _pattern.Length)
        {
            var ch = _pattern[cursor];
            if (ch == '-')
            {
                enabling = false;
                cursor++;
                continue;
            }

            if (ch == ')')
            {
                if (!sawOption)
                {
                    return false;
                }

                _options = options;
                _ungreedy = ungreedy;
                _noAutoCapture = noAutoCapture;
                _offset = cursor + 1;
                return true;
            }

            if (!TryApplyOption(ref options, ref ungreedy, ref noAutoCapture, ch, enabling))
            {
                return false;
            }
            sawOption = true;
            cursor++;
        }

        return false;
    }

    private static bool TryApplyOption(
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
            case 'm':
                flag = Pcre2CharacterOptions.Multiline;
                break;
            case 's':
                flag = Pcre2CharacterOptions.DotAll;
                break;
            case 'x':
                flag = Pcre2CharacterOptions.Extended;
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
        if ((_options & (Pcre2CharacterOptions.Extended | Pcre2CharacterOptions.ExtendedMore)) == 0)
        {
            return;
        }

        while (_offset < _pattern.Length)
        {
            if (_pattern[_offset] is ' ' or '\t' or '\n' or '\v' or '\f' or '\r')
            {
                _offset++;
                continue;
            }

            if (_pattern[_offset] != '#')
            {
                return;
            }

            while (_offset < _pattern.Length && _pattern[_offset] is not ('\r' or '\n'))
            {
                _offset++;
            }
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

internal sealed class Pcre2BacktrackingLowerer
{
    private readonly List<Pcre2BacktrackingInstruction> _instructions = [];
    private readonly List<Pcre2BacktrackingProgram> _assertionPrograms = [];
    private readonly int _captureCount;
    private readonly Pcre2NameEntry[] _nameEntries;
    private readonly Dictionary<string, int> _nameSlots;
    private readonly Pcre2CompileRequest _request;
    private readonly string[] _groupNames;
    private int _repeatCount;
    private bool _hasBackreferences;
    private bool _hasCaptureWrites;

    internal Pcre2BacktrackingLowerer(
        int captureCount,
        Pcre2NameEntry[] nameEntries,
        Pcre2CompileRequest request,
        string[] groupNames)
    {
        _captureCount = captureCount;
        _nameEntries = nameEntries;
        _nameSlots = nameEntries.ToDictionary(static entry => entry.Name, static entry => entry.Number, StringComparer.Ordinal);
        _request = request;
        _groupNames = groupNames;
    }

    internal Pcre2BacktrackingProgram Lower(IPcre2BacktrackingNode root)
    {
        EmitNode(root);
        _instructions.Add(Pcre2BacktrackingInstruction.CreateAccept());
        return new Pcre2BacktrackingProgram(
            [.. _instructions],
            _request,
            _repeatCount,
            _captureCount + 1,
            _hasBackreferences,
            _hasCaptureWrites,
            [.. _assertionPrograms],
            _groupNames,
            _nameEntries,
            Pcre2BacktrackingAnalysis.GetMinimumByteLength(root),
            Pcre2BacktrackingAnalysis.GetMinimumScalarLength(root),
            Pcre2BacktrackingAnalysis.GetMaximumScalarLength(root),
            Pcre2BacktrackingAnalysis.GetLeadingAsciiByte(root));
    }

    private void EmitNode(IPcre2BacktrackingNode node)
    {
        switch (node)
        {
            case Pcre2EmptyBacktrackingNode:
                return;
            case Pcre2TokenBacktrackingNode token:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateToken(token.Token));
                return;
            case Pcre2SequenceBacktrackingNode sequence:
                foreach (var child in sequence.Children)
                {
                    EmitNode(child);
                }
                return;
            case Pcre2AlternationBacktrackingNode alternation:
                EmitAlternation(alternation);
                return;
            case Pcre2RepeatBacktrackingNode repeat:
                EmitRepeat(repeat);
                return;
            case Pcre2CaptureBacktrackingNode capture:
                _hasCaptureWrites = true;
                _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureStart(capture.Slot));
                EmitNode(capture.Body);
                _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureEnd(capture.Slot));
                return;
            case Pcre2BackreferenceBacktrackingNode backreference:
                _hasBackreferences = true;
                _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreference(
                    ResolveBackreference(backreference.Target),
                    backreference.Options));
                return;
            case Pcre2AssertionBacktrackingNode assertion:
                var assertionLowerer = new Pcre2BacktrackingLowerer(
                    _captureCount,
                    _nameEntries,
                    _request,
                    _groupNames);
                var assertionProgramId = _assertionPrograms.Count;
                var assertionProgram = assertionLowerer.Lower(assertion.Body);
                _assertionPrograms.Add(assertionProgram);
                _hasBackreferences |= assertionProgram.HasBackreferences;
                _hasCaptureWrites |= assertionProgram.HasCaptureWrites;
                _instructions.Add(Pcre2BacktrackingInstruction.CreateAssertion(
                    assertionProgramId,
                    assertion.AssertionKind));
                return;
            default:
                throw new InvalidOperationException("The PCRE2 backtracking syntax node is not lowerable.");
        }
    }

    private void EmitAlternation(Pcre2AlternationBacktrackingNode alternation)
    {
        var exitJumps = new List<int>(alternation.Alternatives.Length - 1);
        for (var i = 0; i < alternation.Alternatives.Length - 1; i++)
        {
            var splitIndex = _instructions.Count;
            _instructions.Add(default);
            var primaryTarget = _instructions.Count;
            EmitNode(alternation.Alternatives[i]);
            var jumpIndex = _instructions.Count;
            _instructions.Add(default);
            exitJumps.Add(jumpIndex);
            var secondaryTarget = _instructions.Count;
            _instructions[splitIndex] = Pcre2BacktrackingInstruction.CreateSplit(primaryTarget, secondaryTarget);
        }

        EmitNode(alternation.Alternatives[^1]);
        var exitTarget = _instructions.Count;
        foreach (var jumpIndex in exitJumps)
        {
            _instructions[jumpIndex] = Pcre2BacktrackingInstruction.CreateJump(exitTarget);
        }
    }

    private void EmitRepeat(Pcre2RepeatBacktrackingNode repeat)
    {
        var repeatId = _repeatCount++;
        var repeatIndex = _instructions.Count;
        _instructions.Add(default);
        var bodyTarget = _instructions.Count;
        EmitNode(repeat.Body);
        _instructions.Add(Pcre2BacktrackingInstruction.CreateRepeatEnd(repeatIndex));
        var exitTarget = _instructions.Count;
        _instructions.Add(Pcre2BacktrackingInstruction.CreateRepeatExit(repeatId));
        _instructions[repeatIndex] = Pcre2BacktrackingInstruction.CreateRepeat(
            bodyTarget,
            exitTarget,
            repeatId,
            repeat.Minimum,
            repeat.Maximum,
            repeat.Preference);
    }

    private int ResolveBackreference(Pcre2BackreferenceTarget target)
    {
        var slot = target.Kind switch
        {
            Pcre2BackreferenceTargetKind.Absolute => target.Number,
            Pcre2BackreferenceTargetKind.Relative when target.Number > 0 =>
                target.CaptureCountAtReference + target.Number,
            Pcre2BackreferenceTargetKind.Relative =>
                target.CaptureCountAtReference + target.Number + 1,
            Pcre2BackreferenceTargetKind.Named when _nameSlots.TryGetValue(target.Name, out var namedSlot) => namedSlot,
            _ => 0,
        };
        if (slot <= 0 || slot > _captureCount)
        {
            throw new Pcre2CompileException("Reference to a non-existent capturing group.", Pcre2ErrorKind.UnrecognizedEscape);
        }

        return slot;
    }
}

internal static class Pcre2BacktrackingAnalysis
{
    internal static int GetMinimumByteLength(IPcre2BacktrackingNode node)
    {
        return node switch
        {
            Pcre2EmptyBacktrackingNode => 0,
            Pcre2TokenBacktrackingNode token => ConsumesScalar(token.Token.Kind) ? 1 : 0,
            Pcre2SequenceBacktrackingNode sequence => GetSequenceMinimum(sequence),
            Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Min(GetMinimumByteLength),
            Pcre2RepeatBacktrackingNode repeat => SaturatingMultiply(GetMinimumByteLength(repeat.Body), repeat.Minimum),
            Pcre2CaptureBacktrackingNode capture => GetMinimumByteLength(capture.Body),
            Pcre2BackreferenceBacktrackingNode => 0,
            Pcre2AssertionBacktrackingNode => 0,
            _ => 0,
        };
    }

    internal static int GetMinimumScalarLength(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2EmptyBacktrackingNode => 0,
        Pcre2TokenBacktrackingNode token => ConsumesScalar(token.Token.Kind) ? 1 : 0,
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Aggregate(
            0,
            static (length, child) => SaturatingAdd(length, GetMinimumScalarLength(child))),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Min(GetMinimumScalarLength),
        Pcre2RepeatBacktrackingNode repeat => SaturatingMultiply(GetMinimumScalarLength(repeat.Body), repeat.Minimum),
        Pcre2CaptureBacktrackingNode capture => GetMinimumScalarLength(capture.Body),
        Pcre2BackreferenceBacktrackingNode => 0,
        Pcre2AssertionBacktrackingNode => 0,
        _ => 0,
    };

    internal static int GetMaximumScalarLength(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2EmptyBacktrackingNode => 0,
        Pcre2TokenBacktrackingNode token => token.Token.Kind == Pcre2CharacterTokenKind.NewlineSequence
            ? 2
            : ConsumesScalar(token.Token.Kind) ? 1 : 0,
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Aggregate(
            0,
            static (length, child) => SaturatingAdd(length, GetMaximumScalarLength(child))),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Max(GetMaximumScalarLength),
        Pcre2RepeatBacktrackingNode repeat => SaturatingMultiply(GetMaximumScalarLength(repeat.Body), repeat.Maximum),
        Pcre2CaptureBacktrackingNode capture => GetMaximumScalarLength(capture.Body),
        Pcre2BackreferenceBacktrackingNode => int.MaxValue,
        Pcre2AssertionBacktrackingNode => 0,
        _ => int.MaxValue,
    };

    internal static byte? GetLeadingAsciiByte(IPcre2BacktrackingNode node)
    {
        return TryGetLeadingAsciiByte(node, out var leading) ? leading : null;
    }

    private static int GetSequenceMinimum(Pcre2SequenceBacktrackingNode sequence)
    {
        var result = 0;
        foreach (var child in sequence.Children)
        {
            result = SaturatingAdd(result, GetMinimumByteLength(child));
        }

        return result;
    }

    private static bool TryGetLeadingAsciiByte(IPcre2BacktrackingNode node, out byte leading)
    {
        switch (node)
        {
            case Pcre2TokenBacktrackingNode token when
                token.Token.Kind == Pcre2CharacterTokenKind.Literal &&
                token.Token.Literal.IsAscii &&
                (token.Token.Options & Pcre2CharacterOptions.Caseless) == 0:
                leading = (byte)token.Token.Literal.Value;
                return true;
            case Pcre2SequenceBacktrackingNode sequence:
                foreach (var child in sequence.Children)
                {
                    if (TryGetLeadingAsciiByte(child, out leading))
                    {
                        return true;
                    }

                    if (CanConsume(child))
                    {
                        break;
                    }
                }
                break;
            case Pcre2AlternationBacktrackingNode alternation:
                byte? common = null;
                foreach (var alternative in alternation.Alternatives)
                {
                    if (!TryGetLeadingAsciiByte(alternative, out var branchLeading))
                    {
                        leading = 0;
                        return false;
                    }

                    if (common is { } value && value != branchLeading)
                    {
                        leading = 0;
                        return false;
                    }

                    common = branchLeading;
                }

                if (common is { } commonValue)
                {
                    leading = commonValue;
                    return true;
                }
                break;
            case Pcre2RepeatBacktrackingNode repeat when repeat.Minimum > 0:
                return TryGetLeadingAsciiByte(repeat.Body, out leading);
            case Pcre2CaptureBacktrackingNode capture:
                return TryGetLeadingAsciiByte(capture.Body, out leading);
            case Pcre2AssertionBacktrackingNode:
                break;
        }

        leading = 0;
        return false;
    }

    private static bool ConsumesScalar(Pcre2CharacterTokenKind kind) => kind is
        Pcre2CharacterTokenKind.Literal or
        Pcre2CharacterTokenKind.CharacterClass or
        Pcre2CharacterTokenKind.Any or
        Pcre2CharacterTokenKind.AnyNotNewline or
        Pcre2CharacterTokenKind.NewlineSequence;

    private static bool CanConsume(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2EmptyBacktrackingNode => false,
        Pcre2TokenBacktrackingNode token => ConsumesScalar(token.Token.Kind),
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(CanConsume),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(CanConsume),
        Pcre2RepeatBacktrackingNode repeat => repeat.Maximum > 0 && CanConsume(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => CanConsume(capture.Body),
        Pcre2BackreferenceBacktrackingNode => true,
        Pcre2AssertionBacktrackingNode => false,
        _ => true,
    };

    private static int SaturatingAdd(int left, int right) =>
        left > int.MaxValue - right ? int.MaxValue : left + right;

    private static int SaturatingMultiply(int value, int multiplier) =>
        value == 0 || multiplier == 0
            ? 0
            : value > int.MaxValue / multiplier ? int.MaxValue : value * multiplier;
}

internal enum Pcre2BacktrackingResumeAction : byte
{
    None = 0,
    EnterRepeat = 1,
}

internal readonly record struct Pcre2BacktrackingFrame(
    int Instruction,
    int InputOffsetInBytes,
    int RepeatCheckpoint,
    int CaptureCheckpoint,
    Pcre2BacktrackingResumeAction ResumeAction,
    int RepeatId,
    int RepeatCount,
    int RepeatPosition);

internal readonly record struct Pcre2RepeatMutation(
    int RepeatId,
    int PreviousCount,
    int PreviousPosition);

internal readonly record struct Pcre2CaptureMutation(
    int Slot,
    int PreviousStart,
    int PreviousEnd,
    int PreviousOpenStart);

internal readonly record struct Pcre2CaptureByteRange(bool Success, int StartOffsetInBytes, int EndOffsetInBytes);

internal enum Pcre2CaptureMaterialization : byte
{
    None = 0,
    FinalSlots = 1,
}

internal readonly record struct Pcre2BacktrackingMatch(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int ConsumedEndOffsetInBytes,
    Pcre2CaptureByteRange[] Captures)
{
    internal static Pcre2BacktrackingMatch NoMatch =>
        new(false, 0, 0, 0, []);

    internal Pcre2CharacterMatch ToCharacterMatch() => Success
        ? new Pcre2CharacterMatch(true, StartOffsetInBytes, EndOffsetInBytes, ConsumedEndOffsetInBytes)
        : Pcre2CharacterMatch.NoMatch;
}

internal static class Pcre2BacktrackingRunner
{
    internal static Pcre2CharacterMatch Match(
        Pcre2BacktrackingProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            ref input,
            start,
            matchOptions,
            Pcre2CaptureMaterialization.None,
            ref budget).ToCharacterMatch();

    internal static Pcre2BacktrackingMatch MatchDetailed(
        Pcre2BacktrackingProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            ref input,
            start,
            matchOptions,
            Pcre2CaptureMaterialization.FinalSlots,
            ref budget);

    private static Pcre2BacktrackingMatch MatchCore(
        Pcre2BacktrackingProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ref Pcre2ResourceBudget budget)
    {
        var bytes = input.Bytes;
        var anchored = (program.Request.Options & Pcre2CompileOptions.Anchored) != 0 ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0;
        var candidate = start.Value;
        var maximumCandidate = program.MinimumByteLength > bytes.Length
            ? -1
            : bytes.Length - program.MinimumByteLength;
        while (candidate <= maximumCandidate)
        {
            budget.ChargeCandidate();
            if (TryMatchAt(
                    program,
                    bytes,
                    candidate,
                    start.Value,
                    matchOptions,
                    captureMaterialization,
                    ReadOnlySpan<int>.Empty,
                    ReadOnlySpan<int>.Empty,
                    0,
                    false,
                    ref budget,
                    out var match))
            {
                return match;
            }

            if (anchored || !Pcre2CharacterRunner.TryAdvanceCandidate(
                    program.Request,
                    program.LeadingAsciiByte,
                    ref input,
                    candidate,
                    !budget.RequiresCandidateMetering,
                    out candidate))
            {
                break;
            }
        }

        return Pcre2BacktrackingMatch.NoMatch;
    }

    private static bool TryMatchAt(
        Pcre2BacktrackingProgram program,
        ReadOnlySpan<byte> input,
        int candidate,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ReadOnlySpan<int> initialCaptureStarts,
        ReadOnlySpan<int> initialCaptureEnds,
        int depthBase,
        bool isAssertion,
        ref Pcre2ResourceBudget budget,
        out Pcre2BacktrackingMatch match)
    {
        var configuredFrameLimit = budget.Limits.DepthLimit == 0
            ? int.MaxValue
            : (int)Math.Min(budget.Limits.DepthLimit, int.MaxValue);
        var frameLimit = configuredFrameLimit == int.MaxValue
            ? int.MaxValue
            : Math.Max(0, configuredFrameLimit - depthBase);
        var frames = new Utf8PooledStateStack<Pcre2BacktrackingFrame>(frameLimit);
        var repeatMutations = new Utf8PooledStateStack<Pcre2RepeatMutation>(int.MaxValue);
        var captureMutations = new Utf8PooledStateStack<Pcre2CaptureMutation>(int.MaxValue);
        var rentedCounts = program.RepeatCount == 0 ? null : ArrayPool<int>.Shared.Rent(program.RepeatCount);
        var rentedPositions = program.RepeatCount == 0 ? null : ArrayPool<int>.Shared.Rent(program.RepeatCount);
        var needsCaptureState = program.HasBackreferences ||
            captureMaterialization == Pcre2CaptureMaterialization.FinalSlots ||
            !initialCaptureStarts.IsEmpty;
        var rentedCaptureStarts = !needsCaptureState || program.CaptureSlotCount <= 1
            ? null
            : ArrayPool<int>.Shared.Rent(program.CaptureSlotCount);
        var rentedCaptureEnds = !needsCaptureState || program.CaptureSlotCount <= 1
            ? null
            : ArrayPool<int>.Shared.Rent(program.CaptureSlotCount);
        var rentedCaptureOpenStarts = !needsCaptureState || program.CaptureSlotCount <= 1
            ? null
            : ArrayPool<int>.Shared.Rent(program.CaptureSlotCount);
        var counts = rentedCounts is null ? Span<int>.Empty : rentedCounts.AsSpan(0, program.RepeatCount);
        var positions = rentedPositions is null ? Span<int>.Empty : rentedPositions.AsSpan(0, program.RepeatCount);
        var captureStarts = rentedCaptureStarts is null
            ? Span<int>.Empty
            : rentedCaptureStarts.AsSpan(0, program.CaptureSlotCount);
        var captureEnds = rentedCaptureEnds is null
            ? Span<int>.Empty
            : rentedCaptureEnds.AsSpan(0, program.CaptureSlotCount);
        var captureOpenStarts = rentedCaptureOpenStarts is null
            ? Span<int>.Empty
            : rentedCaptureOpenStarts.AsSpan(0, program.CaptureSlotCount);
        counts.Fill(-1);
        positions.Fill(-1);
        captureStarts.Fill(-1);
        captureEnds.Fill(-1);
        captureOpenStarts.Fill(-1);
        if (!initialCaptureStarts.IsEmpty)
        {
            initialCaptureStarts.CopyTo(captureStarts);
            initialCaptureEnds.CopyTo(captureEnds);
        }
        budget.ChargeHeap(
            (ulong)program.RepeatCount * 8UL +
            (ulong)captureStarts.Length * 12UL);

        try
        {
            var instructionIndex = 0;
            var inputIndex = candidate;
            while (true)
            {
                budget.ChargeBacktracking();
                var instruction = program.Instructions[instructionIndex];
                switch (instruction.Kind)
                {
                    case Pcre2BacktrackingInstructionKind.Token:
                        if (Pcre2CharacterRunner.TryMatchToken(
                                instruction.Token,
                                program.Request,
                                input,
                                inputIndex,
                                firstMatchingPosition,
                                matchOptions,
                                out inputIndex))
                        {
                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.Split:
                        PushFrame(
                            ref frames,
                            new Pcre2BacktrackingFrame(
                                instruction.SecondaryTarget,
                                inputIndex,
                                repeatMutations.Count,
                                captureMutations.Count,
                                Pcre2BacktrackingResumeAction.None,
                                0,
                                0,
                                0),
                            repeatMutations.Count,
                            captureMutations.Count,
                            program.RepeatCount,
                            captureStarts.Length,
                            depthBase,
                            ref budget);
                        instructionIndex = instruction.PrimaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Jump:
                        instructionIndex = instruction.PrimaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Repeat:
                        var repeatId = instruction.RepeatId;
                        var count = counts[repeatId];
                        if (count < 0)
                        {
                            SetRepeat(repeatId, 0, -1, counts, positions, ref repeatMutations, ref budget);
                            count = 0;
                        }

                        var madeNoProgress = count > 0 && positions[repeatId] == inputIndex;
                        if (count < instruction.Minimum || !madeNoProgress && count < instruction.Maximum)
                        {
                            if (count >= instruction.Minimum)
                            {
                                if (instruction.Preference == Pcre2RepeatPreference.Greedy)
                                {
                                    PushFrame(
                                        ref frames,
                                        new Pcre2BacktrackingFrame(
                                            instruction.SecondaryTarget,
                                            inputIndex,
                                            repeatMutations.Count,
                                            captureMutations.Count,
                                            Pcre2BacktrackingResumeAction.None,
                                            0,
                                            0,
                                            0),
                                        repeatMutations.Count,
                                        captureMutations.Count,
                                        program.RepeatCount,
                                        captureStarts.Length,
                                        depthBase,
                                        ref budget);
                                }
                                else
                                {
                                    PushFrame(
                                        ref frames,
                                        new Pcre2BacktrackingFrame(
                                            instruction.PrimaryTarget,
                                            inputIndex,
                                            repeatMutations.Count,
                                            captureMutations.Count,
                                            Pcre2BacktrackingResumeAction.EnterRepeat,
                                            repeatId,
                                            count + 1,
                                            inputIndex),
                                        repeatMutations.Count,
                                        captureMutations.Count,
                                        program.RepeatCount,
                                        captureStarts.Length,
                                        depthBase,
                                        ref budget);
                                    instructionIndex = instruction.SecondaryTarget;
                                    continue;
                                }
                            }

                            SetRepeat(repeatId, count + 1, inputIndex, counts, positions, ref repeatMutations, ref budget);
                            instructionIndex = instruction.PrimaryTarget;
                            continue;
                        }

                        instructionIndex = instruction.SecondaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.RepeatEnd:
                        instructionIndex = instruction.PrimaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.RepeatExit:
                        SetRepeat(instruction.RepeatId, -1, -1, counts, positions, ref repeatMutations, ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.CaptureStart:
                        if (captureStarts.IsEmpty)
                        {
                            instructionIndex++;
                            continue;
                        }

                        SetCapture(
                            instruction.CaptureSlot,
                            captureStarts[instruction.CaptureSlot],
                            captureEnds[instruction.CaptureSlot],
                            inputIndex,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            ref captureMutations,
                            ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.CaptureEnd:
                        if (captureStarts.IsEmpty)
                        {
                            instructionIndex++;
                            continue;
                        }

                        SetCapture(
                            instruction.CaptureSlot,
                            captureOpenStarts[instruction.CaptureSlot],
                            inputIndex,
                            -1,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            ref captureMutations,
                            ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Backreference:
                        var captureSlot = instruction.CaptureSlot;
                        if (captureStarts[captureSlot] >= 0 && captureEnds[captureSlot] >= captureStarts[captureSlot] &&
                            TryMatchBackreference(
                                input,
                                captureStarts[captureSlot],
                                captureEnds[captureSlot],
                                inputIndex,
                                (instruction.BackreferenceOptions & Pcre2CharacterOptions.Caseless) != 0,
                                out inputIndex))
                        {
                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.Assertion:
                        if (TryEvaluateAssertion(
                                program.AssertionPrograms[instruction.AssertionProgramId],
                                instruction.AssertionKind,
                                input,
                                inputIndex,
                                matchOptions,
                                captureStarts,
                                captureEnds,
                                depthBase + frames.Count + 1,
                                ref budget,
                                out var assertionCaptures))
                        {
                            for (var slot = 1; slot < assertionCaptures.Length; slot++)
                            {
                                var capture = assertionCaptures[slot];
                                if (!capture.Success)
                                {
                                    continue;
                                }

                                SetCapture(
                                    slot,
                                    capture.StartOffsetInBytes,
                                    capture.EndOffsetInBytes,
                                    -1,
                                    captureStarts,
                                    captureEnds,
                                    captureOpenStarts,
                                    ref captureMutations,
                                    ref budget);
                            }

                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.Accept:
                        var endAnchored = !isAssertion && (program.Request.Options & Pcre2CompileOptions.EndAnchored) != 0 ||
                            (matchOptions & Pcre2MatchOptions.EndAnchored) != 0;
                        var emptyDisallowed = (matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && inputIndex == candidate ||
                            (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0 && candidate == firstMatchingPosition && inputIndex == candidate;
                        if ((!endAnchored || inputIndex == input.Length) && !emptyDisallowed)
                        {
                            var captures = captureMaterialization == Pcre2CaptureMaterialization.FinalSlots
                                ? MaterializeCaptures(program.CaptureSlotCount, candidate, inputIndex, captureStarts, captureEnds)
                                : [];
                            match = new Pcre2BacktrackingMatch(
                                true,
                                candidate,
                                inputIndex,
                                inputIndex,
                                captures);
                            return true;
                        }
                        break;

                    default:
                        break;
                }

                if (!TryResume(
                        ref frames,
                        ref repeatMutations,
                        ref captureMutations,
                        counts,
                        positions,
                        captureStarts,
                        captureEnds,
                        captureOpenStarts,
                        ref budget,
                        out instructionIndex,
                        out inputIndex))
                {
                    match = Pcre2BacktrackingMatch.NoMatch;
                    return false;
                }
            }
        }
        finally
        {
            captureMutations.Dispose();
            repeatMutations.Dispose();
            frames.Dispose();
            if (rentedCounts is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCounts);
            }
            if (rentedPositions is not null)
            {
                ArrayPool<int>.Shared.Return(rentedPositions);
            }
            if (rentedCaptureStarts is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCaptureStarts);
            }
            if (rentedCaptureEnds is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCaptureEnds);
            }
            if (rentedCaptureOpenStarts is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCaptureOpenStarts);
            }
        }
    }

    private static bool TryEvaluateAssertion(
        Pcre2BacktrackingProgram assertionProgram,
        Pcre2AssertionKind assertionKind,
        ReadOnlySpan<byte> input,
        int inputIndex,
        Pcre2MatchOptions outerMatchOptions,
        ReadOnlySpan<int> captureStarts,
        ReadOnlySpan<int> captureEnds,
        int depth,
        ref Pcre2ResourceBudget budget,
        out Pcre2CaptureByteRange[] captures)
    {
        budget.ChargeFrame((uint)depth, checked((ulong)depth * 32UL + (ulong)captureStarts.Length * 12UL));
        var isNegative = assertionKind is Pcre2AssertionKind.NegativeLookahead or Pcre2AssertionKind.NegativeLookbehind;
        var isLookbehind = assertionKind is Pcre2AssertionKind.PositiveLookbehind or Pcre2AssertionKind.NegativeLookbehind;
        var nestedOptions = outerMatchOptions & (Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol);
        nestedOptions |= Pcre2MatchOptions.Anchored;
        var captureMaterialization = !captureStarts.IsEmpty && assertionProgram.HasCaptureWrites
            ? Pcre2CaptureMaterialization.FinalSlots
            : Pcre2CaptureMaterialization.None;

        if (!isLookbehind)
        {
            budget.ChargeCandidate();
            var matched = TryMatchAt(
                assertionProgram,
                input,
                inputIndex,
                inputIndex,
                nestedOptions,
                captureMaterialization,
                captureStarts,
                captureEnds,
                depth,
                true,
                ref budget,
                out var assertionMatch);
            captures = !isNegative && matched ? assertionMatch.Captures : [];
            return isNegative ? !matched : matched;
        }

        nestedOptions |= Pcre2MatchOptions.EndAnchored;
        var earliest = inputIndex;
        var maximumLength = assertionProgram.MaximumScalarLength;
        for (var scalarCount = 0; scalarCount < maximumLength &&
             TryRetreatScalar(input, earliest, out var previous); scalarCount++)
        {
            earliest = previous;
        }

        var latest = inputIndex;
        for (var scalarCount = 0; scalarCount < assertionProgram.MinimumScalarLength; scalarCount++)
        {
            if (!TryRetreatScalar(input, latest, out latest))
            {
                captures = [];
                return isNegative;
            }
        }

        var candidate = earliest;
        while (candidate <= latest)
        {
            budget.ChargeCandidate();
            if (TryMatchAt(
                    assertionProgram,
                    input[..inputIndex],
                    candidate,
                    candidate,
                    nestedOptions,
                    captureMaterialization,
                    captureStarts,
                    captureEnds,
                    depth,
                    true,
                    ref budget,
                    out var assertionMatch))
            {
                captures = isNegative ? [] : assertionMatch.Captures;
                return !isNegative;
            }

            if (candidate == latest ||
                Rune.DecodeFromUtf8(input[candidate..latest], out _, out var width) != OperationStatus.Done)
            {
                break;
            }

            candidate += width;
        }

        captures = [];
        return isNegative;
    }

    private static bool TryRetreatScalar(ReadOnlySpan<byte> input, int position, out int previous)
    {
        if (position == 0 ||
            Rune.DecodeLastFromUtf8(input[..position], out _, out var width) != OperationStatus.Done)
        {
            previous = position;
            return false;
        }

        previous = position - width;
        return true;
    }

    private static void PushFrame(
        ref Utf8PooledStateStack<Pcre2BacktrackingFrame> frames,
        Pcre2BacktrackingFrame frame,
        int repeatMutationCount,
        int captureMutationCount,
        int repeatCount,
        int captureSlotCount,
        int depthBase,
        ref Pcre2ResourceBudget budget)
    {
        var nextDepth = checked(depthBase + frames.Count + 1);
        var estimatedHeapBytes = checked(
            (ulong)nextDepth * 32UL +
            (ulong)repeatMutationCount * 12UL +
            (ulong)captureMutationCount * 16UL +
            (ulong)repeatCount * 8UL +
            (ulong)captureSlotCount * 12UL);
        budget.ChargeFrame((uint)nextDepth, estimatedHeapBytes);
        if (!frames.TryPush(frame))
        {
            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", "DepthLimit");
        }
    }

    private static bool TryResume(
        ref Utf8PooledStateStack<Pcre2BacktrackingFrame> frames,
        ref Utf8PooledStateStack<Pcre2RepeatMutation> repeatMutations,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> captureMutations,
        Span<int> counts,
        Span<int> positions,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        ref Pcre2ResourceBudget budget,
        out int instructionIndex,
        out int inputIndex)
    {
        if (frames.Count == 0)
        {
            instructionIndex = 0;
            inputIndex = 0;
            return false;
        }

        var frame = frames.Pop();
        RollbackRepeats(frame.RepeatCheckpoint, ref repeatMutations, counts, positions);
        RollbackCaptures(
            frame.CaptureCheckpoint,
            ref captureMutations,
            captureStarts,
            captureEnds,
            captureOpenStarts);
        if (frame.ResumeAction == Pcre2BacktrackingResumeAction.EnterRepeat)
        {
            SetRepeat(
                frame.RepeatId,
                frame.RepeatCount,
                frame.RepeatPosition,
                counts,
                positions,
                ref repeatMutations,
                ref budget);
        }

        instructionIndex = frame.Instruction;
        inputIndex = frame.InputOffsetInBytes;
        return true;
    }

    private static void SetRepeat(
        int repeatId,
        int count,
        int position,
        Span<int> counts,
        Span<int> positions,
        ref Utf8PooledStateStack<Pcre2RepeatMutation> mutations,
        ref Pcre2ResourceBudget budget)
    {
        budget.ChargeHeap(checked((ulong)(mutations.Count + 1) * 12UL + (ulong)counts.Length * 8UL));
        if (!mutations.TryPush(new Pcre2RepeatMutation(repeatId, counts[repeatId], positions[repeatId])))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", "HeapLimit");
        }

        counts[repeatId] = count;
        positions[repeatId] = position;
    }

    private static void RollbackRepeats(
        int checkpoint,
        ref Utf8PooledStateStack<Pcre2RepeatMutation> mutations,
        Span<int> counts,
        Span<int> positions)
    {
        while (mutations.Count > checkpoint)
        {
            var mutation = mutations.Pop();
            counts[mutation.RepeatId] = mutation.PreviousCount;
            positions[mutation.RepeatId] = mutation.PreviousPosition;
        }
    }

    private static void SetCapture(
        int slot,
        int start,
        int end,
        int openStart,
        Span<int> starts,
        Span<int> ends,
        Span<int> openStarts,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> mutations,
        ref Pcre2ResourceBudget budget)
    {
        budget.ChargeHeap(checked((ulong)(mutations.Count + 1) * 16UL + (ulong)starts.Length * 12UL));
        if (!mutations.TryPush(new Pcre2CaptureMutation(slot, starts[slot], ends[slot], openStarts[slot])))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", "HeapLimit");
        }

        starts[slot] = start;
        ends[slot] = end;
        openStarts[slot] = openStart;
    }

    private static void RollbackCaptures(
        int checkpoint,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> mutations,
        Span<int> starts,
        Span<int> ends,
        Span<int> openStarts)
    {
        while (mutations.Count > checkpoint)
        {
            var mutation = mutations.Pop();
            starts[mutation.Slot] = mutation.PreviousStart;
            ends[mutation.Slot] = mutation.PreviousEnd;
            openStarts[mutation.Slot] = mutation.PreviousOpenStart;
        }
    }

    private static bool TryMatchBackreference(
        ReadOnlySpan<byte> input,
        int captureStart,
        int captureEnd,
        int subjectStart,
        bool caseless,
        out int subjectEnd)
    {
        var capture = input[captureStart..captureEnd];
        if (!caseless)
        {
            if (subjectStart <= input.Length - capture.Length &&
                input.Slice(subjectStart, capture.Length).SequenceEqual(capture))
            {
                subjectEnd = subjectStart + capture.Length;
                return true;
            }

            subjectEnd = subjectStart;
            return false;
        }

        var captureIndex = captureStart;
        var subjectIndex = subjectStart;
        while (captureIndex < captureEnd)
        {
            if (subjectIndex >= input.Length ||
                Rune.DecodeFromUtf8(input[captureIndex..captureEnd], out var capturedRune, out var capturedWidth) != OperationStatus.Done ||
                Rune.DecodeFromUtf8(input[subjectIndex..], out var subjectRune, out var subjectWidth) != OperationStatus.Done ||
                !Pcre2CharacterSemantics.Equals(capturedRune, subjectRune, true))
            {
                subjectEnd = subjectStart;
                return false;
            }

            captureIndex += capturedWidth;
            subjectIndex += subjectWidth;
        }

        subjectEnd = subjectIndex;
        return true;
    }

    private static Pcre2CaptureByteRange[] MaterializeCaptures(
        int captureSlotCount,
        int matchStart,
        int matchEnd,
        ReadOnlySpan<int> captureStarts,
        ReadOnlySpan<int> captureEnds)
    {
        var captures = new Pcre2CaptureByteRange[captureSlotCount];
        captures[0] = new Pcre2CaptureByteRange(true, matchStart, matchEnd);
        for (var slot = 1; slot < captureSlotCount; slot++)
        {
            var start = captureStarts[slot];
            var end = captureEnds[slot];
            captures[slot] = start >= 0 && end >= start
                ? new Pcre2CaptureByteRange(true, start, end)
                : new Pcre2CaptureByteRange(false, 0, 0);
        }

        return captures;
    }
}
