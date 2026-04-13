using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

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

internal readonly struct AsciiStructuralVerifierStep
{
    private AsciiStructuralVerifierStep(
        AsciiStructuralVerifierStepKind kind,
        string? set = null,
        int minCount = 0,
        int maxCount = int.MaxValue,
        AsciiStructuralSuffixPart[]? suffixParts = null,
        Utf8BoundaryRequirement boundaryRequirement = Utf8BoundaryRequirement.None)
    {
        Kind = kind;
        Set = set;
        MinCount = minCount;
        MaxCount = maxCount;
        SuffixParts = suffixParts;
        BoundaryRequirement = boundaryRequirement;
    }

    public AsciiStructuralVerifierStepKind Kind { get; }

    public string? Set { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiStructuralSuffixPart[]? SuffixParts { get; }

    public Utf8BoundaryRequirement BoundaryRequirement { get; }

    public static AsciiStructuralVerifierStep ConsumeSeparator(string set, int minCount) =>
        new(AsciiStructuralVerifierStepKind.ConsumeSeparator, set: set, minCount: minCount);

    public static AsciiStructuralVerifierStep RequireIdentifierStart(string set) =>
        new(AsciiStructuralVerifierStepKind.RequireIdentifierStart, set: set);

    public static AsciiStructuralVerifierStep ConsumeIdentifierTail(string set, int minCount, int maxCount) =>
        new(AsciiStructuralVerifierStepKind.ConsumeIdentifierTail, set: set, minCount: minCount, maxCount: maxCount);

    public static AsciiStructuralVerifierStep MatchSuffixAtCurrent(AsciiStructuralSuffixPart[] suffixParts) =>
        new(AsciiStructuralVerifierStepKind.MatchSuffixAtCurrent, suffixParts: suffixParts);

    public static AsciiStructuralVerifierStep MatchSuffixAfterTail(AsciiStructuralSuffixPart[] suffixParts, int minTailCount) =>
        new(AsciiStructuralVerifierStepKind.MatchSuffixAfterTail, suffixParts: suffixParts, minCount: minTailCount);

    public static AsciiStructuralVerifierStep RequireTrailingBoundary(Utf8BoundaryRequirement requirement) =>
        new(AsciiStructuralVerifierStepKind.RequireTrailingBoundary, boundaryRequirement: requirement);

    public static AsciiStructuralVerifierStep Accept() =>
        new(AsciiStructuralVerifierStepKind.Accept);
}

internal readonly struct AsciiStructuralCompiledSuffixPart
{
    public AsciiStructuralCompiledSuffixPart(
        byte[]? literalUtf8,
        string? separatorSet,
        AsciiCharClass? separatorCharClass,
        int separatorMinCount)
    {
        LiteralUtf8 = literalUtf8;
        SeparatorSet = separatorSet;
        SeparatorCharClass = separatorCharClass;
        SeparatorMinCount = separatorMinCount;
    }

    public byte[]? LiteralUtf8 { get; }

    public string? SeparatorSet { get; }

    public AsciiCharClass? SeparatorCharClass { get; }

    public int SeparatorMinCount { get; }

    public bool IsLiteral => LiteralUtf8 is { Length: > 0 };

    public bool IsSeparator => !string.IsNullOrEmpty(SeparatorSet);
}

internal readonly struct AsciiStructuralVerifierProgram
{
    public AsciiStructuralVerifierProgram(AsciiStructuralVerifierStep[]? steps)
    {
        Steps = steps is { Length: > 0 } ? steps : [];
        LinearProgram = AsciiStructuralLinearVerifierProgram.Create(Steps);
    }

    public AsciiStructuralVerifierStep[] Steps { get; }

    public AsciiStructuralLinearVerifierProgram LinearProgram { get; }

    public bool HasValue => Steps is { Length: > 0 };

    public static AsciiStructuralVerifierProgram Create(AsciiStructuralIdentifierFamilyPlan plan)
    {
        var steps = new List<AsciiStructuralVerifierStep>(8);
        if (!string.IsNullOrEmpty(plan.SeparatorSet))
        {
            steps.Add(AsciiStructuralVerifierStep.ConsumeSeparator(plan.SeparatorSet, plan.SeparatorMinCount));
        }

        if (!string.IsNullOrEmpty(plan.IdentifierStartSet))
        {
            steps.Add(AsciiStructuralVerifierStep.RequireIdentifierStart(plan.IdentifierStartSet));
            steps.Add(AsciiStructuralVerifierStep.ConsumeIdentifierTail(plan.IdentifierTailSet, plan.IdentifierTailMinCount, plan.IdentifierTailMaxCount));
            if (plan.SuffixParts.Length > 0)
            {
                steps.Add(AsciiStructuralVerifierStep.MatchSuffixAfterTail(plan.SuffixParts, plan.IdentifierTailMinCount));
            }
        }
        else if (plan.SuffixParts.Length > 0)
        {
            steps.Add(AsciiStructuralVerifierStep.MatchSuffixAtCurrent(plan.SuffixParts));
        }

        if (plan.TrailingBoundary != Utf8BoundaryRequirement.None)
        {
            steps.Add(AsciiStructuralVerifierStep.RequireTrailingBoundary(plan.TrailingBoundary));
        }

        steps.Add(AsciiStructuralVerifierStep.Accept());
        return new AsciiStructuralVerifierProgram([.. steps]);
    }

    public bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, out int matchedLength)
    {
        return LinearProgram.TryMatch(input, matchIndex, prefixLength, out matchedLength);
    }
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

internal readonly struct AsciiStructuralLinearVerifierInstruction
{
    public AsciiStructuralLinearVerifierInstruction(
        AsciiStructuralLinearVerifierInstructionKind kind,
        string? set = null,
        AsciiCharClass? charClass = null,
        int minCount = 0,
        int maxCount = int.MaxValue,
        AsciiStructuralCompiledSuffixPart[]? suffixParts = null,
        Utf8BoundaryRequirement boundaryRequirement = Utf8BoundaryRequirement.None)
    {
        Kind = kind;
        Set = set;
        CharClass = charClass;
        MinCount = minCount;
        MaxCount = maxCount;
        SuffixParts = suffixParts;
        BoundaryRequirement = boundaryRequirement;
    }

    public AsciiStructuralLinearVerifierInstructionKind Kind { get; }

    public string? Set { get; }

    public AsciiCharClass? CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiStructuralCompiledSuffixPart[]? SuffixParts { get; }

    public Utf8BoundaryRequirement BoundaryRequirement { get; }
}

internal readonly struct AsciiStructuralLinearVerifierProgram
{
    public AsciiStructuralLinearVerifierProgram(AsciiStructuralLinearVerifierInstruction[]? instructions)
    {
        Instructions = instructions is { Length: > 0 } ? instructions : [];
    }

    public AsciiStructuralLinearVerifierInstruction[] Instructions { get; }

    public bool HasValue => Instructions is { Length: > 0 };

    public static AsciiStructuralLinearVerifierProgram Create(ReadOnlySpan<AsciiStructuralVerifierStep> steps)
    {
        if (steps.Length == 0)
        {
            return default;
        }

        var instructions = new AsciiStructuralLinearVerifierInstruction[steps.Length];
        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            var charClass = TryCreateAsciiCharClass(step.Set, out var createdCharClass)
                ? createdCharClass
                : null;
            var compiledSuffixParts = CreateCompiledSuffixParts(step.SuffixParts);
            instructions[i] = step.Kind switch
            {
                AsciiStructuralVerifierStepKind.ConsumeSeparator
                    => new AsciiStructuralLinearVerifierInstruction(AsciiStructuralLinearVerifierInstructionKind.ConsumeSetLoop, set: step.Set, charClass: charClass, minCount: step.MinCount),
                AsciiStructuralVerifierStepKind.RequireIdentifierStart
                    => new AsciiStructuralLinearVerifierInstruction(AsciiStructuralLinearVerifierInstructionKind.RequireSetByte, set: step.Set, charClass: charClass),
                AsciiStructuralVerifierStepKind.ConsumeIdentifierTail
                    => new AsciiStructuralLinearVerifierInstruction(AsciiStructuralLinearVerifierInstructionKind.ConsumeSetTail, set: step.Set, charClass: charClass, minCount: step.MinCount, maxCount: step.MaxCount),
                AsciiStructuralVerifierStepKind.MatchSuffixAtCurrent
                    => new AsciiStructuralLinearVerifierInstruction(AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAtCurrent, suffixParts: compiledSuffixParts),
                AsciiStructuralVerifierStepKind.MatchSuffixAfterTail
                    => new AsciiStructuralLinearVerifierInstruction(AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAfterTail, minCount: step.MinCount, suffixParts: compiledSuffixParts),
                AsciiStructuralVerifierStepKind.RequireTrailingBoundary
                    => new AsciiStructuralLinearVerifierInstruction(AsciiStructuralLinearVerifierInstructionKind.RequireBoundary, boundaryRequirement: step.BoundaryRequirement),
                AsciiStructuralVerifierStepKind.Accept
                    => new AsciiStructuralLinearVerifierInstruction(AsciiStructuralLinearVerifierInstructionKind.Accept),
                _ => default,
            };
        }

        return new AsciiStructuralLinearVerifierProgram(instructions);
    }

    public bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, out int matchedLength)
    {
        matchedLength = 0;
        if (!HasValue)
        {
            return false;
        }

        var index = matchIndex + prefixLength;
        var tailStart = -1;
        var tailEnd = -1;
        foreach (var instruction in Instructions)
        {
            switch (instruction.Kind)
            {
                case AsciiStructuralLinearVerifierInstructionKind.ConsumeSetLoop:
                    if (!TryConsumeSetLoop(input, ref index, instruction.Set!, instruction.CharClass, instruction.MinCount))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.RequireSetByte:
                    if ((uint)index >= (uint)input.Length || !MatchesSet(input[index], instruction.Set!, instruction.CharClass))
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
                           MatchesSet(input[index], instruction.Set!, instruction.CharClass) &&
                           consumed < instruction.MaxCount)
                    {
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
                    if (!TryMatchSuffixParts(input, index, instruction.SuffixParts, out index))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAfterTail:
                    if (tailStart < 0 || tailEnd < tailStart)
                    {
                        return false;
                    }

                    if (!TryMatchSuffixPartsAfterTail(input, tailStart + instruction.MinCount, tailEnd, instruction.SuffixParts, out index))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.RequireBoundary:
                    if (!MatchesBoundaryRequirement(instruction.BoundaryRequirement, input, index))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.Accept:
                    matchedLength = index - matchIndex;
                    return true;
            }
        }

        return false;
    }

    private static bool TryConsumeSetLoop(ReadOnlySpan<byte> input, ref int index, string set, AsciiCharClass? charClass, int minCount)
    {
        var count = 0;
        while ((uint)index < (uint)input.Length && MatchesSet(input[index], set, charClass))
        {
            index++;
            count++;
        }

        return count >= minCount;
    }

    private static bool TryMatchSuffixParts(ReadOnlySpan<byte> input, int startIndex, ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts, out int endIndex)
    {
        endIndex = startIndex;
        var index = startIndex;

        for (var i = 0; i < suffixParts.Length; i++)
        {
            var part = suffixParts[i];
            if (part.IsSeparator)
            {
                if (!TryConsumeSetLoop(input, ref index, part.SeparatorSet!, part.SeparatorCharClass, part.SeparatorMinCount))
                {
                    return false;
                }

                continue;
            }

            var literal = part.LiteralUtf8;
            if (literal is null ||
                literal.Length == 0 ||
                input.Length - index < literal.Length ||
                !input.Slice(index, literal.Length).SequenceEqual(literal))
            {
                return false;
            }

            index += literal.Length;
        }

        endIndex = index;
        return true;
    }

    private static bool TryMatchSuffixPartsAfterTail(
        ReadOnlySpan<byte> input,
        int searchStart,
        int tailEnd,
        ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts,
        out int endIndex)
    {
        endIndex = tailEnd;
        if (suffixParts.Length == 0)
        {
            return false;
        }

        if (suffixParts[0].IsSeparator)
        {
            return TryMatchSuffixParts(input, tailEnd, suffixParts, out endIndex);
        }

        var firstLiteral = suffixParts[0].LiteralUtf8;
        if (firstLiteral is null || firstLiteral.Length == 0)
        {
            return false;
        }

        for (var start = tailEnd - firstLiteral.Length; start >= searchStart; start--)
        {
            if (TryMatchSuffixParts(input, start, suffixParts, out endIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSet(byte value, string runtimeSet, AsciiCharClass? charClass)
    {
        return charClass is not null
            ? charClass.Contains(value)
            : value < 128 && RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, runtimeSet);
    }

    private static AsciiStructuralCompiledSuffixPart[]? CreateCompiledSuffixParts(AsciiStructuralSuffixPart[]? suffixParts)
    {
        if (suffixParts is null)
        {
            return null;
        }

        var compiled = new AsciiStructuralCompiledSuffixPart[suffixParts.Length];
        for (var i = 0; i < suffixParts.Length; i++)
        {
            var part = suffixParts[i];
            compiled[i] = new AsciiStructuralCompiledSuffixPart(
                part.LiteralUtf8,
                part.SeparatorSet,
                TryCreateAsciiCharClass(part.SeparatorSet, out var charClass) ? charClass : null,
                part.SeparatorMinCount);
        }

        return compiled;
    }

    private static bool TryCreateAsciiCharClass(string? runtimeSet, out AsciiCharClass charClass)
    {
        if (string.IsNullOrEmpty(runtimeSet))
        {
            charClass = null!;
            return false;
        }

        switch (runtimeSet)
        {
            case RuntimeFrontEnd.RegexCharClass.SpaceClass:
            case RuntimeFrontEnd.RegexCharClass.ECMASpaceClass:
                charClass = CreateAsciiCharClass(static ch => ch is ' ' or '\t' or '\r' or '\n' or '\f' or '\v', negated: false);
                return true;

            case RuntimeFrontEnd.RegexCharClass.NotSpaceClass:
            case RuntimeFrontEnd.RegexCharClass.NotECMASpaceClass:
                charClass = CreateAsciiCharClass(static ch => ch is ' ' or '\t' or '\r' or '\n' or '\f' or '\v', negated: true);
                return true;
        }

        if (!RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet))
        {
            charClass = null!;
            return false;
        }

        var negated = RuntimeFrontEnd.RegexCharClass.IsNegated(runtimeSet);
        var matches = new bool[128];
        for (var i = 0; i < matches.Length; i++)
        {
            matches[i] = RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)i, runtimeSet);
        }

        charClass = new AsciiCharClass(matches, negated);
        return true;
    }

    private static AsciiCharClass CreateAsciiCharClass(Func<char, bool> predicate, bool negated)
    {
        var matches = new bool[128];
        for (var i = 0; i < matches.Length; i++)
        {
            matches[i] = predicate((char)i);
        }

        return new AsciiCharClass(matches, negated);
    }

    private static bool MatchesBoundaryRequirement(Utf8BoundaryRequirement requirement, ReadOnlySpan<byte> input, int byteOffset)
    {
        return requirement switch
        {
            Utf8BoundaryRequirement.None => true,
            Utf8BoundaryRequirement.Boundary => IsWordBoundary(input, byteOffset),
            Utf8BoundaryRequirement.NonBoundary => !IsWordBoundary(input, byteOffset),
            _ => false,
        };
    }

    private static bool IsWordBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var previousIsWord = byteOffset > 0 &&
            RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar((char)input[byteOffset - 1]);
        var nextIsWord = byteOffset < input.Length &&
            RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar((char)input[byteOffset]);
        return previousIsWord != nextIsWord;
    }
}
