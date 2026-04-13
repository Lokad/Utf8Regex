using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;
using Lokad.Utf8Regex.Internal.Utilities;

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
    public Utf8AsciiDeterministicStep(
        Utf8AsciiDeterministicStepKind kind,
        byte literal = 0,
        AsciiCharClass? charClass = null,
        int minCount = 0,
        int maxCount = 0)
    {
        Kind = kind;
        Literal = literal;
        CharClass = charClass;
        MinCount = minCount;
        MaxCount = maxCount;
    }

    public Utf8AsciiDeterministicStepKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass? CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }
}

internal readonly struct Utf8AsciiDeterministicFixedWidthCheck
{
    public Utf8AsciiDeterministicFixedWidthCheck(
        Utf8AsciiDeterministicFixedWidthCheckKind kind,
        byte literal = 0,
        AsciiCharClass? charClass = null)
    {
        Kind = kind;
        Literal = literal;
        CharClass = charClass;
    }

    public Utf8AsciiDeterministicFixedWidthCheckKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass? CharClass { get; }
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
        Utf8AsciiDeterministicFixedWidthCheck[]? fixedWidthChecks = null,
        int fixedWidthLength = 0)
    {
        Steps = steps;
        FixedLiteralChecks = fixedLiteralChecks;
        SearchLiteralOffset = searchLiteralOffset;
        SearchLiterals = searchLiterals;
        IsEndAnchored = isEndAnchored;
        IgnoreCase = ignoreCase;
        FixedWidthChecks = fixedWidthChecks ?? [];
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
                    => new Utf8AsciiDeterministicStep(Utf8AsciiDeterministicStepKind.Literal, literal: instruction.Literal),
                Utf8StructuralLinearInstructionKind.AnyByte
                    => new Utf8AsciiDeterministicStep(Utf8AsciiDeterministicStepKind.AnyByte),
                Utf8StructuralLinearInstructionKind.CharClass when instruction.CharClass is not null
                    => new Utf8AsciiDeterministicStep(Utf8AsciiDeterministicStepKind.CharClass, charClass: instruction.CharClass),
                Utf8StructuralLinearInstructionKind.RunCharClass when instruction.CharClass is not null
                    => new Utf8AsciiDeterministicStep(Utf8AsciiDeterministicStepKind.RunCharClass, charClass: instruction.CharClass, minCount: instruction.MinCount, maxCount: instruction.MaxCount),
                Utf8StructuralLinearInstructionKind.Accept
                    => new Utf8AsciiDeterministicStep(Utf8AsciiDeterministicStepKind.Accept),
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
                    => new Utf8AsciiDeterministicFixedWidthCheck(Utf8AsciiDeterministicFixedWidthCheckKind.Literal, literal: step.Literal),
                Utf8AsciiDeterministicStepKind.AnyByte
                    => new Utf8AsciiDeterministicFixedWidthCheck(Utf8AsciiDeterministicFixedWidthCheckKind.AnyByte),
                Utf8AsciiDeterministicStepKind.CharClass when step.CharClass is not null
                    => new Utf8AsciiDeterministicFixedWidthCheck(Utf8AsciiDeterministicFixedWidthCheckKind.CharClass, charClass: step.CharClass),
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
    public Utf8StructuralLinearInstruction(
        Utf8StructuralLinearInstructionKind kind,
        byte literal = 0,
        AsciiCharClass? charClass = null,
        int minCount = 0,
        int maxCount = 0,
        AsciiCharClass? secondaryCharClass = null,
        int secondaryMinCount = 0,
        int auxiliaryMinCount = 0,
        string? set = null)
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

    public AsciiCharClass? CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiCharClass? SecondaryCharClass { get; }

    public int SecondaryMinCount { get; }

    public int AuxiliaryMinCount { get; }

    public string? Set { get; }
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
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.RunCharClass, charClass: runPlan.CharClass, minCount: runPlan.MinLength, maxCount: runPlan.MaxLength),
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.Accept),
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
                AsciiSimplePatternTokenKind.Literal => new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.Literal, literal: token.Literal),
                AsciiSimplePatternTokenKind.Dot => new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.AnyByte),
                AsciiSimplePatternTokenKind.CharClass => new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.CharClass, charClass: token.CharClass),
                _ => default,
            };
        }

        instructions[^1] = new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.Accept);
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
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.LiteralFamilyRun),
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.Accept),
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
                new Utf8StructuralLinearInstruction(
                    Utf8StructuralLinearInstructionKind.RepeatedSegment,
                    charClass: repeatedSegmentPlan.LeadingCharClass,
                    minCount: repeatedSegmentPlan.RepetitionMinCount,
                    maxCount: repeatedSegmentPlan.RepetitionMaxCount,
                    secondaryCharClass: repeatedSegmentPlan.TrailingCharClass,
                    secondaryMinCount: repeatedSegmentPlan.TrailingMinCount,
                    auxiliaryMinCount: repeatedSegmentPlan.SeparatorMinCount,
                    set: repeatedSegmentPlan.SeparatorSet),
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.Accept),
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
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.TokenWindow),
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.Accept),
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
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.QuotedRelation),
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.Accept),
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
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.OrderedLiteralWindow),
                new Utf8StructuralLinearInstruction(Utf8StructuralLinearInstructionKind.Accept),
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
    public Utf8StructuralLinearProgram(
        Utf8StructuralLinearProgramKind kind,
        Utf8StructuralLinearInstructionProgram instructionProgram = default,
        Utf8AsciiDeterministicProgram deterministicProgram = default,
        AsciiSimplePatternRunPlan runPlan = default,
        AsciiSimplePatternPlan simplePatternPlan = default,
        bool allowsUtf8ByteSafe = false,
        AsciiStructuralTokenWindowPlan tokenWindowPlan = default,
        AsciiStructuralRepeatedSegmentPlan repeatedSegmentPlan = default,
        AsciiStructuralQuotedRelationPlan quotedRelationPlan = default,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan = default,
        AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan = default,
        Utf8SearchPlan searchPlan = default,
        Utf8StructuralSearchPlan structuralSearchPlan = default,
        Utf8StructuralVerifierPlan structuralVerifierPlan = default)
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

    public static Utf8StructuralLinearProgram Create(Utf8ExecutionPlan executionPlan, Utf8SearchPlan searchPlan, Utf8StructuralSearchPlan structuralSearchPlan)
    {
        return executionPlan.NativeKind switch
        {
            NativeExecutionKind.AsciiSimplePattern when executionPlan.SimplePatternPlan.RunPlan.HasValue
                => new Utf8StructuralLinearProgram(
                    Utf8StructuralLinearProgramKind.AsciiCharClassRun,
                    instructionProgram: Utf8StructuralLinearInstructionProgram.Create(executionPlan.SimplePatternPlan.RunPlan),
                    deterministicProgram: Utf8AsciiDeterministicProgram.Create(Utf8StructuralLinearInstructionProgram.Create(executionPlan.SimplePatternPlan.RunPlan)),
                    runPlan: executionPlan.SimplePatternPlan.RunPlan,
                    allowsUtf8ByteSafe: executionPlan.SimplePatternPlan.IsUtf8ByteSafe),
            NativeExecutionKind.AsciiSimplePattern when CanUseFixedTokenPattern(executionPlan.SimplePatternPlan)
                => new Utf8StructuralLinearProgram(
                    Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern,
                    instructionProgram: Utf8StructuralLinearInstructionProgram.Create(executionPlan.SimplePatternPlan),
                    deterministicProgram: Utf8AsciiDeterministicProgram.Create(Utf8StructuralLinearInstructionProgram.Create(executionPlan.SimplePatternPlan)),
                    simplePatternPlan: executionPlan.SimplePatternPlan,
                    allowsUtf8ByteSafe: executionPlan.SimplePatternPlan.IsUtf8ByteSafe),
            NativeExecutionKind.AsciiSimplePattern when CanUseLiteralFamilyRunPattern(executionPlan.SimplePatternPlan)
                => new Utf8StructuralLinearProgram(
                    Utf8StructuralLinearProgramKind.AsciiLiteralFamilyRun,
                    instructionProgram: Utf8StructuralLinearInstructionProgram.CreateLiteralFamilyRun(executionPlan.SimplePatternPlan),
                    simplePatternPlan: executionPlan.SimplePatternPlan,
                    allowsUtf8ByteSafe: executionPlan.SimplePatternPlan.IsUtf8ByteSafe),
            NativeExecutionKind.AsciiStructuralIdentifierFamily when executionPlan.StructuralVerifier.Kind == Utf8StructuralVerifierKind.AsciiStructuralProgram
                => new Utf8StructuralLinearProgram(
                    Utf8StructuralLinearProgramKind.AsciiStructuralFamily,
                    structuralIdentifierFamilyPlan: executionPlan.StructuralIdentifierFamilyPlan,
                    searchPlan: searchPlan,
                    structuralSearchPlan: structuralSearchPlan,
                    structuralVerifierPlan: executionPlan.StructuralVerifier),
            NativeExecutionKind.AsciiStructuralTokenWindow when executionPlan.StructuralTokenWindowPlan.HasValue
                => new Utf8StructuralLinearProgram(
                    Utf8StructuralLinearProgramKind.AsciiTokenWindow,
                    instructionProgram: Utf8StructuralLinearInstructionProgram.Create(executionPlan.StructuralTokenWindowPlan),
                    tokenWindowPlan: executionPlan.StructuralTokenWindowPlan,
                    allowsUtf8ByteSafe: true),
            NativeExecutionKind.AsciiStructuralRepeatedSegment when executionPlan.StructuralRepeatedSegmentPlan.HasValue
                => new Utf8StructuralLinearProgram(
                    Utf8StructuralLinearProgramKind.AsciiRepeatedSegment,
                    instructionProgram: Utf8StructuralLinearInstructionProgram.Create(executionPlan.StructuralRepeatedSegmentPlan),
                    repeatedSegmentPlan: executionPlan.StructuralRepeatedSegmentPlan),
            NativeExecutionKind.AsciiStructuralQuotedRelation when executionPlan.StructuralQuotedRelationPlan.HasValue
                => new Utf8StructuralLinearProgram(
                    Utf8StructuralLinearProgramKind.AsciiQuotedRelation,
                    instructionProgram: Utf8StructuralLinearInstructionProgram.Create(executionPlan.StructuralQuotedRelationPlan),
                    quotedRelationPlan: executionPlan.StructuralQuotedRelationPlan),
            NativeExecutionKind.AsciiOrderedLiteralWindow when executionPlan.OrderedLiteralWindowPlan.HasValue
                => new Utf8StructuralLinearProgram(
                    Utf8StructuralLinearProgramKind.AsciiOrderedLiteralWindow,
                    instructionProgram: Utf8StructuralLinearInstructionProgram.Create(executionPlan.OrderedLiteralWindowPlan),
                    orderedLiteralWindowPlan: executionPlan.OrderedLiteralWindowPlan,
                    allowsUtf8ByteSafe: true,
                    searchPlan: searchPlan),
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
            firstBranch[runStart].CharClass is not { } runClass)
        {
            return false;
        }

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
    protected Utf8StructuralLinearRuntime(Utf8StructuralLinearProgram program)
    {
        Program = program;
    }

    protected Utf8StructuralLinearProgram Program { get; }

    public abstract bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget);

    public abstract int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget);

    public abstract Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget);

    public abstract bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionBudget? budget, out int matchIndex, out int matchedLength);

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

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
    {
        return false;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
    {
        return 0;
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
    {
        return Utf8ValueMatch.NoMatch;
    }

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionBudget? budget, out int matchIndex, out int matchedLength)
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

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
    {
        return Utf8AsciiInstructionLinearExecutor.SupportsInput(Program, validation) &&
            Utf8AsciiInstructionLinearExecutor.FindNext(Program, input, 0, budget, out _) >= 0;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
    {
        if (!Utf8AsciiInstructionLinearExecutor.SupportsInput(Program, validation))
        {
            return 0;
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_structural_linear_automaton");
        if (Program.DeterministicProgram.HasValue)
        {
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

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
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

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionBudget? budget, out int matchIndex, out int matchedLength)
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
    public static bool SupportsInput(Utf8StructuralLinearProgram program, Utf8ValidationResult validation)
    {
        return validation.IsAscii || program.AllowsUtf8ByteSafe;
    }

    public static int FindNext(Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        return Utf8AsciiInstructionLinearExecutor.FindNext(program, input, startIndex, budget, out matchedLength);
    }
}

internal static class Utf8AsciiInstructionLinearExecutor
{
    public static bool SupportsInput(Utf8StructuralLinearProgram program, Utf8ValidationResult validation)
    {
        return validation.IsAscii || program.AllowsUtf8ByteSafe;
    }

    public static int FindNext(Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedLength)
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
                budget?.Step(input);

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
                budget?.Step(input);

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
            instructionProgram.Instructions[0].CharClass is { } runCharClass)
        {
            var matchIndex = Utf8AsciiCharClassRunExecutor.FindNext(
                input,
                new AsciiSimplePatternRunPlan(runCharClass, instructionProgram.Instructions[0].MinCount, instructionProgram.Instructions[0].MaxCount),
                startIndex,
                out matchedLength);
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
            instructionProgram.Instructions[0].CharClass is { } segmentLeading)
        {
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

                budget?.Step(input);
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
                var runStart = Utf8AsciiCharClassRunExecutor.FindNext(input, plan.LeadingRunPlan, candidateStart, out var runLength);
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
                budget?.Step(input);
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
                budget?.Step(input);
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
            var plan = program.OrderedLiteralWindowPlan;
            var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();

            // For literal-family leading, use multi-literal search via the search plan.
            if (plan.IsLiteralFamily && program.SearchPlan.AlternateLiteralSearch is { } familySearch)
            {
                return plan.HasPairedTrailingLiterals
                    ? FindNextPairedOrderedLiteralFamilyWindow(input, plan, familySearch, startIndex, budget, diagnostics, out matchedLength)
                    : FindNextOrderedLiteralFamilyWindow(input, plan, familySearch, trailingLiteral, startIndex, budget, diagnostics, out matchedLength);
            }

            var leadingLiteral = plan.LeadingLiteralUtf8.AsSpan();
            var searchFrom = startIndex;
            while (searchFrom <= input.Length - leadingLiteral.Length)
            {
                budget?.Step(input);

                var relative = input[searchFrom..].IndexOf(leadingLiteral);
                if (relative < 0)
                {
                    return -1;
                }

                var leadingStart = searchFrom + relative;
                if (!MatchesBoundaryRequirement(input, leadingStart, plan.LeadingLiteralLeadingBoundary) ||
                    !MatchesBoundaryRequirement(input, leadingStart + leadingLiteral.Length, plan.LeadingLiteralTrailingBoundary))
                {
                    searchFrom = leadingStart + 1;
                    continue;
                }

                var gapSearchStart = leadingStart + leadingLiteral.Length;

                // Enforce leading separator (e.g., \s+ before the gap).
                if (plan.GapLeadingSeparatorMinCount > 0)
                {
                    var sepCount = 0;
                    while (gapSearchStart + sepCount < input.Length &&
                           input[gapSearchStart + sepCount] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
                    {
                        sepCount++;
                    }

                    if (sepCount < plan.GapLeadingSeparatorMinCount)
                    {
                        searchFrom = leadingStart + 1;
                        continue;
                    }

                    gapSearchStart += sepCount;
                }

                var gapSearchEnd = Math.Min(input.Length, gapSearchStart + plan.MaxGap + trailingLiteral.Length);

                // When the gap excludes \n (dot without Singleline), clamp to same line.
                if (plan.GapSameLine)
                {
                    var newlineOffset = input[gapSearchStart..gapSearchEnd].IndexOf((byte)'\n');
                    if (newlineOffset >= 0)
                    {
                        gapSearchEnd = gapSearchStart + newlineOffset;
                    }
                }

                var gapSlice = input[gapSearchStart..gapSearchEnd];
                var trailingRelative = gapSlice.IndexOf(trailingLiteral);
                if (trailingRelative < 0)
                {
                    searchFrom = leadingStart + 1;
                    continue;
                }

                var trailingStart = gapSearchStart + trailingRelative;
                diagnostics?.CountSearchCandidate();
                if (MatchesBoundaryRequirement(input, trailingStart, plan.TrailingLiteralLeadingBoundary) &&
                    MatchesBoundaryRequirement(input, trailingStart + trailingLiteral.Length, plan.TrailingLiteralTrailingBoundary))
                {
                    diagnostics?.CountVerifierInvocation();
                    diagnostics?.CountVerifierMatch();
                    matchedLength = plan.YieldLeadingLiteralOnly
                        ? leadingLiteral.Length
                        : trailingStart + trailingLiteral.Length - leadingStart;
                    return leadingStart;
                }

                searchFrom = leadingStart + 1;
            }

            return -1;
        }

        for (var candidate = startIndex; candidate <= input.Length; candidate++)
        {
            budget?.Step(input);
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

    public static int FindNextDeterministic(Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedLength)
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

    public static int FindNextDeterministicFixedWidth(Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedLength)
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
        Utf8StructuralLinearProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8AsciiDeterministicScanState state,
        Utf8ExecutionBudget? budget,
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
        Utf8StructuralLinearProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8AsciiDeterministicScanState state,
        Utf8ExecutionBudget? budget,
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
                    budget?.Step(input);

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
                budget?.Step(input);

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
                budget?.Step(input);
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
            budget?.Step(input);
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
        Utf8StructuralLinearProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8AsciiDeterministicScanState state,
        Utf8ExecutionBudget? budget,
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
                    budget?.Step(input);

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
                budget?.Step(input);

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
                budget?.Step(input);
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
            budget?.Step(input);
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

    public static int CountDeterministic(Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
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

    public static int CountDeterministicFixedWidth(Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
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

    private static bool TryMatchAt(ReadOnlySpan<byte> input, Utf8StructuralLinearProgram program, int startIndex, out int matchedLength)
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

                case Utf8StructuralLinearInstructionKind.CharClass when instruction.CharClass is not null:
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

                case Utf8StructuralLinearInstructionKind.RunCharClass when instruction.CharClass is not null:
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
                    instruction.CharClass is not null &&
                    instruction.SecondaryCharClass is not null &&
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

                case Utf8AsciiDeterministicStepKind.CharClass when step.CharClass is not null:
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

                case Utf8AsciiDeterministicStepKind.RunCharClass when step.CharClass is not null:
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

                case Utf8AsciiDeterministicFixedWidthCheckKind.CharClass when check.CharClass is not null:
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
        if (!TryConsumeExactRun(input, ref index, plan.LeadingCharClass!, plan.LeadingLength))
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
                    case AsciiSimplePatternTokenKind.CharClass when token.CharClass is null || !token.CharClass.Contains(value):
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

            if (!TryConsumeExactRun(input, ref index, plan.TrailingCharClass!, plan.TrailingLength))
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
            plan.QuotedRunClass is not { } runClass)
        {
            return false;
        }

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
            plan.PrefixedTailClass is not { } tailClass)
        {
            return false;
        }

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

    private static bool MatchesBoundaryRequirement(ReadOnlySpan<byte> input, int byteOffset, Utf8BoundaryRequirement requirement)
    {
        return requirement switch
        {
            Utf8BoundaryRequirement.None => true,
            Utf8BoundaryRequirement.Boundary => IsWordBoundary(input, byteOffset),
            Utf8BoundaryRequirement.NonBoundary => !IsWordBoundary(input, byteOffset),
            _ => true,
        };
    }

    private static bool IsWordBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var previousIsWord = byteOffset > 0 &&
            FrontEnd.Runtime.RegexCharClass.IsBoundaryWordChar((char)input[byteOffset - 1]);
        var nextIsWord = byteOffset < input.Length &&
            FrontEnd.Runtime.RegexCharClass.IsBoundaryWordChar((char)input[byteOffset]);
        return previousIsWord != nextIsWord;
    }

    private static int FindNextOrderedLiteralFamilyWindow(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch,
        ReadOnlySpan<byte> trailingLiteral,
        int startIndex,
        Utf8ExecutionBudget? budget,
        Utf8SearchDiagnosticsSession? diagnostics,
        out int matchedLength)
    {
        matchedLength = 0;
        var searchFrom = startIndex;
        while (searchFrom < input.Length)
        {
            budget?.Step(input);

            if (!familySearch.TryFindFirstMatchWithLength(input[searchFrom..], out var relativeIndex, out var leadingMatchLength))
            {
                return -1;
            }

            var leadingStart = searchFrom + relativeIndex;
            searchFrom = leadingStart + 1;

            if (!MatchesBoundaryRequirement(input, leadingStart, plan.LeadingLiteralLeadingBoundary) ||
                !MatchesBoundaryRequirement(input, leadingStart + leadingMatchLength, plan.LeadingLiteralTrailingBoundary))
            {
                continue;
            }

            var gapSearchStart = leadingStart + leadingMatchLength;

            if (plan.GapLeadingSeparatorMinCount > 0)
            {
                var sepCount = 0;
                while (gapSearchStart + sepCount < input.Length &&
                       input[gapSearchStart + sepCount] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
                {
                    sepCount++;
                }

                if (sepCount < plan.GapLeadingSeparatorMinCount)
                {
                    continue;
                }

                gapSearchStart += sepCount;
            }

            var gapSearchEnd = Math.Min(input.Length, gapSearchStart + plan.MaxGap + trailingLiteral.Length);

            if (plan.GapSameLine)
            {
                var newlineOffset = input[gapSearchStart..gapSearchEnd].IndexOf((byte)'\n');
                if (newlineOffset >= 0)
                {
                    gapSearchEnd = gapSearchStart + newlineOffset;
                }
            }

            var gapSlice = input[gapSearchStart..gapSearchEnd];
            var trailingRelative = gapSlice.IndexOf(trailingLiteral);
            if (trailingRelative < 0)
            {
                continue;
            }

            var trailingStart = gapSearchStart + trailingRelative;
            diagnostics?.CountSearchCandidate();
            if (MatchesBoundaryRequirement(input, trailingStart, plan.TrailingLiteralLeadingBoundary) &&
                MatchesBoundaryRequirement(input, trailingStart + trailingLiteral.Length, plan.TrailingLiteralTrailingBoundary))
            {
                diagnostics?.CountVerifierInvocation();
                diagnostics?.CountVerifierMatch();
                matchedLength = plan.YieldLeadingLiteralOnly
                    ? leadingMatchLength
                    : trailingStart + trailingLiteral.Length - leadingStart;
                return leadingStart;
            }
        }

        return -1;
    }

    private static int FindNextPairedOrderedLiteralFamilyWindow(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch,
        int startIndex,
        Utf8ExecutionBudget? budget,
        Utf8SearchDiagnosticsSession? diagnostics,
        out int matchedLength)
    {
        matchedLength = 0;
        var searchFrom = startIndex;
        while (searchFrom < input.Length)
        {
            budget?.Step(input);

            if (!familySearch.TryFindFirstMatchWithLength(input[searchFrom..], out var relativeIndex, out var leadingMatchLength))
            {
                return -1;
            }

            var leadingStart = searchFrom + relativeIndex;
            searchFrom = leadingStart + 1;

            if (!TryResolvePairedLeadingBranch(plan, input, leadingStart, leadingMatchLength, out var trailingLiteral))
            {
                continue;
            }

            if (!MatchesBoundaryRequirement(input, leadingStart, plan.LeadingLiteralLeadingBoundary) ||
                !MatchesBoundaryRequirement(input, leadingStart + leadingMatchLength, plan.LeadingLiteralTrailingBoundary))
            {
                continue;
            }

            var gapSearchStart = leadingStart + leadingMatchLength;

            if (plan.GapLeadingSeparatorMinCount > 0)
            {
                var sepCount = 0;
                while (gapSearchStart + sepCount < input.Length &&
                       input[gapSearchStart + sepCount] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
                {
                    sepCount++;
                }

                if (sepCount < plan.GapLeadingSeparatorMinCount)
                {
                    continue;
                }

                gapSearchStart += sepCount;
            }

            var gapSearchEnd = Math.Min(input.Length, gapSearchStart + plan.MaxGap + trailingLiteral.Length);
            if (plan.GapSameLine)
            {
                var newlineOffset = input[gapSearchStart..gapSearchEnd].IndexOf((byte)'\n');
                if (newlineOffset >= 0)
                {
                    gapSearchEnd = gapSearchStart + newlineOffset;
                }
            }

            var trailingRelative = input[gapSearchStart..gapSearchEnd].IndexOf(trailingLiteral);
            if (trailingRelative < 0)
            {
                continue;
            }

            var trailingStart = gapSearchStart + trailingRelative;
            diagnostics?.CountSearchCandidate();
            if (MatchesBoundaryRequirement(input, trailingStart, plan.TrailingLiteralLeadingBoundary) &&
                MatchesBoundaryRequirement(input, trailingStart + trailingLiteral.Length, plan.TrailingLiteralTrailingBoundary))
            {
                diagnostics?.CountVerifierInvocation();
                diagnostics?.CountVerifierMatch();
                matchedLength = plan.YieldLeadingLiteralOnly
                    ? leadingMatchLength
                    : trailingStart + trailingLiteral.Length - leadingStart;
                return leadingStart;
            }
        }

        return -1;
    }

    private static bool TryResolvePairedLeadingBranch(
        AsciiOrderedLiteralWindowPlan plan,
        ReadOnlySpan<byte> input,
        int leadingStart,
        int leadingMatchLength,
        out ReadOnlySpan<byte> trailingLiteral)
    {
        trailingLiteral = default;
        if (!plan.HasPairedTrailingLiterals)
        {
            return false;
        }

        var leadingLiterals = plan.LeadingLiteralsUtf8!;
        var trailingLiterals = plan.TrailingLiteralsUtf8!;
        for (var i = 0; i < leadingLiterals.Length; i++)
        {
            var leadingLiteral = leadingLiterals[i];
            if (leadingLiteral.Length != leadingMatchLength)
            {
                continue;
            }

            if (input[leadingStart..].StartsWith(leadingLiteral))
            {
                trailingLiteral = trailingLiterals[i];
                return true;
            }
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

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
    {
        return UsesRightToLeft(verifierRuntime)
            ? verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input))
            : FindNext(input, verifierRuntime, 0, budget, out _) >= 0;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_structural_linear_automaton");
        return UsesRightToLeft(verifierRuntime)
            ? verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input))
            : CountForward(input, verifierRuntime, budget);
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
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

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionBudget? budget, out int matchIndex, out int matchedLength)
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

    private int CountForward(ReadOnlySpan<byte> input, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget)
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
        Utf8ExecutionBudget? budget,
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

            budget?.Step(input);
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

    private int FindNext(ReadOnlySpan<byte> input, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionBudget? budget, out int matchedLength)
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

                budget?.Step(input);
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
            budget?.Step(input);
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
