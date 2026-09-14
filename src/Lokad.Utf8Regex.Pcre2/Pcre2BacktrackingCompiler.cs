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
    PossessiveTokenRepeat = 22,
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

    internal static Pcre2BacktrackingInstruction CreatePossessiveTokenRepeat(
        Pcre2CharacterToken token,
        int minimum,
        int maximum) =>
        new(
            Pcre2BacktrackingInstructionKind.PossessiveTokenRepeat,
            token,
            0,
            0,
            0,
            minimum,
            maximum,
            Pcre2RepeatPreference.Possessive);

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
            groupNames);
        var program = lowerer.Lower(root);
        return new Pcre2CompiledBacktrackingOutcome(
            new Pcre2BacktrackingSyntaxTree(root),
            program);
    }
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

