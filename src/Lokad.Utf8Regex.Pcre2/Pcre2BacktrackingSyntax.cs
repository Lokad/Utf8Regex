using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

// PLAN.md D09: backtracking syntax tree, node model, and program model, split from the compiler collection along the syntax ownership boundary.
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
    CollapsibleTokenRepeat = 23,
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

    internal static Pcre2BacktrackingInstruction CreateCollapsibleTokenRepeat(
        Pcre2CharacterToken token,
        int fallbackTarget,
        int exitTarget,
        int repeatId,
        int minimum,
        int maximum) =>
        new(
            Pcre2BacktrackingInstructionKind.CollapsibleTokenRepeat,
            token,
            fallbackTarget,
            exitTarget,
            repeatId,
            minimum,
            maximum,
            Pcre2RepeatPreference.Greedy);

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
