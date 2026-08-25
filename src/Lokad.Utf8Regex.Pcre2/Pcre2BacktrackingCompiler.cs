using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

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
        Pcre2BacktrackingProgram program,
        Pcre2BacktrackingProgram captureFreeProgram)
    {
        SyntaxTree = syntaxTree;
        Program = program;
        CaptureFreeProgram = captureFreeProgram;
    }

    internal Pcre2BacktrackingSyntaxTree SyntaxTree { get; }

    internal Pcre2BacktrackingProgram Program { get; }

    internal Pcre2BacktrackingProgram CaptureFreeProgram { get; }
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
    SubroutineCall = 8,
    Conditional = 9,
    Atomic = 10,
    ControlVerb = 11,
    MatchBoundaryReset = 12,
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

internal sealed class Pcre2SubroutineCallBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2SubroutineCallBacktrackingNode(Pcre2BackreferenceTarget target)
    {
        Target = target;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.SubroutineCall;

    internal Pcre2BackreferenceTarget Target { get; }
}

internal enum Pcre2BacktrackingConditionKind : byte
{
    CaptureSet = 0,
    Assertion = 1,
    RecursionAny = 2,
    RecursionSlot = 3,
}

internal sealed class Pcre2ConditionalBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2ConditionalBacktrackingNode(
        Pcre2BacktrackingConditionKind conditionKind,
        Pcre2BackreferenceTarget target,
        Pcre2AssertionBacktrackingNode? assertion,
        IPcre2BacktrackingNode yesBranch,
        IPcre2BacktrackingNode noBranch)
    {
        ConditionKind = conditionKind;
        Target = target;
        Assertion = assertion;
        YesBranch = yesBranch;
        NoBranch = noBranch;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Conditional;

    internal Pcre2BacktrackingConditionKind ConditionKind { get; }

    internal Pcre2BackreferenceTarget Target { get; }

    internal Pcre2AssertionBacktrackingNode? Assertion { get; }

    internal IPcre2BacktrackingNode YesBranch { get; }

    internal IPcre2BacktrackingNode NoBranch { get; }
}

internal sealed class Pcre2AtomicBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2AtomicBacktrackingNode(IPcre2BacktrackingNode body)
    {
        Body = body;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.Atomic;

    internal IPcre2BacktrackingNode Body { get; }
}

internal enum Pcre2ControlVerbKind : byte
{
    Accept = 0,
    Fail = 1,
    Mark = 2,
    Commit = 3,
    Prune = 4,
    Skip = 5,
    Then = 6,
}

internal sealed class Pcre2ControlVerbBacktrackingNode : IPcre2BacktrackingNode
{
    internal Pcre2ControlVerbBacktrackingNode(Pcre2ControlVerbKind verbKind, string name)
    {
        VerbKind = verbKind;
        Name = name;
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.ControlVerb;

    internal Pcre2ControlVerbKind VerbKind { get; }

    internal string Name { get; }
}

internal sealed class Pcre2MatchBoundaryResetBacktrackingNode : IPcre2BacktrackingNode
{
    internal static Pcre2MatchBoundaryResetBacktrackingNode Instance { get; } = new();

    private Pcre2MatchBoundaryResetBacktrackingNode()
    {
    }

    public Pcre2BacktrackingNodeKind Kind => Pcre2BacktrackingNodeKind.MatchBoundaryReset;
}

internal enum Pcre2RepeatPreference : byte
{
    Greedy = 0,
    Lazy = 1,
    Possessive = 2,
}

internal sealed class Pcre2BackreferenceSlotSet
{
    internal Pcre2BackreferenceSlotSet(int[] slots)
    {
        Slots = slots;
    }

    internal int[] Slots { get; }
}

internal sealed class Pcre2BacktrackingCondition
{
    internal Pcre2BacktrackingCondition(
        Pcre2BacktrackingConditionKind kind,
        int[] captureSlots,
        int assertionProgramId,
        Pcre2AssertionKind assertionKind,
        int subroutineSlot)
    {
        Kind = kind;
        CaptureSlots = captureSlots;
        AssertionProgramId = assertionProgramId;
        AssertionKind = assertionKind;
        SubroutineSlot = subroutineSlot;
    }

    internal Pcre2BacktrackingConditionKind Kind { get; }

    internal int[] CaptureSlots { get; }

    internal int AssertionProgramId { get; }

    internal Pcre2AssertionKind AssertionKind { get; }

    internal int SubroutineSlot { get; }
}

internal sealed class Pcre2BacktrackingProgram
{
    internal Pcre2BacktrackingProgram(
        Pcre2BacktrackingInstruction[] instructions,
        Pcre2CompileRequest request,
        int repeatCount,
        int captureSlotCount,
        bool requiresCaptureState,
        bool hasCaptureWrites,
        bool usesBacktrackingControlVerbs,
        bool usesMatchBoundaryReset,
        bool mayReportNonMonotoneMatchOffsets,
        bool mayThrowDeferredLookaroundReset,
        bool suppressesUnresetEmptyMatches,
        bool restrictsSearchToInitialCandidate,
        int autoPossessiveRepeatCount,
        Pcre2BacktrackingProgram[] assertionPrograms,
        Pcre2BackreferenceSlotSet[] backreferenceSlotSets,
        Pcre2BacktrackingCondition[] conditions,
        int[] subroutineTargets,
        bool[] subroutinePreservesUnsetCaptures,
        string[] markNames,
        string[] groupNames,
        Pcre2NameEntry[] nameEntries,
        int minimumByteLength,
        int analyzedMinimumByteLength,
        int minimumScalarLength,
        int maximumScalarLength,
        byte? leadingAsciiByte,
        bool leadingExtendedGraphemeCluster,
        bool usesCodeUnit)
    {
        Instructions = instructions;
        Request = request;
        RepeatCount = repeatCount;
        CaptureSlotCount = captureSlotCount;
        RequiresCaptureState = requiresCaptureState;
        HasCaptureWrites = hasCaptureWrites;
        UsesBacktrackingControlVerbs = usesBacktrackingControlVerbs;
        UsesMatchBoundaryReset = usesMatchBoundaryReset ||
            instructions.Any(static instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.MatchBoundaryReset) ||
            assertionPrograms.Any(static program => program.UsesMatchBoundaryReset);
        MayReportNonMonotoneMatchOffsets = mayReportNonMonotoneMatchOffsets;
        MayThrowDeferredLookaroundReset = mayThrowDeferredLookaroundReset;
        SuppressesUnresetEmptyMatches = suppressesUnresetEmptyMatches;
        RestrictsSearchToInitialCandidate = restrictsSearchToInitialCandidate;
        AutoPossessiveRepeatCount = autoPossessiveRepeatCount;
        AssertionPrograms = assertionPrograms;
        BackreferenceSlotSets = backreferenceSlotSets;
        Conditions = conditions;
        SubroutineTargets = subroutineTargets;
        SubroutinePreservesUnsetCaptures = subroutinePreservesUnsetCaptures;
        MarkNames = markNames;
        GroupNames = groupNames;
        NameEntries = nameEntries;
        MinimumByteLength = minimumByteLength;
        AnalyzedMinimumByteLength = analyzedMinimumByteLength;
        MinimumScalarLength = minimumScalarLength;
        MaximumScalarLength = maximumScalarLength;
        LeadingAsciiByte = leadingAsciiByte;
        LeadingExtendedGraphemeCluster = leadingExtendedGraphemeCluster;
        UsesCodeUnit = usesCodeUnit;
    }

    internal Pcre2BacktrackingInstruction[] Instructions { get; }

    internal Pcre2CompileRequest Request { get; }

    internal int RepeatCount { get; }

    internal int CaptureSlotCount { get; }

    internal bool RequiresCaptureState { get; }

    internal bool HasCaptureWrites { get; }

    internal bool UsesBacktrackingControlVerbs { get; }

    internal bool UsesMatchBoundaryReset { get; }

    internal bool MayReportNonMonotoneMatchOffsets { get; }

    internal bool MayThrowDeferredLookaroundReset { get; }

    internal bool SuppressesUnresetEmptyMatches { get; }

    internal bool RestrictsSearchToInitialCandidate { get; }

    internal int AutoPossessiveRepeatCount { get; }

    internal Pcre2BacktrackingProgram[] AssertionPrograms { get; }

    internal Pcre2BackreferenceSlotSet[] BackreferenceSlotSets { get; }

    internal Pcre2BacktrackingCondition[] Conditions { get; }

    internal int[] SubroutineTargets { get; }

    internal bool[] SubroutinePreservesUnsetCaptures { get; }

    internal string[] MarkNames { get; }

    internal string[] GroupNames { get; }

    internal Pcre2NameEntry[] NameEntries { get; }

    internal int MinimumByteLength { get; }

    internal int AnalyzedMinimumByteLength { get; }

    internal int MinimumScalarLength { get; }

    internal int MaximumScalarLength { get; }

    internal byte? LeadingAsciiByte { get; }

    internal bool LeadingExtendedGraphemeCluster { get; }

    internal bool UsesCodeUnit { get; }
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
    BackreferenceSlotSet = 11,
    SubroutineCall = 12,
    SubroutineReturn = 13,
    Conditional = 14,
    AtomicStart = 15,
    AtomicEnd = 16,
    ControlAccept = 17,
    ControlFail = 18,
    ControlMark = 19,
    ControlDeferred = 20,
    MatchBoundaryReset = 21,
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

    internal int BackreferenceSlotSetId => RepeatId;

    internal int SubroutineSlot => RepeatId;

    internal int ConditionId => RepeatId;

    internal Pcre2ControlVerbKind ControlVerb => (Pcre2ControlVerbKind)Minimum;

    internal int ControlNameId => RepeatId;

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

    internal static Pcre2BacktrackingInstruction CreateBackreferenceSlotSet(
        int backreferenceSlotSetId,
        Pcre2CharacterOptions options) =>
        new(Pcre2BacktrackingInstructionKind.BackreferenceSlotSet, default, 0, 0, backreferenceSlotSetId, (int)options, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateSubroutineCall(int slot) =>
        new(Pcre2BacktrackingInstructionKind.SubroutineCall, default, 0, 0, slot, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateSubroutineReturn() =>
        new(Pcre2BacktrackingInstructionKind.SubroutineReturn, default, 0, 0, 0, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateConditional(
        int conditionId,
        int yesTarget,
        int noTarget) =>
        new(Pcre2BacktrackingInstructionKind.Conditional, default, yesTarget, noTarget, conditionId, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateAtomicStart() =>
        new(Pcre2BacktrackingInstructionKind.AtomicStart, default, 0, 0, 0, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateAtomicEnd() =>
        new(Pcre2BacktrackingInstructionKind.AtomicEnd, default, 0, 0, 0, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateControlAccept(int nameId) =>
        new(Pcre2BacktrackingInstructionKind.ControlAccept, default, 0, 0, nameId, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateControlFail(int nameId) =>
        new(Pcre2BacktrackingInstructionKind.ControlFail, default, 0, 0, nameId, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateControlMark(int nameId) =>
        new(Pcre2BacktrackingInstructionKind.ControlMark, default, 0, 0, nameId, 0, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateControlDeferred(
        Pcre2ControlVerbKind verb,
        int nameId,
        int thenTarget) =>
        new(Pcre2BacktrackingInstructionKind.ControlDeferred, default, thenTarget, 0, nameId, (int)verb, 0, Pcre2RepeatPreference.Greedy);

    internal static Pcre2BacktrackingInstruction CreateMatchBoundaryReset() =>
        new(Pcre2BacktrackingInstructionKind.MatchBoundaryReset, default, 0, 0, 0, 0, 0, Pcre2RepeatPreference.Greedy);
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
        var lowerer = new Pcre2BacktrackingLowerer(
            parser.CaptureCount,
            nameEntries,
            parser.CaptureDefinitions,
            request,
            groupNames,
            emitCaptureWrites: true);
        var program = lowerer.Lower(root);
        var captureFreeProgram = program;
        if (!program.RequiresCaptureState && program.HasCaptureWrites)
        {
            captureFreeProgram = new Pcre2BacktrackingLowerer(
                parser.CaptureCount,
                nameEntries,
                parser.CaptureDefinitions,
                request,
                groupNames,
                emitCaptureWrites: false).Lower(root);
        }

        return new Pcre2CompiledBacktrackingOutcome(
            new Pcre2BacktrackingSyntaxTree(root),
            program,
            captureFreeProgram);
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

    internal Pcre2NameEntry[] NameEntries => [.. _nameEntries];

    internal IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> CaptureDefinitions => _captureDefinitions;

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

            var literalStart = _offset;
            if (Rune.DecodeFromUtf16(_pattern.AsSpan(_offset), out var literal, out var length) != OperationStatus.Done)
            {
                children = [];
                return false;
            }

            _offset += length;
            result.Add(new Pcre2TokenBacktrackingNode(Pcre2CharacterToken.CreateLiteral(
                literal,
                _options,
                literalStart,
                length)));
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
            name.Length > 255)
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
            if (!TryParseAlternation(true, out _) || _offset >= _pattern.Length || _pattern[_offset] != ')')
            {
                return false;
            }

            _offset++;
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
        _offset += openingLength;
        if (!TryParseAlternation(true, out var body) || _offset >= _pattern.Length || _pattern[_offset] != ')')
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
        _sawControlFlow = true;
        _offset += 3;
        var captureBase = _captureCount;
        var maximumCaptureCount = captureBase;
        var alternatives = new List<IPcre2BacktrackingNode>();
        var outerOptions = _options;
        var outerUngreedy = _ungreedy;
        var outerNoAutoCapture = _noAutoCapture;
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
        if (_captureSlotNames.TryGetValue(slot, out var existingSlotName) &&
            !string.Equals(existingSlotName, name, StringComparison.Ordinal))
        {
            throw new Pcre2CompileException(
                "A branch-reset capture number cannot have more than one name.",
                Pcre2ErrorKind.InvalidAfterParensQuery);
        }

        foreach (var entry in _nameEntries)
        {
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
        var allowDuplicateNames = _allowDuplicateNames;
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
                _allowDuplicateNames = allowDuplicateNames;
                _offset = cursor + 1;
                return true;
            }

            if (!TryApplyOption(ref options, ref ungreedy, ref noAutoCapture, ch, enabling))
            {
                if (ch == 'J')
                {
                    allowDuplicateNames = enabling;
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

}

internal sealed class Pcre2BacktrackingLowerer
{
    private readonly List<Pcre2BacktrackingInstruction> _instructions = [];
    private readonly List<Pcre2BacktrackingProgram> _assertionPrograms = [];
    private readonly List<Pcre2BackreferenceSlotSet> _backreferenceSlotSets = [];
    private readonly List<Pcre2BacktrackingCondition> _conditions = [];
    private readonly List<string> _markNames = [];
    private readonly Dictionary<string, int> _markNameIds = new(StringComparer.Ordinal);
    private readonly int _captureCount;
    private readonly Pcre2NameEntry[] _nameEntries;
    private readonly IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> _captureDefinitions;
    private readonly Dictionary<string, int[]> _nameSlots;
    private readonly Pcre2CompileRequest _request;
    private readonly string[] _groupNames;
    private readonly bool _emitCaptureWrites;
    private int _repeatCount;
    private bool _requiresCaptureState;
    private bool _hasCaptureWrites;
    private bool _usesBacktrackingControlVerbs;
    private bool _usesMatchBoundaryReset;
    private bool _mayReportNonMonotoneMatchOffsets;
    private bool _mayThrowDeferredLookaroundReset;
    private bool _hasSubroutineCalls;
    private int _autoPossessiveRepeatCount;
    private List<int>? _thenPatches;

    internal Pcre2BacktrackingLowerer(
        int captureCount,
        Pcre2NameEntry[] nameEntries,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        Pcre2CompileRequest request,
        string[] groupNames,
        bool emitCaptureWrites)
    {
        _captureCount = captureCount;
        _nameEntries = nameEntries;
        _captureDefinitions = captureDefinitions;
        _nameSlots = nameEntries
            .GroupBy(static entry => entry.Name, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static entry => entry.Number).ToArray(),
                StringComparer.Ordinal);
        _request = request;
        _groupNames = groupNames;
        _emitCaptureWrites = emitCaptureWrites;
    }

    internal Pcre2BacktrackingProgram Lower(IPcre2BacktrackingNode root)
    {
        EmitNode(root, null);
        _instructions.Add(Pcre2BacktrackingInstruction.CreateAccept());
        var subroutineTargets = Array.Empty<int>();
        var subroutinePreservesUnsetCaptures = Array.Empty<bool>();
        if (_hasSubroutineCalls)
        {
            subroutineTargets = new int[_captureCount + 1];
            subroutinePreservesUnsetCaptures = new bool[_captureCount + 1];
            subroutineTargets.AsSpan().Fill(-1);
            subroutineTargets[0] = _instructions.Count;
            subroutinePreservesUnsetCaptures[0] = !ContainsDeferredControlVerb(root);
            EmitNode(root, null);
            _instructions.Add(Pcre2BacktrackingInstruction.CreateSubroutineReturn());
            foreach (var definition in _captureDefinitions.OrderBy(static pair => pair.Key))
            {
                subroutineTargets[definition.Key] = _instructions.Count;
                subroutinePreservesUnsetCaptures[definition.Key] = !ContainsDeferredControlVerb(definition.Value);
                EmitNode(definition.Value, null);
                _instructions.Add(Pcre2BacktrackingInstruction.CreateSubroutineReturn());
            }
        }

        return new Pcre2BacktrackingProgram(
            [.. _instructions],
            _request,
            _repeatCount,
            _captureCount + 1,
            _requiresCaptureState,
            _hasCaptureWrites,
            _usesBacktrackingControlVerbs,
            _usesMatchBoundaryReset,
            _mayReportNonMonotoneMatchOffsets,
            _mayThrowDeferredLookaroundReset,
            SuppressesUnresetEmptyMatches(root),
            Pcre2BacktrackingAnalysis.RestrictsSearchToInitialCandidate(root),
            _autoPossessiveRepeatCount,
            [.. _assertionPrograms],
            [.. _backreferenceSlotSets],
            [.. _conditions],
            subroutineTargets,
            subroutinePreservesUnsetCaptures,
            [.. _markNames],
            _groupNames,
            _nameEntries,
            Pcre2BacktrackingAnalysis.GetMinimumByteLength(root),
            Pcre2BacktrackingAnalysis.GetMinimumByteLength(root, _captureDefinitions, _nameSlots),
            Pcre2BacktrackingAnalysis.GetMinimumScalarLength(root),
            Pcre2BacktrackingAnalysis.GetMaximumScalarLength(root),
            Pcre2BacktrackingAnalysis.GetLeadingAsciiByte(root),
            Pcre2BacktrackingAnalysis.StartsWithExtendedGraphemeCluster(root),
            Pcre2BacktrackingAnalysis.ContainsCodeUnit(root));
    }

    private void EmitNode(
        IPcre2BacktrackingNode node,
        Pcre2CharacterToken? requiredFollowingLiteral)
    {
        bool CanAutoPossess(
            Pcre2RepeatBacktrackingNode repeat,
            Pcre2CharacterToken followingLiteral)
        {
            // The current lowering uses an atomic checkpoint. Keep metered plans on
            // their original instruction/depth accounting until commitment has a
            // dedicated instruction that preserves those observable limits.
            if (!Pcre2GlobalOperationDriver.HasUnmeteredExecution(_request) ||
                repeat.Maximum == 0 ||
                repeat.Body is not Pcre2TokenBacktrackingNode repeated ||
                followingLiteral.Kind != Pcre2CharacterTokenKind.Literal ||
                followingLiteral.Options != Pcre2CharacterOptions.None)
            {
                return false;
            }

            var following = followingLiteral.Literal;
            var caseless = (repeated.Token.Options & Pcre2CharacterOptions.Caseless) != 0;
            var ucp = (_request.Options & Pcre2CompileOptions.Ucp) != 0;
            return repeated.Token.Kind switch
            {
                Pcre2CharacterTokenKind.Literal =>
                    !Pcre2CharacterSemantics.Equals(repeated.Token.Literal, following, caseless),
                Pcre2CharacterTokenKind.CharacterClass =>
                    !repeated.Token.CharacterClass.Matches(following, ucp, caseless),
                _ => false,
            };
        }

        static bool TryGetRequiredLeadingLiteral(
            IPcre2BacktrackingNode candidate,
            out Pcre2CharacterToken literal)
        {
            switch (candidate)
            {
                case Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Literal,
                        Options: Pcre2CharacterOptions.None,
                    } token,
                }:
                    literal = token;
                    return true;
                case Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence:
                    return TryGetRequiredLeadingLiteral(sequence.Children[0], out literal);
                case Pcre2AlternationBacktrackingNode alternation:
                    Pcre2CharacterToken? common = null;
                    foreach (var alternative in alternation.Alternatives)
                    {
                        if (!TryGetRequiredLeadingLiteral(alternative, out var branchLiteral) ||
                            common is { } value && value.Literal != branchLiteral.Literal)
                        {
                            literal = default;
                            return false;
                        }

                        common = branchLiteral;
                    }

                    if (common is { } commonLiteral)
                    {
                        literal = commonLiteral;
                        return true;
                    }
                    break;
                case Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat:
                    return TryGetRequiredLeadingLiteral(repeat.Body, out literal);
                case Pcre2CaptureBacktrackingNode capture:
                    return TryGetRequiredLeadingLiteral(capture.Body, out literal);
                case Pcre2AtomicBacktrackingNode atomic:
                    return TryGetRequiredLeadingLiteral(atomic.Body, out literal);
            }

            literal = default;
            return false;
        }

        switch (node)
        {
            case Pcre2EmptyBacktrackingNode:
                return;
            case Pcre2TokenBacktrackingNode token:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateToken(token.Token));
                return;
            case Pcre2SequenceBacktrackingNode sequence:
                for (var index = 0; index < sequence.Children.Length; index++)
                {
                    Pcre2CharacterToken? childFollowingLiteral = null;
                    if (index + 1 < sequence.Children.Length)
                    {
                        if (TryGetRequiredLeadingLiteral(sequence.Children[index + 1], out var nextLiteral))
                        {
                            childFollowingLiteral = nextLiteral;
                        }
                    }
                    else
                    {
                        childFollowingLiteral = requiredFollowingLiteral;
                    }

                    EmitNode(sequence.Children[index], childFollowingLiteral);
                }
                return;
            case Pcre2AlternationBacktrackingNode alternation:
                EmitAlternation(alternation, requiredFollowingLiteral);
                return;
            case Pcre2RepeatBacktrackingNode repeat:
                var autoPossessive = repeat.Preference == Pcre2RepeatPreference.Greedy &&
                    requiredFollowingLiteral is { } followingLiteral &&
                    CanAutoPossess(repeat, followingLiteral);
                if (repeat.Preference == Pcre2RepeatPreference.Possessive || autoPossessive)
                {
                    _autoPossessiveRepeatCount += autoPossessive ? 1 : 0;
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateAtomicStart());
                    EmitRepeat(new Pcre2RepeatBacktrackingNode(
                        repeat.Body,
                        repeat.Minimum,
                        repeat.Maximum,
                        Pcre2RepeatPreference.Greedy));
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateAtomicEnd());
                }
                else
                {
                    EmitRepeat(repeat);
                }
                return;
            case Pcre2CaptureBacktrackingNode capture:
                if (_emitCaptureWrites)
                {
                    _hasCaptureWrites = true;
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureStart(capture.Slot));
                }
                EmitNode(capture.Body, requiredFollowingLiteral);
                if (_emitCaptureWrites)
                {
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureEnd(capture.Slot));
                }
                return;
            case Pcre2BackreferenceBacktrackingNode backreference:
                _requiresCaptureState = true;
                EmitBackreference(backreference);
                return;
            case Pcre2AssertionBacktrackingNode assertion:
                var assertionLowerer = new Pcre2BacktrackingLowerer(
                    _captureCount,
                    _nameEntries,
                    _captureDefinitions,
                    _request,
                    _groupNames,
                    _emitCaptureWrites);
                var assertionProgramId = _assertionPrograms.Count;
                var assertionProgram = assertionLowerer.Lower(assertion.Body);
                _assertionPrograms.Add(assertionProgram);
                _requiresCaptureState |= assertionProgram.RequiresCaptureState;
                _hasCaptureWrites |= assertionProgram.HasCaptureWrites;
                _usesBacktrackingControlVerbs |= assertionProgram.UsesBacktrackingControlVerbs;
                _usesMatchBoundaryReset |= assertionProgram.UsesMatchBoundaryReset;
                _mayReportNonMonotoneMatchOffsets |= ContainsMatchBoundaryReset(assertion.Body);
                _mayThrowDeferredLookaroundReset |=
                    assertionProgram.UsesMatchBoundaryReset && !ContainsMatchBoundaryReset(assertion.Body);
                foreach (var markName in assertionProgram.MarkNames)
                {
                    _ = GetMarkNameId(markName);
                }
                _instructions.Add(Pcre2BacktrackingInstruction.CreateAssertion(
                    assertionProgramId,
                    assertion.AssertionKind));
                return;
            case Pcre2SubroutineCallBacktrackingNode subroutineCall:
                _hasSubroutineCalls = true;
                _instructions.Add(Pcre2BacktrackingInstruction.CreateSubroutineCall(
                    ResolveSubroutine(subroutineCall.Target)));
                return;
            case Pcre2ConditionalBacktrackingNode conditional:
                EmitConditional(conditional);
                return;
            case Pcre2AtomicBacktrackingNode atomic:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateAtomicStart());
                EmitNode(atomic.Body, null);
                _instructions.Add(Pcre2BacktrackingInstruction.CreateAtomicEnd());
                return;
            case Pcre2ControlVerbBacktrackingNode controlVerb:
                EmitControlVerb(controlVerb);
                return;
            case Pcre2MatchBoundaryResetBacktrackingNode:
                _usesMatchBoundaryReset = true;
                _instructions.Add(Pcre2BacktrackingInstruction.CreateMatchBoundaryReset());
                return;
            default:
                throw new InvalidOperationException("The PCRE2 backtracking syntax node is not lowerable.");
        }
    }

    private void EmitAlternation(
        Pcre2AlternationBacktrackingNode alternation,
        Pcre2CharacterToken? requiredFollowingLiteral)
    {
        var parentThenPatches = _thenPatches;
        var exitJumps = new List<int>(alternation.Alternatives.Length - 1);
        for (var i = 0; i < alternation.Alternatives.Length - 1; i++)
        {
            var branchThenPatches = new List<int>();
            _thenPatches = branchThenPatches;
            var splitIndex = _instructions.Count;
            _instructions.Add(default);
            var primaryTarget = _instructions.Count;
            EmitNode(alternation.Alternatives[i], requiredFollowingLiteral);
            var jumpIndex = _instructions.Count;
            _instructions.Add(default);
            exitJumps.Add(jumpIndex);
            var secondaryTarget = _instructions.Count;
            _instructions[splitIndex] = Pcre2BacktrackingInstruction.CreateSplit(primaryTarget, secondaryTarget);
            foreach (var thenPatch in branchThenPatches)
            {
                var instruction = _instructions[thenPatch];
                _instructions[thenPatch] = Pcre2BacktrackingInstruction.CreateControlDeferred(
                    Pcre2ControlVerbKind.Then,
                    instruction.ControlNameId,
                    secondaryTarget);
            }
        }

        _thenPatches = parentThenPatches;
        EmitNode(alternation.Alternatives[^1], requiredFollowingLiteral);
        var exitTarget = _instructions.Count;
        foreach (var jumpIndex in exitJumps)
        {
            _instructions[jumpIndex] = Pcre2BacktrackingInstruction.CreateJump(exitTarget);
        }

        _thenPatches = parentThenPatches;
    }

    private void EmitControlVerb(Pcre2ControlVerbBacktrackingNode controlVerb)
    {
        _usesBacktrackingControlVerbs = true;
        var nameId = GetMarkNameId(controlVerb.Name);
        switch (controlVerb.VerbKind)
        {
            case Pcre2ControlVerbKind.Accept:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlAccept(nameId));
                return;
            case Pcre2ControlVerbKind.Fail:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlFail(nameId));
                return;
            case Pcre2ControlVerbKind.Mark:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlMark(nameId));
                return;
            case Pcre2ControlVerbKind.Then:
                var instructionIndex = _instructions.Count;
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlDeferred(
                    controlVerb.VerbKind,
                    nameId,
                    -1));
                _thenPatches?.Add(instructionIndex);
                return;
            default:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlDeferred(
                    controlVerb.VerbKind,
                    nameId,
                    -1));
                return;
        }
    }

    private int GetMarkNameId(string name)
    {
        if (name.Length == 0)
        {
            return -1;
        }

        if (_markNameIds.TryGetValue(name, out var id))
        {
            return id;
        }

        id = _markNames.Count;
        _markNames.Add(name);
        _markNameIds.Add(name, id);
        return id;
    }

    private void EmitRepeat(Pcre2RepeatBacktrackingNode repeat)
    {
        var repeatId = _repeatCount++;
        var repeatIndex = _instructions.Count;
        _instructions.Add(default);
        var bodyTarget = _instructions.Count;
        EmitNode(repeat.Body, null);
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

    private void EmitConditional(Pcre2ConditionalBacktrackingNode conditional)
    {
        var conditionId = _conditions.Count;
        _conditions.Add(CreateCondition(conditional));
        var conditionIndex = _instructions.Count;
        _instructions.Add(default);
        var yesTarget = _instructions.Count;
        EmitNode(conditional.YesBranch, null);
        var exitJump = _instructions.Count;
        _instructions.Add(default);
        var noTarget = _instructions.Count;
        EmitNode(conditional.NoBranch, null);
        var exitTarget = _instructions.Count;
        _instructions[conditionIndex] = Pcre2BacktrackingInstruction.CreateConditional(
            conditionId,
            yesTarget,
            noTarget);
        _instructions[exitJump] = Pcre2BacktrackingInstruction.CreateJump(exitTarget);
    }

    private Pcre2BacktrackingCondition CreateCondition(Pcre2ConditionalBacktrackingNode conditional)
    {
        if (conditional.ConditionKind == Pcre2BacktrackingConditionKind.Assertion)
        {
            var assertion = conditional.Assertion ??
                throw new InvalidOperationException("A PCRE2 assertion condition requires an assertion body.");
            var assertionLowerer = new Pcre2BacktrackingLowerer(
                _captureCount,
                _nameEntries,
                _captureDefinitions,
                _request,
                _groupNames,
                _emitCaptureWrites);
            var assertionProgramId = _assertionPrograms.Count;
            var assertionProgram = assertionLowerer.Lower(assertion.Body);
            _assertionPrograms.Add(assertionProgram);
            _requiresCaptureState |= assertionProgram.RequiresCaptureState;
            _hasCaptureWrites |= assertionProgram.HasCaptureWrites;
            _usesBacktrackingControlVerbs |= assertionProgram.UsesBacktrackingControlVerbs;
            _usesMatchBoundaryReset |= assertionProgram.UsesMatchBoundaryReset;
            _mayReportNonMonotoneMatchOffsets |= ContainsMatchBoundaryReset(assertion.Body);
            _mayThrowDeferredLookaroundReset |=
                assertionProgram.UsesMatchBoundaryReset && !ContainsMatchBoundaryReset(assertion.Body);
            foreach (var markName in assertionProgram.MarkNames)
            {
                _ = GetMarkNameId(markName);
            }
            return new Pcre2BacktrackingCondition(
                conditional.ConditionKind,
                [],
                assertionProgramId,
                assertion.AssertionKind,
                0);
        }

        if (conditional.ConditionKind == Pcre2BacktrackingConditionKind.RecursionAny)
        {
            return new Pcre2BacktrackingCondition(
                conditional.ConditionKind,
                [],
                0,
                default,
                0);
        }

        if (conditional.ConditionKind == Pcre2BacktrackingConditionKind.RecursionSlot)
        {
            return new Pcre2BacktrackingCondition(
                conditional.ConditionKind,
                [],
                0,
                default,
                ResolveSubroutine(conditional.Target));
        }

        var captureSlots = conditional.Target.Kind == Pcre2BackreferenceTargetKind.Named
            ? ResolveNamedSlots(conditional.Target.Name)
            : [ResolveBackreference(conditional.Target)];
        _requiresCaptureState = true;
        return new Pcre2BacktrackingCondition(
            conditional.ConditionKind,
            captureSlots,
            0,
            default,
            0);
    }

    private int[] ResolveNamedSlots(string name)
    {
        if (!_nameSlots.TryGetValue(name, out var slots))
        {
            throw new Pcre2CompileException(
                "Reference to a non-existent capturing group.",
                Pcre2ErrorKind.UnrecognizedEscape);
        }

        return slots;
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
            _ => 0,
        };
        if (slot <= 0 || slot > _captureCount)
        {
            throw new Pcre2CompileException("Reference to a non-existent capturing group.", Pcre2ErrorKind.UnrecognizedEscape);
        }

        return slot;
    }

    private void EmitBackreference(Pcre2BackreferenceBacktrackingNode backreference)
    {
        if (backreference.Target.Kind != Pcre2BackreferenceTargetKind.Named)
        {
            _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreference(
                ResolveBackreference(backreference.Target),
                backreference.Options));
            return;
        }

        if (!_nameSlots.TryGetValue(backreference.Target.Name, out var slots))
        {
            throw new Pcre2CompileException(
                "Reference to a non-existent capturing group.",
                Pcre2ErrorKind.UnrecognizedEscape);
        }

        if (slots.Length == 1)
        {
            _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreference(slots[0], backreference.Options));
            return;
        }

        var slotSetId = _backreferenceSlotSets.Count;
        _backreferenceSlotSets.Add(new Pcre2BackreferenceSlotSet(slots));
        _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreferenceSlotSet(
            slotSetId,
            backreference.Options));
    }

    private int ResolveSubroutine(Pcre2BackreferenceTarget target)
    {
        var slot = target.Kind switch
        {
            Pcre2BackreferenceTargetKind.Absolute => target.Number,
            Pcre2BackreferenceTargetKind.Relative when target.Number > 0 =>
                target.CaptureCountAtReference + target.Number,
            Pcre2BackreferenceTargetKind.Relative =>
                target.CaptureCountAtReference + target.Number + 1,
            Pcre2BackreferenceTargetKind.Named when _nameSlots.TryGetValue(target.Name, out var slots) => slots[0],
            _ => -1,
        };
        if (slot < 0 || slot > _captureCount || slot > 0 && !_captureDefinitions.ContainsKey(slot))
        {
            throw new Pcre2CompileException(
                "Reference to a non-existent subroutine.",
                Pcre2ErrorKind.InvalidAfterParensQuery);
        }

        return slot;
    }

    private static bool ContainsDeferredControlVerb(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2ControlVerbBacktrackingNode control => control.VerbKind is
            Pcre2ControlVerbKind.Commit or
            Pcre2ControlVerbKind.Prune or
            Pcre2ControlVerbKind.Skip or
            Pcre2ControlVerbKind.Then,
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(ContainsDeferredControlVerb),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(ContainsDeferredControlVerb),
        Pcre2RepeatBacktrackingNode repeat => ContainsDeferredControlVerb(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => ContainsDeferredControlVerb(capture.Body),
        Pcre2AssertionBacktrackingNode assertion => ContainsDeferredControlVerb(assertion.Body),
        Pcre2ConditionalBacktrackingNode conditional =>
            conditional.Assertion is not null && ContainsDeferredControlVerb(conditional.Assertion.Body) ||
            ContainsDeferredControlVerb(conditional.YesBranch) ||
            ContainsDeferredControlVerb(conditional.NoBranch),
        Pcre2AtomicBacktrackingNode atomic => ContainsDeferredControlVerb(atomic.Body),
        _ => false,
    };

    private static bool ContainsMatchBoundaryReset(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2MatchBoundaryResetBacktrackingNode => true,
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(ContainsMatchBoundaryReset),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(ContainsMatchBoundaryReset),
        Pcre2RepeatBacktrackingNode repeat => ContainsMatchBoundaryReset(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => ContainsMatchBoundaryReset(capture.Body),
        Pcre2AssertionBacktrackingNode assertion => ContainsMatchBoundaryReset(assertion.Body),
        Pcre2ConditionalBacktrackingNode conditional =>
            conditional.Assertion is not null && ContainsMatchBoundaryReset(conditional.Assertion.Body) ||
            ContainsMatchBoundaryReset(conditional.YesBranch) ||
            ContainsMatchBoundaryReset(conditional.NoBranch),
        Pcre2AtomicBacktrackingNode atomic => ContainsMatchBoundaryReset(atomic.Body),
        _ => false,
    };

    private static bool SuppressesUnresetEmptyMatches(IPcre2BacktrackingNode node) =>
        node is Pcre2RepeatBacktrackingNode
        {
            Minimum: 0,
        } repeat && ContainsMatchBoundaryReset(repeat.Body);
}

internal static class Pcre2BacktrackingAnalysis
{
    // This is deliberately narrower than general anchoring analysis. It proves
    // only that every successful path reaches a subject-start or first-match
    // position assertion before any input can be consumed. A failed attempt at
    // the caller's initial candidate therefore cannot be rescued by bumpalong.
    internal static bool RestrictsSearchToInitialCandidate(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2TokenBacktrackingNode { Token: var token } => token.Kind switch
        {
            Pcre2CharacterTokenKind.BeginningOfSubject or
            Pcre2CharacterTokenKind.FirstMatchingPosition => true,
            Pcre2CharacterTokenKind.BeginningOfLine =>
                (token.Options & Pcre2CharacterOptions.Multiline) == 0,
            _ => false,
        },
        Pcre2SequenceBacktrackingNode sequence => SequenceRestrictsSearch(sequence),
        Pcre2AlternationBacktrackingNode alternation =>
            alternation.Alternatives.All(RestrictsSearchToInitialCandidate),
        Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat =>
            RestrictsSearchToInitialCandidate(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => RestrictsSearchToInitialCandidate(capture.Body),
        Pcre2AtomicBacktrackingNode atomic => RestrictsSearchToInitialCandidate(atomic.Body),
        _ => false,
    };

    internal static int GetMinimumByteLength(
        IPcre2BacktrackingNode node,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots) =>
        GetMinimumByteLength(node, captureDefinitions, nameSlots, []);

    internal static int GetMinimumByteLength(IPcre2BacktrackingNode node)
    {
        return node switch
        {
            Pcre2EmptyBacktrackingNode => 0,
            Pcre2TokenBacktrackingNode token => ConsumesInput(token.Token.Kind) ? 1 : 0,
            Pcre2SequenceBacktrackingNode sequence => GetSequenceMinimum(sequence),
            Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Min(GetMinimumByteLength),
            Pcre2RepeatBacktrackingNode repeat => SaturatingMultiply(GetMinimumByteLength(repeat.Body), repeat.Minimum),
            Pcre2CaptureBacktrackingNode capture => GetMinimumByteLength(capture.Body),
            Pcre2BackreferenceBacktrackingNode => 0,
            Pcre2AssertionBacktrackingNode => 0,
            Pcre2SubroutineCallBacktrackingNode => 0,
            Pcre2ConditionalBacktrackingNode conditional => Math.Min(
                GetMinimumByteLength(conditional.YesBranch),
                GetMinimumByteLength(conditional.NoBranch)),
            Pcre2AtomicBacktrackingNode atomic => GetMinimumByteLength(atomic.Body),
            Pcre2ControlVerbBacktrackingNode => 0,
            Pcre2MatchBoundaryResetBacktrackingNode => 0,
            _ => 0,
        };
    }

    private static int GetMinimumByteLength(
        IPcre2BacktrackingNode node,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots,
        HashSet<int> activeCaptures)
    {
        switch (node)
        {
            case Pcre2EmptyBacktrackingNode:
                return 0;
            case Pcre2TokenBacktrackingNode token:
                return ConsumesInput(token.Token.Kind) ? 1 : 0;
            case Pcre2SequenceBacktrackingNode sequence:
                var sequenceMinimum = 0;
                foreach (var child in sequence.Children)
                {
                    sequenceMinimum = SaturatingAdd(
                        sequenceMinimum,
                        GetMinimumByteLength(child, captureDefinitions, nameSlots, activeCaptures));
                }

                return sequenceMinimum;
            case Pcre2AlternationBacktrackingNode alternation:
                return alternation.Alternatives.Min(
                    child => GetMinimumByteLength(child, captureDefinitions, nameSlots, activeCaptures));
            case Pcre2RepeatBacktrackingNode repeat:
                return SaturatingMultiply(
                    GetMinimumByteLength(repeat.Body, captureDefinitions, nameSlots, activeCaptures),
                    repeat.Minimum);
            case Pcre2CaptureBacktrackingNode capture:
                return GetMinimumByteLength(capture.Body, captureDefinitions, nameSlots, activeCaptures);
            case Pcre2BackreferenceBacktrackingNode backreference:
                return GetBackreferenceMinimum(backreference.Target, captureDefinitions, nameSlots, activeCaptures);
            case Pcre2AssertionBacktrackingNode:
                return 0;
            case Pcre2SubroutineCallBacktrackingNode subroutine:
                var subroutineSlot = ResolveReferenceSlot(subroutine.Target);
                return subroutineSlot > 0
                    ? GetCaptureMinimum(subroutineSlot, captureDefinitions, nameSlots, activeCaptures)
                    : 0;
            case Pcre2ConditionalBacktrackingNode conditional:
                return Math.Min(
                    GetMinimumByteLength(conditional.YesBranch, captureDefinitions, nameSlots, activeCaptures),
                    GetMinimumByteLength(conditional.NoBranch, captureDefinitions, nameSlots, activeCaptures));
            case Pcre2AtomicBacktrackingNode atomic:
                return GetMinimumByteLength(atomic.Body, captureDefinitions, nameSlots, activeCaptures);
            default:
                return 0;
        }
    }

    private static int GetBackreferenceMinimum(
        Pcre2BackreferenceTarget target,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots,
        HashSet<int> activeCaptures)
    {
        if (target.Kind != Pcre2BackreferenceTargetKind.Named)
        {
            return GetCaptureMinimum(
                ResolveReferenceSlot(target),
                captureDefinitions,
                nameSlots,
                activeCaptures);
        }

        if (!nameSlots.TryGetValue(target.Name, out var slots) || slots.Length == 0)
        {
            return 0;
        }

        var minimum = int.MaxValue;
        foreach (var slot in slots)
        {
            minimum = Math.Min(
                minimum,
                GetCaptureMinimum(slot, captureDefinitions, nameSlots, activeCaptures));
        }

        return minimum == int.MaxValue ? 0 : minimum;
    }

    private static int GetCaptureMinimum(
        int slot,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots,
        HashSet<int> activeCaptures)
    {
        if (slot <= 0 ||
            !captureDefinitions.TryGetValue(slot, out var capture) ||
            !activeCaptures.Add(slot))
        {
            return 0;
        }

        var minimum = GetMinimumByteLength(capture.Body, captureDefinitions, nameSlots, activeCaptures);
        _ = activeCaptures.Remove(slot);
        return minimum;
    }

    private static int ResolveReferenceSlot(Pcre2BackreferenceTarget target) => target.Kind switch
    {
        Pcre2BackreferenceTargetKind.Absolute => target.Number,
        Pcre2BackreferenceTargetKind.Relative when target.Number > 0 =>
            target.CaptureCountAtReference + target.Number,
        Pcre2BackreferenceTargetKind.Relative =>
            target.CaptureCountAtReference + target.Number + 1,
        _ => 0,
    };

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
        Pcre2SubroutineCallBacktrackingNode => 0,
        Pcre2ConditionalBacktrackingNode conditional => Math.Min(
            GetMinimumScalarLength(conditional.YesBranch),
            GetMinimumScalarLength(conditional.NoBranch)),
        Pcre2AtomicBacktrackingNode atomic => GetMinimumScalarLength(atomic.Body),
        Pcre2ControlVerbBacktrackingNode => 0,
        Pcre2MatchBoundaryResetBacktrackingNode => 0,
        _ => 0,
    };

    internal static int GetMaximumScalarLength(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2EmptyBacktrackingNode => 0,
        Pcre2TokenBacktrackingNode token => token.Token.Kind switch
        {
            Pcre2CharacterTokenKind.NewlineSequence => 2,
            Pcre2CharacterTokenKind.ExtendedGraphemeCluster => int.MaxValue,
            _ => ConsumesScalar(token.Token.Kind) ? 1 : 0,
        },
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Aggregate(
            0,
            static (length, child) => SaturatingAdd(length, GetMaximumScalarLength(child))),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Max(GetMaximumScalarLength),
        Pcre2RepeatBacktrackingNode repeat => SaturatingMultiply(GetMaximumScalarLength(repeat.Body), repeat.Maximum),
        Pcre2CaptureBacktrackingNode capture => GetMaximumScalarLength(capture.Body),
        Pcre2BackreferenceBacktrackingNode => int.MaxValue,
        Pcre2AssertionBacktrackingNode => 0,
        Pcre2SubroutineCallBacktrackingNode => int.MaxValue,
        Pcre2ConditionalBacktrackingNode conditional => Math.Max(
            GetMaximumScalarLength(conditional.YesBranch),
            GetMaximumScalarLength(conditional.NoBranch)),
        Pcre2AtomicBacktrackingNode atomic => GetMaximumScalarLength(atomic.Body),
        Pcre2ControlVerbBacktrackingNode => 0,
        Pcre2MatchBoundaryResetBacktrackingNode => 0,
        _ => int.MaxValue,
    };

    internal static byte? GetLeadingAsciiByte(IPcre2BacktrackingNode node)
    {
        return TryGetLeadingAsciiByte(node, out var leading) ? leading : null;
    }

    internal static bool StartsWithExtendedGraphemeCluster(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2TokenBacktrackingNode token =>
            token.Token.Kind == Pcre2CharacterTokenKind.ExtendedGraphemeCluster,
        Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence =>
            StartsWithExtendedGraphemeCluster(sequence.Children[0]),
        Pcre2AlternationBacktrackingNode alternation =>
            alternation.Alternatives.All(StartsWithExtendedGraphemeCluster),
        Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat =>
            StartsWithExtendedGraphemeCluster(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => StartsWithExtendedGraphemeCluster(capture.Body),
        Pcre2AtomicBacktrackingNode atomic => StartsWithExtendedGraphemeCluster(atomic.Body),
        _ => false,
    };

    internal static bool ContainsCodeUnit(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2TokenBacktrackingNode token => token.Token.Kind == Pcre2CharacterTokenKind.CodeUnit,
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(ContainsCodeUnit),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(ContainsCodeUnit),
        Pcre2RepeatBacktrackingNode repeat => ContainsCodeUnit(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => ContainsCodeUnit(capture.Body),
        Pcre2AssertionBacktrackingNode assertion => ContainsCodeUnit(assertion.Body),
        Pcre2ConditionalBacktrackingNode conditional =>
            conditional.Assertion is not null && ContainsCodeUnit(conditional.Assertion.Body) ||
            ContainsCodeUnit(conditional.YesBranch) ||
            ContainsCodeUnit(conditional.NoBranch),
        Pcre2AtomicBacktrackingNode atomic => ContainsCodeUnit(atomic.Body),
        _ => false,
    };

    private static int GetSequenceMinimum(Pcre2SequenceBacktrackingNode sequence)
    {
        var result = 0;
        foreach (var child in sequence.Children)
        {
            result = SaturatingAdd(result, GetMinimumByteLength(child));
        }

        return result;
    }

    private static bool SequenceRestrictsSearch(Pcre2SequenceBacktrackingNode sequence)
    {
        foreach (var child in sequence.Children)
        {
            if (RestrictsSearchToInitialCandidate(child))
            {
                return true;
            }

            if (CanConsume(child))
            {
                return false;
            }
        }

        return false;
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
            case Pcre2ConditionalBacktrackingNode conditional:
                if (TryGetLeadingAsciiByte(conditional.YesBranch, out var yesLeading) &&
                    TryGetLeadingAsciiByte(conditional.NoBranch, out var noLeading) &&
                    yesLeading == noLeading)
                {
                    leading = yesLeading;
                    return true;
                }
                break;
            case Pcre2AtomicBacktrackingNode atomic:
                return TryGetLeadingAsciiByte(atomic.Body, out leading);
        }

        leading = 0;
        return false;
    }

    private static bool ConsumesScalar(Pcre2CharacterTokenKind kind) => kind is
        Pcre2CharacterTokenKind.Literal or
        Pcre2CharacterTokenKind.CharacterClass or
        Pcre2CharacterTokenKind.Any or
        Pcre2CharacterTokenKind.AnyNotNewline or
        Pcre2CharacterTokenKind.NewlineSequence or
        Pcre2CharacterTokenKind.ExtendedGraphemeCluster;

    private static bool ConsumesInput(Pcre2CharacterTokenKind kind) =>
        ConsumesScalar(kind) || kind == Pcre2CharacterTokenKind.CodeUnit;

    private static bool CanConsume(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2EmptyBacktrackingNode => false,
        Pcre2TokenBacktrackingNode token => ConsumesInput(token.Token.Kind),
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(CanConsume),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(CanConsume),
        Pcre2RepeatBacktrackingNode repeat => repeat.Maximum > 0 && CanConsume(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => CanConsume(capture.Body),
        Pcre2BackreferenceBacktrackingNode => true,
        Pcre2AssertionBacktrackingNode => false,
        Pcre2SubroutineCallBacktrackingNode => true,
        Pcre2ConditionalBacktrackingNode conditional =>
            CanConsume(conditional.YesBranch) || CanConsume(conditional.NoBranch),
        Pcre2AtomicBacktrackingNode atomic => CanConsume(atomic.Body),
        Pcre2ControlVerbBacktrackingNode => false,
        Pcre2MatchBoundaryResetBacktrackingNode => false,
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
    ControlVerb = 2,
}

internal readonly record struct Pcre2BacktrackingFrame(
    int Instruction,
    int InputOffsetInBytes,
    int ReportedStartOffsetInBytes,
    bool MatchBoundaryWasReset,
    int RepeatCheckpoint,
    int CaptureCheckpoint,
    int CallCheckpoint,
    int AtomicCheckpoint,
    int MarkMutationCheckpoint,
    int MarkTrailCheckpoint,
    Pcre2BacktrackingResumeAction ResumeAction,
    int RepeatId,
    int RepeatCount,
    int RepeatPosition,
    Pcre2ControlVerbKind ControlVerb,
    int ControlTarget,
    int ControlPosition,
    int ControlNameId);

internal readonly record struct Pcre2RepeatMutation(
    int RepeatId,
    int PreviousCount,
    int PreviousPosition);

internal readonly record struct Pcre2CaptureMutation(
    int Slot,
    int PreviousStart,
    int PreviousEnd,
    int PreviousOpenStart,
    int PreviousOwner,
    int PreviousOpenOwner);

internal readonly record struct Pcre2SubroutineCallFrame(
    int ReturnInstruction,
    int SubroutineSlot,
    int RepeatCheckpoint,
    int CaptureCheckpoint,
    int BacktrackingCheckpoint,
    int AtomicCheckpoint,
    int MarkTrailCheckpoint,
    bool PreserveUnsetCaptures);

internal readonly record struct Pcre2SubroutineCaptureSnapshot(
    int Start,
    int End,
    int OpenStart,
    int Owner,
    int OpenOwner);

internal readonly record struct Pcre2SubroutineRepeatSnapshot(int Count, int Position);

internal enum Pcre2SubroutineCallMutationKind : byte
{
    Pushed = 0,
    Popped = 1,
}

internal readonly record struct Pcre2SubroutineCallMutation(
    Pcre2SubroutineCallMutationKind Kind,
    Pcre2SubroutineCallFrame Frame);

internal readonly record struct Pcre2AtomicCheckpoint(int BacktrackingCheckpoint, int MarkTrailCheckpoint);

internal readonly record struct Pcre2MarkMutation(int PreviousMarkId, int PreviousPosition);

internal readonly record struct Pcre2MarkTrailEntry(int MarkId, int Position);

internal enum Pcre2BacktrackingFailureKind : byte
{
    None = 0,
    Commit = 1,
    Prune = 2,
    Skip = 3,
}

internal readonly record struct Pcre2BacktrackingFailure(Pcre2BacktrackingFailureKind Kind, int SkipPosition);

internal readonly record struct Pcre2CaptureByteRange(bool Success, int StartOffsetInBytes, int EndOffsetInBytes);

internal readonly struct Pcre2BacktrackingCaptureResult
{
    private readonly object? _value;

    private Pcre2BacktrackingCaptureResult(object value) => _value = value;

    internal static Pcre2BacktrackingCaptureResult FromAssertionRanges(Pcre2CaptureByteRange[] captures) =>
        new(captures);

    internal static Pcre2BacktrackingCaptureResult FromProjectedGroups(Pcre2GroupData[] groups) =>
        new(groups);

    internal Pcre2CaptureByteRange[] AssertionRanges => _value as Pcre2CaptureByteRange[] ?? [];

    internal Pcre2GroupData[] ProjectedGroups => _value as Pcre2GroupData[] ?? [];
}

internal enum Pcre2CaptureMaterialization : byte
{
    None = 0,
    AssertionRanges = 1,
    ProjectedFinalSlots = 2,
}

internal readonly record struct Pcre2BacktrackingMatch(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int ConsumedStartOffsetInBytes,
    int ConsumedEndOffsetInBytes,
    bool MatchBoundaryWasReset,
    Pcre2BacktrackingCaptureResult CaptureResult,
    string? Mark)
{
    internal static Pcre2BacktrackingMatch NoMatch =>
        new(false, 0, 0, 0, 0, false, default, null);

    internal Pcre2CharacterMatch ToCharacterMatch() => Success
        ? new Pcre2CharacterMatch(
            true,
            StartOffsetInBytes,
            EndOffsetInBytes,
            ConsumedStartOffsetInBytes,
            ConsumedEndOffsetInBytes,
            MatchBoundaryWasReset)
        : Pcre2CharacterMatch.NoMatch;
}

internal static class Pcre2BacktrackingRunner
{
    internal static Pcre2CharacterMatch Match(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            candidateSearch,
            ref input,
            start,
            start,
            matchOptions,
            Pcre2CaptureMaterialization.None,
            ref budget).ToCharacterMatch();

    internal static Pcre2CharacterMatch Match(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition searchStart,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            candidateSearch,
            ref input,
            searchStart,
            firstMatchingPosition,
            matchOptions,
            Pcre2CaptureMaterialization.None,
            ref budget).ToCharacterMatch();

    internal static Pcre2BacktrackingMatch MatchDetailed(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            candidateSearch,
            ref input,
            start,
            start,
            matchOptions,
            Pcre2CaptureMaterialization.ProjectedFinalSlots,
            ref budget);

    internal static Pcre2BacktrackingMatch MatchDetailed(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition searchStart,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            candidateSearch,
            ref input,
            searchStart,
            firstMatchingPosition,
            matchOptions,
            Pcre2CaptureMaterialization.ProjectedFinalSlots,
            ref budget);

    private static Pcre2BacktrackingMatch MatchCore(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ref Pcre2ResourceBudget budget)
    {
        var bytes = input.Bytes;
        var anchored = program.RestrictsSearchToInitialCandidate ||
            (program.Request.Options & Pcre2CompileOptions.Anchored) != 0 ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0;
        var candidate = start.Value;
        var useCandidateSearch = !anchored &&
            candidateSearch.HasValue &&
            matchOptions == Pcre2MatchOptions.None &&
            budget.IsUnmetered;
        var candidateState = new PreparedSearchScanState(
            candidate,
            new PreparedMultiLiteralScanState(candidate, candidate, 0));
        var nextWindowCandidate = -1;
        var lastWindowCandidate = -1;
        var windowCandidateEnd = -1;
        string? lastMark = null;
        var maximumCandidate = program.MinimumByteLength > bytes.Length
            ? -1
            : bytes.Length - program.MinimumByteLength;
        while (candidate <= maximumCandidate)
        {
            if (useCandidateSearch)
            {
                if (!TryFindNextCandidate(
                        candidateSearch,
                        program.Request,
                        bytes,
                        start.Value,
                        ref candidateState,
                        ref nextWindowCandidate,
                        ref lastWindowCandidate,
                        ref windowCandidateEnd,
                        out candidate) ||
                    candidate > maximumCandidate)
                {
                    break;
                }
            }

            budget.ChargeCandidate();
            if (TryMatchAt(
                    program,
                    bytes,
                    ref input,
                    candidate,
                    firstMatchingPosition.Value,
                    matchOptions,
                    captureMaterialization,
                    ReadOnlySpan<int>.Empty,
                    ReadOnlySpan<int>.Empty,
                    0,
                    false,
                    ref budget,
                    out var match,
                    out var failure))
            {
                return match;
            }

            lastMark = match.Mark ?? lastMark;

            if (failure.Kind == Pcre2BacktrackingFailureKind.Commit)
            {
                break;
            }

            if (failure.Kind == Pcre2BacktrackingFailureKind.Skip && failure.SkipPosition > candidate)
            {
                candidate = failure.SkipPosition;
                continue;
            }

            if (useCandidateSearch)
            {
                continue;
            }

            if (anchored || !Pcre2CharacterRunner.TryAdvanceCandidate(
                    program.Request,
                    program.LeadingAsciiByte,
                    ref input,
                    candidate,
                    !budget.RequiresCandidateMetering,
                    out var nextCandidate))
            {
                break;
            }

            if (program.LeadingExtendedGraphemeCluster &&
                (program.Request.Options & Pcre2CompileOptions.FirstLine) == 0 &&
                Pcre2GraphemeClusterSemantics.TryGetWidth(bytes, candidate, out var graphemeWidth))
            {
                candidate += graphemeWidth;
            }
            else
            {
                candidate = nextCandidate;
            }
        }

        return new Pcre2BacktrackingMatch(false, 0, 0, 0, 0, false, default, lastMark);
    }

    private static bool TryFindNextCandidate(
        Pcre2CandidateSearchProgram candidateSearch,
        Pcre2CompileRequest request,
        ReadOnlySpan<byte> input,
        int lowerBound,
        ref PreparedSearchScanState state,
        ref int nextWindowCandidate,
        ref int lastWindowCandidate,
        ref int windowCandidateEnd,
        out int candidate)
    {
        if (candidateSearch.Kind == Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun)
        {
            return TryFindNextLeadingAsciiWordBoundaryRun(
                input,
                lowerBound,
                candidateSearch.MinimumWordRunLength,
                ref nextWindowCandidate,
                out candidate);
        }

        if (candidateSearch.Kind == Pcre2CandidateSearchKind.BoundedLiteralWindow)
        {
            return TryFindNextBoundedWindowCandidate(
                candidateSearch,
                input,
                lowerBound,
                ref state,
                ref nextWindowCandidate,
                ref lastWindowCandidate,
                ref windowCandidateEnd,
                out candidate);
        }

        while (candidateSearch.Searcher.TryFindNextOverlappingMatch(input, ref state, out var preparedCandidate))
        {
            if (candidateSearch.Kind is Pcre2CandidateSearchKind.BranchLeadingLiterals or
                Pcre2CandidateSearchKind.LeadingAsciiSet or
                Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow)
            {
                if (candidateSearch.Kind == Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow &&
                    !MatchesCandidateWindow(input, preparedCandidate.Index, candidateSearch.WindowConstraint))
                {
                    continue;
                }

                candidate = preparedCandidate.Index;
                return true;
            }

            if (candidateSearch.Kind != Pcre2CandidateSearchKind.LeadingRunThenLiteral)
            {
                break;
            }

            var runStart = preparedCandidate.Index;
            var valid = true;
            for (var i = candidateSearch.LeadingRunTokens.Length - 1; i >= 0; i--)
            {
                var consumed = false;
                while (TryRetreatScalarToBoundary(input, lowerBound, runStart, out var previous) &&
                       Pcre2CharacterRunner.TryMatchToken(
                           candidateSearch.LeadingRunTokens[i],
                           request,
                           input,
                           previous,
                           lowerBound,
                           Pcre2MatchOptions.None,
                           out var next) &&
                       next == runStart)
                {
                    runStart = previous;
                    consumed = true;
                }

                if (!consumed)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                candidate = runStart;
                return true;
            }
        }

        candidate = -1;
        return false;
    }

    private static bool TryFindNextLeadingAsciiWordBoundaryRun(
        ReadOnlySpan<byte> input,
        int lowerBound,
        int minimumWordRunLength,
        ref int scanOffset,
        out int candidate)
    {
        var offset = Math.Max(lowerBound, scanOffset);
        while (offset < input.Length)
        {
            if (!IsAsciiWordByte(input[offset]))
            {
                offset++;
                continue;
            }

            var runStart = offset;
            do
            {
                offset++;
            }
            while (offset < input.Length && IsAsciiWordByte(input[offset]));

            scanOffset = offset;
            if ((runStart == 0 || !IsAsciiWordByte(input[runStart - 1])) &&
                offset - runStart >= minimumWordRunLength)
            {
                candidate = runStart;
                return true;
            }
        }

        scanOffset = input.Length;
        candidate = -1;
        return false;
    }

    private static bool IsAsciiWordByte(byte value) =>
        value is >= (byte)'a' and <= (byte)'z' or
            >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'0' and <= (byte)'9' or
            (byte)'_';

    private static bool TryFindNextBoundedWindowCandidate(
        Pcre2CandidateSearchProgram candidateSearch,
        ReadOnlySpan<byte> input,
        int lowerBound,
        ref PreparedSearchScanState state,
        ref int nextWindowCandidate,
        ref int lastWindowCandidate,
        ref int windowCandidateEnd,
        out int candidate)
    {
        while (true)
        {
            while (nextWindowCandidate <= windowCandidateEnd)
            {
                var current = nextWindowCandidate++;
                if (current > lastWindowCandidate &&
                    (current == input.Length || (input[current] & 0xC0) != 0x80))
                {
                    lastWindowCandidate = current;
                    candidate = current;
                    return true;
                }
            }

            if (!candidateSearch.Searcher.TryFindNextOverlappingMatch(input, ref state, out var requiredLiteral))
            {
                candidate = -1;
                return false;
            }

            nextWindowCandidate = Math.Max(
                Math.Max(lowerBound, lastWindowCandidate + 1),
                requiredLiteral.Index - candidateSearch.WindowConstraint.MaximumOffset);
            windowCandidateEnd = requiredLiteral.Index - candidateSearch.WindowConstraint.MinimumOffset;
        }
    }

    private static bool MatchesCandidateWindow(
        ReadOnlySpan<byte> input,
        int candidate,
        Pcre2CandidateWindowConstraint constraint)
    {
        var latest = Math.Min(constraint.MaximumOffset, input.Length - candidate - constraint.Literal.Length);
        for (var offset = constraint.MinimumOffset; offset <= latest; offset++)
        {
            if (input.Slice(candidate + offset, constraint.Literal.Length).SequenceEqual(constraint.Literal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryRetreatScalarToBoundary(
        ReadOnlySpan<byte> input,
        int lowerBound,
        int position,
        out int previous)
    {
        if (position <= lowerBound)
        {
            previous = 0;
            return false;
        }

        previous = position - 1;
        while (previous > lowerBound && (input[previous] & 0xC0) == 0x80)
        {
            previous--;
        }

        return true;
    }

    private static bool TryMatchAt(
        Pcre2BacktrackingProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8ValidatedInput projectionInput,
        int candidate,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ReadOnlySpan<int> initialCaptureStarts,
        ReadOnlySpan<int> initialCaptureEnds,
        int depthBase,
        bool isAssertion,
        ref Pcre2ResourceBudget budget,
        out Pcre2BacktrackingMatch match,
        out Pcre2BacktrackingFailure failure)
    {
        var configuredFrameLimit = budget.Limits.DepthLimit == 0
            ? int.MaxValue
            : (int)Math.Min(budget.Limits.DepthLimit, int.MaxValue);
        var frameLimit = configuredFrameLimit == int.MaxValue
            ? int.MaxValue
            : Math.Max(0, configuredFrameLimit - depthBase);
        var frames = new Utf8PooledStateStack<Pcre2BacktrackingFrame>(frameLimit, budget.CollectsDiagnostics);
        var repeatMutations = new Utf8PooledStateStack<Pcre2RepeatMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var captureMutations = new Utf8PooledStateStack<Pcre2CaptureMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var subroutineCalls = new Utf8PooledStateStack<Pcre2SubroutineCallFrame>(frameLimit, budget.CollectsDiagnostics);
        var subroutineCallMutations = new Utf8PooledStateStack<Pcre2SubroutineCallMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var atomicCheckpoints = new Utf8PooledStateStack<Pcre2AtomicCheckpoint>(frameLimit, budget.CollectsDiagnostics);
        var markMutations = new Utf8PooledStateStack<Pcre2MarkMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var markTrail = new Utf8PooledStateStack<Pcre2MarkTrailEntry>(int.MaxValue, budget.CollectsDiagnostics);
        var rentedRepeatState = program.RepeatCount == 0
            ? null
            : ArrayPool<int>.Shared.Rent(checked(program.RepeatCount * 2));
        var needsCaptureState = program.RequiresCaptureState ||
            captureMaterialization != Pcre2CaptureMaterialization.None ||
            !initialCaptureStarts.IsEmpty;
        var captureStateLength = !needsCaptureState || program.CaptureSlotCount <= 1
            ? 0
            : checked(program.CaptureSlotCount * 5);
        var rentedCaptureState = captureStateLength == 0
            ? null
            : ArrayPool<int>.Shared.Rent(captureStateLength);
        var captureState = rentedCaptureState is null
            ? Span<int>.Empty
            : rentedCaptureState.AsSpan(0, captureStateLength);

        var rentedCaptureRestoreGenerations = program.SubroutineTargets.Length == 0 || captureState.IsEmpty
            ? null
            : ArrayPool<int>.Shared.Rent(program.CaptureSlotCount);
        var rentedCaptureRestoreSlots = program.SubroutineTargets.Length == 0 || captureState.IsEmpty
            ? null
            : ArrayPool<int>.Shared.Rent(program.CaptureSlotCount);
        var rentedCaptureRestoreSnapshots = program.SubroutineTargets.Length == 0 || captureState.IsEmpty
            ? null
            : ArrayPool<Pcre2SubroutineCaptureSnapshot>.Shared.Rent(program.CaptureSlotCount);
        var rentedRepeatRestoreGenerations = program.SubroutineTargets.Length == 0 || program.RepeatCount == 0
            ? null
            : ArrayPool<int>.Shared.Rent(program.RepeatCount);
        var rentedRepeatRestoreIds = program.SubroutineTargets.Length == 0 || program.RepeatCount == 0
            ? null
            : ArrayPool<int>.Shared.Rent(program.RepeatCount);
        var rentedRepeatRestoreSnapshots = program.SubroutineTargets.Length == 0 || program.RepeatCount == 0
            ? null
            : ArrayPool<Pcre2SubroutineRepeatSnapshot>.Shared.Rent(program.RepeatCount);
        var counts = rentedRepeatState is null
            ? Span<int>.Empty
            : rentedRepeatState.AsSpan(0, program.RepeatCount);
        var positions = rentedRepeatState is null
            ? Span<int>.Empty
            : rentedRepeatState.AsSpan(program.RepeatCount, program.RepeatCount);
        var captureStarts = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState[..program.CaptureSlotCount];
        var captureEnds = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState.Slice(program.CaptureSlotCount, program.CaptureSlotCount);
        var captureOpenStarts = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState.Slice(program.CaptureSlotCount * 2, program.CaptureSlotCount);
        var captureOwners = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState.Slice(program.CaptureSlotCount * 3, program.CaptureSlotCount);
        var captureOpenOwners = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState.Slice(program.CaptureSlotCount * 4, program.CaptureSlotCount);
        var captureRestoreGenerations = rentedCaptureRestoreGenerations is null
            ? Span<int>.Empty
            : rentedCaptureRestoreGenerations.AsSpan(0, program.CaptureSlotCount);
        var captureRestoreSlots = rentedCaptureRestoreSlots is null
            ? Span<int>.Empty
            : rentedCaptureRestoreSlots.AsSpan(0, program.CaptureSlotCount);
        var captureRestoreSnapshots = rentedCaptureRestoreSnapshots is null
            ? Span<Pcre2SubroutineCaptureSnapshot>.Empty
            : rentedCaptureRestoreSnapshots.AsSpan(0, program.CaptureSlotCount);
        var repeatRestoreGenerations = rentedRepeatRestoreGenerations is null
            ? Span<int>.Empty
            : rentedRepeatRestoreGenerations.AsSpan(0, program.RepeatCount);
        var repeatRestoreIds = rentedRepeatRestoreIds is null
            ? Span<int>.Empty
            : rentedRepeatRestoreIds.AsSpan(0, program.RepeatCount);
        var repeatRestoreSnapshots = rentedRepeatRestoreSnapshots is null
            ? Span<Pcre2SubroutineRepeatSnapshot>.Empty
            : rentedRepeatRestoreSnapshots.AsSpan(0, program.RepeatCount);
        counts.Fill(-1);
        positions.Fill(-1);
        captureStarts.Fill(-1);
        captureEnds.Fill(-1);
        captureOpenStarts.Fill(-1);
        captureOwners.Fill(-1);
        captureOpenOwners.Fill(-1);
        captureRestoreGenerations.Clear();
        repeatRestoreGenerations.Clear();
        if (!initialCaptureStarts.IsEmpty)
        {
            initialCaptureStarts.CopyTo(captureStarts);
            initialCaptureEnds.CopyTo(captureEnds);
            for (var slot = 1; slot < captureStarts.Length; slot++)
            {
                if (captureStarts[slot] >= 0)
                {
                    captureOwners[slot] = 0;
                }
            }
        }
        budget.ChargeHeap(
            (ulong)program.RepeatCount * 8UL +
            (ulong)captureStarts.Length * 20UL +
            (ulong)captureRestoreSnapshots.Length * 28UL +
            (ulong)repeatRestoreSnapshots.Length * 16UL);

        try
        {
            var instructionIndex = 0;
            var inputIndex = candidate;
            var reportedStartOffsetInBytes = candidate;
            var matchBoundaryWasReset = false;
            var subroutineRestoreGeneration = 0;
            var currentMarkId = -1;
            var currentMarkPosition = -1;
            var lastEncounteredMarkId = -1;
            while (true)
            {
                var instruction = program.Instructions[instructionIndex];
                budget.ChargeBacktracking(instruction.Kind, instruction.Token.Kind);
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
                                reportedStartOffsetInBytes,
                                matchBoundaryWasReset,
                                repeatMutations.Count,
                                captureMutations.Count,
                                subroutineCallMutations.Count,
                                atomicCheckpoints.Count,
                                markMutations.Count,
                                markTrail.Count,
                                Pcre2BacktrackingResumeAction.None,
                                0,
                                0,
                                0,
                                default,
                                -1,
                                0,
                                -1),
                            repeatMutations.Count,
                            captureMutations.Count,
                            subroutineCallMutations.Count,
                            atomicCheckpoints.Count,
                            markMutations.Count,
                            markTrail.Count,
                            program.RepeatCount,
                            captureStarts.Length,
                            depthBase,
                            subroutineCalls.Count,
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
                                            reportedStartOffsetInBytes,
                                            matchBoundaryWasReset,
                                            repeatMutations.Count,
                                            captureMutations.Count,
                                            subroutineCallMutations.Count,
                                            atomicCheckpoints.Count,
                                            markMutations.Count,
                                            markTrail.Count,
                                            Pcre2BacktrackingResumeAction.None,
                                            0,
                                            0,
                                            0,
                                            default,
                                            -1,
                                            0,
                                            -1),
                                        repeatMutations.Count,
                                        captureMutations.Count,
                                        subroutineCallMutations.Count,
                                        atomicCheckpoints.Count,
                                        markMutations.Count,
                                        markTrail.Count,
                                        program.RepeatCount,
                                        captureStarts.Length,
                                        depthBase,
                                        subroutineCalls.Count,
                                        ref budget);
                                }
                                else
                                {
                                    PushFrame(
                                        ref frames,
                                        new Pcre2BacktrackingFrame(
                                            instruction.PrimaryTarget,
                                            inputIndex,
                                            reportedStartOffsetInBytes,
                                            matchBoundaryWasReset,
                                            repeatMutations.Count,
                                            captureMutations.Count,
                                            subroutineCallMutations.Count,
                                            atomicCheckpoints.Count,
                                            markMutations.Count,
                                            markTrail.Count,
                                            Pcre2BacktrackingResumeAction.EnterRepeat,
                                            repeatId,
                                            count + 1,
                                            inputIndex,
                                            default,
                                            -1,
                                            0,
                                            -1),
                                        repeatMutations.Count,
                                        captureMutations.Count,
                                        subroutineCallMutations.Count,
                                        atomicCheckpoints.Count,
                                        markMutations.Count,
                                        markTrail.Count,
                                        program.RepeatCount,
                                        captureStarts.Length,
                                        depthBase,
                                        subroutineCalls.Count,
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

                        var startCaptureSlot = instruction.CaptureSlot;
                        var currentCallDepth = subroutineCalls.Count;
                        if (captureStarts[startCaptureSlot] >= 0 && captureOwners[startCaptureSlot] > currentCallDepth ||
                            captureOpenStarts[startCaptureSlot] >= 0 && captureOpenOwners[startCaptureSlot] > currentCallDepth)
                        {
                            instructionIndex++;
                            continue;
                        }

                        SetCapture(
                            startCaptureSlot,
                            captureStarts[startCaptureSlot],
                            captureEnds[startCaptureSlot],
                            inputIndex,
                            captureOwners[startCaptureSlot],
                            currentCallDepth,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
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

                        var endCaptureSlot = instruction.CaptureSlot;
                        if (captureOpenStarts[endCaptureSlot] < 0 ||
                            captureOpenOwners[endCaptureSlot] != subroutineCalls.Count)
                        {
                            instructionIndex++;
                            continue;
                        }

                        SetCapture(
                            endCaptureSlot,
                            captureOpenStarts[endCaptureSlot],
                            inputIndex,
                            -1,
                            captureOpenOwners[endCaptureSlot],
                            -1,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
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

                    case Pcre2BacktrackingInstructionKind.BackreferenceSlotSet:
                        var duplicateSlots = program.BackreferenceSlotSets[instruction.BackreferenceSlotSetId].Slots;
                        var selectedSlot = 0;
                        foreach (var slot in duplicateSlots)
                        {
                            if (captureStarts[slot] >= 0 && captureEnds[slot] >= captureStarts[slot])
                            {
                                selectedSlot = slot;
                                break;
                            }
                        }

                        if (selectedSlot != 0 && TryMatchBackreference(
                                input,
                                captureStarts[selectedSlot],
                                captureEnds[selectedSlot],
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
                                ref projectionInput,
                                inputIndex,
                                firstMatchingPosition,
                                matchOptions,
                                captureStarts,
                                captureEnds,
                                depthBase + frames.Count + subroutineCalls.Count + 1,
                                ref budget,
                                out var assertionCaptures,
                                out var assertionMark,
                                out var assertionReportedStart))
                        {
                            if (assertionReportedStart >= 0)
                            {
                                reportedStartOffsetInBytes = assertionReportedStart;
                                matchBoundaryWasReset = true;
                            }

                            MergeAssertionCaptures(
                                assertionCaptures,
                                subroutineCalls.Count,
                                captureStarts,
                                captureEnds,
                                captureOpenStarts,
                                captureOwners,
                                captureOpenOwners,
                                ref captureMutations,
                                ref budget);
                            MergeAssertionMark(
                                program,
                                assertionMark,
                                inputIndex,
                                ref currentMarkId,
                                ref currentMarkPosition,
                                ref lastEncounteredMarkId,
                                ref markMutations,
                                ref markTrail,
                                ref budget);

                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.SubroutineCall:
                        var subroutineSlot = instruction.SubroutineSlot;
                        var subroutineTarget = (uint)subroutineSlot < (uint)program.SubroutineTargets.Length
                            ? program.SubroutineTargets[subroutineSlot]
                            : -1;
                        if (subroutineTarget < 0)
                        {
                            break;
                        }

                        PushSubroutineCall(
                            new Pcre2SubroutineCallFrame(
                                instructionIndex + 1,
                                subroutineSlot,
                                repeatMutations.Count,
                                captureMutations.Count,
                                frames.Count,
                                atomicCheckpoints.Count,
                                markTrail.Count,
                                program.SubroutinePreservesUnsetCaptures[subroutineSlot]),
                            ref subroutineCalls,
                            ref subroutineCallMutations,
                            depthBase + frames.Count,
                            ref budget);
                        for (var activeRepeatId = 0; activeRepeatId < counts.Length; activeRepeatId++)
                        {
                            if (counts[activeRepeatId] >= 0)
                            {
                                SetRepeat(
                                    activeRepeatId,
                                    -1,
                                    -1,
                                    counts,
                                    positions,
                                    ref repeatMutations,
                                    ref budget);
                            }
                        }
                        instructionIndex = subroutineTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.SubroutineReturn:
                        if (subroutineCalls.Count == 0)
                        {
                            break;
                        }

                        var returnCall = subroutineCalls[subroutineCalls.Count - 1];
                        RestoreSubroutineState(
                            returnCall,
                            subroutineCalls.Count,
                            counts,
                            positions,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
                            repeatRestoreGenerations,
                            repeatRestoreIds,
                            repeatRestoreSnapshots,
                            captureRestoreGenerations,
                            captureRestoreSlots,
                            captureRestoreSnapshots,
                            ref subroutineRestoreGeneration,
                            ref repeatMutations,
                            ref captureMutations,
                            ref budget);
                        var completedCall = subroutineCalls.Pop();
                        RecordSubroutineCallMutation(
                            Pcre2SubroutineCallMutationKind.Popped,
                            completedCall,
                            ref subroutineCallMutations,
                            ref budget);
                        instructionIndex = completedCall.ReturnInstruction;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Conditional:
                        var condition = program.Conditions[instruction.ConditionId];
                        var conditionMet = condition.Kind switch
                        {
                            Pcre2BacktrackingConditionKind.CaptureSet =>
                                IsAnyCaptureSet(condition.CaptureSlots, captureStarts, captureEnds),
                            Pcre2BacktrackingConditionKind.RecursionAny => subroutineCalls.Count != 0,
                            Pcre2BacktrackingConditionKind.RecursionSlot =>
                                IsSubroutineActive(condition.SubroutineSlot, subroutineCalls),
                            _ => false,
                        };
                        Pcre2CaptureByteRange[] conditionalCaptures = [];
                        if (condition.Kind == Pcre2BacktrackingConditionKind.Assertion)
                        {
                            conditionMet = TryEvaluateAssertion(
                                program.AssertionPrograms[condition.AssertionProgramId],
                                condition.AssertionKind,
                                input,
                                ref projectionInput,
                                inputIndex,
                                firstMatchingPosition,
                                matchOptions,
                                captureStarts,
                                captureEnds,
                                depthBase + frames.Count + subroutineCalls.Count + 1,
                                ref budget,
                                out conditionalCaptures,
                                out var conditionalMark,
                                out var conditionalReportedStart);
                            if (conditionalReportedStart >= 0)
                            {
                                reportedStartOffsetInBytes = conditionalReportedStart;
                                matchBoundaryWasReset = true;
                            }
                            MergeAssertionMark(
                                program,
                                conditionalMark,
                                inputIndex,
                                ref currentMarkId,
                                ref currentMarkPosition,
                                ref lastEncounteredMarkId,
                                ref markMutations,
                                ref markTrail,
                                ref budget);
                        }

                        MergeAssertionCaptures(
                            conditionalCaptures,
                            subroutineCalls.Count,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
                            ref captureMutations,
                            ref budget);

                        instructionIndex = conditionMet
                            ? instruction.PrimaryTarget
                            : instruction.SecondaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.AtomicStart:
                        var nextAtomicDepth = checked(depthBase + subroutineCalls.Count + atomicCheckpoints.Count + 1);
                        budget.ChargeFrame(
                            (uint)nextAtomicDepth,
                            checked((ulong)nextAtomicDepth * 32UL + (ulong)(atomicCheckpoints.Count + 1) * 8UL));
                        if (!atomicCheckpoints.TryPush(new Pcre2AtomicCheckpoint(frames.Count, markTrail.Count)))
                        {
                            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
                        }
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.AtomicEnd:
                        if (atomicCheckpoints.Count == 0)
                        {
                            break;
                        }

                        var atomic = atomicCheckpoints.Pop();
                        frames.Truncate(atomic.BacktrackingCheckpoint);
                        markTrail.Truncate(atomic.MarkTrailCheckpoint);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.ControlMark:
                        SetMark(
                            instruction.ControlNameId,
                            inputIndex,
                            true,
                            ref currentMarkId,
                            ref currentMarkPosition,
                            ref lastEncounteredMarkId,
                            ref markMutations,
                            ref markTrail,
                            ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.ControlAccept:
                        SetMark(
                            instruction.ControlNameId,
                            inputIndex,
                            false,
                            ref currentMarkId,
                            ref currentMarkPosition,
                            ref lastEncounteredMarkId,
                            ref markMutations,
                            ref markTrail,
                            ref budget);
                        CloseOpenCaptures(
                            inputIndex,
                            subroutineCalls.Count,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
                            ref captureMutations,
                            ref budget);
                        if (subroutineCalls.Count != 0)
                        {
                            var acceptedCall = subroutineCalls[subroutineCalls.Count - 1];
                            frames.Truncate(acceptedCall.BacktrackingCheckpoint);
                            atomicCheckpoints.Truncate(acceptedCall.AtomicCheckpoint);
                            markTrail.Truncate(acceptedCall.MarkTrailCheckpoint);
                            RestoreSubroutineState(
                                acceptedCall,
                                subroutineCalls.Count,
                                counts,
                                positions,
                                captureStarts,
                                captureEnds,
                                captureOpenStarts,
                                captureOwners,
                                captureOpenOwners,
                                repeatRestoreGenerations,
                                repeatRestoreIds,
                                repeatRestoreSnapshots,
                                captureRestoreGenerations,
                                captureRestoreSlots,
                                captureRestoreSnapshots,
                                ref subroutineRestoreGeneration,
                                ref repeatMutations,
                                ref captureMutations,
                                ref budget);
                            var completedAcceptedCall = subroutineCalls.Pop();
                            RecordSubroutineCallMutation(
                                Pcre2SubroutineCallMutationKind.Popped,
                                completedAcceptedCall,
                                ref subroutineCallMutations,
                                ref budget);
                            instructionIndex = completedAcceptedCall.ReturnInstruction;
                            continue;
                        }

                        var acceptEmptyDisallowed =
                            (matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && inputIndex == reportedStartOffsetInBytes ||
                            (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0 &&
                            candidate == firstMatchingPosition && inputIndex == reportedStartOffsetInBytes;
                        if (!acceptEmptyDisallowed)
                        {
                            ThrowIfDisallowedNonMonotoneLookaroundReset(
                                program,
                                isAssertion,
                                matchBoundaryWasReset,
                                reportedStartOffsetInBytes,
                                candidate,
                                inputIndex);
                            var acceptedCaptures = MaterializeCaptureResult(
                                captureMaterialization,
                                program.CaptureSlotCount,
                                reportedStartOffsetInBytes,
                                inputIndex,
                                captureStarts,
                                captureEnds,
                                ref projectionInput);
                            failure = default;
                            match = new Pcre2BacktrackingMatch(
                                true,
                                reportedStartOffsetInBytes,
                                inputIndex,
                                candidate,
                                inputIndex,
                                matchBoundaryWasReset,
                                acceptedCaptures,
                                GetMark(program, currentMarkId));
                            return true;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.ControlFail:
                        SetMark(
                            instruction.ControlNameId,
                            inputIndex,
                            false,
                            ref currentMarkId,
                            ref currentMarkPosition,
                            ref lastEncounteredMarkId,
                            ref markMutations,
                            ref markTrail,
                            ref budget);
                        break;

                    case Pcre2BacktrackingInstructionKind.ControlDeferred:
                        if (instruction.ControlVerb != Pcre2ControlVerbKind.Skip)
                        {
                            SetMark(
                                instruction.ControlNameId,
                                inputIndex,
                                false,
                                ref currentMarkId,
                                ref currentMarkPosition,
                                ref lastEncounteredMarkId,
                                ref markMutations,
                                ref markTrail,
                                ref budget);
                        }
                        PushFrame(
                            ref frames,
                            new Pcre2BacktrackingFrame(
                                instructionIndex,
                                inputIndex,
                                reportedStartOffsetInBytes,
                                matchBoundaryWasReset,
                                repeatMutations.Count,
                                captureMutations.Count,
                                subroutineCallMutations.Count,
                                atomicCheckpoints.Count,
                                markMutations.Count,
                                markTrail.Count,
                                Pcre2BacktrackingResumeAction.ControlVerb,
                                0,
                                0,
                                0,
                                instruction.ControlVerb,
                                instruction.PrimaryTarget,
                                inputIndex,
                                instruction.ControlNameId),
                            repeatMutations.Count,
                            captureMutations.Count,
                            subroutineCallMutations.Count,
                            atomicCheckpoints.Count,
                            markMutations.Count,
                            markTrail.Count,
                            program.RepeatCount,
                            captureStarts.Length,
                            depthBase,
                            subroutineCalls.Count,
                            ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.MatchBoundaryReset:
                        reportedStartOffsetInBytes = inputIndex;
                        matchBoundaryWasReset = true;
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Accept:
                        var endAnchored = !isAssertion && (program.Request.Options & Pcre2CompileOptions.EndAnchored) != 0 ||
                            (matchOptions & Pcre2MatchOptions.EndAnchored) != 0;
                        var emptyDisallowed = (matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && inputIndex == reportedStartOffsetInBytes ||
                            (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0 && candidate == firstMatchingPosition && inputIndex == reportedStartOffsetInBytes;
                        if ((!endAnchored || inputIndex == input.Length) && !emptyDisallowed)
                        {
                            ThrowIfDisallowedNonMonotoneLookaroundReset(
                                program,
                                isAssertion,
                                matchBoundaryWasReset,
                                reportedStartOffsetInBytes,
                                candidate,
                                inputIndex);
                            var captures = MaterializeCaptureResult(
                                captureMaterialization,
                                program.CaptureSlotCount,
                                reportedStartOffsetInBytes,
                                inputIndex,
                                captureStarts,
                                captureEnds,
                                ref projectionInput);
                            match = new Pcre2BacktrackingMatch(
                                true,
                                reportedStartOffsetInBytes,
                                inputIndex,
                                candidate,
                                inputIndex,
                                matchBoundaryWasReset,
                                captures,
                                GetMark(program, currentMarkId));
                            failure = default;
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
                        ref subroutineCalls,
                        ref subroutineCallMutations,
                        ref atomicCheckpoints,
                        ref markMutations,
                        ref markTrail,
                        counts,
                        positions,
                        captureStarts,
                        captureEnds,
                        captureOpenStarts,
                        captureOwners,
                        captureOpenOwners,
                        ref reportedStartOffsetInBytes,
                        ref matchBoundaryWasReset,
                        ref currentMarkId,
                        ref currentMarkPosition,
                        ref budget,
                        out instructionIndex,
                        out inputIndex,
                        out failure))
                {
                    match = new Pcre2BacktrackingMatch(
                        false,
                        0,
                        0,
                        0,
                        0,
                        false,
                        default,
                        GetMark(program, lastEncounteredMarkId));
                    return false;
                }
            }
        }
        finally
        {
            var stackRents = frames.RentCount +
                repeatMutations.RentCount +
                captureMutations.RentCount +
                subroutineCalls.RentCount +
                subroutineCallMutations.RentCount +
                atomicCheckpoints.RentCount +
                markMutations.RentCount +
                markTrail.RentCount;
            var stackGrowths = Math.Max(0, frames.RentCount - 1) +
                Math.Max(0, repeatMutations.RentCount - 1) +
                Math.Max(0, captureMutations.RentCount - 1) +
                Math.Max(0, subroutineCalls.RentCount - 1) +
                Math.Max(0, subroutineCallMutations.RentCount - 1) +
                Math.Max(0, atomicCheckpoints.RentCount - 1) +
                Math.Max(0, markMutations.RentCount - 1) +
                Math.Max(0, markTrail.RentCount - 1);
            var fixedRents = (rentedRepeatState is null ? 0 : 1) +
                (rentedCaptureState is null ? 0 : 1) +
                (rentedCaptureRestoreGenerations is null ? 0 : 1) +
                (rentedCaptureRestoreSlots is null ? 0 : 1) +
                (rentedCaptureRestoreSnapshots is null ? 0 : 1) +
                (rentedRepeatRestoreGenerations is null ? 0 : 1) +
                (rentedRepeatRestoreIds is null ? 0 : 1) +
                (rentedRepeatRestoreSnapshots is null ? 0 : 1);
            budget.RecordWorkspacePoolTraffic(
                (ulong)fixedRents,
                (ulong)frames.RentCount,
                (ulong)repeatMutations.RentCount,
                (ulong)captureMutations.RentCount,
                (ulong)(stackRents - frames.RentCount - repeatMutations.RentCount - captureMutations.RentCount),
                (ulong)stackGrowths);
            markTrail.Dispose();
            markMutations.Dispose();
            atomicCheckpoints.Dispose();
            subroutineCallMutations.Dispose();
            subroutineCalls.Dispose();
            captureMutations.Dispose();
            repeatMutations.Dispose();
            frames.Dispose();
            if (rentedRepeatState is not null)
            {
                ArrayPool<int>.Shared.Return(rentedRepeatState);
            }
            if (rentedCaptureState is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCaptureState);
            }
            if (rentedCaptureRestoreGenerations is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCaptureRestoreGenerations);
            }
            if (rentedCaptureRestoreSlots is not null)
            {
                ArrayPool<int>.Shared.Return(rentedCaptureRestoreSlots);
            }
            if (rentedCaptureRestoreSnapshots is not null)
            {
                ArrayPool<Pcre2SubroutineCaptureSnapshot>.Shared.Return(rentedCaptureRestoreSnapshots);
            }
            if (rentedRepeatRestoreGenerations is not null)
            {
                ArrayPool<int>.Shared.Return(rentedRepeatRestoreGenerations);
            }
            if (rentedRepeatRestoreIds is not null)
            {
                ArrayPool<int>.Shared.Return(rentedRepeatRestoreIds);
            }
            if (rentedRepeatRestoreSnapshots is not null)
            {
                ArrayPool<Pcre2SubroutineRepeatSnapshot>.Shared.Return(rentedRepeatRestoreSnapshots);
            }
        }
    }

    private static bool TryEvaluateAssertion(
        Pcre2BacktrackingProgram assertionProgram,
        Pcre2AssertionKind assertionKind,
        ReadOnlySpan<byte> input,
        ref Utf8ValidatedInput projectionInput,
        int inputIndex,
        int outerFirstMatchingPosition,
        Pcre2MatchOptions outerMatchOptions,
        ReadOnlySpan<int> captureStarts,
        ReadOnlySpan<int> captureEnds,
        int depth,
        ref Pcre2ResourceBudget budget,
        out Pcre2CaptureByteRange[] captures,
        out string? mark,
        out int reportedStartOffsetInBytes)
    {
        budget.ChargeFrame((uint)depth, checked((ulong)depth * 32UL + (ulong)captureStarts.Length * 12UL));
        var isNegative = assertionKind is Pcre2AssertionKind.NegativeLookahead or Pcre2AssertionKind.NegativeLookbehind;
        var isLookbehind = assertionKind is Pcre2AssertionKind.PositiveLookbehind or Pcre2AssertionKind.NegativeLookbehind;
        var nestedOptions = outerMatchOptions & (Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol);
        nestedOptions |= Pcre2MatchOptions.Anchored;
        var captureMaterialization = !captureStarts.IsEmpty && assertionProgram.HasCaptureWrites
            ? Pcre2CaptureMaterialization.AssertionRanges
            : Pcre2CaptureMaterialization.None;

        if (!isLookbehind)
        {
            budget.ChargeCandidate();
            var matched = TryMatchAt(
                assertionProgram,
                input,
                ref projectionInput,
                inputIndex,
                outerFirstMatchingPosition,
                nestedOptions,
                captureMaterialization,
                captureStarts,
                captureEnds,
                depth,
                true,
                ref budget,
                out var assertionMatch,
                out _);
            captures = matched ? assertionMatch.CaptureResult.AssertionRanges : [];
            mark = matched ? assertionMatch.Mark : null;
            reportedStartOffsetInBytes = matched && !isNegative && assertionMatch.MatchBoundaryWasReset
                ? assertionMatch.StartOffsetInBytes
                : -1;
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
                mark = null;
                reportedStartOffsetInBytes = -1;
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
                    ref projectionInput,
                    candidate,
                    outerFirstMatchingPosition,
                    nestedOptions,
                    captureMaterialization,
                    captureStarts,
                    captureEnds,
                    depth,
                    true,
                    ref budget,
                    out var assertionMatch,
                    out _))
            {
                captures = assertionMatch.CaptureResult.AssertionRanges;
                mark = assertionMatch.Mark;
                reportedStartOffsetInBytes = !isNegative && assertionMatch.MatchBoundaryWasReset
                    ? assertionMatch.StartOffsetInBytes
                    : -1;
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
        mark = null;
        reportedStartOffsetInBytes = -1;
        return isNegative;
    }

    private static void ThrowIfDisallowedNonMonotoneLookaroundReset(
        Pcre2BacktrackingProgram program,
        bool isAssertion,
        bool matchBoundaryWasReset,
        int reportedStartOffsetInBytes,
        int consumedStartOffsetInBytes,
        int consumedEndOffsetInBytes)
    {
        if (!isAssertion && matchBoundaryWasReset &&
            (reportedStartOffsetInBytes < consumedStartOffsetInBytes ||
             reportedStartOffsetInBytes > consumedEndOffsetInBytes) &&
            !program.Request.Settings.AllowLookaroundBackslashK)
        {
            throw new Pcre2MatchException(
                "disallowed use of \\K in lookaround",
                Pcre2ErrorKind.DisallowedLookaroundBackslashK);
        }
    }

    private static bool IsAnyCaptureSet(
        ReadOnlySpan<int> slots,
        ReadOnlySpan<int> captureStarts,
        ReadOnlySpan<int> captureEnds)
    {
        foreach (var slot in slots)
        {
            if ((uint)slot < (uint)captureStarts.Length &&
                captureStarts[slot] >= 0 &&
                captureEnds[slot] >= captureStarts[slot])
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSubroutineActive(
        int slot,
        Utf8PooledStateStack<Pcre2SubroutineCallFrame> calls)
    {
        for (var index = calls.Count - 1; index >= 0; index--)
        {
            if (calls[index].SubroutineSlot == slot)
            {
                return true;
            }
        }

        return false;
    }

    private static void MergeAssertionCaptures(
        ReadOnlySpan<Pcre2CaptureByteRange> assertionCaptures,
        int callDepth,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        Span<int> captureOwners,
        Span<int> captureOpenOwners,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2ResourceBudget budget)
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
                callDepth,
                -1,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners,
                ref captureMutations,
                ref budget);
        }
    }

    private static void MergeAssertionMark(
        Pcre2BacktrackingProgram program,
        string? assertionMark,
        int inputIndex,
        ref int currentMarkId,
        ref int currentMarkPosition,
        ref int lastEncounteredMarkId,
        ref Utf8PooledStateStack<Pcre2MarkMutation> markMutations,
        ref Utf8PooledStateStack<Pcre2MarkTrailEntry> markTrail,
        ref Pcre2ResourceBudget budget)
    {
        if (assertionMark is null)
        {
            return;
        }

        var markId = Array.IndexOf(program.MarkNames, assertionMark);
        SetMark(
            markId,
            inputIndex,
            false,
            ref currentMarkId,
            ref currentMarkPosition,
            ref lastEncounteredMarkId,
            ref markMutations,
            ref markTrail,
            ref budget);
    }

    private static void CloseOpenCaptures(
        int inputIndex,
        int callDepth,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        Span<int> captureOwners,
        Span<int> captureOpenOwners,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2ResourceBudget budget)
    {
        for (var slot = 1; slot < captureOpenStarts.Length; slot++)
        {
            if (captureOpenStarts[slot] < 0 || captureOpenOwners[slot] != callDepth)
            {
                continue;
            }

            SetCapture(
                slot,
                captureOpenStarts[slot],
                inputIndex,
                -1,
                callDepth,
                -1,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners,
                ref captureMutations,
                ref budget);
        }
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
        int callMutationCount,
        int atomicCount,
        int markMutationCount,
        int markTrailCount,
        int repeatCount,
        int captureSlotCount,
        int depthBase,
        int callCount,
        ref Pcre2ResourceBudget budget)
    {
        var nextDepth = checked(depthBase + callCount + frames.Count + 1);
        var estimatedHeapBytes = checked(
            (ulong)nextDepth * 32UL +
            (ulong)repeatMutationCount * 12UL +
            (ulong)captureMutationCount * 24UL +
            (ulong)callMutationCount * 24UL +
            (ulong)atomicCount * 8UL +
            (ulong)markMutationCount * 8UL +
            (ulong)markTrailCount * 8UL +
            (ulong)repeatCount * 8UL +
            (ulong)captureSlotCount * 20UL);
        budget.ChargeFrame((uint)nextDepth, estimatedHeapBytes);
        if (!frames.TryPush(frame))
        {
            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
        }
    }

    private static bool TryResume(
        ref Utf8PooledStateStack<Pcre2BacktrackingFrame> frames,
        ref Utf8PooledStateStack<Pcre2RepeatMutation> repeatMutations,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> captureMutations,
        ref Utf8PooledStateStack<Pcre2SubroutineCallFrame> subroutineCalls,
        ref Utf8PooledStateStack<Pcre2SubroutineCallMutation> subroutineCallMutations,
        ref Utf8PooledStateStack<Pcre2AtomicCheckpoint> atomicCheckpoints,
        ref Utf8PooledStateStack<Pcre2MarkMutation> markMutations,
        ref Utf8PooledStateStack<Pcre2MarkTrailEntry> markTrail,
        Span<int> counts,
        Span<int> positions,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        Span<int> captureOwners,
        Span<int> captureOpenOwners,
        ref int reportedStartOffsetInBytes,
        ref bool matchBoundaryWasReset,
        ref int currentMarkId,
        ref int currentMarkPosition,
        ref Pcre2ResourceBudget budget,
        out int instructionIndex,
        out int inputIndex,
        out Pcre2BacktrackingFailure failure)
    {
        while (frames.Count != 0)
        {
            var frame = frames.Pop();
            RollbackRepeats(frame.RepeatCheckpoint, ref repeatMutations, counts, positions);
            RollbackCaptures(
                frame.CaptureCheckpoint,
                ref captureMutations,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners);
            RollbackSubroutineCalls(
                frame.CallCheckpoint,
                ref subroutineCalls,
                ref subroutineCallMutations);
            atomicCheckpoints.Truncate(frame.AtomicCheckpoint);
            RollbackMarks(
                frame.MarkMutationCheckpoint,
                frame.MarkTrailCheckpoint,
                ref markMutations,
                ref markTrail,
                ref currentMarkId,
                ref currentMarkPosition);
            reportedStartOffsetInBytes = frame.ReportedStartOffsetInBytes;
            matchBoundaryWasReset = frame.MatchBoundaryWasReset;
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

            if (frame.ResumeAction != Pcre2BacktrackingResumeAction.ControlVerb)
            {
                instructionIndex = frame.Instruction;
                inputIndex = frame.InputOffsetInBytes;
                failure = default;
                return true;
            }

            if (subroutineCalls.Count != 0)
            {
                var call = subroutineCalls[subroutineCalls.Count - 1];
                frames.Truncate(Math.Min(frames.Count, call.BacktrackingCheckpoint));
                continue;
            }

            if (frame.ControlVerb == Pcre2ControlVerbKind.Then && frame.ControlTarget >= 0)
            {
                var targetFrame = -1;
                for (var index = frames.Count - 1; index >= 0; index--)
                {
                    if (frames[index].Instruction == frame.ControlTarget &&
                        frames[index].ResumeAction == Pcre2BacktrackingResumeAction.None)
                    {
                        targetFrame = index;
                        break;
                    }
                }

                if (targetFrame >= 0)
                {
                    frames.Truncate(targetFrame + 1);
                    continue;
                }
            }

            if (frame.ControlVerb == Pcre2ControlVerbKind.Skip && frame.ControlNameId >= 0)
            {
                var namedSkipPosition = FindMarkPosition(frame.ControlNameId, markTrail);
                if (namedSkipPosition < 0)
                {
                    continue;
                }

                instructionIndex = 0;
                inputIndex = 0;
                failure = new Pcre2BacktrackingFailure(
                    Pcre2BacktrackingFailureKind.Skip,
                    namedSkipPosition);
                return false;
            }

            instructionIndex = 0;
            inputIndex = 0;
            failure = frame.ControlVerb switch
            {
                Pcre2ControlVerbKind.Commit => new Pcre2BacktrackingFailure(
                    Pcre2BacktrackingFailureKind.Commit,
                    0),
                Pcre2ControlVerbKind.Skip => new Pcre2BacktrackingFailure(
                    Pcre2BacktrackingFailureKind.Skip,
                    frame.ControlPosition),
                _ => new Pcre2BacktrackingFailure(
                    Pcre2BacktrackingFailureKind.Prune,
                    0),
            };
            return false;
        }

        instructionIndex = 0;
        inputIndex = 0;
        failure = default;
        return false;
    }

    private static void PushSubroutineCall(
        Pcre2SubroutineCallFrame frame,
        ref Utf8PooledStateStack<Pcre2SubroutineCallFrame> calls,
        ref Utf8PooledStateStack<Pcre2SubroutineCallMutation> mutations,
        int depthBase,
        ref Pcre2ResourceBudget budget)
    {
        var nextDepth = checked(depthBase + calls.Count + 1);
        budget.ChargeFrame(
            (uint)nextDepth,
            checked((ulong)nextDepth * 32UL + (ulong)(mutations.Count + 1) * 12UL));
        if (!calls.TryPush(frame))
        {
            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
        }

        RecordSubroutineCallMutation(
            Pcre2SubroutineCallMutationKind.Pushed,
            frame,
            ref mutations,
            ref budget);
    }

    private static void RestoreSubroutineState(
        Pcre2SubroutineCallFrame call,
        int callDepth,
        Span<int> counts,
        Span<int> positions,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        Span<int> captureOwners,
        Span<int> captureOpenOwners,
        Span<int> repeatRestoreGenerations,
        Span<int> repeatRestoreIds,
        Span<Pcre2SubroutineRepeatSnapshot> repeatRestoreSnapshots,
        Span<int> captureRestoreGenerations,
        Span<int> captureRestoreSlots,
        Span<Pcre2SubroutineCaptureSnapshot> captureRestoreSnapshots,
        ref int restoreGeneration,
        ref Utf8PooledStateStack<Pcre2RepeatMutation> repeatMutations,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2ResourceBudget budget)
    {
        restoreGeneration++;
        if (restoreGeneration <= 0)
        {
            repeatRestoreGenerations.Clear();
            captureRestoreGenerations.Clear();
            restoreGeneration = 1;
        }

        var generation = restoreGeneration;

        var repeatRestoreCount = 0;
        var repeatMutationCount = repeatMutations.Count;
        for (var index = call.RepeatCheckpoint; index < repeatMutationCount; index++)
        {
            var mutation = repeatMutations[index];
            if (repeatRestoreGenerations[mutation.RepeatId] == generation)
            {
                continue;
            }

            repeatRestoreGenerations[mutation.RepeatId] = generation;
            repeatRestoreIds[repeatRestoreCount++] = mutation.RepeatId;
            repeatRestoreSnapshots[mutation.RepeatId] = new Pcre2SubroutineRepeatSnapshot(
                mutation.PreviousCount,
                mutation.PreviousPosition);
        }

        for (var index = 0; index < repeatRestoreCount; index++)
        {
            var repeatId = repeatRestoreIds[index];
            var snapshot = repeatRestoreSnapshots[repeatId];
            SetRepeat(
                repeatId,
                snapshot.Count,
                snapshot.Position,
                counts,
                positions,
                ref repeatMutations,
                ref budget);
        }

        var captureRestoreCount = 0;
        var captureMutationCount = captureMutations.Count;
        for (var index = call.CaptureCheckpoint; index < captureMutationCount; index++)
        {
            var mutation = captureMutations[index];
            if (captureRestoreGenerations[mutation.Slot] == generation)
            {
                continue;
            }

            captureRestoreGenerations[mutation.Slot] = generation;
            captureRestoreSlots[captureRestoreCount++] = mutation.Slot;
            captureRestoreSnapshots[mutation.Slot] = new Pcre2SubroutineCaptureSnapshot(
                mutation.PreviousStart,
                mutation.PreviousEnd,
                mutation.PreviousOpenStart,
                mutation.PreviousOwner,
                mutation.PreviousOpenOwner);
        }

        for (var index = 0; index < captureRestoreCount; index++)
        {
            var slot = captureRestoreSlots[index];
            var finalStart = captureStarts[slot];
            var finalEnd = captureEnds[slot];
            var finalOwner = captureOwners[slot];
            var snapshot = captureRestoreSnapshots[slot];
            SetCapture(
                slot,
                snapshot.Start,
                snapshot.End,
                snapshot.OpenStart,
                snapshot.Owner,
                snapshot.OpenOwner,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners,
                ref captureMutations,
                ref budget);
            if (call.PreserveUnsetCaptures &&
                snapshot.Start < 0 && snapshot.OpenStart < 0 &&
                finalStart >= 0 && finalEnd >= finalStart)
            {
                SetCapture(
                    slot,
                    finalStart,
                    finalEnd,
                    -1,
                    Math.Max(finalOwner, callDepth),
                    -1,
                    captureStarts,
                    captureEnds,
                    captureOpenStarts,
                    captureOwners,
                    captureOpenOwners,
                    ref captureMutations,
                    ref budget);
            }
        }
    }

    private static void RecordSubroutineCallMutation(
        Pcre2SubroutineCallMutationKind kind,
        Pcre2SubroutineCallFrame frame,
        ref Utf8PooledStateStack<Pcre2SubroutineCallMutation> mutations,
        ref Pcre2ResourceBudget budget)
    {
        budget.ChargeHeap(checked((ulong)(mutations.Count + 1) * 12UL));
        if (!mutations.TryPush(new Pcre2SubroutineCallMutation(kind, frame)))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }
    }

    private static void RollbackSubroutineCalls(
        int checkpoint,
        ref Utf8PooledStateStack<Pcre2SubroutineCallFrame> calls,
        ref Utf8PooledStateStack<Pcre2SubroutineCallMutation> mutations)
    {
        while (mutations.Count > checkpoint)
        {
            var mutation = mutations.Pop();
            if (mutation.Kind == Pcre2SubroutineCallMutationKind.Pushed)
            {
                _ = calls.Pop();
            }
            else if (!calls.TryPush(mutation.Frame))
            {
                throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
            }
        }
    }

    private static void SetMark(
        int markId,
        int position,
        bool addToTrail,
        ref int currentMarkId,
        ref int currentMarkPosition,
        ref int lastEncounteredMarkId,
        ref Utf8PooledStateStack<Pcre2MarkMutation> mutations,
        ref Utf8PooledStateStack<Pcre2MarkTrailEntry> trail,
        ref Pcre2ResourceBudget budget)
    {
        if (markId < 0)
        {
            return;
        }

        budget.ChargeHeap(checked(
            (ulong)(mutations.Count + 1) * 8UL +
            (ulong)(trail.Count + (addToTrail ? 1 : 0)) * 8UL));
        if (!mutations.TryPush(new Pcre2MarkMutation(currentMarkId, currentMarkPosition)))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }

        if (addToTrail && !trail.TryPush(new Pcre2MarkTrailEntry(markId, position)))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }

        currentMarkId = markId;
        currentMarkPosition = position;
        lastEncounteredMarkId = markId;
    }

    private static void RollbackMarks(
        int mutationCheckpoint,
        int trailCheckpoint,
        ref Utf8PooledStateStack<Pcre2MarkMutation> mutations,
        ref Utf8PooledStateStack<Pcre2MarkTrailEntry> trail,
        ref int currentMarkId,
        ref int currentMarkPosition)
    {
        while (mutations.Count > mutationCheckpoint)
        {
            var mutation = mutations.Pop();
            currentMarkId = mutation.PreviousMarkId;
            currentMarkPosition = mutation.PreviousPosition;
        }

        trail.Truncate(trailCheckpoint);
    }

    private static int FindMarkPosition(
        int markId,
        Utf8PooledStateStack<Pcre2MarkTrailEntry> trail)
    {
        for (var index = trail.Count - 1; index >= 0; index--)
        {
            var mark = trail[index];
            if (mark.MarkId == markId)
            {
                return mark.Position;
            }
        }

        return -1;
    }

    private static string? GetMark(Pcre2BacktrackingProgram program, int markId) =>
        (uint)markId < (uint)program.MarkNames.Length ? program.MarkNames[markId] : null;

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
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
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
        int owner,
        int openOwner,
        Span<int> starts,
        Span<int> ends,
        Span<int> openStarts,
        Span<int> owners,
        Span<int> openOwners,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> mutations,
        ref Pcre2ResourceBudget budget)
    {
        budget.ChargeHeap(checked((ulong)(mutations.Count + 1) * 24UL + (ulong)starts.Length * 20UL));
        if (!mutations.TryPush(new Pcre2CaptureMutation(
                slot,
                starts[slot],
                ends[slot],
                openStarts[slot],
                owners[slot],
                openOwners[slot])))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }

        starts[slot] = start;
        ends[slot] = end;
        openStarts[slot] = openStart;
        owners[slot] = owner;
        openOwners[slot] = openOwner;
    }

    private static void RollbackCaptures(
        int checkpoint,
        ref Utf8PooledStateStack<Pcre2CaptureMutation> mutations,
        Span<int> starts,
        Span<int> ends,
        Span<int> openStarts,
        Span<int> owners,
        Span<int> openOwners)
    {
        while (mutations.Count > checkpoint)
        {
            var mutation = mutations.Pop();
            starts[mutation.Slot] = mutation.PreviousStart;
            ends[mutation.Slot] = mutation.PreviousEnd;
            openStarts[mutation.Slot] = mutation.PreviousOpenStart;
            owners[mutation.Slot] = mutation.PreviousOwner;
            openOwners[mutation.Slot] = mutation.PreviousOpenOwner;
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

    private static Pcre2BacktrackingCaptureResult MaterializeCaptureResult(
        Pcre2CaptureMaterialization materialization,
        int captureSlotCount,
        int matchStart,
        int matchEnd,
        ReadOnlySpan<int> captureStarts,
        ReadOnlySpan<int> captureEnds,
        ref Utf8ValidatedInput input) => materialization switch
        {
            Pcre2CaptureMaterialization.AssertionRanges =>
                Pcre2BacktrackingCaptureResult.FromAssertionRanges(
                    MaterializeCaptures(captureSlotCount, matchStart, matchEnd, captureStarts, captureEnds)),
            Pcre2CaptureMaterialization.ProjectedFinalSlots =>
                Pcre2BacktrackingCaptureResult.FromProjectedGroups(
                    Pcre2Runner.ProjectCaptures(
                        captureSlotCount,
                        matchStart,
                        matchEnd,
                        captureStarts,
                        captureEnds,
                        ref input)),
            _ => default,
        };
}
