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
        int minimumByteLength,
        byte? leadingAsciiByte)
    {
        Instructions = instructions;
        Request = request;
        RepeatCount = repeatCount;
        MinimumByteLength = minimumByteLength;
        LeadingAsciiByte = leadingAsciiByte;
    }

    internal Pcre2BacktrackingInstruction[] Instructions { get; }

    internal Pcre2CompileRequest Request { get; }

    internal int RepeatCount { get; }

    internal int MinimumByteLength { get; }

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

    internal int Minimum { get; }

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

        var lowerer = new Pcre2BacktrackingLowerer();
        var instructions = lowerer.Lower(root);
        return new Pcre2CompiledBacktrackingOutcome(
            new Pcre2BacktrackingSyntaxTree(root),
            new Pcre2BacktrackingProgram(
                instructions,
                request,
                lowerer.RepeatCount,
                Pcre2BacktrackingAnalysis.GetMinimumByteLength(root),
                Pcre2BacktrackingAnalysis.GetLeadingAsciiByte(root)));
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

    internal Pcre2BacktrackingParser(Pcre2CompileRequest request)
    {
        _request = request;
        _pattern = request.Pattern;
        _options = GetInitialOptions(request.Options);
        _ungreedy = (request.Options & Pcre2CompileOptions.Ungreedy) != 0;
        _noAutoCapture = (request.Options & Pcre2CompileOptions.NoAutoCapture) != 0;
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
        if (_pattern[_offset] == '(')
        {
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

            Pcre2CharacterOptions flag;
            switch (ch)
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
                    flag = Pcre2CharacterOptions.None;
                    noAutoCapture = enabling;
                    break;
                case 'U':
                    flag = Pcre2CharacterOptions.None;
                    ungreedy = enabling;
                    break;
                default:
                    return false;
            }

            sawOption = true;
            options = enabling ? options | flag : options & ~flag;
            cursor++;
        }

        return false;
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
    private int _repeatCount;

    internal int RepeatCount => _repeatCount;

    internal Pcre2BacktrackingInstruction[] Lower(IPcre2BacktrackingNode root)
    {
        EmitNode(root);
        _instructions.Add(Pcre2BacktrackingInstruction.CreateAccept());
        return [.. _instructions];
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
            _ => 0,
        };
    }

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
    Pcre2BacktrackingResumeAction ResumeAction,
    int RepeatId,
    int RepeatCount,
    int RepeatPosition);

internal readonly record struct Pcre2RepeatMutation(
    int RepeatId,
    int PreviousCount,
    int PreviousPosition);

internal static class Pcre2BacktrackingRunner
{
    internal static Pcre2CharacterMatch Match(
        Pcre2BacktrackingProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
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
            if (TryMatchAt(program, bytes, candidate, start.Value, matchOptions, ref budget, out var end))
            {
                return Pcre2CharacterMatch.Create(candidate, end);
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

        return Pcre2CharacterMatch.NoMatch;
    }

    private static bool TryMatchAt(
        Pcre2BacktrackingProgram program,
        ReadOnlySpan<byte> input,
        int candidate,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget,
        out int end)
    {
        var frameLimit = budget.Limits.DepthLimit == 0
            ? int.MaxValue
            : (int)Math.Min(budget.Limits.DepthLimit, int.MaxValue);
        var frames = new Utf8PooledStateStack<Pcre2BacktrackingFrame>(frameLimit);
        var mutations = new Utf8PooledStateStack<Pcre2RepeatMutation>(int.MaxValue);
        var rentedCounts = program.RepeatCount == 0 ? null : ArrayPool<int>.Shared.Rent(program.RepeatCount);
        var rentedPositions = program.RepeatCount == 0 ? null : ArrayPool<int>.Shared.Rent(program.RepeatCount);
        var counts = rentedCounts is null ? Span<int>.Empty : rentedCounts.AsSpan(0, program.RepeatCount);
        var positions = rentedPositions is null ? Span<int>.Empty : rentedPositions.AsSpan(0, program.RepeatCount);
        counts.Fill(-1);
        positions.Fill(-1);
        budget.ChargeHeap((ulong)program.RepeatCount * 8UL);

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
                                mutations.Count,
                                Pcre2BacktrackingResumeAction.None,
                                0,
                                0,
                                0),
                            mutations.Count,
                            program.RepeatCount,
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
                            SetRepeat(repeatId, 0, -1, counts, positions, ref mutations, ref budget);
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
                                            mutations.Count,
                                            Pcre2BacktrackingResumeAction.None,
                                            0,
                                            0,
                                            0),
                                        mutations.Count,
                                        program.RepeatCount,
                                        ref budget);
                                }
                                else
                                {
                                    PushFrame(
                                        ref frames,
                                        new Pcre2BacktrackingFrame(
                                            instruction.PrimaryTarget,
                                            inputIndex,
                                            mutations.Count,
                                            Pcre2BacktrackingResumeAction.EnterRepeat,
                                            repeatId,
                                            count + 1,
                                            inputIndex),
                                        mutations.Count,
                                        program.RepeatCount,
                                        ref budget);
                                    instructionIndex = instruction.SecondaryTarget;
                                    continue;
                                }
                            }

                            SetRepeat(repeatId, count + 1, inputIndex, counts, positions, ref mutations, ref budget);
                            instructionIndex = instruction.PrimaryTarget;
                            continue;
                        }

                        instructionIndex = instruction.SecondaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.RepeatEnd:
                        instructionIndex = instruction.PrimaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.RepeatExit:
                        SetRepeat(instruction.RepeatId, -1, -1, counts, positions, ref mutations, ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Accept:
                        var endAnchored = (program.Request.Options & Pcre2CompileOptions.EndAnchored) != 0 ||
                            (matchOptions & Pcre2MatchOptions.EndAnchored) != 0;
                        var emptyDisallowed = (matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && inputIndex == candidate ||
                            (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0 && candidate == firstMatchingPosition && inputIndex == candidate;
                        if ((!endAnchored || inputIndex == input.Length) && !emptyDisallowed)
                        {
                            end = inputIndex;
                            return true;
                        }
                        break;

                    default:
                        break;
                }

                if (!TryResume(ref frames, ref mutations, counts, positions, ref budget, out instructionIndex, out inputIndex))
                {
                    end = 0;
                    return false;
                }
            }
        }
        finally
        {
            mutations.Dispose();
            frames.Dispose();
            if (rentedCounts is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCounts);
            }
            if (rentedPositions is not null)
            {
                ArrayPool<int>.Shared.Return(rentedPositions);
            }
        }
    }

    private static void PushFrame(
        ref Utf8PooledStateStack<Pcre2BacktrackingFrame> frames,
        Pcre2BacktrackingFrame frame,
        int mutationCount,
        int repeatCount,
        ref Pcre2ResourceBudget budget)
    {
        var nextDepth = checked(frames.Count + 1);
        var estimatedHeapBytes = checked(
            (ulong)nextDepth * 32UL +
            (ulong)mutationCount * 12UL +
            (ulong)repeatCount * 8UL);
        budget.ChargeFrame((uint)nextDepth, estimatedHeapBytes);
        if (!frames.TryPush(frame))
        {
            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", "DepthLimit");
        }
    }

    private static bool TryResume(
        ref Utf8PooledStateStack<Pcre2BacktrackingFrame> frames,
        ref Utf8PooledStateStack<Pcre2RepeatMutation> mutations,
        Span<int> counts,
        Span<int> positions,
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
        RollbackRepeats(frame.RepeatCheckpoint, ref mutations, counts, positions);
        if (frame.ResumeAction == Pcre2BacktrackingResumeAction.EnterRepeat)
        {
            SetRepeat(
                frame.RepeatId,
                frame.RepeatCount,
                frame.RepeatPosition,
                counts,
                positions,
                ref mutations,
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
}
