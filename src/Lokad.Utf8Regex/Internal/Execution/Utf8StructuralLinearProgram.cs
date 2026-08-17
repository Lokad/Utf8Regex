using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8StructuralLinearProgramKind : byte
{
    None = 0,
    AsciiCharClassRun = 1,
    AsciiStructuralFamily = 2,
    AsciiTokenWindow = 3,
    AsciiRepeatedSegment = 4,
    AsciiFixedTokenPattern = 5,
    AsciiQuotedRelation = 6,
    AsciiLiteralFamilyRun = 7,
    AsciiOrderedLiteralWindow = 8,
}

internal enum Utf8StructuralLinearInstructionKind : byte
{
    Literal = 0,
    AnyByte = 1,
    CharClass = 2,
    RunCharClass = 3,
    RepeatedSegment = 4,
    TokenWindow = 5,
    QuotedRelation = 6,
    LiteralFamilyRun = 7,
    Accept = 8,
    OrderedLiteralWindow = 9,
}

internal enum Utf8AsciiDeterministicStepKind : byte
{
    None = 0,
    Literal = 1,
    AnyByte = 2,
    CharClass = 3,
    RunCharClass = 4,
    Accept = 5,
}

internal enum Utf8AsciiDeterministicFixedWidthCheckKind : byte
{
    None = 0,
    Literal = 1,
    AnyByte = 2,
    CharClass = 3,
}

internal readonly struct Utf8AsciiDeterministicStep
{
    private Utf8AsciiDeterministicStep(
        Utf8AsciiDeterministicStepKind kind,
        byte literal,
        AsciiCharClass charClass,
        int minCount,
        int maxCount)
    {
        Kind = kind;
        Literal = literal;
        CharClass = charClass;
        MinCount = minCount;
        MaxCount = maxCount;
    }

    public Utf8AsciiDeterministicStepKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public static Utf8AsciiDeterministicStep CreateLiteral(byte literal) =>
        new(Utf8AsciiDeterministicStepKind.Literal, literal, default, 0, 0);

    public static Utf8AsciiDeterministicStep CreateAnyByte() =>
        new(Utf8AsciiDeterministicStepKind.AnyByte, 0, default, 0, 0);

    public static Utf8AsciiDeterministicStep CreateCharClass(AsciiCharClass charClass) =>
        new(Utf8AsciiDeterministicStepKind.CharClass, 0, charClass, 0, 0);

    public static Utf8AsciiDeterministicStep CreateRunCharClass(AsciiCharClass charClass, int minCount, int maxCount) =>
        new(Utf8AsciiDeterministicStepKind.RunCharClass, 0, charClass, minCount, maxCount);

    public static Utf8AsciiDeterministicStep CreateAccept() =>
        new(Utf8AsciiDeterministicStepKind.Accept, 0, default, 0, 0);
}

internal readonly struct Utf8AsciiDeterministicFixedWidthCheck
{
    private Utf8AsciiDeterministicFixedWidthCheck(
        Utf8AsciiDeterministicFixedWidthCheckKind kind,
        byte literal,
        AsciiCharClass charClass)
    {
        Kind = kind;
        Literal = literal;
        CharClass = charClass;
    }

    public Utf8AsciiDeterministicFixedWidthCheckKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass CharClass { get; }

    public static Utf8AsciiDeterministicFixedWidthCheck CreateLiteral(byte literal) =>
        new(Utf8AsciiDeterministicFixedWidthCheckKind.Literal, literal, default);

    public static Utf8AsciiDeterministicFixedWidthCheck CreateAnyByte() =>
        new(Utf8AsciiDeterministicFixedWidthCheckKind.AnyByte, 0, default);

    public static Utf8AsciiDeterministicFixedWidthCheck CreateCharClass(AsciiCharClass charClass) =>
        new(Utf8AsciiDeterministicFixedWidthCheckKind.CharClass, 0, charClass);
}

internal readonly struct Utf8AsciiDeterministicProgram
{
    public Utf8AsciiDeterministicProgram(
        Utf8AsciiDeterministicStep[] steps,
        AsciiFixedLiteralCheck[] fixedLiteralChecks,
        int searchLiteralOffset,
        byte[][] searchLiterals,
        bool isEndAnchored,
        bool ignoreCase,
        Utf8AsciiDeterministicFixedWidthCheck[] fixedWidthChecks,
        int fixedWidthLength)
    {
        Steps = steps;
        FixedLiteralChecks = fixedLiteralChecks;
        SearchLiteralOffset = searchLiteralOffset;
        SearchLiterals = searchLiterals;
        IsEndAnchored = isEndAnchored;
        IgnoreCase = ignoreCase;
        FixedWidthChecks = fixedWidthChecks;
        FixedWidthLength = fixedWidthLength;
    }

    public Utf8AsciiDeterministicStep[] Steps { get; }

    public AsciiFixedLiteralCheck[] FixedLiteralChecks { get; }

    public int SearchLiteralOffset { get; }

    public byte[][] SearchLiterals { get; }

    public bool IsEndAnchored { get; }

    public bool IgnoreCase { get; }

    public Utf8AsciiDeterministicFixedWidthCheck[] FixedWidthChecks { get; }

    public int FixedWidthLength { get; }

    public bool HasValue => Steps is { Length: > 0 };

    public static Utf8AsciiDeterministicProgram Create(Utf8StructuralLinearInstructionProgram instructionProgram)
    {
        if (!instructionProgram.HasValue)
        {
            return default;
        }

        var steps = new Utf8AsciiDeterministicStep[instructionProgram.Instructions.Length];
        for (var i = 0; i < instructionProgram.Instructions.Length; i++)
        {
            var instruction = instructionProgram.Instructions[i];
            steps[i] = instruction.Kind switch
            {
                Utf8StructuralLinearInstructionKind.Literal
                    => Utf8AsciiDeterministicStep.CreateLiteral(instruction.Literal),
                Utf8StructuralLinearInstructionKind.AnyByte
                    => Utf8AsciiDeterministicStep.CreateAnyByte(),
                Utf8StructuralLinearInstructionKind.CharClass when !instruction.CharClass.IsEmpty
                    => Utf8AsciiDeterministicStep.CreateCharClass(instruction.CharClass),
                Utf8StructuralLinearInstructionKind.RunCharClass when !instruction.CharClass.IsEmpty
                    => Utf8AsciiDeterministicStep.CreateRunCharClass(instruction.CharClass, instruction.MinCount, instruction.MaxCount),
                Utf8StructuralLinearInstructionKind.Accept
                    => Utf8AsciiDeterministicStep.CreateAccept(),
                _ => default,
            };

            if (steps[i].Kind == default &&
                instruction.Kind is not Utf8StructuralLinearInstructionKind.Literal and not Utf8StructuralLinearInstructionKind.Accept)
            {
                return default;
            }
        }

        var fixedWidthChecks = CreateFixedWidthChecks(steps);
        return new Utf8AsciiDeterministicProgram(
            steps,
            instructionProgram.FixedLiteralChecks,
            instructionProgram.SearchLiteralOffset,
            instructionProgram.SearchLiterals,
            instructionProgram.IsEndAnchored,
            instructionProgram.IgnoreCase,
            fixedWidthChecks,
            fixedWidthChecks.Length);
    }

    private static Utf8AsciiDeterministicFixedWidthCheck[] CreateFixedWidthChecks(Utf8AsciiDeterministicStep[] steps)
    {
        if (steps.Length <= 1)
        {
            return [];
        }

        var checks = new Utf8AsciiDeterministicFixedWidthCheck[steps.Length - 1];
        for (var i = 0; i < checks.Length; i++)
        {
            var step = steps[i];
            checks[i] = step.Kind switch
            {
                Utf8AsciiDeterministicStepKind.Literal
                    => Utf8AsciiDeterministicFixedWidthCheck.CreateLiteral(step.Literal),
                Utf8AsciiDeterministicStepKind.AnyByte
                    => Utf8AsciiDeterministicFixedWidthCheck.CreateAnyByte(),
                Utf8AsciiDeterministicStepKind.CharClass when !step.CharClass.IsEmpty
                    => Utf8AsciiDeterministicFixedWidthCheck.CreateCharClass(step.CharClass),
                _ => default,
            };

            if (checks[i].Kind == default)
            {
                return [];
            }
        }

        return checks;
    }
}

internal struct Utf8AsciiDeterministicScanState
{
    public Utf8AsciiDeterministicScanState(int nextStartIndex, int searchFrom)
    {
        NextStartIndex = nextStartIndex;
        SearchFrom = searchFrom;
    }

    public int NextStartIndex { get; set; }

    public int SearchFrom { get; set; }
}

internal readonly struct Utf8AsciiDeterministicMatch
{
    public Utf8AsciiDeterministicMatch(int index, int length)
    {
        Index = index;
        Length = length;
    }

    public int Index { get; }

    public int Length { get; }
}

internal readonly struct Utf8StructuralLinearInstruction
{
    private Utf8StructuralLinearInstruction(
        Utf8StructuralLinearInstructionKind kind,
        byte literal,
        AsciiCharClass charClass,
        int minCount,
        int maxCount,
        AsciiCharClass secondaryCharClass,
        int secondaryMinCount,
        int auxiliaryMinCount,
        string set)
    {
        Kind = kind;
        Literal = literal;
        CharClass = charClass;
        MinCount = minCount;
        MaxCount = maxCount;
        SecondaryCharClass = secondaryCharClass;
        SecondaryMinCount = secondaryMinCount;
        AuxiliaryMinCount = auxiliaryMinCount;
        Set = set;
    }

    public Utf8StructuralLinearInstructionKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiCharClass SecondaryCharClass { get; }

    public int SecondaryMinCount { get; }

    public int AuxiliaryMinCount { get; }

    public string Set { get; }

    public static Utf8StructuralLinearInstruction CreateLiteral(byte literal) =>
        new(Utf8StructuralLinearInstructionKind.Literal, literal, default, 0, 0, default, 0, 0, string.Empty);

    public static Utf8StructuralLinearInstruction CreateAnyByte() =>
        CreateMarker(Utf8StructuralLinearInstructionKind.AnyByte);

    public static Utf8StructuralLinearInstruction CreateCharClass(AsciiCharClass charClass) =>
        new(Utf8StructuralLinearInstructionKind.CharClass, 0, charClass, 0, 0, default, 0, 0, string.Empty);

    public static Utf8StructuralLinearInstruction CreateRunCharClass(AsciiCharClass charClass, int minCount, int maxCount) =>
        new(Utf8StructuralLinearInstructionKind.RunCharClass, 0, charClass, minCount, maxCount, default, 0, 0, string.Empty);

    public static Utf8StructuralLinearInstruction CreateRepeatedSegment(
        AsciiCharClass charClass,
        int minCount,
        int maxCount,
        AsciiCharClass secondaryCharClass,
        int secondaryMinCount,
        int auxiliaryMinCount,
        string set) =>
        new(
            Utf8StructuralLinearInstructionKind.RepeatedSegment,
            0,
            charClass,
            minCount,
            maxCount,
            secondaryCharClass,
            secondaryMinCount,
            auxiliaryMinCount,
            set);

    public static Utf8StructuralLinearInstruction CreateTokenWindow() =>
        CreateMarker(Utf8StructuralLinearInstructionKind.TokenWindow);

    public static Utf8StructuralLinearInstruction CreateQuotedRelation() =>
        CreateMarker(Utf8StructuralLinearInstructionKind.QuotedRelation);

    public static Utf8StructuralLinearInstruction CreateLiteralFamilyRun() =>
        CreateMarker(Utf8StructuralLinearInstructionKind.LiteralFamilyRun);

    public static Utf8StructuralLinearInstruction CreateOrderedLiteralWindow() =>
        CreateMarker(Utf8StructuralLinearInstructionKind.OrderedLiteralWindow);

    public static Utf8StructuralLinearInstruction CreateAccept() =>
        CreateMarker(Utf8StructuralLinearInstructionKind.Accept);

    private static Utf8StructuralLinearInstruction CreateMarker(Utf8StructuralLinearInstructionKind kind) =>
        new(kind, 0, default, 0, 0, default, 0, 0, string.Empty);
}

internal readonly struct Utf8StructuralLinearInstructionProgram
{
    public Utf8StructuralLinearInstructionProgram(
        Utf8StructuralLinearInstruction[] instructions,
        PreparedByteSearch search,
        int searchLiteralOffset,
        byte[][] searchLiterals,
        AsciiFixedLiteralCheck[] fixedLiteralChecks,
        bool isStartAnchored,
        bool isEndAnchored,
        bool ignoreCase)
    {
        Instructions = instructions;
        Search = search;
        SearchLiteralOffset = searchLiteralOffset;
        SearchLiterals = searchLiterals;
        SearchLiteralsSearch = searchLiterals is { Length: > 1 }
            ? new PreparedLiteralSetSearch(searchLiterals)
            : default;
        FixedLiteralChecks = fixedLiteralChecks;
        IsStartAnchored = isStartAnchored;
        IsEndAnchored = isEndAnchored;
        IgnoreCase = ignoreCase;
    }

    public Utf8StructuralLinearInstruction[] Instructions { get; }

    public PreparedByteSearch Search { get; }

    public int SearchLiteralOffset { get; }

    public byte[][] SearchLiterals { get; }

    public PreparedLiteralSetSearch SearchLiteralsSearch { get; }

    public AsciiFixedLiteralCheck[] FixedLiteralChecks { get; }

    public bool IsStartAnchored { get; }

    public bool IsEndAnchored { get; }

    public bool IgnoreCase { get; }

    public bool HasValue => Instructions.Length > 0;

    public static Utf8StructuralLinearInstructionProgram Create(AsciiSimplePatternRunPlan runPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            [
                Utf8StructuralLinearInstruction.CreateRunCharClass(runPlan.CharClass, runPlan.MinLength, runPlan.MaxLength),
                Utf8StructuralLinearInstruction.CreateAccept(),
            ],
            runPlan.Search,
            searchLiteralOffset: 0,
            searchLiterals: [],
            fixedLiteralChecks: [],
            isStartAnchored: false,
            isEndAnchored: false,
            ignoreCase: false);
    }

    public static Utf8StructuralLinearInstructionProgram Create(AsciiSimplePatternPlan simplePatternPlan)
    {
        var branch = simplePatternPlan.Branches[0];
        var instructions = new Utf8StructuralLinearInstruction[branch.Length + 1];
        for (var i = 0; i < branch.Length; i++)
        {
            var token = branch[i];
            instructions[i] = token.Kind switch
            {
                AsciiSimplePatternTokenKind.Literal => Utf8StructuralLinearInstruction.CreateLiteral(token.Literal),
                AsciiSimplePatternTokenKind.Dot => Utf8StructuralLinearInstruction.CreateAnyByte(),
                AsciiSimplePatternTokenKind.CharClass => Utf8StructuralLinearInstruction.CreateCharClass(token.CharClass),
                _ => default,
            };
        }

        instructions[^1] = Utf8StructuralLinearInstruction.CreateAccept();
        return new Utf8StructuralLinearInstructionProgram(
            instructions,
            default,
            simplePatternPlan.SearchLiteralOffset,
            simplePatternPlan.SearchLiterals,
            simplePatternPlan.FixedLiteralChecks,
            simplePatternPlan.IsStartAnchored,
            simplePatternPlan.IsEndAnchored,
            simplePatternPlan.IgnoreCase);
    }

    public static Utf8StructuralLinearInstructionProgram CreateLiteralFamilyRun(AsciiSimplePatternPlan simplePatternPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            [
                Utf8StructuralLinearInstruction.CreateLiteralFamilyRun(),
                Utf8StructuralLinearInstruction.CreateAccept(),
            ],
            default,
            simplePatternPlan.SearchLiteralOffset,
            simplePatternPlan.SearchLiterals,
            simplePatternPlan.FixedLiteralChecks,
            simplePatternPlan.IsStartAnchored,
            simplePatternPlan.IsEndAnchored,
            simplePatternPlan.IgnoreCase);
    }

    public static Utf8StructuralLinearInstructionProgram Create(AsciiStructuralRepeatedSegmentPlan repeatedSegmentPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            [
                Utf8StructuralLinearInstruction.CreateRepeatedSegment(
                    repeatedSegmentPlan.LeadingCharClass,
                    repeatedSegmentPlan.RepetitionMinCount,
                    repeatedSegmentPlan.RepetitionMaxCount,
                    repeatedSegmentPlan.TrailingCharClass,
                    repeatedSegmentPlan.TrailingMinCount,
                    repeatedSegmentPlan.SeparatorMinCount,
                    repeatedSegmentPlan.SeparatorSet),
                Utf8StructuralLinearInstruction.CreateAccept(),
            ],
            default,
            searchLiteralOffset: 0,
            searchLiterals: [],
            fixedLiteralChecks: [],
            isStartAnchored: false,
            isEndAnchored: false,
            ignoreCase: false);
    }

    public static Utf8StructuralLinearInstructionProgram Create(AsciiStructuralTokenWindowPlan tokenWindowPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            [
                Utf8StructuralLinearInstruction.CreateTokenWindow(),
                Utf8StructuralLinearInstruction.CreateAccept(),
            ],
            tokenWindowPlan.LeadingRunPlan.Search,
            searchLiteralOffset: 0,
            searchLiterals: [],
            fixedLiteralChecks: [],
            isStartAnchored: false,
            isEndAnchored: false,
            ignoreCase: false);
    }

    public static Utf8StructuralLinearInstructionProgram Create(AsciiStructuralQuotedRelationPlan quotedRelationPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            [
                Utf8StructuralLinearInstruction.CreateQuotedRelation(),
                Utf8StructuralLinearInstruction.CreateAccept(),
            ],
            default,
            searchLiteralOffset: 0,
            searchLiterals: [],
            fixedLiteralChecks: [],
            isStartAnchored: false,
            isEndAnchored: false,
            ignoreCase: false);
    }

    public static Utf8StructuralLinearInstructionProgram Create(AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            [
                Utf8StructuralLinearInstruction.CreateOrderedLiteralWindow(),
                Utf8StructuralLinearInstruction.CreateAccept(),
            ],
            default,
            searchLiteralOffset: 0,
            searchLiterals: [],
            fixedLiteralChecks: [],
            isStartAnchored: false,
            isEndAnchored: false,
            ignoreCase: false);
    }
}

internal readonly struct Utf8StructuralLinearProgram
{
    private Utf8StructuralLinearProgram(
        Utf8StructuralLinearProgramKind kind,
        Utf8StructuralLinearInstructionProgram instructionProgram,
        Utf8AsciiDeterministicProgram deterministicProgram,
        AsciiSimplePatternRunPlan runPlan,
        AsciiSimplePatternPlan simplePatternPlan,
        bool allowsUtf8ByteSafe,
        AsciiStructuralTokenWindowPlan tokenWindowPlan,
        AsciiStructuralRepeatedSegmentPlan repeatedSegmentPlan,
        AsciiStructuralQuotedRelationPlan quotedRelationPlan,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan,
        AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan,
        Utf8SearchPlan searchPlan,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralVerifierPlan structuralVerifierPlan)
    {
        Kind = kind;
        InstructionProgram = instructionProgram;
        DeterministicProgram = deterministicProgram;
        RunPlan = runPlan;
        SimplePatternPlan = simplePatternPlan;
        AllowsUtf8ByteSafe = allowsUtf8ByteSafe;
        TokenWindowPlan = tokenWindowPlan;
        RepeatedSegmentPlan = repeatedSegmentPlan;
        QuotedRelationPlan = quotedRelationPlan;
        OrderedLiteralWindowPlan = orderedLiteralWindowPlan;
        StructuralIdentifierFamilyPlan = structuralIdentifierFamilyPlan;
        SearchPlan = searchPlan;
        StructuralSearchPlan = structuralSearchPlan;
        StructuralVerifierPlan = structuralVerifierPlan;
    }

    public Utf8StructuralLinearProgramKind Kind { get; }

    public Utf8StructuralLinearInstructionProgram InstructionProgram { get; }

    public Utf8AsciiDeterministicProgram DeterministicProgram { get; }

    public AsciiSimplePatternRunPlan RunPlan { get; }

    public AsciiSimplePatternPlan SimplePatternPlan { get; }

    public bool AllowsUtf8ByteSafe { get; }

    public AsciiStructuralTokenWindowPlan TokenWindowPlan { get; }

    public AsciiStructuralRepeatedSegmentPlan RepeatedSegmentPlan { get; }

    public AsciiStructuralQuotedRelationPlan QuotedRelationPlan { get; }

    public AsciiOrderedLiteralWindowPlan OrderedLiteralWindowPlan { get; }

    public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan { get; }

    public Utf8SearchPlan SearchPlan { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8StructuralVerifierPlan StructuralVerifierPlan { get; }

    public bool HasValue => Kind != Utf8StructuralLinearProgramKind.None;

    private static Utf8StructuralLinearProgram ForCharClassRun(
        Utf8StructuralLinearInstructionProgram instructions,
        Utf8AsciiDeterministicProgram deterministic,
        AsciiSimplePatternRunPlan runPlan,
        bool allowsUtf8ByteSafe) =>
        new(Utf8StructuralLinearProgramKind.AsciiCharClassRun, instructions, deterministic, runPlan, default,
            allowsUtf8ByteSafe, default, default, default, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForFixedTokenPattern(
        Utf8StructuralLinearInstructionProgram instructions,
        Utf8AsciiDeterministicProgram deterministic,
        AsciiSimplePatternPlan simplePatternPlan) =>
        new(Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern, instructions, deterministic, default, simplePatternPlan,
            simplePatternPlan.IsUtf8ByteSafe, default, default, default, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForLiteralFamilyRun(
        Utf8StructuralLinearInstructionProgram instructions,
        AsciiSimplePatternPlan simplePatternPlan) =>
        new(Utf8StructuralLinearProgramKind.AsciiLiteralFamilyRun, instructions, default, default, simplePatternPlan,
            simplePatternPlan.IsUtf8ByteSafe, default, default, default, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForStructuralFamily(
        AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralVerifierPlan verifierPlan) =>
        new(Utf8StructuralLinearProgramKind.AsciiStructuralFamily, default, default, default, default,
            false, default, default, default, default, familyPlan, searchPlan, structuralSearchPlan, verifierPlan);

    private static Utf8StructuralLinearProgram ForTokenWindow(
        Utf8StructuralLinearInstructionProgram instructions,
        AsciiStructuralTokenWindowPlan plan) =>
        new(Utf8StructuralLinearProgramKind.AsciiTokenWindow, instructions, default, default, default,
            true, plan, default, default, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForRepeatedSegment(
        Utf8StructuralLinearInstructionProgram instructions,
        AsciiStructuralRepeatedSegmentPlan plan) =>
        new(Utf8StructuralLinearProgramKind.AsciiRepeatedSegment, instructions, default, default, default,
            false, default, plan, default, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForQuotedRelation(
        Utf8StructuralLinearInstructionProgram instructions,
        AsciiStructuralQuotedRelationPlan plan) =>
        new(Utf8StructuralLinearProgramKind.AsciiQuotedRelation, instructions, default, default, default,
            false, default, default, plan, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForOrderedLiteralWindow(
        Utf8StructuralLinearInstructionProgram instructions,
        AsciiOrderedLiteralWindowPlan plan,
        Utf8SearchPlan searchPlan) =>
        new(Utf8StructuralLinearProgramKind.AsciiOrderedLiteralWindow, instructions, default, default, default,
            true, default, default, default, plan, default, searchPlan, default, default);

    public static Utf8StructuralLinearProgram Create(
        NativeExecutionKind executionKind,
        AsciiSimplePatternPlan simplePatternPlan,
        AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan,
        AsciiStructuralTokenWindowPlan structuralTokenWindowPlan,
        AsciiStructuralRepeatedSegmentPlan structuralRepeatedSegmentPlan,
        AsciiStructuralQuotedRelationPlan structuralQuotedRelationPlan,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan,
        Utf8StructuralVerifierPlan structuralVerifier,
        Utf8SearchPlan searchPlan,
        Utf8StructuralSearchPlan structuralSearchPlan)
    {
        return executionKind switch
        {
            NativeExecutionKind.AsciiSimplePattern when simplePatternPlan.RunPlan.HasValue
                => ForCharClassRun(
                    Utf8StructuralLinearInstructionProgram.Create(simplePatternPlan.RunPlan),
                    Utf8AsciiDeterministicProgram.Create(Utf8StructuralLinearInstructionProgram.Create(simplePatternPlan.RunPlan)),
                    simplePatternPlan.RunPlan,
                    simplePatternPlan.IsUtf8ByteSafe),
            NativeExecutionKind.AsciiSimplePattern when CanUseFixedTokenPattern(simplePatternPlan)
                => ForFixedTokenPattern(
                    Utf8StructuralLinearInstructionProgram.Create(simplePatternPlan),
                    Utf8AsciiDeterministicProgram.Create(Utf8StructuralLinearInstructionProgram.Create(simplePatternPlan)),
                    simplePatternPlan),
            NativeExecutionKind.AsciiSimplePattern when CanUseLiteralFamilyRunPattern(simplePatternPlan)
                => ForLiteralFamilyRun(
                    Utf8StructuralLinearInstructionProgram.CreateLiteralFamilyRun(simplePatternPlan),
                    simplePatternPlan),
            NativeExecutionKind.AsciiStructuralIdentifierFamily when structuralVerifier.Kind == Utf8StructuralVerifierKind.AsciiStructuralProgram
                => ForStructuralFamily(structuralIdentifierFamilyPlan, searchPlan, structuralSearchPlan, structuralVerifier),
            NativeExecutionKind.AsciiStructuralTokenWindow when structuralTokenWindowPlan.HasValue
                => ForTokenWindow(Utf8StructuralLinearInstructionProgram.Create(structuralTokenWindowPlan), structuralTokenWindowPlan),
            NativeExecutionKind.AsciiStructuralRepeatedSegment when structuralRepeatedSegmentPlan.HasValue
                => ForRepeatedSegment(Utf8StructuralLinearInstructionProgram.Create(structuralRepeatedSegmentPlan), structuralRepeatedSegmentPlan),
            NativeExecutionKind.AsciiStructuralQuotedRelation when structuralQuotedRelationPlan.HasValue
                => ForQuotedRelation(Utf8StructuralLinearInstructionProgram.Create(structuralQuotedRelationPlan), structuralQuotedRelationPlan),
            NativeExecutionKind.AsciiOrderedLiteralWindow when orderedLiteralWindowPlan.HasValue
                => ForOrderedLiteralWindow(
                    Utf8StructuralLinearInstructionProgram.Create(orderedLiteralWindowPlan),
                    orderedLiteralWindowPlan,
                    searchPlan),
            _ => default,
        };
    }

    private static bool CanUseFixedTokenPattern(AsciiSimplePatternPlan simplePatternPlan)
    {
        if (simplePatternPlan.RunPlan.HasValue ||
            simplePatternPlan.Branches.Length != 1 ||
            !simplePatternPlan.IsFixedLength)
        {
            return false;
        }

        foreach (var check in simplePatternPlan.FixedLiteralChecks)
        {
            if (check.Offset < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanUseLiteralFamilyRunPattern(AsciiSimplePatternPlan simplePatternPlan)
    {
        if (simplePatternPlan.RunPlan.HasValue ||
            simplePatternPlan.Branches.Length <= 1 ||
            !simplePatternPlan.IsFixedLength ||
            simplePatternPlan.SearchLiteralOffset != 0 ||
            simplePatternPlan.SearchLiterals.Length <= 1 ||
            simplePatternPlan.IgnoreCase)
        {
            return false;
        }

        var firstBranch = simplePatternPlan.Branches[0];
        var runStart = 0;
        while (runStart < firstBranch.Length && firstBranch[runStart].Kind == AsciiSimplePatternTokenKind.Literal)
        {
            runStart++;
        }

        if (runStart == 0 || runStart >= firstBranch.Length)
        {
            return false;
        }

        if (firstBranch[runStart].Kind != AsciiSimplePatternTokenKind.CharClass ||
            firstBranch[runStart].CharClass.IsEmpty)
        {
            return false;
        }

        var runClass = firstBranch[runStart].CharClass;

        for (var i = runStart; i < firstBranch.Length; i++)
        {
            if (firstBranch[i].Kind != AsciiSimplePatternTokenKind.CharClass ||
                firstBranch[i].CharClass != runClass)
            {
                return false;
            }
        }

        foreach (var branch in simplePatternPlan.Branches)
        {
            if (branch.Length != firstBranch.Length)
            {
                return false;
            }

            for (var i = 0; i < runStart; i++)
            {
                if (branch[i].Kind != AsciiSimplePatternTokenKind.Literal)
                {
                    return false;
                }
            }

            for (var i = runStart; i < branch.Length; i++)
            {
                if (branch[i].Kind != AsciiSimplePatternTokenKind.CharClass ||
                    branch[i].CharClass != runClass)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

internal abstract class Utf8StructuralLinearRuntime
{
    private readonly Utf8StructuralLinearProgram _program;

    protected Utf8StructuralLinearRuntime(Utf8StructuralLinearProgram program)
    {
        _program = program;
    }

    protected ref readonly Utf8StructuralLinearProgram Program => ref _program;

    /// <summary>
    /// Evaluates whether the validated complete subject contains a match. All
    /// scanning and verification charges the supplied deadline; false is final.
    /// </summary>
    public abstract bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Counts nonoverlapping matches in the validated complete subject using
    /// .NET global progression and charging the supplied deadline.
    /// </summary>
    public abstract int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Returns the first capture-zero match in the validated complete subject,
    /// or <see cref="Utf8ValueMatch.NoMatch"/>, while charging the deadline.
    /// </summary>
    public abstract Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Finds the first match whose byte start is at or after
    /// <paramref name="startIndex"/> in a validated subject. The start must be
    /// an admitted boundary. A false result definitively exhausts the suffix,
    /// output values are then unspecified, and all work charges the deadline.
    /// </summary>
    public abstract bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength);

    public static Utf8StructuralLinearRuntime Create(Utf8StructuralLinearProgram program)
    {
        return program.Kind switch
        {
            Utf8StructuralLinearProgramKind.AsciiCharClassRun => new Utf8AsciiInstructionLinearRuntime(program),
            Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern => new Utf8AsciiInstructionLinearRuntime(program),
            Utf8StructuralLinearProgramKind.AsciiLiteralFamilyRun => new Utf8AsciiInstructionLinearRuntime(program),
            Utf8StructuralLinearProgramKind.AsciiStructuralFamily => new Utf8AsciiStructuralFamilyLinearRuntime(program),
            Utf8StructuralLinearProgramKind.AsciiTokenWindow => new Utf8AsciiInstructionLinearRuntime(program),
            Utf8StructuralLinearProgramKind.AsciiRepeatedSegment => new Utf8AsciiInstructionLinearRuntime(program),
            Utf8StructuralLinearProgramKind.AsciiQuotedRelation => new Utf8AsciiInstructionLinearRuntime(program),
            Utf8StructuralLinearProgramKind.AsciiOrderedLiteralWindow => new Utf8AsciiInstructionLinearRuntime(program),
            _ => new Utf8NoStructuralLinearRuntime(program),
        };
    }

}

internal sealed class Utf8NoStructuralLinearRuntime : Utf8StructuralLinearRuntime
{
    public Utf8NoStructuralLinearRuntime(Utf8StructuralLinearProgram program)
        : base(program)
    {
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return false;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return 0;
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return Utf8ValueMatch.NoMatch;
    }

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        return false;
    }
}

internal sealed class Utf8AsciiInstructionLinearRuntime : Utf8StructuralLinearRuntime
{
    public Utf8AsciiInstructionLinearRuntime(Utf8StructuralLinearProgram program)
        : base(program)
    {
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return Utf8AsciiInstructionLinearExecutor.SupportsInput(Program, validation) &&
            Utf8AsciiInstructionLinearExecutor.FindNext(Program, input, 0, budget, out _) >= 0;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        if (!Utf8AsciiInstructionLinearExecutor.SupportsInput(Program, validation))
        {
            return 0;
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralLinearAutomaton);
        if (Program.DeterministicProgram.HasValue)
        {
            if (Utf8AsciiDeterministicFixedWidthCountExecutor.TryCount(Program, input, budget, out var vectorizedCount))
            {
                return vectorizedCount;
            }

            return Utf8AsciiInstructionLinearExecutor.CountDeterministic(Program, input, budget);
        }

        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = Utf8AsciiInstructionLinearExecutor.FindNext(Program, input, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        if (!Utf8AsciiInstructionLinearExecutor.SupportsInput(Program, validation))
        {
            return Utf8ValueMatch.NoMatch;
        }

        var index = Program.DeterministicProgram.HasValue
            ? Utf8AsciiInstructionLinearExecutor.FindNextDeterministic(Program, input, 0, budget, out var matchedLength)
            : Utf8AsciiInstructionLinearExecutor.FindNext(Program, input, 0, budget, out matchedLength);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
    }

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        if (!Utf8AsciiInstructionLinearExecutor.SupportsInput(Program, validation))
        {
            matchIndex = -1;
            matchedLength = 0;
            return false;
        }

        matchIndex = Program.DeterministicProgram.HasValue
            ? Utf8AsciiInstructionLinearExecutor.FindNextDeterministic(Program, input, startIndex, budget, out matchedLength)
            : Utf8AsciiInstructionLinearExecutor.FindNext(Program, input, startIndex, budget, out matchedLength);
        return matchIndex >= 0;
    }
}

internal static class Utf8AsciiFixedTokenLinearExecutor
{
    public static bool SupportsInput(in Utf8StructuralLinearProgram program, Utf8ValidationResult validation)
    {
        return validation.IsAscii || program.AllowsUtf8ByteSafe;
    }

    public static int FindNext(in Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        return Utf8AsciiInstructionLinearExecutor.FindNext(program, input, startIndex, budget, out matchedLength);
    }
}

internal static class Utf8AsciiInstructionLinearExecutor
{
    public static bool SupportsInput(in Utf8StructuralLinearProgram program, Utf8ValidationResult validation)
    {
        return validation.IsAscii || program.AllowsUtf8ByteSafe;
    }

    public static int FindNext(in Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        matchedLength = 0;
        var instructionProgram = program.InstructionProgram;
        var diagnostics = Utf8SearchDiagnosticsSession.Current;
        if (!instructionProgram.HasValue)
        {
            return -1;
        }

        if (instructionProgram.IsStartAnchored && startIndex != 0)
        {
            return -1;
        }

        if (instructionProgram.Instructions.Length >= 2 &&
            instructionProgram.Instructions[0].Kind == Utf8StructuralLinearInstructionKind.LiteralFamilyRun)
        {
            var searchFrom = startIndex + instructionProgram.SearchLiteralOffset;
            while (searchFrom <= input.Length)
            {
                budget.Step();

                var relative = instructionProgram.SearchLiterals.Length == 1
                    ? AsciiSearch.IndexOfExact(input[searchFrom..], instructionProgram.SearchLiterals[0])
                    : instructionProgram.SearchLiteralsSearch.IndexOf(input[searchFrom..]);
                if (relative < 0)
                {
                    return -1;
                }

                var absoluteAnchor = searchFrom + relative;
                var candidate = absoluteAnchor - instructionProgram.SearchLiteralOffset;
                diagnostics?.CountSearchCandidate();
                if (candidate >= startIndex && TryMatchLiteralFamilyRunAt(input, program.SimplePatternPlan, candidate, out matchedLength))
                {
                    diagnostics?.CountVerifierInvocation();
                    diagnostics?.CountVerifierMatch();
                    return candidate;
                }

                searchFrom = absoluteAnchor + 1;
            }

            return -1;
        }

        if (instructionProgram.SearchLiterals.Length > 0)
        {
            var searchFrom = startIndex + instructionProgram.SearchLiteralOffset;
            while (searchFrom <= input.Length)
            {
                budget.Step();

                var relative = instructionProgram.SearchLiterals.Length == 1
                    ? (instructionProgram.IgnoreCase
                        ? AsciiSearch.IndexOfIgnoreCase(input[searchFrom..], instructionProgram.SearchLiterals[0])
                        : AsciiSearch.IndexOfExact(input[searchFrom..], instructionProgram.SearchLiterals[0]))
                    : instructionProgram.SearchLiteralsSearch.IndexOf(input[searchFrom..]);
                if (relative < 0)
                {
                    return -1;
                }

                var absoluteAnchor = searchFrom + relative;
                var candidate = absoluteAnchor - instructionProgram.SearchLiteralOffset;
                diagnostics?.CountSearchCandidate();
                if (candidate >= startIndex && TryMatchAt(input, program, candidate, out matchedLength))
                {
                    diagnostics?.CountVerifierInvocation();
                    diagnostics?.CountVerifierMatch();
                    return candidate;
                }

                searchFrom = absoluteAnchor + 1;
            }

            return -1;
        }

        if (instructionProgram.Instructions.Length >= 2 &&
            instructionProgram.Instructions[0].Kind == Utf8StructuralLinearInstructionKind.RunCharClass &&
            instructionProgram.Instructions[1].Kind == Utf8StructuralLinearInstructionKind.Accept &&
            !instructionProgram.Instructions[0].CharClass.IsEmpty)
        {
            var runCharClass = instructionProgram.Instructions[0].CharClass;
            var matchIndex = Utf8AsciiCharClassRunExecutor.FindNext(
                input,
                new AsciiSimplePatternRunPlan(runCharClass, instructionProgram.Instructions[0].MinCount, instructionProgram.Instructions[0].MaxCount),
                startIndex,
                out matchedLength,
                budget);
            if (matchIndex >= 0)
            {
                diagnostics?.CountSearchCandidate();
                diagnostics?.CountVerifierInvocation();
                diagnostics?.CountVerifierMatch();
            }

            return matchIndex;
        }

        if (instructionProgram.Instructions.Length >= 2 &&
            instructionProgram.Instructions[0].Kind == Utf8StructuralLinearInstructionKind.RepeatedSegment &&
            instructionProgram.Instructions[1].Kind == Utf8StructuralLinearInstructionKind.Accept &&
            !instructionProgram.Instructions[0].CharClass.IsEmpty)
        {
            var segmentLeading = instructionProgram.Instructions[0].CharClass;
            var candidateStart = startIndex;
            while (candidateStart < input.Length)
            {
                while (candidateStart < input.Length && !segmentLeading.Contains(input[candidateStart]))
                {
                    candidateStart++;
                }

                if (candidateStart >= input.Length)
                {
                    return -1;
                }

                budget.Step();
                diagnostics?.CountSearchCandidate();
                if (TryMatchAt(input, program, candidateStart, out matchedLength))
                {
                    diagnostics?.CountVerifierInvocation();
                    diagnostics?.CountVerifierMatch();
                    return candidateStart;
                }

                candidateStart++;
            }

            return -1;
        }

        if (instructionProgram.Instructions.Length >= 2 &&
            instructionProgram.Instructions[0].Kind == Utf8StructuralLinearInstructionKind.TokenWindow)
        {
            var plan = program.TokenWindowPlan;
            var candidateStart = startIndex;
            while (candidateStart <= input.Length)
            {
                var runStart = Utf8AsciiCharClassRunExecutor.FindNext(
                    input,
                    plan.LeadingRunPlan,
                    candidateStart,
                    out var runLength,
                    budget);
                if (runStart < 0)
                {
                    return -1;
                }

                candidateStart = runStart + 1;
                if (runLength != plan.LeadingLength)
                {
                    continue;
                }

                diagnostics?.CountSearchCandidate();
                budget.Step();
                if (TryMatchTokenWindowAt(input, plan, runStart, out matchedLength))
                {
                    diagnostics?.CountVerifierInvocation();
                    diagnostics?.CountVerifierMatch();
                    return runStart;
                }
            }

            return -1;
        }

        if (instructionProgram.Instructions.Length >= 2 &&
            instructionProgram.Instructions[0].Kind == Utf8StructuralLinearInstructionKind.QuotedRelation)
        {
            var searchStart = Math.Max(startIndex, 0);
            while (searchStart < input.Length)
            {
                budget.Step();
                var relative = input[searchStart..].IndexOfAny((byte)'"', (byte)'\'');
                if (relative < 0)
                {
                    return -1;
                }

                var candidate = searchStart + relative;
                diagnostics?.CountSearchCandidate();
                if (TryMatchQuotedRelationAt(input, candidate, program.QuotedRelationPlan, out matchedLength))
                {
                    diagnostics?.CountVerifierInvocation();
                    diagnostics?.CountVerifierMatch();
                    return candidate;
                }

                searchStart = candidate + 1;
            }

            return -1;
        }

        if (instructionProgram.Instructions.Length >= 2 &&
            instructionProgram.Instructions[0].Kind == Utf8StructuralLinearInstructionKind.OrderedLiteralWindow)
        {
            var result = AsciiOrderedLiteralWindowExecutor.FindNext(
                input,
                program.OrderedLiteralWindowPlan,
                program.SearchPlan,
                startIndex,
                budget,
                out matchedLength);
            if (result >= 0)
            {
                diagnostics?.CountSearchCandidate();
                diagnostics?.CountVerifierInvocation();
                diagnostics?.CountVerifierMatch();
            }

            return result;
        }

        for (var candidate = startIndex; candidate <= input.Length; candidate++)
        {
            budget.Step();
            diagnostics?.CountSearchCandidate();
            if (TryMatchAt(input, program, candidate, out matchedLength))
            {
                diagnostics?.CountVerifierInvocation();
                diagnostics?.CountVerifierMatch();
                return candidate;
            }
        }

        return -1;
    }

    public static int FindNextDeterministic(in Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        matchedLength = 0;
        if (!program.DeterministicProgram.HasValue)
        {
            return -1;
        }

        if (program.DeterministicProgram.FixedWidthLength > 0)
        {
            return FindNextDeterministicFixedWidth(program, input, startIndex, budget, out matchedLength);
        }

        var state = new Utf8AsciiDeterministicScanState(startIndex, startIndex + program.DeterministicProgram.SearchLiteralOffset);
        return TryFindNextNonOverlappingDeterministicMatch(program, input, ref state, budget, out var matchIndex, out matchedLength)
            ? matchIndex
            : -1;
    }

    public static int FindNextDeterministicFixedWidth(in Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        matchedLength = 0;
        if (!program.DeterministicProgram.HasValue || program.DeterministicProgram.FixedWidthLength <= 0)
        {
            return -1;
        }

        var state = new Utf8AsciiDeterministicScanState(startIndex, startIndex + program.DeterministicProgram.SearchLiteralOffset);
        if (TryFindNextNonOverlappingDeterministicFixedWidthMatch(program, input, ref state, budget, out var matchIndex))
        {
            matchedLength = program.DeterministicProgram.FixedWidthLength;
            return matchIndex;
        }

        return -1;
    }

    public static bool TryFindNextNonOverlappingDeterministicRawMatch(
        in Utf8StructuralLinearProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8AsciiDeterministicScanState state,
        Utf8ExecutionDeadline budget,
        out Utf8AsciiDeterministicMatch match)
    {
        match = default;
        if (!program.DeterministicProgram.HasValue)
        {
            return false;
        }

        if (program.DeterministicProgram.FixedWidthLength > 0)
        {
            if (!TryFindNextNonOverlappingDeterministicFixedWidthMatch(program, input, ref state, budget, out var fixedWidthMatchIndex))
            {
                return false;
            }

            match = new Utf8AsciiDeterministicMatch(fixedWidthMatchIndex, program.DeterministicProgram.FixedWidthLength);
            return true;
        }

        if (!TryFindNextNonOverlappingDeterministicMatch(program, input, ref state, budget, out var matchIndex, out var matchedLength))
        {
            return false;
        }

        match = new Utf8AsciiDeterministicMatch(matchIndex, matchedLength);
        return true;
    }

    public static bool TryFindNextNonOverlappingDeterministicMatch(
        in Utf8StructuralLinearProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8AsciiDeterministicScanState state,
        Utf8ExecutionDeadline budget,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if (!program.DeterministicProgram.HasValue)
        {
            return false;
        }

        var deterministicProgram = program.DeterministicProgram;
        var diagnostics = Utf8SearchDiagnosticsSession.Current;
        if (program.InstructionProgram.IsStartAnchored && state.NextStartIndex != 0)
        {
            return false;
        }

        if (deterministicProgram.SearchLiterals.Length > 0)
        {
            if (diagnostics is null)
            {
                var searchFrom = Math.Max(state.SearchFrom, state.NextStartIndex + deterministicProgram.SearchLiteralOffset);
                while (searchFrom <= input.Length)
                {
                    budget.Step();

                    var relative = deterministicProgram.SearchLiterals.Length == 1
                        ? (deterministicProgram.IgnoreCase
                            ? AsciiSearch.IndexOfIgnoreCase(input[searchFrom..], deterministicProgram.SearchLiterals[0])
                            : AsciiSearch.IndexOfExact(input[searchFrom..], deterministicProgram.SearchLiterals[0]))
                        : AsciiSearch.IndexOfAnyExact(input[searchFrom..], deterministicProgram.SearchLiterals);
                    if (relative < 0)
                    {
                        return false;
                    }

                    var absoluteAnchor = searchFrom + relative;
                    var candidate = absoluteAnchor - deterministicProgram.SearchLiteralOffset;
                    if (candidate >= state.NextStartIndex && TryMatchAt(input, deterministicProgram, candidate, out matchedLength))
                    {
                        matchIndex = candidate;
                        state.NextStartIndex = candidate + Math.Max(matchedLength, 1);
                        state.SearchFrom = state.NextStartIndex + deterministicProgram.SearchLiteralOffset;
                        return true;
                    }

                    searchFrom = absoluteAnchor + 1;
                }

                state.SearchFrom = searchFrom;
                return false;
            }

            var searchFromWithDiagnostics = Math.Max(state.SearchFrom, state.NextStartIndex + deterministicProgram.SearchLiteralOffset);
            while (searchFromWithDiagnostics <= input.Length)
            {
                budget.Step();

                var relative = deterministicProgram.SearchLiterals.Length == 1
                    ? (deterministicProgram.IgnoreCase
                        ? AsciiSearch.IndexOfIgnoreCase(input[searchFromWithDiagnostics..], deterministicProgram.SearchLiterals[0])
                        : AsciiSearch.IndexOfExact(input[searchFromWithDiagnostics..], deterministicProgram.SearchLiterals[0]))
                    : AsciiSearch.IndexOfAnyExact(input[searchFromWithDiagnostics..], deterministicProgram.SearchLiterals);
                if (relative < 0)
                {
                    return false;
                }

                var absoluteAnchor = searchFromWithDiagnostics + relative;
                var candidate = absoluteAnchor - deterministicProgram.SearchLiteralOffset;
                diagnostics?.CountSearchCandidate();
                if (candidate >= state.NextStartIndex && TryMatchAt(input, deterministicProgram, candidate, out matchedLength))
                {
                    diagnostics?.CountVerifierInvocation();
                    diagnostics?.CountVerifierMatch();
                    matchIndex = candidate;
                    state.NextStartIndex = candidate + Math.Max(matchedLength, 1);
                    state.SearchFrom = state.NextStartIndex + deterministicProgram.SearchLiteralOffset;
                    return true;
                }

                searchFromWithDiagnostics = absoluteAnchor + 1;
            }

            state.SearchFrom = searchFromWithDiagnostics;
            return false;
        }

        if (diagnostics is null)
        {
            for (var candidate = state.NextStartIndex; candidate <= input.Length; candidate++)
            {
                budget.Step();
                if (TryMatchAt(input, deterministicProgram, candidate, out matchedLength))
                {
                    matchIndex = candidate;
                    state.NextStartIndex = candidate + Math.Max(matchedLength, 1);
                    state.SearchFrom = state.NextStartIndex;
                    return true;
                }
            }

            state.SearchFrom = input.Length + 1;
            return false;
        }

        for (var candidate = state.NextStartIndex; candidate <= input.Length; candidate++)
        {
            budget.Step();
            diagnostics?.CountSearchCandidate();
            if (TryMatchAt(input, deterministicProgram, candidate, out matchedLength))
            {
                diagnostics?.CountVerifierInvocation();
                diagnostics?.CountVerifierMatch();
                matchIndex = candidate;
                state.NextStartIndex = candidate + Math.Max(matchedLength, 1);
                state.SearchFrom = state.NextStartIndex;
                return true;
            }
        }

        state.SearchFrom = input.Length + 1;
        return false;
    }

    public static bool TryFindNextNonOverlappingDeterministicFixedWidthMatch(
        in Utf8StructuralLinearProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8AsciiDeterministicScanState state,
        Utf8ExecutionDeadline budget,
        out int matchIndex)
    {
        matchIndex = -1;
        if (!program.DeterministicProgram.HasValue)
        {
            return false;
        }

        var deterministicProgram = program.DeterministicProgram;
        if (deterministicProgram.FixedWidthLength <= 0)
        {
            return false;
        }

        var diagnostics = Utf8SearchDiagnosticsSession.Current;
        if (program.InstructionProgram.IsStartAnchored && state.NextStartIndex != 0)
        {
            return false;
        }

        if (deterministicProgram.SearchLiterals.Length > 0)
        {
            if (diagnostics is null)
            {
                var searchFrom = Math.Max(state.SearchFrom, state.NextStartIndex + deterministicProgram.SearchLiteralOffset);
                while (searchFrom <= input.Length)
                {
                    budget.Step();

                    var relative = deterministicProgram.SearchLiterals.Length == 1
                        ? (deterministicProgram.IgnoreCase
                            ? AsciiSearch.IndexOfIgnoreCase(input[searchFrom..], deterministicProgram.SearchLiterals[0])
                            : AsciiSearch.IndexOfExact(input[searchFrom..], deterministicProgram.SearchLiterals[0]))
                        : AsciiSearch.IndexOfAnyExact(input[searchFrom..], deterministicProgram.SearchLiterals);
                    if (relative < 0)
                    {
                        return false;
                    }

                    var absoluteAnchor = searchFrom + relative;
                    var candidate = absoluteAnchor - deterministicProgram.SearchLiteralOffset;
                    if (candidate >= state.NextStartIndex &&
                        TryMatchFixedWidthAt(input, deterministicProgram, candidate, out _))
                    {
                        matchIndex = candidate;
                        state.NextStartIndex = candidate + deterministicProgram.FixedWidthLength;
                        state.SearchFrom = state.NextStartIndex + deterministicProgram.SearchLiteralOffset;
                        return true;
                    }

                    searchFrom = absoluteAnchor + 1;
                }

                state.SearchFrom = searchFrom;
                return false;
            }

            var searchFromWithDiagnostics = Math.Max(state.SearchFrom, state.NextStartIndex + deterministicProgram.SearchLiteralOffset);
            while (searchFromWithDiagnostics <= input.Length)
            {
                budget.Step();

                var relative = deterministicProgram.SearchLiterals.Length == 1
                    ? (deterministicProgram.IgnoreCase
                        ? AsciiSearch.IndexOfIgnoreCase(input[searchFromWithDiagnostics..], deterministicProgram.SearchLiterals[0])
                        : AsciiSearch.IndexOfExact(input[searchFromWithDiagnostics..], deterministicProgram.SearchLiterals[0]))
                    : AsciiSearch.IndexOfAnyExact(input[searchFromWithDiagnostics..], deterministicProgram.SearchLiterals);
                if (relative < 0)
                {
                    return false;
                }

                var absoluteAnchor = searchFromWithDiagnostics + relative;
                var candidate = absoluteAnchor - deterministicProgram.SearchLiteralOffset;
                diagnostics?.CountSearchCandidate();
                if (candidate >= state.NextStartIndex &&
                    TryMatchFixedWidthAt(input, deterministicProgram, candidate, out _))
                {
                    diagnostics?.CountVerifierInvocation();
                    diagnostics?.CountVerifierMatch();
                    matchIndex = candidate;
                    state.NextStartIndex = candidate + Math.Max(deterministicProgram.FixedWidthLength, 1);
                    state.SearchFrom = state.NextStartIndex + deterministicProgram.SearchLiteralOffset;
                    return true;
                }

                searchFromWithDiagnostics = absoluteAnchor + 1;
            }

            state.SearchFrom = searchFromWithDiagnostics;
            return false;
        }

        if (diagnostics is null)
        {
            for (var candidate = state.NextStartIndex; candidate <= input.Length; candidate++)
            {
                budget.Step();
                if (TryMatchFixedWidthAt(input, deterministicProgram, candidate, out _))
                {
                    matchIndex = candidate;
                    state.NextStartIndex = candidate + deterministicProgram.FixedWidthLength;
                    state.SearchFrom = state.NextStartIndex;
                    return true;
                }
            }

            state.SearchFrom = input.Length + 1;
            return false;
        }

        for (var candidate = state.NextStartIndex; candidate <= input.Length; candidate++)
        {
            budget.Step();
            diagnostics?.CountSearchCandidate();
            if (TryMatchFixedWidthAt(input, deterministicProgram, candidate, out _))
            {
                diagnostics?.CountVerifierInvocation();
                diagnostics?.CountVerifierMatch();
                matchIndex = candidate;
                state.NextStartIndex = candidate + deterministicProgram.FixedWidthLength;
                state.SearchFrom = state.NextStartIndex;
                return true;
            }
        }

        state.SearchFrom = input.Length + 1;
        return false;
    }

    public static int CountDeterministic(in Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (!program.DeterministicProgram.HasValue)
        {
            return 0;
        }

        if (program.DeterministicProgram.FixedWidthLength > 0)
        {
            return CountDeterministicFixedWidth(program, input, budget);
        }

        var count = 0;
        var state = new Utf8AsciiDeterministicScanState(0, program.DeterministicProgram.SearchLiteralOffset);
        while (state.NextStartIndex <= input.Length)
        {
            if (!TryFindNextNonOverlappingDeterministicMatch(program, input, ref state, budget, out _, out _))
            {
                return count;
            }

            count++;
        }

        return count;
    }

    public static int CountDeterministicFixedWidth(in Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (!program.DeterministicProgram.HasValue || program.DeterministicProgram.FixedWidthLength <= 0)
        {
            return 0;
        }

        var count = 0;
        var state = new Utf8AsciiDeterministicScanState(0, program.DeterministicProgram.SearchLiteralOffset);
        while (state.NextStartIndex <= input.Length)
        {
            if (!TryFindNextNonOverlappingDeterministicFixedWidthMatch(program, input, ref state, budget, out _))
            {
                return count;
            }

            count++;
        }

        return count;
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, in Utf8StructuralLinearProgram program, int startIndex, out int matchedLength)
    {
        if (program.DeterministicProgram.HasValue)
        {
            return TryMatchAt(input, program.DeterministicProgram, startIndex, out matchedLength);
        }

        return TryMatchAt(input, program.InstructionProgram, startIndex, out matchedLength);
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, Utf8StructuralLinearInstructionProgram program, int startIndex, out int matchedLength)
    {
        matchedLength = 0;
        if (startIndex < 0)
        {
            return false;
        }

        foreach (var check in program.FixedLiteralChecks)
        {
            var offset = startIndex + check.Offset;
            if (offset < 0 || offset + check.Literal.Length > input.Length)
            {
                return false;
            }

            if (!input.Slice(offset, check.Literal.Length).SequenceEqual(check.Literal))
            {
                return false;
            }
        }

        var index = startIndex;
        foreach (var instruction in program.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8StructuralLinearInstructionKind.Literal:
                    if ((uint)index >= (uint)input.Length)
                    {
                        return false;
                    }

                    var literalValue = input[index];
                    if (program.IgnoreCase)
                    {
                        literalValue = AsciiSearch.FoldCase(literalValue);
                    }

                    if (literalValue != instruction.Literal)
                    {
                        return false;
                    }

                    index++;
                    break;

                case Utf8StructuralLinearInstructionKind.AnyByte:
                    if ((uint)index >= (uint)input.Length)
                    {
                        return false;
                    }

                    index++;
                    break;

                case Utf8StructuralLinearInstructionKind.CharClass when !instruction.CharClass.IsEmpty:
                    if ((uint)index >= (uint)input.Length)
                    {
                        return false;
                    }

                    var classValue = input[index];
                    if (!instruction.CharClass.Contains(program.IgnoreCase ? AsciiSearch.FoldCase(classValue) : classValue))
                    {
                        return false;
                    }

                    index++;
                    break;

                case Utf8StructuralLinearInstructionKind.RunCharClass when !instruction.CharClass.IsEmpty:
                {
                    var count = 0;
                    while ((uint)index < (uint)input.Length &&
                        count < instruction.MaxCount &&
                        instruction.CharClass.Contains(input[index]))
                    {
                        index++;
                        count++;
                    }

                    if (count < instruction.MinCount)
                    {
                        return false;
                    }

                    break;
                }

                case Utf8StructuralLinearInstructionKind.RepeatedSegment when
                    !instruction.CharClass.IsEmpty &&
                    !instruction.SecondaryCharClass.IsEmpty &&
                    !string.IsNullOrEmpty(instruction.Set):
                {
                    var segments = 0;
                    while (segments < instruction.MaxCount &&
                        TryConsumeRepeatedSegment(input, ref index, instruction.CharClass, instruction.SecondaryCharClass, instruction.SecondaryMinCount, instruction.AuxiliaryMinCount, instruction.Set, out _))
                    {
                        segments++;
                    }

                    if (segments < instruction.MinCount)
                    {
                        return false;
                    }

                    break;
                }

                case Utf8StructuralLinearInstructionKind.Accept:
                    if (program.IsEndAnchored && index != input.Length)
                    {
                        return false;
                    }

                    matchedLength = index - startIndex;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, Utf8AsciiDeterministicProgram program, int startIndex, out int matchedLength)
    {
        matchedLength = 0;
        if (startIndex < 0)
        {
            return false;
        }

        if (program.FixedWidthChecks.Length > 0 &&
            TryMatchFixedWidthAt(input, program, startIndex, out matchedLength))
        {
            return true;
        }

        foreach (var check in program.FixedLiteralChecks)
        {
            var offset = startIndex + check.Offset;
            if (offset < 0 || offset + check.Literal.Length > input.Length)
            {
                return false;
            }

            if (!input.Slice(offset, check.Literal.Length).SequenceEqual(check.Literal))
            {
                return false;
            }
        }

        var index = startIndex;
        foreach (var step in program.Steps)
        {
            switch (step.Kind)
            {
                case Utf8AsciiDeterministicStepKind.Literal:
                    if ((uint)index >= (uint)input.Length)
                    {
                        return false;
                    }

                    var literalValue = input[index];
                    if (program.IgnoreCase)
                    {
                        literalValue = AsciiSearch.FoldCase(literalValue);
                    }

                    if (literalValue != step.Literal)
                    {
                        return false;
                    }

                    index++;
                    break;

                case Utf8AsciiDeterministicStepKind.AnyByte:
                    if ((uint)index >= (uint)input.Length)
                    {
                        return false;
                    }

                    index++;
                    break;

                case Utf8AsciiDeterministicStepKind.CharClass when !step.CharClass.IsEmpty:
                    if ((uint)index >= (uint)input.Length)
                    {
                        return false;
                    }

                    var classValue = input[index];
                    if (!step.CharClass.Contains(program.IgnoreCase ? AsciiSearch.FoldCase(classValue) : classValue))
                    {
                        return false;
                    }

                    index++;
                    break;

                case Utf8AsciiDeterministicStepKind.RunCharClass when !step.CharClass.IsEmpty:
                {
                    var count = 0;
                    while ((uint)index < (uint)input.Length &&
                        count < step.MaxCount &&
                        step.CharClass.Contains(input[index]))
                    {
                        index++;
                        count++;
                    }

                    if (count < step.MinCount)
                    {
                        return false;
                    }

                    break;
                }

                case Utf8AsciiDeterministicStepKind.Accept:
                    if (program.IsEndAnchored && index != input.Length)
                    {
                        return false;
                    }

                    matchedLength = index - startIndex;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryMatchFixedWidthAt(ReadOnlySpan<byte> input, Utf8AsciiDeterministicProgram program, int startIndex, out int matchedLength)
    {
        matchedLength = 0;
        var endIndex = startIndex + program.FixedWidthLength;
        if ((uint)startIndex > (uint)input.Length || endIndex > input.Length)
        {
            return false;
        }

        for (var i = 0; i < program.FixedWidthChecks.Length; i++)
        {
            var value = input[startIndex + i];
            var check = program.FixedWidthChecks[i];
            switch (check.Kind)
            {
                case Utf8AsciiDeterministicFixedWidthCheckKind.Literal:
                    if (program.IgnoreCase)
                    {
                        value = AsciiSearch.FoldCase(value);
                    }

                    if (value != check.Literal)
                    {
                        return false;
                    }

                    break;

                case Utf8AsciiDeterministicFixedWidthCheckKind.AnyByte:
                    break;

                case Utf8AsciiDeterministicFixedWidthCheckKind.CharClass when !check.CharClass.IsEmpty:
                    if (!check.CharClass.Contains(program.IgnoreCase ? AsciiSearch.FoldCase(value) : value))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        if (program.IsEndAnchored && endIndex != input.Length)
        {
            return false;
        }

        matchedLength = program.FixedWidthLength;
        return true;
    }

    private static bool TryConsumeRepeatedSegment(
        ReadOnlySpan<byte> input,
        ref int index,
        AsciiCharClass leadingCharClass,
        AsciiCharClass trailingCharClass,
        int trailingMinCount,
        int separatorMinCount,
        string separatorSet,
        out int separatorCount)
    {
        separatorCount = 0;
        if ((uint)index >= (uint)input.Length || !leadingCharClass.Contains(input[index]))
        {
            return false;
        }

        index++;

        var tailCount = 0;
        while ((uint)index < (uint)input.Length && trailingCharClass.Contains(input[index]))
        {
            index++;
            tailCount++;
        }

        if (tailCount < trailingMinCount)
        {
            return false;
        }

        while ((uint)index < (uint)input.Length && MatchesSet(input[index], separatorSet))
        {
            index++;
            separatorCount++;
        }

        return separatorCount >= separatorMinCount;
    }

    private static bool MatchesSet(byte value, string runtimeSet)
    {
        return value < 128 && Lokad.Utf8Regex.Internal.FrontEnd.Runtime.RegexCharClass.CharInClassBase((char)value, runtimeSet);
    }

    private static bool TryMatchTokenWindowAt(ReadOnlySpan<byte> input, AsciiStructuralTokenWindowPlan plan, int startIndex, out int matchedLength)
    {
        matchedLength = 0;
        var index = startIndex;
        if (!TryConsumeExactRun(input, ref index, plan.LeadingCharClass, plan.LeadingLength))
        {
            return false;
        }

        if (!TryConsumeSeparator(input, ref index, plan))
        {
            return false;
        }

        var gapStart = index;
        var latestAnchorStart = Math.Min(input.Length - plan.AnchorLiteralUtf8.Length, gapStart + plan.LeadingGapMax);
        for (var candidateAnchor = latestAnchorStart; candidateAnchor >= gapStart; candidateAnchor--)
        {
            if (!input.Slice(candidateAnchor, plan.AnchorLiteralUtf8.Length).SequenceEqual(plan.AnchorLiteralUtf8))
            {
                continue;
            }

            if (!TryMatchTrailingTokenWindow(input, plan, candidateAnchor + plan.AnchorLiteralUtf8.Length, out var endIndex))
            {
                continue;
            }

            matchedLength = endIndex - startIndex;
            return true;
        }

        return false;
    }

    private static bool TryMatchLiteralFamilyRunAt(ReadOnlySpan<byte> input, AsciiSimplePatternPlan plan, int startIndex, out int matchedLength)
    {
        matchedLength = 0;
        foreach (var branch in plan.Branches)
        {
            if (startIndex + branch.Length > input.Length)
            {
                continue;
            }

            var matched = true;
            for (var i = 0; i < branch.Length; i++)
            {
                var token = branch[i];
                var value = input[startIndex + i];
                switch (token.Kind)
                {
                    case AsciiSimplePatternTokenKind.Literal when value != token.Literal:
                    case AsciiSimplePatternTokenKind.CharClass when token.CharClass.IsEmpty || !token.CharClass.Contains(value):
                    case AsciiSimplePatternTokenKind.Dot:
                        matched = false;
                        break;
                }

                if (!matched)
                {
                    break;
                }
            }

            if (matched)
            {
                matchedLength = branch.Length;
                return true;
            }
        }

        return false;
    }

    private static bool TryMatchTrailingTokenWindow(ReadOnlySpan<byte> input, AsciiStructuralTokenWindowPlan plan, int afterAnchor, out int endIndex)
    {
        endIndex = afterAnchor;
        var earliestSeparator = afterAnchor;
        var latestSeparator = Math.Min(input.Length - plan.SeparatorMinCount - plan.TrailingLength, afterAnchor + plan.TrailingGapMax);
        for (var separatorStart = latestSeparator; separatorStart >= earliestSeparator; separatorStart--)
        {
            var index = separatorStart;
            if (!TryConsumeSeparator(input, ref index, plan))
            {
                continue;
            }

            if (!TryConsumeExactRun(input, ref index, plan.TrailingCharClass, plan.TrailingLength))
            {
                continue;
            }

            endIndex = index;
            return true;
        }

        return false;
    }

    private static bool TryConsumeSeparator(ReadOnlySpan<byte> input, ref int index, AsciiStructuralTokenWindowPlan plan)
    {
        var count = 0;
        while ((uint)index < (uint)input.Length && plan.MatchesSeparator(input[index]))
        {
            index++;
            count++;
        }

        return count >= plan.SeparatorMinCount;
    }

    private static bool TryConsumeExactRun(ReadOnlySpan<byte> input, ref int index, AsciiCharClass charClass, int length)
    {
        if (input.Length - index < length)
        {
            return false;
        }

        for (var i = 0; i < length; i++)
        {
            if (!charClass.Contains(input[index + i]))
            {
                return false;
            }
        }

        index += length;
        return true;
    }

    private static bool TryMatchQuotedRelationAt(ReadOnlySpan<byte> input, int startIndex, AsciiStructuralQuotedRelationPlan plan, out int matchedLength)
    {
        matchedLength = 0;
        if (TryMatchQuotedRelationBranchAt(input, startIndex, plan, plan.FirstBranch, out matchedLength))
        {
            return true;
        }

        return TryMatchQuotedRelationBranchAt(input, startIndex, plan, plan.SecondBranch, out matchedLength);
    }

    private static bool TryMatchQuotedRelationBranchAt(ReadOnlySpan<byte> input, int startIndex, AsciiStructuralQuotedRelationPlan plan, AsciiStructuralQuotedRelationBranchPlan branch, out int matchedLength)
    {
        matchedLength = 0;
        if (!TryConsumeQuotedOperand(input, startIndex, plan, branch.LeadingKind, branch.LeadingRepeat, out var afterLeading))
        {
            return false;
        }

        if (!TryFindTrailingQuotedOperandWithinLines(input, afterLeading, plan, branch.TrailingKind, branch.TrailingRepeat, branch.MaxLineBreaks, out var matchEnd))
        {
            return false;
        }

        matchedLength = matchEnd - startIndex;
        return matchedLength > 0;
    }

    private static bool TryFindTrailingQuotedOperandWithinLines(
        ReadOnlySpan<byte> input,
        int startIndex,
        AsciiStructuralQuotedRelationPlan plan,
        AsciiStructuralQuotedOperandKind kind,
        bool allowRepeat,
        int maxLineBreaks,
        out int matchEnd)
    {
        matchEnd = 0;
        var lineBreaks = 0;
        for (var i = startIndex; i < input.Length; i++)
        {
            if (input[i] == '\n')
            {
                lineBreaks++;
                if (lineBreaks > maxLineBreaks)
                {
                    return false;
                }
            }

            if (input[i] is not ((byte)'"' or (byte)'\''))
            {
                continue;
            }

            if (TryConsumeQuotedOperand(input, i, plan, kind, allowRepeat, out matchEnd))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryConsumeQuotedOperand(
        ReadOnlySpan<byte> input,
        int startIndex,
        AsciiStructuralQuotedRelationPlan plan,
        AsciiStructuralQuotedOperandKind kind,
        bool allowRepeat,
        out int endIndex)
    {
        endIndex = 0;
        if (!TryConsumeSingleQuotedOperand(input, startIndex, plan, kind, out endIndex))
        {
            return false;
        }

        if (!allowRepeat)
        {
            return true;
        }

        while (TryConsumeSingleQuotedOperand(input, endIndex, plan, kind, out var repeatedEnd))
        {
            endIndex = repeatedEnd;
        }

        return true;
    }

    private static bool TryConsumeSingleQuotedOperand(
        ReadOnlySpan<byte> input,
        int startIndex,
        AsciiStructuralQuotedRelationPlan plan,
        AsciiStructuralQuotedOperandKind kind,
        out int endIndex)
    {
        endIndex = 0;
        return kind switch
        {
            AsciiStructuralQuotedOperandKind.QuotedPrefixedRun => TryConsumeQuotedPrefixedRun(input, startIndex, plan, out endIndex),
            AsciiStructuralQuotedOperandKind.QuotedAsciiRun => TryConsumeQuotedAsciiRun(input, startIndex, plan, out endIndex),
            _ => false,
        };
    }

    private static bool TryConsumeQuotedAsciiRun(ReadOnlySpan<byte> input, int startIndex, AsciiStructuralQuotedRelationPlan plan, out int endIndex)
    {
        endIndex = 0;
        if ((uint)startIndex >= (uint)input.Length ||
            input[startIndex] is not ((byte)'"' or (byte)'\'') ||
            plan.QuotedRunClass.IsEmpty)
        {
            return false;
        }

        var runClass = plan.QuotedRunClass;

        var afterBody = startIndex + 1 + plan.QuotedRunLength;
        if (afterBody >= input.Length)
        {
            return false;
        }

        for (var i = 0; i < plan.QuotedRunLength; i++)
        {
            var value = input[startIndex + 1 + i];
            if (value >= 128 || !runClass.Contains(value))
            {
                return false;
            }
        }

        if (input[afterBody] is not ((byte)'"' or (byte)'\''))
        {
            return false;
        }

        endIndex = afterBody + 1;
        return true;
    }

    private static bool TryConsumeQuotedPrefixedRun(ReadOnlySpan<byte> input, int startIndex, AsciiStructuralQuotedRelationPlan plan, out int endIndex)
    {
        endIndex = 0;
        if ((uint)startIndex >= (uint)input.Length ||
            input[startIndex] is not ((byte)'"' or (byte)'\'') ||
            plan.PrefixedTailClass.IsEmpty)
        {
            return false;
        }

        var tailClass = plan.PrefixedTailClass;

        var bodyStart = startIndex + 1;
        foreach (var prefix in plan.PrefixesUtf8)
        {
            if (bodyStart + prefix.Length + plan.PrefixedTailLength >= input.Length)
            {
                continue;
            }

            if (!input.Slice(bodyStart, prefix.Length).SequenceEqual(prefix))
            {
                continue;
            }

            var tailStart = bodyStart + prefix.Length;
            var tailEnd = tailStart + plan.PrefixedTailLength;
            var validTail = true;
            for (var i = tailStart; i < tailEnd; i++)
            {
                var value = input[i];
                if (value >= 128 || !tailClass.Contains(value))
                {
                    validTail = false;
                    break;
                }
            }

            if (!validTail || input[tailEnd] is not ((byte)'"' or (byte)'\''))
            {
                continue;
            }

            endIndex = tailEnd + 1;
            return true;
        }

        return false;
    }

}

internal sealed class Utf8AsciiStructuralFamilyLinearRuntime : Utf8StructuralLinearRuntime
{
    public Utf8AsciiStructuralFamilyLinearRuntime(Utf8StructuralLinearProgram program)
        : base(program)
    {
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return UsesRightToLeft(verifierRuntime)
            ? verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input))
            : FindNext(input, verifierRuntime, 0, budget, out _) >= 0;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralLinearAutomaton);
        return UsesRightToLeft(verifierRuntime)
            ? verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input))
            : CountForward(input, verifierRuntime, budget);
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        if (UsesRightToLeft(verifierRuntime))
        {
            return MatchFallback(input, verifierRuntime);
        }

        var index = FindNext(input, verifierRuntime, 0, budget, out var matchedLength);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
    }

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        if (UsesRightToLeft(verifierRuntime))
        {
            matchIndex = -1;
            matchedLength = 0;
            return false;
        }

        matchIndex = FindNext(input, verifierRuntime, startIndex, budget, out matchedLength);
        return matchIndex >= 0;
    }

    private int CountForward(ReadOnlySpan<byte> input, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        if (CanUseStatefulSearch())
        {
            var statefulCount = 0;
            var state = CreateScanState(0);
            while (TryFindNextStateful(input, verifierRuntime, ref state, budget, out _, out _))
            {
                statefulCount++;
            }

            return statefulCount;
        }

        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, verifierRuntime, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                break;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    internal bool CanUseStatefulSearch()
    {
        return Program.SearchPlan.AlternateLiteralSearchData.HasValue &&
            Program.StructuralSearchPlan.HasValue &&
            Program.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start;
    }

    internal Utf8AsciiStructuralFamilyScanState CreateScanState(int startIndex)
    {
        return new Utf8AsciiStructuralFamilyScanState(
            new Utf8StructuralSearchState(new PreparedSearchScanState(startIndex, default), default),
            startIndex);
    }

    internal bool TryFindNextStateful(
        ReadOnlySpan<byte> input,
        Utf8VerifierRuntime verifierRuntime,
        ref Utf8AsciiStructuralFamilyScanState state,
        Utf8ExecutionDeadline budget,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        var searchData = Program.SearchPlan.AlternateLiteralSearchData;
        if (!CanUseStatefulSearch() || !searchData.HasValue)
        {
            return false;
        }

        var searchState = state.SearchState;
        while (Program.StructuralSearchPlan.TryFindNextCandidate(input, ref searchState, out var candidate))
        {
            if (candidate.StartIndex < state.MinStartIndex)
            {
                continue;
            }

            budget.Step();
            var prefixLength = candidate.MatchLength;
            if (prefixLength <= 0 &&
                !AsciiSearch.TryGetMatchedLiteralLength(input, candidate.StartIndex, searchData.Value, out prefixLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, candidate.StartIndex, prefixLength, Program.StructuralIdentifierFamilyPlan, out matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            matchIndex = candidate.StartIndex;
            state = new Utf8AsciiStructuralFamilyScanState(searchState, candidate.StartIndex + Math.Max(matchedLength, 1));
            return true;
        }

        state = new Utf8AsciiStructuralFamilyScanState(searchState, state.MinStartIndex);
        return false;
    }

    internal bool TryFindNextCandidateStateful(
        ReadOnlySpan<byte> input,
        ref Utf8AsciiStructuralFamilyScanState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        if (!CanUseStatefulSearch())
        {
            return false;
        }

        var searchState = state.SearchState;
        while (Program.StructuralSearchPlan.TryFindNextCandidate(input, ref searchState, out candidate))
        {
            if (candidate.StartIndex < state.MinStartIndex)
            {
                continue;
            }

            state = new Utf8AsciiStructuralFamilyScanState(searchState, candidate.StartIndex + 1);
            return true;
        }

        state = new Utf8AsciiStructuralFamilyScanState(searchState, state.MinStartIndex);
        return false;
    }

    private int FindNext(ReadOnlySpan<byte> input, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        matchedLength = 0;
        var searchData = Program.SearchPlan.AlternateLiteralSearchData;
        if (!searchData.HasValue)
        {
            return -1;
        }

        if (Program.StructuralSearchPlan.HasValue && Program.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start)
        {
            var state = new Utf8StructuralSearchState(new PreparedSearchScanState(startIndex, default), default);
            while (Program.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate))
            {
                if (candidate.StartIndex < startIndex)
                {
                    continue;
                }

                budget.Step();
                var prefixLength = candidate.MatchLength;
                if (prefixLength <= 0 &&
                    !AsciiSearch.TryGetMatchedLiteralLength(input, candidate.StartIndex, searchData.Value, out prefixLength))
                {
                    continue;
                }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, candidate.StartIndex, prefixLength, Program.StructuralIdentifierFamilyPlan, out matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return candidate.StartIndex;
            }
            }

            return -1;
        }

        for (var candidate = Utf8SearchExecutor.FindNext(Program.SearchPlan, input, startIndex);
            candidate >= 0;
            candidate = Utf8SearchExecutor.FindNext(Program.SearchPlan, input, candidate + 1))
        {
            budget.Step();
            if (!AsciiSearch.TryGetMatchedLiteralLength(input, candidate, searchData.Value, out var prefixLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (verifierRuntime.StructuralVerifierRuntime.TryMatch(input, candidate, prefixLength, budget, out matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return candidate;
            }
        }

        return -1;
    }

    private static Utf8ValueMatch MatchFallback(ReadOnlySpan<byte> input, Utf8VerifierRuntime verifierRuntime)
    {
        var decoded = Encoding.UTF8.GetString(input);
        var match = verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        if (!match.Success)
        {
            return Utf8ValueMatch.NoMatch;
        }

        var boundaryMap = Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        if (boundaryMap.TryGetByteRange(match.Index, match.Length, out var indexInBytes, out var lengthInBytes))
        {
            return new Utf8ValueMatch(true, true, match.Index, match.Length, indexInBytes, lengthInBytes);
        }

        return new Utf8ValueMatch(true, false, match.Index, match.Length);
    }

    private static bool UsesRightToLeft(Utf8VerifierRuntime verifierRuntime)
    {
        return (verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Options & RegexOptions.RightToLeft) != 0;
    }
}

internal readonly record struct Utf8AsciiStructuralFamilyScanState(Utf8StructuralSearchState SearchState, int MinStartIndex);
