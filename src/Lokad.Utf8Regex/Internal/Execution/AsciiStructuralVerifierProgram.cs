using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum AsciiStructuralVerifierStepKind : byte
{
    ConsumeSeparator = 0,
    RequireIdentifierStart = 1,
    ConsumeIdentifierTail = 2,
    MatchSuffixAtCurrent = 3,
    MatchSuffixAfterTail = 4,
    RequireTrailingBoundary = 5,
    Accept = 6,
}

internal readonly partial struct AsciiStructuralVerifierStep
{
    private AsciiStructuralVerifierStep(
        AsciiStructuralVerifierStepKind kind,
        AsciiCharClass charClass,
        int minCount,
        int maxCount,
        AsciiStructuralSuffixPart[] suffixParts,
        Utf8BoundaryRequirement boundaryRequirement)
    {
        Kind = kind;
        CharClass = charClass;
        MinCount = minCount;
        MaxCount = maxCount;
        SuffixParts = suffixParts;
        BoundaryRequirement = boundaryRequirement;
    }

    public AsciiStructuralVerifierStepKind Kind { get; }

    public AsciiCharClass CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiStructuralSuffixPart[] SuffixParts { get; }

    public Utf8BoundaryRequirement BoundaryRequirement { get; }

    public static AsciiStructuralVerifierStep ConsumeSeparator(AsciiCharClass charClass, int minCount) =>
        new(AsciiStructuralVerifierStepKind.ConsumeSeparator, charClass, minCount, int.MaxValue, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep RequireIdentifierStart(AsciiCharClass charClass) =>
        new(AsciiStructuralVerifierStepKind.RequireIdentifierStart, charClass, 0, int.MaxValue, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep ConsumeIdentifierTail(AsciiCharClass charClass, int minCount, int maxCount) =>
        new(AsciiStructuralVerifierStepKind.ConsumeIdentifierTail, charClass, minCount, maxCount, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep MatchSuffixAtCurrent(AsciiStructuralSuffixPart[] suffixParts) =>
        new(AsciiStructuralVerifierStepKind.MatchSuffixAtCurrent, default, 0, int.MaxValue, suffixParts, Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep MatchSuffixAfterTail(AsciiStructuralSuffixPart[] suffixParts, int minTailCount) =>
        new(AsciiStructuralVerifierStepKind.MatchSuffixAfterTail, default, minTailCount, int.MaxValue, suffixParts, Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep RequireTrailingBoundary(Utf8BoundaryRequirement requirement) =>
        new(AsciiStructuralVerifierStepKind.RequireTrailingBoundary, default, 0, int.MaxValue, [], requirement);

    public static AsciiStructuralVerifierStep Accept() =>
        new(AsciiStructuralVerifierStepKind.Accept, default, 0, int.MaxValue, [], Utf8BoundaryRequirement.None);
}

internal readonly partial struct AsciiStructuralVerifierProgram
{
    public AsciiStructuralVerifierProgram(AsciiStructuralVerifierStep[]? steps)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            Steps = steps is { Length: > 0 } ? steps : [];
            LinearProgram = AsciiStructuralLinearVerifierProgram.Create(Steps);
            construction.Complete();
        }
        finally { owner.LeaveDepth(); }
    }

    public AsciiStructuralVerifierStep[] Steps { get; }

    public AsciiStructuralLinearVerifierProgram LinearProgram { get; }

    public bool HasValue => Steps is { Length: > 0 };

    public static AsciiStructuralVerifierProgram Create(AsciiStructuralIdentifierFamilyPlan plan)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            var count = 1 + (plan.HasSeparator ? 1 : 0)
                + (plan.HasIdentifier ? 2 : 0)
                + (plan.SuffixParts.Length > 0 ? 1 : 0)
                + (plan.TrailingBoundary != Utf8BoundaryRequirement.None ? 1 : 0);
            var steps = owner.AllocateArray<AsciiStructuralVerifierStep>(count, false);
            owner.ChargeWork((ulong)count);
            var index = 0;
            if (plan.HasSeparator)
            {
                steps[index++] = AsciiStructuralVerifierStep.ConsumeSeparator(plan.SeparatorCharClass, plan.SeparatorMinCount);
            }

            if (plan.HasIdentifier)
            {
                steps[index++] = AsciiStructuralVerifierStep.RequireIdentifierStart(plan.IdentifierStartCharClass);
                steps[index++] = AsciiStructuralVerifierStep.ConsumeIdentifierTail(plan.IdentifierTailCharClass, plan.IdentifierTailMinCount, plan.IdentifierTailMaxCount);
                if (plan.SuffixParts.Length > 0)
                {
                    steps[index++] = AsciiStructuralVerifierStep.MatchSuffixAfterTail(plan.SuffixParts, plan.IdentifierTailMinCount);
                }
            }
            else if (plan.SuffixParts.Length > 0)
            {
                steps[index++] = AsciiStructuralVerifierStep.MatchSuffixAtCurrent(plan.SuffixParts);
            }

            if (plan.TrailingBoundary != Utf8BoundaryRequirement.None)
            {
                steps[index++] = AsciiStructuralVerifierStep.RequireTrailingBoundary(plan.TrailingBoundary);
            }

            steps[index++] = AsciiStructuralVerifierStep.Accept();
            var result = new AsciiStructuralVerifierProgram(steps);
            construction.Complete(); return result;
        }
        finally { owner.LeaveDepth(); }
    }

    public bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, out int matchedLength)
    {
        return LinearProgram.TryMatch(input, matchIndex, prefixLength, out matchedLength);
    }

    internal bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength,
        Utf8ExecutionDeadline deadline, out int matchedLength)
        => LinearProgram.TryMatch(input, matchIndex, prefixLength, deadline, out matchedLength);

    internal bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength,
        ref Utf8SliceInvocation invocation, out int matchedLength, out Utf8ExecutionLimitKind breach)
        => LinearProgram.TryMatch(input, matchIndex, prefixLength, ref invocation, out matchedLength, out breach);
}

internal enum AsciiStructuralLinearVerifierInstructionKind : byte
{
    ConsumeSetLoop = 0,
    RequireSetByte = 1,
    ConsumeSetTail = 2,
    MatchSuffixAtCurrent = 3,
    MatchSuffixAfterTail = 4,
    RequireBoundary = 5,
    Accept = 6,
}

internal readonly partial struct AsciiStructuralLinearVerifierInstruction
{
    private AsciiStructuralLinearVerifierInstruction(
        AsciiStructuralLinearVerifierInstructionKind kind,
        AsciiCharClass charClass,
        int minCount,
        int maxCount,
        AsciiStructuralSuffixPart[] suffixParts,
        Utf8BoundaryRequirement boundaryRequirement)
    {
        Kind = kind;
        CharClass = charClass;
        MinCount = minCount;
        MaxCount = maxCount;
        SuffixParts = suffixParts;
        BoundaryRequirement = boundaryRequirement;
    }

    public AsciiStructuralLinearVerifierInstructionKind Kind { get; }

    public AsciiCharClass CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiStructuralSuffixPart[] SuffixParts { get; }

    public Utf8BoundaryRequirement BoundaryRequirement { get; }

    public static AsciiStructuralLinearVerifierInstruction ConsumeSetLoop(AsciiCharClass charClass, int minCount) =>
        new(AsciiStructuralLinearVerifierInstructionKind.ConsumeSetLoop, charClass, minCount, int.MaxValue, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction RequireSetByte(AsciiCharClass charClass) =>
        new(AsciiStructuralLinearVerifierInstructionKind.RequireSetByte, charClass, 0, int.MaxValue, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction ConsumeSetTail(AsciiCharClass charClass, int minCount, int maxCount) =>
        new(AsciiStructuralLinearVerifierInstructionKind.ConsumeSetTail, charClass, minCount, maxCount, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction MatchSuffixAtCurrent(AsciiStructuralSuffixPart[] suffixParts) =>
        new(AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAtCurrent, default, 0, int.MaxValue, suffixParts, Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction MatchSuffixAfterTail(AsciiStructuralSuffixPart[] suffixParts, int minCount) =>
        new(AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAfterTail, default, minCount, int.MaxValue, suffixParts, Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction RequireBoundary(Utf8BoundaryRequirement boundaryRequirement) =>
        new(AsciiStructuralLinearVerifierInstructionKind.RequireBoundary, default, 0, int.MaxValue, [], boundaryRequirement);

    public static AsciiStructuralLinearVerifierInstruction Accept() =>
        new(AsciiStructuralLinearVerifierInstructionKind.Accept, default, 0, int.MaxValue, [], Utf8BoundaryRequirement.None);
}

internal readonly partial struct AsciiStructuralLinearVerifierProgram
{
    public AsciiStructuralLinearVerifierProgram(AsciiStructuralLinearVerifierInstruction[]? instructions)
    {
        Instructions = instructions is { Length: > 0 } ? instructions : [];
    }

    public AsciiStructuralLinearVerifierInstruction[] Instructions { get; }

    public bool HasValue => Instructions is { Length: > 0 };

    public static AsciiStructuralLinearVerifierProgram Create(ReadOnlySpan<AsciiStructuralVerifierStep> steps)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            if (steps.Length == 0)
            {
                return default;
            }

            var instructions = owner.AllocateArray<AsciiStructuralLinearVerifierInstruction>(steps.Length, false);
            for (var i = 0; i < steps.Length; i++)
            {
                owner.ChargeWork(1);
                var step = steps[i];
                instructions[i] = step.Kind switch
                {
                    AsciiStructuralVerifierStepKind.ConsumeSeparator
                        => AsciiStructuralLinearVerifierInstruction.ConsumeSetLoop(step.CharClass, step.MinCount),
                    AsciiStructuralVerifierStepKind.RequireIdentifierStart
                        => AsciiStructuralLinearVerifierInstruction.RequireSetByte(step.CharClass),
                    AsciiStructuralVerifierStepKind.ConsumeIdentifierTail
                        => AsciiStructuralLinearVerifierInstruction.ConsumeSetTail(step.CharClass, step.MinCount, step.MaxCount),
                    AsciiStructuralVerifierStepKind.MatchSuffixAtCurrent
                        => AsciiStructuralLinearVerifierInstruction.MatchSuffixAtCurrent(step.SuffixParts),
                    AsciiStructuralVerifierStepKind.MatchSuffixAfterTail
                        => AsciiStructuralLinearVerifierInstruction.MatchSuffixAfterTail(step.SuffixParts, step.MinCount),
                    AsciiStructuralVerifierStepKind.RequireTrailingBoundary
                        => AsciiStructuralLinearVerifierInstruction.RequireBoundary(step.BoundaryRequirement),
                    AsciiStructuralVerifierStepKind.Accept
                        => AsciiStructuralLinearVerifierInstruction.Accept(),
                    _ => default,
                };
            }

            var result = new AsciiStructuralLinearVerifierProgram(instructions);
            construction.Complete(); return result;
        }
        finally { owner.LeaveDepth(); }
    }

    public bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, out int matchedLength)
        => TryMatch(input, matchIndex, prefixLength, Utf8ExecutionDeadline.Infinite, out matchedLength);

    internal bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength,
        Utf8ExecutionDeadline deadline, out int matchedLength)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, CancellationToken.None);
        var found = TryMatch(input, matchIndex, prefixLength, ref invocation, out matchedLength, out var breach);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("An unlimited ASCII structural invocation exceeded its representable state extent.");
        return found;
    }

    internal bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength,
        ref Utf8SliceInvocation invocation, out int matchedLength, out Utf8ExecutionLimitKind breach)
    {
        matchedLength = 0;
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return false;
        breach = invocation.Budget.TryNoteDepth(1);
        if (breach != Utf8ExecutionLimitKind.None) return false;
        if (!HasValue)
        {
            return false;
        }

        var index = matchIndex + prefixLength;
        var tailStart = -1;
        var tailEnd = -1;
        foreach (var instruction in Instructions)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            switch (instruction.Kind)
            {
                case AsciiStructuralLinearVerifierInstructionKind.ConsumeSetLoop:
                    if (!AsciiStructuralSuffixMatcher.TryConsumeSetLoop(input, ref index, instruction.CharClass, instruction.MinCount, ref invocation, out breach))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.RequireSetByte:
                    if ((uint)index >= (uint)input.Length || !AsciiStructuralSuffixMatcher.MatchesSet(input[index], instruction.CharClass))
                    {
                        return false;
                    }

                    index++;
                    tailStart = index;
                    tailEnd = index;
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.ConsumeSetTail:
                    var consumed = 0;
                    while ((uint)index < (uint)input.Length &&
                           AsciiStructuralSuffixMatcher.MatchesSet(input[index], instruction.CharClass) &&
                           consumed < instruction.MaxCount)
                    {
                        if ((consumed & 2047) == 0)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return false;
                        }
                        index++;
                        consumed++;
                    }

                    tailEnd = index;
                    if (tailStart < 0 || tailEnd - tailStart < instruction.MinCount)
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAtCurrent:
                    if (!AsciiStructuralSuffixMatcher.TryMatch(input, index, instruction.SuffixParts, ref invocation, out index, out breach))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAfterTail:
                    if (tailStart < 0 || tailEnd < tailStart)
                    {
                        return false;
                    }

                    if (!AsciiStructuralSuffixMatcher.TryMatchAfterTail(input, tailStart + instruction.MinCount, tailEnd, instruction.SuffixParts, ref invocation, out index, out breach))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.RequireBoundary:
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(instruction.BoundaryRequirement, input, index))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.Accept:
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    matchedLength = index - matchIndex;
                    return true;
            }
        }

        return false;
    }

}
