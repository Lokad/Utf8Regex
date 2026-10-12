using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Partial-probe execution engines for <see cref="Utf8Pcre2Regex"/>.</summary>
public sealed partial class Utf8Pcre2Regex
{
    private ref struct Pcre2PartialProbeRunner
{
    private Utf8ValidatedInput _input;
    private Pcre2ResourceBudget _budget;
    private readonly Pcre2NameEntry[] _names;
    private readonly Pcre2BacktrackingProgram? _captureProgram;
    private int _originalStart;

    internal Pcre2PartialProbeRunner(Utf8ValidatedInput input, Pcre2CompiledProgram program,
        Pcre2NameEntry[] names, CancellationToken token)
    {
        _input = input;
        _budget = new Pcre2ResourceBudget(program.Request.DefaultLimits,
            Pcre2SliceDriver.CreateInvocation(program.Request, token), collectDiagnostics: false);
        _names = names;
        _captureProgram = program.Operations.Match switch
        {
            Pcre2BacktrackingDirectProgram native when native.Program.CaptureSlotCount > 1 => native.Program,
            Pcre2SliceDirectProgram slice when slice.Fallback.CaptureSlotCount > 1 => slice.Fallback,
            Pcre2SingleTokenRepeatDirectProgram repeat when repeat.Program.Fallback.CaptureSlotCount > 1 => repeat.Program.Fallback,
            _ => null,
        };
        _originalStart = 0;
    }

    internal Utf8Pcre2ProbeResult Probe(
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        Pcre2PartialProbeProgram program)
    {
        var input = _input.Bytes;
        _originalStart = startOffsetInBytes;
        try
        {
            _budget.CheckDeadline();
            _budget.ChargeWorkspaceStep();
            return program.Kind switch
            {
                Pcre2PartialProbeProgramKind.PartialSoftDotAllLiteral => ProbeViaPartialSoftDotAllLiteral(input, partialMode, startOffsetInBytes),
                Pcre2PartialProbeProgramKind.AbPlus => ProbeViaAbPlus(input, partialMode, startOffsetInBytes),
                Pcre2PartialProbeProgramKind.LiteralAlternation => ProbeViaLiteralAlternation(input, partialMode, startOffsetInBytes, program.Operands),
                Pcre2PartialProbeProgramKind.OrderedLiteralAlternation => ProbeViaOrderedLiteralAlternation(input, partialMode, startOffsetInBytes, program.Operands),
                Pcre2PartialProbeProgramKind.WordBoundaryLiteral => ProbeViaWordBoundaryLiteral(input, partialMode, startOffsetInBytes, program.Operands[0]),
                Pcre2PartialProbeProgramKind.InspectedPrefixLiteral => ProbeViaInspectedPrefixLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.Operands[1], program.Flag),
                Pcre2PartialProbeProgramKind.InspectedContextLiteral => ProbeViaInspectedContextLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.Operands[1], program.Operands[2], program.Flag),
                Pcre2PartialProbeProgramKind.EndAssertion => ProbeViaEndAssertion(input, partialMode, startOffsetInBytes, program.Flag),
                Pcre2PartialProbeProgramKind.TrailingCWithLookbehind => ProbeViaTrailingCWithLookbehind(input, partialMode, startOffsetInBytes, program.Flag),
                Pcre2PartialProbeProgramKind.NegativeStartClassDotStar => ProbeViaNegativeStartClassDotStar(input, partialMode, startOffsetInBytes),
                Pcre2PartialProbeProgramKind.AnchoredNewlineSequence => ProbeViaAnchoredNewlineSequence(input, partialMode, startOffsetInBytes, program.First, program.Second, program.Flag),
                Pcre2PartialProbeProgramKind.AnchoredOptionalNewlineThenX => ProbeViaAnchoredOptionalNewlineThenX(input, partialMode, startOffsetInBytes),
                Pcre2PartialProbeProgramKind.AnchoredAtomicAPlusB => ProbeViaAnchoredAtomicAPlusB(input, partialMode, startOffsetInBytes),
                Pcre2PartialProbeProgramKind.AnchoredRepeatedLiteral => ProbeViaAnchoredRepeatedLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.First),
                Pcre2PartialProbeProgramKind.AnchoredConditionalAbcDotStarOrZ => ProbeViaAnchoredConditionalAbcDotStarOrZ(input, partialMode, startOffsetInBytes),
                Pcre2PartialProbeProgramKind.AnchoredLiteralPlusTerminal => ProbeViaAnchoredLiteralPlusTerminal(input, partialMode, startOffsetInBytes, program.Operands[0], (byte)program.Terminal),
                Pcre2PartialProbeProgramKind.AnchoredAPlusWord => ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, program.Flag, false),
                Pcre2PartialProbeProgramKind.AnchoredAaOrAPlusWord => ProbeViaAnchoredAaOrAPlusWord(input, partialMode, startOffsetInBytes),
                Pcre2PartialProbeProgramKind.AnchoredACrlfEnd => ProbeViaAnchoredACrlfEnd(input, partialMode, startOffsetInBytes, program.Flag),
                Pcre2PartialProbeProgramKind.CrlfDotQuantifier => ProbeViaCrlfDotQuantifier(input, partialMode, startOffsetInBytes, program.First, program.Second),
                Pcre2PartialProbeProgramKind.TrailingLiteralAssertion => ProbeViaTrailingLiteralAssertion(input, partialMode, startOffsetInBytes, program.Operands[0], program.TrailingAssertion),
                Pcre2PartialProbeProgramKind.AnchoredPrefixLiteral => ProbeViaAnchoredPrefixLiteral(input, partialMode, startOffsetInBytes, program.Operands[0]),
                Pcre2PartialProbeProgramKind.AnchoredExactLiteral => ProbeViaAnchoredExactLiteral(input, partialMode, startOffsetInBytes, program.Operands[0]),
                _ => throw new InvalidOperationException("The compiled partial-probe program is not executable."),
            };
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    private Utf8Pcre2ProbeResult ProbeViaPartialSoftDotAllLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (partialMode == Pcre2PartialMode.None)
        {
            return remaining.Length > 0 && remaining[0] == (byte)'f'
                ? Full([Project(0, startOffsetInBytes, input.Length)])
                : NoMatch();
        }

        if (remaining.StartsWith("for"u8))
        {
            return Partial(Project(0, startOffsetInBytes, startOffsetInBytes + 3));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAbPlus(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        for (var i = 0; i < remaining.Length; i++)
        {
            _budget.ChargeWorkspaceStep();
            if (remaining[i] != (byte)'a')
            {
                continue;
            }

            var j = i + 1;
            while (j < remaining.Length && remaining[j] == (byte)'b')
            {
                _budget.ChargeWorkspaceStep();
                j++;
            }

            if (j > i + 1)
            {
                var matchStart = startOffsetInBytes + i;
                if (partialMode == Pcre2PartialMode.Hard && j == remaining.Length)
                {
                    return Partial(Project(0, matchStart, startOffsetInBytes + j));
                }

                return Full([Project(0, matchStart, startOffsetInBytes + j),
                    Project(1, matchStart, matchStart + 1)]);
            }

            if (partialMode != Pcre2PartialMode.None && i == remaining.Length - 1)
            {
                var partialStart = startOffsetInBytes + i;
                return Partial(Project(0, partialStart, partialStart + 1));
            }
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaLiteralAlternation(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[][] literalBytes)
    {
        var remaining = input[startOffsetInBytes..];
        var bestIndex = -1;
        byte[]? bestLiteral = null;
        foreach (var literal in literalBytes)
        {
            _budget.ChargeWorkspaceStep();
            var index = IndexOf(remaining, literal);
            if (index < 0)
            {
                continue;
            }

            if (bestIndex < 0 || index < bestIndex)
            {
                bestIndex = index;
                bestLiteral = literal;
            }
        }

        if (bestLiteral is not null)
        {
            var matchStart = startOffsetInBytes + bestIndex;
            return Full([Project(0, matchStart, matchStart + bestLiteral.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = 0;
            foreach (var literal in literalBytes)
            {
                _budget.ChargeWorkspaceStep();
                partialLength = Math.Max(partialLength, LongestTrailingPrefixLength(remaining, literal));
            }

            if (partialLength > 0)
            {
                return Partial(Project(0, input.Length - partialLength, input.Length));
            }
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaOrderedLiteralAlternation(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[][] literalBytes)
    {
        var remaining = input[startOffsetInBytes..];
        Pcre2GroupData? firstPartial = null;

        for (var start = 0; start < remaining.Length; start++)
        {
            _budget.ChargeWorkspaceStep();
            var candidate = remaining[start..];
            for (var i = 0; i < literalBytes.Length; i++)
            {
                _budget.ChargeWorkspaceStep();
                var literal = literalBytes[i];
                if (candidate.StartsWith(literal))
                {
                    var matchStart = startOffsetInBytes + start;
                    if (partialMode == Pcre2PartialMode.Hard &&
                        candidate.Length > literal.Length &&
                        HasLongerPartialContinuation(candidate, literalBytes, i + 1))
                    {
                        return Partial(Project(0, matchStart, input.Length));
                    }

                    return Full([Project(0, matchStart, matchStart + literal.Length)]);
                }

                if (partialMode == Pcre2PartialMode.None ||
                    candidate.Length == 0 ||
                    candidate.Length >= literal.Length ||
                    !literal.AsSpan(0, candidate.Length).SequenceEqual(candidate))
                {
                    continue;
                }

                var partial = Project(0, startOffsetInBytes + start, input.Length);
                if (partialMode == Pcre2PartialMode.Hard)
                {
                    return Partial(partial);
                }

                firstPartial ??= partial;
            }
        }

        return firstPartial is Pcre2GroupData partialMatch
            ? Partial(partialMatch)
            : NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaWordBoundaryLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8)
    {
        var remaining = input[startOffsetInBytes..];
        for (var start = 0; start < remaining.Length; start++)
        {
            _budget.ChargeWorkspaceStep();
            if (!HasAsciiWordBoundary(remaining, start))
            {
                continue;
            }

            var candidate = remaining[start..];
            if (candidate.StartsWith(literalUtf8))
            {
                var matchStart = startOffsetInBytes + start;
                var matchEnd = matchStart + literalUtf8.Length;
                if (matchEnd < input.Length)
                {
                    if (!HasAsciiWordBoundary(input, matchEnd))
                    {
                        continue;
                    }

                    return Full([Project(0, matchStart, matchEnd)]);
                }

                if (partialMode == Pcre2PartialMode.Hard)
                {
                    return Partial(Project(0, matchStart, matchEnd));
                }

                return Full([Project(0, matchStart, matchEnd)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                candidate.Length == 0 ||
                candidate.Length >= literalUtf8.Length ||
                !literalUtf8.AsSpan(0, candidate.Length).SequenceEqual(candidate))
            {
                continue;
            }

            return Partial(Project(0, startOffsetInBytes + start, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaInspectedPrefixLiteral(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[] prefixUtf8,
        byte[] literalUtf8,
        bool includePrefixInPartial)
    {
        var remaining = input[startOffsetInBytes..];
        for (var start = 0; start <= remaining.Length - prefixUtf8.Length; start++)
        {
            _budget.ChargeWorkspaceStep();
            if (!remaining[start..].StartsWith(prefixUtf8))
            {
                continue;
            }

            var suffix = remaining[(start + prefixUtf8.Length)..];
            if (suffix.StartsWith(literalUtf8))
            {
                var matchStart = startOffsetInBytes + start + prefixUtf8.Length;
                return Full([Project(0, matchStart, matchStart + literalUtf8.Length)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                suffix.Length == 0 ||
                suffix.Length >= literalUtf8.Length ||
                !literalUtf8.AsSpan(0, suffix.Length).SequenceEqual(suffix))
            {
                continue;
            }

            var partialStart = includePrefixInPartial
                ? startOffsetInBytes + start
                : startOffsetInBytes + start + prefixUtf8.Length;
            return Partial(Project(0, partialStart, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaInspectedContextLiteral(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[] prefixUtf8,
        byte[] literalUtf8,
        byte[] suffixUtf8,
        bool includePrefixInPartial)
    {
        var remaining = input[startOffsetInBytes..];
        var maxStart = remaining.Length - prefixUtf8.Length - literalUtf8.Length;
        for (var start = 0; start <= maxStart; start++)
        {
            _budget.ChargeWorkspaceStep();
            if (prefixUtf8.Length > 0 && !remaining[start..].StartsWith(prefixUtf8))
            {
                continue;
            }

            var matchRelativeStart = start + prefixUtf8.Length;
            if (!remaining[matchRelativeStart..].StartsWith(literalUtf8))
            {
                continue;
            }

            var suffixStart = matchRelativeStart + literalUtf8.Length;
            var suffix = remaining[suffixStart..];
            if (suffix.StartsWith(suffixUtf8))
            {
                var matchStart = startOffsetInBytes + matchRelativeStart;
                return Full([Project(0, matchStart, matchStart + literalUtf8.Length)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                suffix.Length >= suffixUtf8.Length ||
                !suffixUtf8.AsSpan(0, suffix.Length).SequenceEqual(suffix))
            {
                continue;
            }

            var partialStart = includePrefixInPartial
                ? startOffsetInBytes + start
                : startOffsetInBytes + matchRelativeStart;
            return Partial(Project(0, partialStart, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaEndAssertion(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool allowFinalNewline)
    {
        var matchIndex = input.Length;
        var partialStart = input.Length;
        if (allowFinalNewline && input.Length > startOffsetInBytes && input[^1] == (byte)'\n')
        {
            matchIndex = input.Length - 1;
            partialStart = input.Length - 1;
        }

        if (partialMode == Pcre2PartialMode.Hard)
        {
            return Partial(Project(0, partialStart, input.Length));
        }

        return Full([Project(0, matchIndex, matchIndex)]);
    }

    private Utf8Pcre2ProbeResult ProbeViaTrailingCWithLookbehind(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool requireAtLeastOneC)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return NoMatch();
        }

        var trailingCs = 0;
        for (var i = remaining.Length - 1; i >= 0 && remaining[i] == (byte)'c'; i--)
        {
            _budget.ChargeWorkspaceStep();
            trailingCs++;
        }

        var matchStart = input.Length - trailingCs;
        if (trailingCs > 0)
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Partial(Project(0, matchStart, input.Length));
            }

            return Full([Project(0, matchStart, input.Length)]);
        }

        if (remaining[^1] is (byte)'b' or (byte)'c')
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Partial(Project(0, input.Length, input.Length));
            }

            if (!requireAtLeastOneC)
            {
                return Full([Project(0, input.Length, input.Length)]);
            }
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaNegativeStartClassDotStar(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return partialMode == Pcre2PartialMode.Hard
                ? Partial(Project(0, startOffsetInBytes, startOffsetInBytes))
                : Full([Project(0, startOffsetInBytes, startOffsetInBytes)]);
        }

        if (remaining[0] is (byte)'a' or (byte)'b')
        {
            return partialMode == Pcre2PartialMode.Hard
                ? Partial(Project(0, startOffsetInBytes, startOffsetInBytes))
                : NoMatch();
        }

        return Full([Project(0, startOffsetInBytes, input.Length)]);
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredAtomicAPlusB(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0 || remaining[0] != (byte)'a')
        {
            return NoMatch();
        }

        var cursor = 0;
        while (cursor < remaining.Length && remaining[cursor] == (byte)'a')
        {
            _budget.ChargeWorkspaceStep();
            cursor++;
        }

        if (cursor < remaining.Length && remaining[cursor] == (byte)'b')
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            return Partial(Project(0, startOffsetInBytes, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredNewlineSequence(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, int minCount, int maxCount, bool requireTerminalX)
    {
        var remaining = input[startOffsetInBytes..];
        var cursor = 0;
        var count = 0;
        while (cursor < remaining.Length && count < maxCount && TryConsumePcre2Newline(remaining[cursor..], out var consumed))
        {
            _budget.ChargeWorkspaceStep();
            cursor += consumed;
            count++;
        }

        if (requireTerminalX)
        {
            if (count >= minCount && cursor < remaining.Length && remaining[cursor] == (byte)'x')
            {
                return Full([Project(0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
            }

            if (partialMode != Pcre2PartialMode.None && cursor == remaining.Length && count > 0 && count <= maxCount)
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }

            return NoMatch();
        }

        if (count >= minCount)
        {
            if (partialMode == Pcre2PartialMode.Hard && remaining.Length == cursor && remaining.Length > 0 && remaining[0] == (byte)'\r')
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }

            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + cursor)]);
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredOptionalNewlineThenX(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length > 0 && remaining[0] == (byte)'x')
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        if (TryConsumePcre2Newline(remaining, out var consumed))
        {
            if (remaining.Length == consumed && partialMode != Pcre2PartialMode.None)
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }

            if (remaining.Length > consumed && remaining[consumed] == (byte)'x')
            {
                return Full([Project(0, startOffsetInBytes, startOffsetInBytes + consumed + 1)]);
            }
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredRepeatedLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8, int repeatCount)
    {
        var remaining = input[startOffsetInBytes..];
        var requiredLength = literalUtf8.Length * repeatCount;
        var matched = 0;
        while (matched < remaining.Length && matched < requiredLength && remaining[matched] == literalUtf8[matched % literalUtf8.Length])
        {
            _budget.ChargeWorkspaceStep();
            matched++;
        }

        if (matched == requiredLength)
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + requiredLength)]);
        }

        if (partialMode != Pcre2PartialMode.None && matched > 0 && matched == remaining.Length)
        {
            return Partial(Project(0, startOffsetInBytes, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredConditionalAbcDotStarOrZ(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("abc"u8))
        {
            return Full([Project(0, startOffsetInBytes, input.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None && "abc"u8.StartsWith(remaining))
        {
            return Partial(Project(0, startOffsetInBytes, input.Length));
        }

        if (remaining.Length > 0 && remaining[0] == (byte)'Z')
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredLiteralPlusTerminal(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8, byte terminal)
    {
        var remaining = input[startOffsetInBytes..];
        var cursor = 0;
        while (cursor + literalUtf8.Length <= remaining.Length && remaining[cursor..].StartsWith(literalUtf8))
        {
            _budget.ChargeWorkspaceStep();
            cursor += literalUtf8.Length;
        }

        if (cursor == 0)
        {
            if (partialMode != Pcre2PartialMode.None && remaining.Length > 0 && literalUtf8.AsSpan(0, remaining.Length).SequenceEqual(remaining))
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }

            return NoMatch();
        }

        if (cursor < remaining.Length && remaining[cursor] == terminal)
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            if (cursor == remaining.Length)
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }

            var tail = remaining[cursor..];
            if (tail.Length > 0 && literalUtf8.AsSpan(0, tail.Length).SequenceEqual(tail))
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredAPlusWord(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool allowEmptyA, bool requireAtLeastTwoLeadingA)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return NoMatch();
        }

        var aCount = 0;
        while (aCount < remaining.Length && remaining[aCount] == (byte)'a')
        {
            _budget.ChargeWorkspaceStep();
            aCount++;
        }

        var minA = allowEmptyA ? 0 : 1;
        if (requireAtLeastTwoLeadingA)
        {
            minA = Math.Max(minA, 2);
        }

        if (aCount < minA)
        {
            if (allowEmptyA && IsAsciiWordByte(remaining[0]))
            {
                return Full([Project(0, startOffsetInBytes, startOffsetInBytes + 1)]);
            }

            return NoMatch();
        }

        if (aCount < remaining.Length && IsAsciiWordByte(remaining[aCount]))
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + aCount + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None && aCount == remaining.Length)
        {
            return Partial(Project(0, startOffsetInBytes, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredAaOrAPlusWord(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length >= 2 && remaining[0] == (byte)'a' && remaining[1] == (byte)'a')
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + 2)]);
        }

        return ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, allowEmptyA: false, requireAtLeastTwoLeadingA: false);
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredACrlfEnd(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool preferExplicitCrAlternative)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.SequenceEqual("a"u8))
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        if (remaining.SequenceEqual("a\r"u8))
        {
            if (preferExplicitCrAlternative && partialMode != Pcre2PartialMode.Hard)
            {
                return Full([Project(0, startOffsetInBytes, startOffsetInBytes + 2)]);
            }

            if (partialMode != Pcre2PartialMode.None)
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaCrlfDotQuantifier(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, int minCount, int maxCount)
    {
        var remaining = input[startOffsetInBytes..];
        var count = Math.Min(remaining.Length, maxCount);
        if (count < minCount)
        {
            if (partialMode != Pcre2PartialMode.None && count > 0)
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }

            return NoMatch();
        }

        if (partialMode == Pcre2PartialMode.Hard && count == remaining.Length)
        {
            return Partial(Project(0, startOffsetInBytes, input.Length));
        }

        return Full([Project(0, startOffsetInBytes, startOffsetInBytes + count)]);
    }

    private bool TryConsumePcre2Newline(ReadOnlySpan<byte> input, out int consumed)
    {
        if (input.Length >= 2 && input[0] == (byte)'\r' && input[1] == (byte)'\n')
        {
            consumed = 2;
            return true;
        }

        if (input.Length >= 1 && input[0] is (byte)'\r' or (byte)'\n')
        {
            consumed = 1;
            return true;
        }

        consumed = 0;
        return false;
    }

    private Utf8Pcre2ProbeResult ProbeViaTrailingLiteralAssertion(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[] literalUtf8,
        Pcre2PartialProbeTrailingAssertion assertion)
    {
        var remaining = input[startOffsetInBytes..];
        var index = IndexOf(remaining, literalUtf8);
        if (index < 0)
        {
            if (partialMode != Pcre2PartialMode.None &&
                assertion == Pcre2PartialProbeTrailingAssertion.Dollar)
            {
                var partialLength = LongestTrailingSuffixLength(remaining, literalUtf8);
                if (partialLength > 0)
                {
                    return Partial(Project(0, input.Length - partialLength, input.Length));
                }
            }

            return NoMatch();
        }

        var matchStart = startOffsetInBytes + index;
        var matchEnd = matchStart + literalUtf8.Length;
        var atEnd = matchEnd == input.Length;
        var hasFinalNewline = matchEnd + 1 == input.Length && input[matchEnd] == (byte)'\n';
        var rightIsWord = matchEnd < input.Length && IsAsciiWordByte(input[matchEnd]);

        var full = assertion switch
        {
            Pcre2PartialProbeTrailingAssertion.Dollar => atEnd || hasFinalNewline,
            Pcre2PartialProbeTrailingAssertion.EndAbsolute => atEnd,
            Pcre2PartialProbeTrailingAssertion.EndBeforeFinalNewline => atEnd || hasFinalNewline,
            Pcre2PartialProbeTrailingAssertion.WordBoundary => !rightIsWord,
            Pcre2PartialProbeTrailingAssertion.NonWordBoundary => rightIsWord,
            _ => false,
        };

        if (full)
        {
            if (partialMode == Pcre2PartialMode.Hard &&
                assertion is Pcre2PartialProbeTrailingAssertion.Dollar or Pcre2PartialProbeTrailingAssertion.EndAbsolute or Pcre2PartialProbeTrailingAssertion.EndBeforeFinalNewline or Pcre2PartialProbeTrailingAssertion.WordBoundary or Pcre2PartialProbeTrailingAssertion.NonWordBoundary &&
                atEnd)
            {
                return Partial(Project(0, matchStart, matchEnd));
            }

            return Full([Project(0, matchStart, matchEnd)]);
        }

        if (partialMode != Pcre2PartialMode.None &&
            assertion is Pcre2PartialProbeTrailingAssertion.WordBoundary or Pcre2PartialProbeTrailingAssertion.NonWordBoundary &&
            atEnd)
        {
            return Partial(Project(0, matchStart, matchEnd));
        }

        return NoMatch();
    }

    private bool HasAsciiWordBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var leftIsWord = byteOffset > 0 && IsAsciiWordByte(input[byteOffset - 1]);
        var rightIsWord = byteOffset < input.Length && IsAsciiWordByte(input[byteOffset]);
        return leftIsWord != rightIsWord;
    }

    private bool IsAsciiWordByte(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'a' and <= (byte)'z' or
            >= (byte)'0' and <= (byte)'9' or
            (byte)'_';
    }

    private bool HasLongerPartialContinuation(ReadOnlySpan<byte> candidate, byte[][] literalBytes, int startLiteralIndex)
    {
        for (var i = startLiteralIndex; i < literalBytes.Length; i++)
        {
            _budget.ChargeWorkspaceStep();
            var literal = literalBytes[i];
            if (candidate.Length < literal.Length && literal.AsSpan(0, candidate.Length).SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredExactLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.SequenceEqual(literalUtf8))
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Partial(Project(0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length));
            }

            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = LongestLeadingExactPrefixLength(remaining, literalUtf8);
            if (partialLength > 0)
            {
                return Partial(Project(0, startOffsetInBytes, startOffsetInBytes + partialLength));
            }
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredPrefixLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith(literalUtf8))
        {
            return Full([Project(0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = LongestLeadingExactPrefixLength(remaining, literalUtf8);
            if (partialLength > 0)
            {
                return Partial(Project(0, startOffsetInBytes, startOffsetInBytes + partialLength));
            }
        }

        return NoMatch();
    }

    private int LongestLeadingExactPrefixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        if (subject.Length == 0 || subject.Length >= literal.Length)
        {
            return 0;
        }

        return literal[..subject.Length].SequenceEqual(subject) ? subject.Length : 0;
    }

    private int LongestTrailingSuffixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        var maxLength = Math.Min(subject.Length, literal.Length - 1);
        for (var length = maxLength; length >= 1; length--)
        {
            _budget.ChargeWorkspaceStep();
            if (subject[^length..].SequenceEqual(literal[(literal.Length - length)..]))
            {
                return length;
            }
        }

        return 0;
    }

    private int LongestTrailingPrefixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        var maxLength = Math.Min(subject.Length, literal.Length - 1);
        for (var length = maxLength; length >= 1; length--)
        {
            _budget.ChargeWorkspaceStep();
            if (subject[^length..].SequenceEqual(literal[..length]))
            {
                return length;
            }
        }

        return 0;
    }

    private Pcre2GroupData Project(int number, int start, int end)
    {
        var cursor = _input.CreateProjectionCursor();
        var group = Pcre2GlobalCursorProjection.Project(start, end, ref _input, ref cursor, ref _budget);
        return group with { Number = number };
    }

    private Utf8Pcre2ProbeResult Full(scoped ReadOnlySpan<Pcre2GroupData> groups)
    {
        if (_captureProgram is { } captureProgram && groups.Length < captureProgram.CaptureSlotCount)
        {
            // Partial selection has already chosen this full range. Reuse the
            // admitted native program at that start to obtain final captures,
            // preserving the original \G position and invocation allowance.
            var value = groups[0];
            var detailed = Pcre2BacktrackingRunner.MatchDetailed(captureProgram, default, ref _input,
                new Utf8BytePosition(value.StartOffsetInBytes), new Utf8BytePosition(_originalStart),
                Pcre2MatchOptions.Anchored, ref _budget);
            try
            {
                if (!detailed.Success || detailed.StartOffsetInBytes != value.StartOffsetInBytes ||
                    detailed.EndOffsetInBytes != value.EndOffsetInBytes)
                    throw new InvalidOperationException("The full probe range disagrees with its admitted capture program.");
                _budget.CheckDeadline();
                return Utf8Pcre2ProbeResult.CreateFullMatch(_input.Bytes,
                    detailed.CaptureResult.ProjectedGroups, _names, detailed.Mark);
            }
            finally { detailed.CaptureResult.DetachProjectedWorking(ref _budget); }
        }
        var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(groups.Length);
        Pcre2SliceDriver.ThrowIfSliceBreached(_budget.WorkingInvocation.Budget.TryChargeResultBytes(bytes));
        _budget.ReserveWorking(bytes);
        try
        {
            var owned = groups.ToArray();
            _budget.CheckDeadline();
            return Utf8Pcre2ProbeResult.CreateFullMatch(_input.Bytes, owned, _names);
        }
        finally { _budget.ReleaseWorking(bytes); }
    }

    private Utf8Pcre2ProbeResult Partial(Pcre2GroupData group)
    {
        _budget.CheckDeadline();
        return Utf8Pcre2ProbeResult.CreatePartial(_input.Bytes, group);
    }

    private Utf8Pcre2ProbeResult NoMatch()
    {
        _budget.CheckDeadline();
        return Utf8Pcre2ProbeResult.CreateNoMatch(_input.Bytes);
    }

    private int IndexOf(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        if (_budget.IsUnmetered) return subject.IndexOf(literal);
        _budget.ChargeWorkspaceStep();
        if (literal.IsEmpty) return 0;
        var last = subject.Length - literal.Length;
        for (var position = 0; position <= last;)
        {
            _budget.CheckDeadline();
            _budget.ChargeWorkspaceStep();
            var candidates = Math.Min(4096, last - position + 1);
            // Keep Span.IndexOf's SIMD kernel. Overlap the final literal tail
            // so a hit across a controlled chunk boundary cannot be missed.
            var index = subject.Slice(position, candidates + literal.Length - 1).IndexOf(literal);
            if (index >= 0) return position + index;
            position += candidates;
        }
        return -1;
    }

    }
}
