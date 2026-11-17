using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8StructuralLinearProgramKind : byte
{
    None = 0,
    AsciiStructuralFamily = 2,
    AsciiTokenWindow = 3,
    AsciiRepeatedSegment = 4,
    AsciiQuotedRelation = 6,
    AsciiOrderedLiteralWindow = 8,
}

internal enum Utf8StructuralLinearInstructionKind : byte
{
    RepeatedSegment = 4,    TokenWindow = 5,    QuotedRelation = 6,    Accept = 8,    OrderedLiteralWindow = 9}

internal readonly struct Utf8StructuralLinearInstruction
{
    private Utf8StructuralLinearInstruction(
        Utf8StructuralLinearInstructionKind kind, AsciiCharClass charClass, int minCount, int maxCount, AsciiCharClass secondaryCharClass, int secondaryMinCount, int auxiliaryMinCount, AsciiCharClass set)
    {
        Kind = kind;
        CharClass = charClass;
        MinCount = minCount;
        MaxCount = maxCount;
        SecondaryCharClass = secondaryCharClass;
        SecondaryMinCount = secondaryMinCount;
        AuxiliaryMinCount = auxiliaryMinCount;
        Set = set;
    }

    public Utf8StructuralLinearInstructionKind Kind { get; }

    public AsciiCharClass CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiCharClass SecondaryCharClass { get; }

    public int SecondaryMinCount { get; }

    public int AuxiliaryMinCount { get; }

    public AsciiCharClass Set { get; }

    public static Utf8StructuralLinearInstruction CreateRepeatedSegment(
        AsciiCharClass charClass, int minCount, int maxCount, AsciiCharClass secondaryCharClass, int secondaryMinCount, int auxiliaryMinCount, AsciiCharClass set) =>
        new(
            Utf8StructuralLinearInstructionKind.RepeatedSegment,
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

    public static Utf8StructuralLinearInstruction CreateOrderedLiteralWindow() =>
        CreateMarker(Utf8StructuralLinearInstructionKind.OrderedLiteralWindow);

    public static Utf8StructuralLinearInstruction CreateAccept() =>
        CreateMarker(Utf8StructuralLinearInstructionKind.Accept);

    private static Utf8StructuralLinearInstruction CreateMarker(Utf8StructuralLinearInstructionKind kind) =>
        new(kind, default, 0, 0, default, 0, 0, default);
}

internal readonly struct Utf8StructuralLinearInstructionProgram
{
    public Utf8StructuralLinearInstructionProgram(Utf8StructuralLinearInstruction[] instructions)
    {
        Instructions = instructions;
    }

    public Utf8StructuralLinearInstruction[] Instructions { get; }

    public bool HasValue => Instructions is { Length: > 0 };

    public static Utf8StructuralLinearInstructionProgram Create(AsciiStructuralRepeatedSegmentPlan repeatedSegmentPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            CreatePair(Utf8StructuralLinearInstruction.CreateRepeatedSegment(
                    repeatedSegmentPlan.LeadingCharClass,
                    repeatedSegmentPlan.RepetitionMinCount,
                    repeatedSegmentPlan.RepetitionMaxCount,
                    repeatedSegmentPlan.TrailingCharClass,
                    repeatedSegmentPlan.TrailingMinCount,
                    repeatedSegmentPlan.SeparatorMinCount,
                    repeatedSegmentPlan.SeparatorCharClass)));
    }

    public static Utf8StructuralLinearInstructionProgram Create(AsciiStructuralTokenWindowPlan tokenWindowPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            CreatePair(Utf8StructuralLinearInstruction.CreateTokenWindow()));
    }

    public static Utf8StructuralLinearInstructionProgram Create(AsciiStructuralQuotedRelationPlan quotedRelationPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            CreatePair(Utf8StructuralLinearInstruction.CreateQuotedRelation()));
    }

    public static Utf8StructuralLinearInstructionProgram Create(AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan)
    {
        return new Utf8StructuralLinearInstructionProgram(
            CreatePair(Utf8StructuralLinearInstruction.CreateOrderedLiteralWindow()));
    }

    private static Utf8StructuralLinearInstruction[] CreatePair(Utf8StructuralLinearInstruction first)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var instructions = scope.Budget.AllocateArray<Utf8StructuralLinearInstruction>(2, true);
        scope.Budget.ChargeWork(2);
        instructions[0] = first;
        instructions[1] = Utf8StructuralLinearInstruction.CreateAccept();
        scope.Complete();
        return instructions;
    }
}

internal readonly struct Utf8StructuralLinearProgram
{
    private Utf8StructuralLinearProgram(
        Utf8StructuralLinearProgramKind kind, Utf8StructuralLinearInstructionProgram instructionProgram, bool allowsUtf8ByteSafe, AsciiStructuralTokenWindowPlan tokenWindowPlan, AsciiStructuralQuotedRelationPlan quotedRelationPlan, AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan, AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan, Utf8SearchPlan searchPlan, Utf8StructuralSearchPlan structuralSearchPlan, Utf8StructuralVerifierPlan structuralVerifierPlan)
    {
        Kind = kind;
        InstructionProgram = instructionProgram;
        AllowsUtf8ByteSafe = allowsUtf8ByteSafe;
        TokenWindowPlan = tokenWindowPlan;
        QuotedRelationPlan = quotedRelationPlan;
        OrderedLiteralWindowPlan = orderedLiteralWindowPlan;
        StructuralIdentifierFamilyPlan = structuralIdentifierFamilyPlan;
        SearchPlan = searchPlan;
        StructuralSearchPlan = structuralSearchPlan;
        StructuralVerifierPlan = structuralVerifierPlan;
    }

    public Utf8StructuralLinearProgramKind Kind { get; }

    public Utf8StructuralLinearInstructionProgram InstructionProgram { get; }

    public bool AllowsUtf8ByteSafe { get; }

    public AsciiStructuralTokenWindowPlan TokenWindowPlan { get; }

    public AsciiStructuralQuotedRelationPlan QuotedRelationPlan { get; }

    public AsciiOrderedLiteralWindowPlan OrderedLiteralWindowPlan { get; }

    public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan { get; }

    public Utf8SearchPlan SearchPlan { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8StructuralVerifierPlan StructuralVerifierPlan { get; }

    public bool HasValue => Kind != Utf8StructuralLinearProgramKind.None;

    private static Utf8StructuralLinearProgram ForStructuralFamily(
        AsciiStructuralIdentifierFamilyPlan familyPlan, Utf8SearchPlan searchPlan, Utf8StructuralSearchPlan structuralSearchPlan, Utf8StructuralVerifierPlan verifierPlan) =>
        new(Utf8StructuralLinearProgramKind.AsciiStructuralFamily, default,             false, default, default, default, familyPlan, searchPlan, structuralSearchPlan, verifierPlan);

    private static Utf8StructuralLinearProgram ForTokenWindow(
        Utf8StructuralLinearInstructionProgram instructions, AsciiStructuralTokenWindowPlan plan) =>
        new(Utf8StructuralLinearProgramKind.AsciiTokenWindow, instructions,             true, plan, default, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForRepeatedSegment(
        Utf8StructuralLinearInstructionProgram instructions) =>
        new(Utf8StructuralLinearProgramKind.AsciiRepeatedSegment, instructions,             false, default, default, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForQuotedRelation(
        Utf8StructuralLinearInstructionProgram instructions, AsciiStructuralQuotedRelationPlan plan) =>
        new(Utf8StructuralLinearProgramKind.AsciiQuotedRelation, instructions,             false, default, plan, default, default, default, default, default);

    private static Utf8StructuralLinearProgram ForOrderedLiteralWindow(
        Utf8StructuralLinearInstructionProgram instructions, AsciiOrderedLiteralWindowPlan plan, Utf8SearchPlan searchPlan) =>
        new(Utf8StructuralLinearProgramKind.AsciiOrderedLiteralWindow, instructions,             true, default, default, plan, default, searchPlan, default, default);

    public static Utf8StructuralLinearProgram Create(
        NativeExecutionKind executionKind, AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan, AsciiStructuralTokenWindowPlan structuralTokenWindowPlan, AsciiStructuralRepeatedSegmentPlan structuralRepeatedSegmentPlan, AsciiStructuralQuotedRelationPlan structuralQuotedRelationPlan, AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan, Utf8StructuralVerifierPlan structuralVerifier, Utf8SearchPlan searchPlan, Utf8StructuralSearchPlan structuralSearchPlan)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var selected = executionKind switch
        {
            NativeExecutionKind.AsciiStructuralIdentifierFamily when structuralVerifier.Kind == Utf8StructuralVerifierKind.AsciiStructuralProgram
                => ForStructuralFamily(structuralIdentifierFamilyPlan, searchPlan, structuralSearchPlan, structuralVerifier),
            NativeExecutionKind.AsciiStructuralTokenWindow when structuralTokenWindowPlan.HasValue
                => ForTokenWindow(Utf8StructuralLinearInstructionProgram.Create(structuralTokenWindowPlan), structuralTokenWindowPlan),
            NativeExecutionKind.AsciiStructuralRepeatedSegment when structuralRepeatedSegmentPlan.HasValue
                => ForRepeatedSegment(Utf8StructuralLinearInstructionProgram.Create(structuralRepeatedSegmentPlan)),
            NativeExecutionKind.AsciiStructuralQuotedRelation when structuralQuotedRelationPlan.HasValue
                => ForQuotedRelation(Utf8StructuralLinearInstructionProgram.Create(structuralQuotedRelationPlan), structuralQuotedRelationPlan),
            NativeExecutionKind.AsciiOrderedLiteralWindow when orderedLiteralWindowPlan.HasValue
                => ForOrderedLiteralWindow(
                    Utf8StructuralLinearInstructionProgram.Create(orderedLiteralWindowPlan),
                    orderedLiteralWindowPlan,
                    searchPlan),
            _ => default,
        };
        scope.Complete();
        return selected;
    }
}

internal abstract class Utf8StructuralLinearRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;

    protected Utf8StructuralLinearRuntime(Utf8PreparedRegex regexPlan)
    {
        _regexPlan = regexPlan;
    }

    protected Utf8PreparedRegex PreparedRegex => _regexPlan;

    protected ref readonly Utf8StructuralLinearProgram Program => ref _regexPlan.StructuralLinearProgram;

    /// <summary>
    /// Evaluates whether the validated complete subject contains a match. All
    /// scanning and verification charges the supplied deadline; false is final.
    /// </summary>
    public abstract bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Counts nonoverlapping matches in the validated complete subject using
    /// scalar global progression and charging the supplied deadline.
    /// </summary>
    public abstract int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Returns the first capture-zero match in the validated complete subject,
    /// or a default range, while charging the deadline.
    /// </summary>
    public abstract Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Finds the first match whose byte start is at or after
    /// <paramref name="startIndex"/> in a validated subject. The start must be
    /// an admitted boundary. A false result definitively exhausts the suffix,
    /// output values are then unspecified, and all work charges the deadline.
    /// </summary>
    public abstract bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength);

    public static Utf8StructuralLinearRuntime Create(Utf8PreparedRegex regexPlan)
    {
        using var scope = Utf8ConstructionScope.Enter();
        // The prepared regex already owns the program. Borrow its storage rather
        // than copying the large program and all its alternative plan payloads.
        // Selected subtypes add no fields to this one-reference object state.
        var result = scope.Budget.AllocateObject<Utf8PreparedRegex, Utf8StructuralLinearRuntime>(regexPlan,
            static value => value.StructuralLinearProgram.Kind switch
        {
            Utf8StructuralLinearProgramKind.AsciiStructuralFamily => new Utf8AsciiStructuralFamilyLinearRuntime(value),
            Utf8StructuralLinearProgramKind.AsciiTokenWindow => new Utf8AsciiInstructionLinearRuntime(value),
            Utf8StructuralLinearProgramKind.AsciiRepeatedSegment => new Utf8AsciiInstructionLinearRuntime(value),
            Utf8StructuralLinearProgramKind.AsciiQuotedRelation => new Utf8AsciiInstructionLinearRuntime(value),
            Utf8StructuralLinearProgramKind.AsciiOrderedLiteralWindow => new Utf8AsciiInstructionLinearRuntime(value),
            _ => new Utf8NoStructuralLinearRuntime(value),
        });
        scope.Complete();
        return result;
    }

}

internal sealed class Utf8NoStructuralLinearRuntime : Utf8StructuralLinearRuntime
{
    public Utf8NoStructuralLinearRuntime(Utf8PreparedRegex regexPlan)
        : base(regexPlan)
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

    public override Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return default(Utf8CaptureRange);
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
    public Utf8AsciiInstructionLinearRuntime(Utf8PreparedRegex regexPlan)
        : base(regexPlan)
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

    public override Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        if (!Utf8AsciiInstructionLinearExecutor.SupportsInput(Program, validation))
        {
            return default(Utf8CaptureRange);
        }

        var index = Utf8AsciiInstructionLinearExecutor.FindNext(Program, input, 0, budget, out var matchedLength);
        return index < 0
            ? default(Utf8CaptureRange)
            : new Utf8CaptureRange(index, (index) + (matchedLength));
    }

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        if (!Utf8AsciiInstructionLinearExecutor.SupportsInput(Program, validation))
        {
            matchIndex = -1;
            matchedLength = 0;
            return false;
        }

        matchIndex = Utf8AsciiInstructionLinearExecutor.FindNext(Program, input, startIndex, budget, out matchedLength);
        return matchIndex >= 0;
    }
}

internal static partial class Utf8AsciiInstructionLinearExecutor
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
                    // A greedy outer + owns adjacent complete relations as
                    // one match. Keep each extension on the original clock.
                    if (program.QuotedRelationPlan.RepeatWholeRelation)
                    {
                        while (candidate + matchedLength < input.Length &&
                            input[candidate + matchedLength] is (byte)'"' or (byte)'\'')
                        {
                            budget.Step();
                            if (!TryMatchQuotedRelationAt(input, candidate + matchedLength,
                                program.QuotedRelationPlan, out var continuation)) break;
                            matchedLength += continuation;
                        }
                    }
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

    private static bool TryMatchAt(ReadOnlySpan<byte> input, in Utf8StructuralLinearProgram program, int startIndex, out int matchedLength)
    {

        return TryMatchAt(input, program.InstructionProgram, startIndex, out matchedLength);
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, Utf8StructuralLinearInstructionProgram program, int startIndex, out int matchedLength)
    {
        matchedLength = 0;
        if (startIndex < 0)
        {
            return false;
        }

        var index = startIndex;
        foreach (var instruction in program.Instructions)
        {
            switch (instruction.Kind)
            {

                case Utf8StructuralLinearInstructionKind.RepeatedSegment when
                    !instruction.CharClass.IsEmpty &&
                    !instruction.SecondaryCharClass.IsEmpty &&
                    !instruction.Set.IsEmpty:
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

                    matchedLength = index - startIndex;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryConsumeRepeatedSegment(
        ReadOnlySpan<byte> input, ref int index, AsciiCharClass leadingCharClass, AsciiCharClass trailingCharClass, int trailingMinCount, int separatorMinCount, AsciiCharClass separatorSet, out int separatorCount)
    {
        separatorCount = 0;
        if ((uint)index >= (uint)input.Length || !leadingCharClass.Contains(input[index]))
        {
            return false;
        }

        var position = index + 1;

        var tailCount = 0;
        while ((uint)position < (uint)input.Length && trailingCharClass.Contains(input[position]))
        {
            position++;
            tailCount++;
        }

        if (tailCount < trailingMinCount)
        {
            return false;
        }

        while ((uint)position < (uint)input.Length && MatchesSet(input[position], separatorSet))
        {
            position++;
            separatorCount++;
        }

        if (separatorCount < separatorMinCount) return false;
        // A failed extra segment cannot extend an already successful repeat.
        index = position;
        return true;
    }

    private static bool MatchesSet(byte value, AsciiCharClass runtimeSet)
    {
        return runtimeSet.Contains(value);
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
        ReadOnlySpan<byte> input, int startIndex, AsciiStructuralQuotedRelationPlan plan, AsciiStructuralQuotedOperandKind kind, bool allowRepeat, int maxLineBreaks, out int matchEnd)
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
        ReadOnlySpan<byte> input, int startIndex, AsciiStructuralQuotedRelationPlan plan, AsciiStructuralQuotedOperandKind kind, bool allowRepeat, out int endIndex)
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
        ReadOnlySpan<byte> input, int startIndex, AsciiStructuralQuotedRelationPlan plan, AsciiStructuralQuotedOperandKind kind, out int endIndex)
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
    public Utf8AsciiStructuralFamilyLinearRuntime(Utf8PreparedRegex regexPlan)
        : base(regexPlan)
    {
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return UsesRightToLeft(PreparedRegex)
            ? Utf8PreparedScalarExecutor.IsMatch(PreparedRegex, input, validation, budget)
            : FindNext(input, verifierRuntime, 0, budget, out _) >= 0;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralLinearAutomaton);
        return UsesRightToLeft(PreparedRegex)
            ? Utf8PreparedScalarExecutor.Count(PreparedRegex, input, validation, budget)
            : CountForward(input, verifierRuntime, budget);
    }

    public override Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        if (UsesRightToLeft(PreparedRegex))
        {
            return Utf8PreparedScalarExecutor.Match(PreparedRegex, input, validation, budget);
        }

        var index = FindNext(input, verifierRuntime, 0, budget, out var matchedLength);
        return index < 0
            ? default(Utf8CaptureRange)
            : new Utf8CaptureRange(index, (index) + (matchedLength));
    }

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        if (UsesRightToLeft(PreparedRegex))
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
        if ((!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null) &&
            Program.StructuralVerifierPlan.AsciiProgram.LinearProgram.HasValue && Program.StructuralSearchPlan.HasValue)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            var breach = Utf8AsciiStructuralIdentifierFamilyExecutor.TryCount(Program.StructuralSearchPlan,
                Program.StructuralVerifierPlan.AsciiProgram, input, ref invocation, out var boundedCount);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            return boundedCount;
        }
        if (CanUseStatefulSearch())
        {
            var statefulCount = 0;
            var state = CreateScanState(0);
            while (TryFindNextStateful(input, ref state, budget, out _, out _))
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
        ReadOnlySpan<byte> input, ref Utf8AsciiStructuralFamilyScanState state, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null) &&
            Program.StructuralVerifierPlan.AsciiProgram.LinearProgram.HasValue && Program.StructuralSearchPlan.HasValue)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            var pending = state.SearchState;
            var breach = Utf8AsciiStructuralIdentifierFamilyExecutor.TryFindNext(Program.StructuralSearchPlan,
                Program.StructuralVerifierPlan.AsciiProgram, input, state.MinStartIndex, ref pending, ref invocation,
                out var match, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            state = new Utf8AsciiStructuralFamilyScanState(pending, found ? match.Index + Math.Max(1, match.Length) : state.MinStartIndex);
            matchIndex = found ? match.Index : -1;
            matchedLength = found ? match.Length : 0;
            return found;
        }
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

    private int FindNext(ReadOnlySpan<byte> input, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        matchedLength = 0;
        if ((!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null) &&
            Program.StructuralVerifierPlan.AsciiProgram.LinearProgram.HasValue && Program.StructuralSearchPlan.HasValue)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            var state = new Utf8StructuralSearchState(new PreparedSearchScanState(startIndex, default), default);
            var breach = Utf8AsciiStructuralIdentifierFamilyExecutor.TryFindNext(Program.StructuralSearchPlan,
                Program.StructuralVerifierPlan.AsciiProgram, input, startIndex, ref state, ref invocation, out var match, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            matchedLength = found ? match.Length : 0;
            return found ? match.Index : -1;
        }
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

    private static bool UsesRightToLeft(Utf8PreparedRegex regexPlan)
    {
        return (regexPlan.SemanticRegex.Options & RegexOptions.RightToLeft) != 0;
    }
}

internal readonly record struct Utf8AsciiStructuralFamilyScanState(Utf8StructuralSearchState SearchState, int MinStartIndex);
