using Lokad.Utf8Regex.Internal.Planning;
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
        string set,
        int minCount,
        int maxCount,
        AsciiStructuralSuffixPart[] suffixParts,
        Utf8BoundaryRequirement boundaryRequirement)
    {
        Kind = kind;
        Set = set;
        MinCount = minCount;
        MaxCount = maxCount;
        SuffixParts = suffixParts;
        BoundaryRequirement = boundaryRequirement;
    }

    public AsciiStructuralVerifierStepKind Kind { get; }

    public string Set { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiStructuralSuffixPart[] SuffixParts { get; }

    public Utf8BoundaryRequirement BoundaryRequirement { get; }

    public static AsciiStructuralVerifierStep ConsumeSeparator(string set, int minCount) =>
        new(AsciiStructuralVerifierStepKind.ConsumeSeparator, set, minCount, int.MaxValue, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep RequireIdentifierStart(string set) =>
        new(AsciiStructuralVerifierStepKind.RequireIdentifierStart, set, 0, int.MaxValue, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep ConsumeIdentifierTail(string set, int minCount, int maxCount) =>
        new(AsciiStructuralVerifierStepKind.ConsumeIdentifierTail, set, minCount, maxCount, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep MatchSuffixAtCurrent(AsciiStructuralSuffixPart[] suffixParts) =>
        new(AsciiStructuralVerifierStepKind.MatchSuffixAtCurrent, string.Empty, 0, int.MaxValue, suffixParts, Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep MatchSuffixAfterTail(AsciiStructuralSuffixPart[] suffixParts, int minTailCount) =>
        new(AsciiStructuralVerifierStepKind.MatchSuffixAfterTail, string.Empty, minTailCount, int.MaxValue, suffixParts, Utf8BoundaryRequirement.None);

    public static AsciiStructuralVerifierStep RequireTrailingBoundary(Utf8BoundaryRequirement requirement) =>
        new(AsciiStructuralVerifierStepKind.RequireTrailingBoundary, string.Empty, 0, int.MaxValue, [], requirement);

    public static AsciiStructuralVerifierStep Accept() =>
        new(AsciiStructuralVerifierStepKind.Accept, string.Empty, 0, int.MaxValue, [], Utf8BoundaryRequirement.None);
}

internal readonly struct AsciiStructuralCompiledSuffixPart
{
    private readonly byte[]? _literalUtf8;
    private readonly string? _separatorSet;

    private AsciiStructuralCompiledSuffixPart(
        AsciiStructuralSuffixPartKind kind,
        byte[] literalUtf8,
        string separatorSet,
        AsciiCharClass separatorCharClass,
        int separatorMinCount)
    {
        Kind = kind;
        _literalUtf8 = literalUtf8;
        _separatorSet = separatorSet;
        SeparatorCharClass = separatorCharClass;
        SeparatorMinCount = separatorMinCount;
    }

    public AsciiStructuralSuffixPartKind Kind { get; }

    public byte[] LiteralUtf8 => _literalUtf8 ?? [];

    public string SeparatorSet => _separatorSet ?? string.Empty;

    public AsciiCharClass SeparatorCharClass { get; }

    public int SeparatorMinCount { get; }

    public bool IsLiteral => Kind == AsciiStructuralSuffixPartKind.Literal;

    public bool IsSeparator => Kind == AsciiStructuralSuffixPartKind.Separator;

    public static AsciiStructuralCompiledSuffixPart CreateLiteral(byte[] literalUtf8) =>
        new(AsciiStructuralSuffixPartKind.Literal, literalUtf8, string.Empty, default, 0);

    public static AsciiStructuralCompiledSuffixPart CreateSeparator(
        string separatorSet,
        AsciiCharClass separatorCharClass,
        int separatorMinCount) =>
        new(AsciiStructuralSuffixPartKind.Separator, [], separatorSet, separatorCharClass, separatorMinCount);
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
    private AsciiStructuralLinearVerifierInstruction(
        AsciiStructuralLinearVerifierInstructionKind kind,
        string set,
        AsciiCharClass charClass,
        int minCount,
        int maxCount,
        AsciiStructuralCompiledSuffixPart[] suffixParts,
        Utf8BoundaryRequirement boundaryRequirement)
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

    public string Set { get; }

    public AsciiCharClass CharClass { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public AsciiStructuralCompiledSuffixPart[] SuffixParts { get; }

    public Utf8BoundaryRequirement BoundaryRequirement { get; }

    public static AsciiStructuralLinearVerifierInstruction ConsumeSetLoop(string set, AsciiCharClass charClass, int minCount) =>
        new(AsciiStructuralLinearVerifierInstructionKind.ConsumeSetLoop, set, charClass, minCount, int.MaxValue, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction RequireSetByte(string set, AsciiCharClass charClass) =>
        new(AsciiStructuralLinearVerifierInstructionKind.RequireSetByte, set, charClass, 0, int.MaxValue, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction ConsumeSetTail(string set, AsciiCharClass charClass, int minCount, int maxCount) =>
        new(AsciiStructuralLinearVerifierInstructionKind.ConsumeSetTail, set, charClass, minCount, maxCount, [], Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction MatchSuffixAtCurrent(AsciiStructuralCompiledSuffixPart[] suffixParts) =>
        new(AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAtCurrent, string.Empty, default, 0, int.MaxValue, suffixParts, Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction MatchSuffixAfterTail(AsciiStructuralCompiledSuffixPart[] suffixParts, int minCount) =>
        new(AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAfterTail, string.Empty, default, minCount, int.MaxValue, suffixParts, Utf8BoundaryRequirement.None);

    public static AsciiStructuralLinearVerifierInstruction RequireBoundary(Utf8BoundaryRequirement boundaryRequirement) =>
        new(AsciiStructuralLinearVerifierInstructionKind.RequireBoundary, string.Empty, default, 0, int.MaxValue, [], boundaryRequirement);

    public static AsciiStructuralLinearVerifierInstruction Accept() =>
        new(AsciiStructuralLinearVerifierInstructionKind.Accept, string.Empty, default, 0, int.MaxValue, [], Utf8BoundaryRequirement.None);
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
                : default;
            var compiledSuffixParts = CreateCompiledSuffixParts(step.SuffixParts);
            instructions[i] = step.Kind switch
            {
                AsciiStructuralVerifierStepKind.ConsumeSeparator
                    => AsciiStructuralLinearVerifierInstruction.ConsumeSetLoop(step.Set, charClass, step.MinCount),
                AsciiStructuralVerifierStepKind.RequireIdentifierStart
                    => AsciiStructuralLinearVerifierInstruction.RequireSetByte(step.Set, charClass),
                AsciiStructuralVerifierStepKind.ConsumeIdentifierTail
                    => AsciiStructuralLinearVerifierInstruction.ConsumeSetTail(step.Set, charClass, step.MinCount, step.MaxCount),
                AsciiStructuralVerifierStepKind.MatchSuffixAtCurrent
                    => AsciiStructuralLinearVerifierInstruction.MatchSuffixAtCurrent(compiledSuffixParts),
                AsciiStructuralVerifierStepKind.MatchSuffixAfterTail
                    => AsciiStructuralLinearVerifierInstruction.MatchSuffixAfterTail(compiledSuffixParts, step.MinCount),
                AsciiStructuralVerifierStepKind.RequireTrailingBoundary
                    => AsciiStructuralLinearVerifierInstruction.RequireBoundary(step.BoundaryRequirement),
                AsciiStructuralVerifierStepKind.Accept
                    => AsciiStructuralLinearVerifierInstruction.Accept(),
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
                    if (!TryConsumeSetLoop(input, ref index, instruction.Set, instruction.CharClass, instruction.MinCount))
                    {
                        return false;
                    }
                    break;

                case AsciiStructuralLinearVerifierInstructionKind.RequireSetByte:
                    if ((uint)index >= (uint)input.Length || !MatchesSet(input[index], instruction.Set, instruction.CharClass))
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
                           MatchesSet(input[index], instruction.Set, instruction.CharClass) &&
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

    private static bool TryConsumeSetLoop(ReadOnlySpan<byte> input, ref int index, string set, AsciiCharClass charClass, int minCount)
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
                if (!TryConsumeSetLoop(input, ref index, part.SeparatorSet, part.SeparatorCharClass, part.SeparatorMinCount))
                {
                    return false;
                }

                continue;
            }

            var literal = part.LiteralUtf8;
            if (literal.Length == 0 ||
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
        if (firstLiteral.Length == 0)
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

    private static bool MatchesSet(byte value, string runtimeSet, AsciiCharClass charClass)
    {
        return !charClass.IsEmpty
            ? charClass.Contains(value)
            : value < 128 && RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, runtimeSet);
    }

    private static AsciiStructuralCompiledSuffixPart[] CreateCompiledSuffixParts(AsciiStructuralSuffixPart[] suffixParts)
    {
        var compiled = new AsciiStructuralCompiledSuffixPart[suffixParts.Length];
        for (var i = 0; i < suffixParts.Length; i++)
        {
            var part = suffixParts[i];
            compiled[i] = part.IsLiteral
                ? AsciiStructuralCompiledSuffixPart.CreateLiteral(part.LiteralUtf8)
                : AsciiStructuralCompiledSuffixPart.CreateSeparator(
                    part.SeparatorSet,
                    TryCreateAsciiCharClass(part.SeparatorSet, out var charClass) ? charClass : default,
                    part.SeparatorMinCount);
        }

        return compiled;
    }

    private static bool TryCreateAsciiCharClass(string? runtimeSet, out AsciiCharClass charClass)
    {
        return FrontEnd.DotNetAsciiCharClassProjector.TryProjectWholeClass(runtimeSet, out charClass);
    }

    private static bool MatchesBoundaryRequirement(Utf8BoundaryRequirement requirement, ReadOnlySpan<byte> input, int byteOffset)
    {
        return requirement switch
        {
            Utf8BoundaryRequirement.None => true,
            Utf8BoundaryRequirement.Boundary => DotNetUtf8WordBoundary.IsBoundary(input, byteOffset),
            Utf8BoundaryRequirement.NonBoundary => !DotNetUtf8WordBoundary.IsBoundary(input, byteOffset),
            _ => false,
        };
    }
}
