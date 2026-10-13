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
    private readonly bool _anchored;
    private readonly Pcre2MatchOptions _matchOptions;
    private readonly Pcre2CompiledProgram _compiled;
    private readonly bool _firstLine;
    private int _originalStart;
    private int _candidateScan;
    private int _candidateLimit;

    internal Pcre2PartialProbeRunner(Utf8ValidatedInput input, Pcre2CompiledProgram program,
        Pcre2NameEntry[] names, Pcre2MatchOptions matchOptions, Pcre2ResourceBudget budget)
    {
        _input = input;
        // Transfer the validation authority into this one-shot runner. Controlled
        // counters share the original owner; unlimited state stays inline.
        _budget = budget;
        _names = names;
        _matchOptions = matchOptions;
        _compiled = program;
        _anchored = (matchOptions & Pcre2MatchOptions.Anchored) != 0 ||
            (program.Request.Options & Pcre2CompileOptions.Anchored) != 0;
        _firstLine = !_anchored && (program.Request.Options & Pcre2CompileOptions.FirstLine) != 0;
        _captureProgram = program.Operations.Match switch
        {
            Pcre2BacktrackingDirectProgram native when native.Program.CaptureSlotCount > 1 => native.Program,
            Pcre2SliceDirectProgram slice when slice.Fallback.CaptureSlotCount > 1 => slice.Fallback,
            Pcre2SingleTokenRepeatDirectProgram repeat when repeat.Program.Fallback.CaptureSlotCount > 1 => repeat.Program.Fallback,
            _ => null,
        };
        _originalStart = 0;
        _candidateScan = 0;
        _candidateLimit = input.Bytes.Length;
    }

    internal Utf8Pcre2ProbeResult Probe(
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        Pcre2CompiledProgram compiled, Pcre2MatchOptions matchOptions)
    {
        var program = compiled.PartialProbe;
        var input = _input.Bytes;
        _originalStart = startOffsetInBytes;
        _candidateScan = startOffsetInBytes;
        try
        {
            _budget.CheckDeadline();
            _budget.ChargeWorkspaceStep();
            while (CanStartCandidate(startOffsetInBytes))
            {
                var accepts = LeadingAssertionAccepts(compiled, input, startOffsetInBytes, matchOptions, out var canRetry);
                canRetry |= program.Kind is Pcre2PartialProbeProgramKind.AtomicAPlusB or
                    Pcre2PartialProbeProgramKind.RepeatedLiteral or Pcre2PartialProbeProgramKind.ConditionalAbcDotStarOrZ or
                    Pcre2PartialProbeProgramKind.LiteralPlusTerminal;
                if (accepts)
                {
                    var result = program.Kind switch
                    {
                        Pcre2PartialProbeProgramKind.LiteralDotStar => ProbeViaLiteralDotStar(input, partialMode, startOffsetInBytes, program.Operands[0], program.Flag, compiled.Request),
                        Pcre2PartialProbeProgramKind.AbPlus => ProbeViaAbPlus(input, partialMode, startOffsetInBytes),
                        Pcre2PartialProbeProgramKind.LiteralAlternation => ProbeViaLiteralAlternation(input, partialMode, startOffsetInBytes, program.Operands),
                        Pcre2PartialProbeProgramKind.OrderedLiteralAlternation => ProbeViaOrderedLiteralAlternation(input, partialMode, startOffsetInBytes, program.Operands),
                        Pcre2PartialProbeProgramKind.WordBoundaryLiteral or Pcre2PartialProbeProgramKind.TrailingLiteralAssertion => ProbeViaLiteralAssertions(input, partialMode, startOffsetInBytes, program.Operands[0], compiled),
                        Pcre2PartialProbeProgramKind.InspectedPrefixLiteral => ProbeViaInspectedLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.Operands[1], ReadOnlySpan<byte>.Empty, program.Flag),
                        Pcre2PartialProbeProgramKind.InspectedContextLiteral => ProbeViaInspectedLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.Operands[1], program.Operands[2], false),
                        Pcre2PartialProbeProgramKind.EndAssertion => ProbeViaEndAssertion(input, partialMode, startOffsetInBytes, program.Flag),
                        Pcre2PartialProbeProgramKind.TrailingCWithLookbehind => ProbeViaTrailingCWithLookbehind(input, partialMode, startOffsetInBytes, program.Flag),
                        Pcre2PartialProbeProgramKind.NegativeStartClassDotStar => ProbeViaNegativeStartClassDotStar(input, partialMode, startOffsetInBytes, compiled.Request),
                        Pcre2PartialProbeProgramKind.AnchoredNewlineSequence => ProbeViaAnchoredNewlineSequence(input, partialMode, startOffsetInBytes, program.First, program.Second, program.Flag, compiled.Request),
                        Pcre2PartialProbeProgramKind.AtomicAPlusB => ProbeViaAtomicAPlusB(input, partialMode, startOffsetInBytes),
                        Pcre2PartialProbeProgramKind.RepeatedLiteral => ProbeViaRepeatedLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.First),
                        Pcre2PartialProbeProgramKind.ConditionalAbcDotStarOrZ => ProbeViaConditionalAbcDotStarOrZ(input, partialMode, startOffsetInBytes, compiled.Request),
                        Pcre2PartialProbeProgramKind.LiteralPlusTerminal => ProbeViaLiteralPlusTerminal(input, partialMode, startOffsetInBytes, program.Operands[0], (byte)program.Terminal),
                        Pcre2PartialProbeProgramKind.AnchoredAPlusWord or Pcre2PartialProbeProgramKind.AnchoredAaOrAPlusWord => ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, compiled),
                        Pcre2PartialProbeProgramKind.CapturedEndAlternative => ProbeViaCapturedEndAlternative(input, partialMode, startOffsetInBytes, compiled),
                        Pcre2PartialProbeProgramKind.CrlfDotQuantifier => ProbeViaCrlfDotQuantifier(input, partialMode, startOffsetInBytes, program.First, program.Second, program.Flag, compiled),
                        Pcre2PartialProbeProgramKind.AnchoredPrefixLiteral => ProbeViaAnchoredPrefixLiteral(input, partialMode, startOffsetInBytes, program.Operands[0]),
                        Pcre2PartialProbeProgramKind.AnchoredExactLiteral => ProbeViaAnchoredExactLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], compiled),
                        _ => throw new InvalidOperationException("The compiled partial-probe program is not executable."),
                    };
                    if (result.Kind != Utf8Pcre2ProbeKind.NoMatch || _anchored || !canRetry) return result;
                }
                // An intrinsic multiline anchor can admit later candidates even
                // when an earlier line's body failed. Keep the original subject
                // and first-position authority for every attempt.
                if (_anchored || !canRetry || !Pcre2CharacterRunner.TryAdvanceCandidate(compiled.Request,
                    null, ref _input, startOffsetInBytes, allowAcceleration: false, out var next))
                    return NoMatch();
                _budget.ChargeWorkspaceStep();
                startOffsetInBytes = next;
            }
            return NoMatch();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    private bool LeadingAssertionAccepts(Pcre2CompiledProgram compiled, ReadOnlySpan<byte> input,
        int start, Pcre2MatchOptions matchOptions, out bool canRetry)
    {
        canRetry = false;
        // Use admitted tokens and the existing whole-subject assertion semantics;
        // the requested start is not a new beginning of subject or line.
        Pcre2CharacterToken? first = compiled.Operations.Match is Pcre2CharacterDirectProgram character && character.Program.Tokens.Length > 0
            ? character.Program.Tokens[0]
            : compiled.SyntaxTree is Pcre2BacktrackingSyntaxTree { Root: Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence } &&
              sequence.Children[0] is Pcre2TokenBacktrackingNode token ? token.Token : null;
        if (first is not { Kind: Pcre2CharacterTokenKind.BeginningOfLine or Pcre2CharacterTokenKind.BeginningOfSubject or Pcre2CharacterTokenKind.FirstMatchingPosition } assertion)
            return true;
        canRetry = assertion.Kind == Pcre2CharacterTokenKind.BeginningOfLine &&
            (assertion.Options & Pcre2CharacterOptions.Multiline) != 0;
        _budget.ChargeWorkspaceStep();
        return Pcre2CharacterRunner.TryMatchToken(assertion, compiled.Request, input, start, _originalStart, matchOptions, out _);
    }

    private Utf8Pcre2ProbeResult ProbeViaLiteralDotStar(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, byte[] literal, bool dotAll, Pcre2CompileRequest request)
    {
        var index = IndexOf(input[startOffsetInBytes..], literal);
        if (index < 0 || !CanStartCandidate(startOffsetInBytes + index)) return NoMatch();

        var start = startOffsetInBytes + index;
        var end = start + literal.Length;
        var dot = Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.Any,
            dotAll ? Pcre2CharacterOptions.DotAll : Pcre2CharacterOptions.None, 0, 0);
        return ProbeDotStar(input, partialMode, start, end, dot, request);
    }

    private Utf8Pcre2ProbeResult ProbeDotStar(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int start, int end, Pcre2CharacterToken dot, Pcre2CompileRequest request)
    {
        // Reuse the admitted whole-subject dot semantics. Scalar widths and the
        // selected newline convention determine the endpoint, not byte count.
        while (Pcre2CharacterRunner.TryMatchToken(dot, request, input, end, _originalStart, _matchOptions, out var next))
        {
            _budget.ChargeWorkspaceStep();
            end = next;
        }
        // Hard partial asks whether another scalar could extend the greedy
        // repeat. Soft partial accepts the complete match already available.
        return partialMode == Pcre2PartialMode.Hard && end == input.Length
            ? Partial(Project(0, start, end))
            : Full([Project(0, start, end)]);
    }

    private Utf8Pcre2ProbeResult ProbeViaAbPlus(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        for (var i = 0; i < remaining.Length && (!_anchored || i == 0) && CanStartCandidate(startOffsetInBytes + i); i++)
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

        // Choose the earliest alternative before checking its line. A later
        // hit in an earlier-listed alternative must not force a whole-line
        // scan before a successful candidate at the requested start.
        if (bestLiteral is not null && CanStartCandidate(startOffsetInBytes + bestIndex))
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

            if (partialLength > 0 && (!_anchored || partialLength == remaining.Length) && CanStartCandidate(input.Length - partialLength))
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

        for (var start = 0; start < remaining.Length && (!_anchored || start == 0) && CanStartCandidate(startOffsetInBytes + start); start++)
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

    private Utf8Pcre2ProbeResult ProbeViaInspectedLiteral(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        ReadOnlySpan<byte> prefixUtf8,
        ReadOnlySpan<byte> literalUtf8,
        ReadOnlySpan<byte> suffixUtf8,
        bool consumesPrefix)
    {
        var first = consumesPrefix ? startOffsetInBytes : Math.Max(0, startOffsetInBytes - prefixUtf8.Length);
        var last = consumesPrefix || prefixUtf8.IsEmpty ? input.Length - 1 : input.Length - prefixUtf8.Length;
        for (var start = first; start <= last; start++)
        {
            _budget.ChargeWorkspaceStep();
            var consumedStart = consumesPrefix ? start : start + prefixUtf8.Length;
            if (_anchored && consumedStart != startOffsetInBytes || !CanStartCandidate(consumedStart)) break;
            if (!input[start..].StartsWith(prefixUtf8))
            {
                // Only a consumed prefix can be an incomplete EOF match.
                // Borrowed lookbehind context must already be present.
                if (consumesPrefix && partialMode != Pcre2PartialMode.None &&
                    LongestLeadingExactPrefixLength(input[start..], prefixUtf8) > 0)
                    return Partial(Project(0, start, input.Length));
                continue;
            }

            var bodyStart = start + prefixUtf8.Length;
            var body = input[bodyStart..];
            if (!body.StartsWith(literalUtf8))
            {
                // Native lookbehind context permits an unanchored empty EOF
                // attempt, while explicit anchors retain their initial cutoff.
                var emptyBody = body.IsEmpty && prefixUtf8.Length > 0 && (consumesPrefix || !_anchored);
                if (partialMode != Pcre2PartialMode.None &&
                    (emptyBody || LongestLeadingExactPrefixLength(body, literalUtf8) > 0))
                    return Partial(Project(0, consumesPrefix ? start : bodyStart, input.Length));
                continue;
            }

            var suffixStart = bodyStart + literalUtf8.Length;
            var suffix = input[suffixStart..];
            if (suffix.StartsWith(suffixUtf8))
                return Full([Project(0, bodyStart, suffixStart)]);

            if (partialMode == Pcre2PartialMode.None ||
                (!suffix.IsEmpty && LongestLeadingExactPrefixLength(suffix, suffixUtf8) == 0))
                continue;

            // Lookbehind precedes group zero; only K includes its consumed
            // prefix in a partial. Lookahead extends the inspected endpoint.
            return Partial(Project(0, consumesPrefix ? start : bodyStart, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaEndAssertion(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool allowFinalNewline)
    {
        _budget.ChargeWorkspaceStep();
        var newline = _compiled.Request.Settings.Newline;
        var matchIndex = allowFinalNewline
            ? Pcre2CharacterSemantics.GetFirstFinalNewlineOrEnd(input, startOffsetInBytes, newline)
            : input.Length;
        var partialStart = matchIndex;
        // A final CR is not yet a CRLF newline. Hard partial can stop there;
        // soft matching may still accept the complete EOF alternative.
        if (allowFinalNewline && newline == Pcre2NewlineConvention.Crlf &&
            input.Length > startOffsetInBytes && input[^1] == (byte)'\r')
            partialStart = input.Length - 1;
        if (_anchored)
        {
            matchIndex = startOffsetInBytes == input.Length || startOffsetInBytes == matchIndex
                ? startOffsetInBytes : -1;
            partialStart = startOffsetInBytes == input.Length || startOffsetInBytes == partialStart
                ? startOffsetInBytes : -1;
        }

        if (partialMode == Pcre2PartialMode.Hard && partialStart >= 0 && CanStartCandidate(partialStart))
        {
            return Partial(Project(0, partialStart, input.Length));
        }
        if (matchIndex >= 0 && AllowsEmptyFull(matchIndex) && CanStartCandidate(matchIndex))
            return Full([Project(0, matchIndex, matchIndex)]);
        // Soft matching may remember a partial then accept a later empty full.
        // Explicit anchoring and FirstLine retain their candidate restrictions.
        if (!_anchored && matchIndex >= 0 && matchIndex < input.Length &&
            AllowsEmptyFull(input.Length) && CanStartCandidate(input.Length))
            return Full([Project(0, input.Length, input.Length)]);
        if (partialMode != Pcre2PartialMode.None && partialStart >= 0 && CanStartCandidate(partialStart))
            return Partial(Project(0, partialStart, input.Length));
        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaTrailingCWithLookbehind(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool requireAtLeastOneC)
    {
        var candidate = startOffsetInBytes;
        while (CanStartCandidate(candidate))
        {
            _budget.ChargeWorkspaceStep();
            var end = candidate;
            while (end < input.Length && input[end] == (byte)'c')
            {
                _budget.ChargeWorkspaceStep();
                end++;
            }
            var hasMinimum = !requireAtLeastOneC || end > candidate;
            if (end == input.Length && (hasMinimum || (!_anchored && candidate != _originalStart)))
            {
                if (partialMode == Pcre2PartialMode.Hard || end == candidate &&
                    (!hasMinimum || end == 0 || input[end - 1] is not ((byte)'b' or (byte)'c') || !AllowsEmptyFull(candidate)))
                    return Partial(Project(0, candidate, end));
            }
            if (hasMinimum && end > 0 && input[end - 1] is (byte)'b' or (byte)'c' &&
                (end > candidate || AllowsEmptyFull(candidate)))
                return Full([Project(0, candidate, end)]);
            if (_anchored || !_input.TryAdvanceScalar(new Utf8BytePosition(candidate), out var next)) return NoMatch();
            candidate = next.Value;
        }
        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaNegativeStartClassDotStar(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, Pcre2CompileRequest request)
    {
        var dot = Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.Any, Pcre2CharacterOptions.None, 0, 0);
        var candidate = startOffsetInBytes;
        while (CanStartCandidate(candidate))
        {
            _budget.ChargeWorkspaceStep();
            if (candidate == input.Length || input[candidate] is not ((byte)'a' or (byte)'b'))
            {
                var end = candidate;
                while (Pcre2CharacterRunner.TryMatchToken(dot, request, input, end, _originalStart, _matchOptions, out var nextEnd))
                {
                    _budget.ChargeWorkspaceStep();
                    end = nextEnd;
                }
                if (end == input.Length && (partialMode == Pcre2PartialMode.Hard ||
                    end == candidate && !AllowsEmptyFull(candidate)))
                    return Partial(Project(0, candidate, end));
                if (end > candidate || AllowsEmptyFull(candidate)) return Full([Project(0, candidate, end)]);
            }
            if (_anchored || !_input.TryAdvanceScalar(new Utf8BytePosition(candidate), out var next)) return NoMatch();
            candidate = next.Value;
        }
        return NoMatch();
    }

    private bool AllowsEmptyFull(int candidate) =>
        (_matchOptions & Pcre2MatchOptions.NotEmpty) == 0 &&
        ((_matchOptions & Pcre2MatchOptions.NotEmptyAtStart) == 0 || candidate != _originalStart);

    private Utf8Pcre2ProbeResult ProbeViaAtomicAPlusB(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
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

        if (partialMode != Pcre2PartialMode.None && cursor == remaining.Length)
        {
            return Partial(Project(0, startOffsetInBytes, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredNewlineSequence(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, int minCount, int maxCount, bool requireTerminalX, Pcre2CompileRequest request)
    {
        var cursor = startOffsetInBytes;
        var count = 0;
        var newline = Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.NewlineSequence, Pcre2CharacterOptions.None, 0, 0);
        while (count < maxCount && Pcre2CharacterRunner.TryMatchToken(newline, request, input,
            cursor, _originalStart, _matchOptions, out var next))
        {
            _budget.ChargeWorkspaceStep();
            var loneFinalCr = next == input.Length && next == cursor + 1 && input[cursor] == (byte)'\r';
            cursor = next;
            count++;
            // CRLF is one atomic newline; a lone final CR can still grow into
            // that atom, so hard partial keeps this uncertainty explicitly.
            if (partialMode == Pcre2PartialMode.Hard && loneFinalCr)
                return Partial(Project(0, startOffsetInBytes, cursor));
        }

        if (requireTerminalX)
        {
            if (count >= minCount && cursor < input.Length && input[cursor] == (byte)'x')
            {
                return Full([Project(0, startOffsetInBytes, cursor + 1)]);
            }

            if (partialMode != Pcre2PartialMode.None && cursor == input.Length && count > 0)
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }

            return NoMatch();
        }

        if (count >= minCount)
        {
            return Full([Project(0, startOffsetInBytes, cursor)]);
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaRepeatedLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8, int repeatCount)
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

    private Utf8Pcre2ProbeResult ProbeViaConditionalAbcDotStarOrZ(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, Pcre2CompileRequest request)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("abc"u8))
        {
            var dot = Pcre2CharacterToken.Create(Pcre2CharacterTokenKind.Any, Pcre2CharacterOptions.None, 0, 0);
            return ProbeDotStar(input, partialMode, startOffsetInBytes, startOffsetInBytes, dot, request);
        }

        // The conditional's dot-star branch can be empty; native partial
        // admission therefore includes an empty lookahead prefix at EOF.
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

    private Utf8Pcre2ProbeResult ProbeViaLiteralPlusTerminal(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8, byte terminal)
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
            if (partialMode != Pcre2PartialMode.None && remaining.Length > 0 &&
                literalUtf8.AsSpan().StartsWith(remaining))
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
            if (tail.Length > 0 && literalUtf8.AsSpan().StartsWith(tail))
            {
                return Partial(Project(0, startOffsetInBytes, input.Length));
            }
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredAPlusWord(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, Pcre2CompiledProgram compiled)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return NoMatch();
        }

        var sequence = ((Pcre2SequenceBacktrackingNode)((Pcre2BacktrackingSyntaxTree)compiled.SyntaxTree).Root).Children;
        if (compiled.PartialProbe.Kind == Pcre2PartialProbeProgramKind.AnchoredAaOrAPlusWord)
        {
            // The first alternative is complete without inspecting a word
            // scalar or EOF. Preserve its priority in both partial modes.
            if (remaining.StartsWith("aa"u8))
                return Full([Project(0, startOffsetInBytes, startOffsetInBytes + 2)]);
            sequence = ((Pcre2SequenceBacktrackingNode)((Pcre2AlternationBacktrackingNode)sequence[1]).Alternatives[1]).Children;
        }
        var word = ((Pcre2TokenBacktrackingNode)sequence[^1]).Token;

        var aCount = 0;
        while (aCount < remaining.Length && remaining[aCount] == (byte)'a')
        {
            _budget.ChargeWorkspaceStep();
            aCount++;
        }

        if (aCount == 0 && !compiled.PartialProbe.Flag)
            return NoMatch();

        // The admitted class owns UCP membership and complete scalar width.
        // ASCII byte classification would falsely reject Unicode word scalars.
        _budget.ChargeWorkspaceStep();
        if (Pcre2CharacterRunner.TryMatchToken(word, compiled.Request, input,
            startOffsetInBytes + aCount, _originalStart, _matchOptions, out var end))
        {
            return Full([Project(0, startOffsetInBytes, end)]);
        }

        if (partialMode != Pcre2PartialMode.None && aCount == remaining.Length)
        {
            return Partial(Project(0, startOffsetInBytes, input.Length));
        }

        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaCapturedEndAlternative(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, Pcre2CompiledProgram compiled)
    {
        var sequence = (Pcre2SequenceBacktrackingNode)((Pcre2BacktrackingSyntaxTree)compiled.SyntaxTree).Root;
        var branches = ((Pcre2AlternationBacktrackingNode)((Pcre2CaptureBacktrackingNode)sequence.Children[1]).Body).Alternatives;
        var first = ((Pcre2SequenceBacktrackingNode)branches[0]).Children;
        var prefix = ((Pcre2TokenBacktrackingNode)first[0]).Token;
        var assertion = ((Pcre2TokenBacktrackingNode)first[1]).Token;

        // Both admitted branches share one leading scalar. Keep the whole
        // subject for their suffixes; later text is not part of the match.
        _budget.ChargeWorkspaceStep();
        if (!Pcre2CharacterRunner.TryMatchToken(prefix, compiled.Request, input,
            startOffsetInBytes, _originalStart, _matchOptions, out var cursor))
            return NoMatch();

        var result = ProbeTrailingAssertion(input, partialMode, startOffsetInBytes, cursor, assertion, compiled.Request);
        if (result.Kind == Utf8Pcre2ProbeKind.FullMatch ||
            partialMode == Pcre2PartialMode.Hard && result.Kind == Utf8Pcre2ProbeKind.PartialMatch)
            return result;

        // Soft partial may retry a later complete alternative. Hard partial
        // keeps the first branch's inspected EOF extent before trying it.
        var terminal = ((Pcre2TokenBacktrackingNode)((Pcre2SequenceBacktrackingNode)branches[1]).Children[1]).Token;
        _budget.ChargeWorkspaceStep();
        if (Pcre2CharacterRunner.TryMatchToken(terminal, compiled.Request, input,
            cursor, _originalStart, _matchOptions, out var end))
            return Full([Project(0, startOffsetInBytes, end)]);
        if (partialMode != Pcre2PartialMode.None && cursor == input.Length)
            return Partial(Project(0, startOffsetInBytes, cursor));
        return result;
    }

    private Utf8Pcre2ProbeResult ProbeViaCrlfDotQuantifier(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, int minCount, int maxCount, bool lazy, Pcre2CompiledProgram compiled)
    {
        var dot = compiled.Operations.Match is Pcre2CharacterDirectProgram character
            ? character.Program.Tokens[0]
            : ((Pcre2TokenBacktrackingNode)((Pcre2RepeatBacktrackingNode)
                ((Pcre2BacktrackingSyntaxTree)compiled.SyntaxTree).Root).Body).Token;
        var excludesNewline = dot.Kind == Pcre2CharacterTokenKind.AnyNotNewline ||
            (dot.Options & Pcre2CharacterOptions.DotAll) == 0;
        var candidate = startOffsetInBytes;
        while (candidate < input.Length && CanStartCandidate(candidate))
        {
            _budget.ChargeWorkspaceStep();
            var end = candidate;
            var count = 0;
            while (count < maxCount && Pcre2CharacterRunner.TryMatchToken(dot, compiled.Request,
                input, end, _originalStart, _matchOptions, out var next))
            {
                _budget.ChargeWorkspaceStep();
                end = next;
                count++;
                // A lone final CR can become a CRLF newline. Hard partial must
                // preserve that uncertainty even for a fixed/maxed repeat.
                if (partialMode == Pcre2PartialMode.Hard && excludesNewline &&
                    end == input.Length && input[end - 1] == (byte)'\r')
                    return Partial(Project(0, candidate, end));
                if (lazy && count >= minCount) return Full([Project(0, candidate, end)]);
            }
            if (count >= minCount)
            {
                if (partialMode == Pcre2PartialMode.Hard && count < maxCount && end == input.Length)
                    return Partial(Project(0, candidate, end));
                return Full([Project(0, candidate, end)]);
            }
            if (partialMode != Pcre2PartialMode.None && count > 0 && end == input.Length)
                return Partial(Project(0, candidate, end));
            if (_anchored) return NoMatch();
            // Native bump-along skips the complete CRLF, while an explicitly
            // requested start at its LF still uses the normal token semantics.
            if (Pcre2CharacterSemantics.TryGetNewlineWidth(input, candidate, compiled.Request.Settings.Newline, out var width))
            {
                candidate += width;
            }
            else if (_input.TryAdvanceScalar(new Utf8BytePosition(candidate), out var nextCandidate)) candidate = nextCandidate.Value;
            else break;
        }
        return NoMatch();
    }

    private Utf8Pcre2ProbeResult ProbeViaLiteralAssertions(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, byte[] literal, Pcre2CompiledProgram compiled)
    {
        var tokens = ((Pcre2CharacterDirectProgram)compiled.Operations.Match).Program.Tokens;
        var intrinsic = tokens[0].Kind is Pcre2CharacterTokenKind.BeginningOfLine or Pcre2CharacterTokenKind.BeginningOfSubject;
        var first = intrinsic ? 1 : 0;
        Pcre2CharacterToken? leading = tokens[first].Kind == Pcre2CharacterTokenKind.WordBoundary ? tokens[first] : null;
        var search = startOffsetInBytes;
        while (search <= input.Length && CanStartCandidate(search))
        {
            _budget.ChargeWorkspaceStep();
            var remaining = input[search..];
            var index = intrinsic ? remaining.StartsWith(literal) ? 0 : -1 : IndexOf(remaining, literal);
            if (index >= 0 && !CanStartCandidate(search + index)) index = -1;
            if (index < 0)
            {
                if (partialMode == Pcre2PartialMode.None) return NoMatch();
                var length = _anchored || intrinsic ? LongestLeadingExactPrefixLength(remaining, literal)
                    : LongestTrailingPrefixLength(remaining, literal);
                if (length > 0)
                {
                    var partialStart = input.Length - length;
                    if (CanStartCandidate(partialStart) && LeadingBoundaryAccepts(leading, compiled.Request, input, partialStart))
                        return Partial(Project(0, partialStart, input.Length));
                }
                // PCRE2 records word-boundary context as lookbehind. Its
                // unanchored EOF attempt can therefore publish an empty partial
                // even when no literal prefix remains; explicit anchors retain
                // their first-candidate optimization and cannot bump to EOF.
                if (!_anchored && CanStartCandidate(input.Length) && (!intrinsic || search == input.Length &&
                    (tokens[0].Options & Pcre2CharacterOptions.Multiline) != 0) &&
                    tokens[^1].Kind is Pcre2CharacterTokenKind.WordBoundary or Pcre2CharacterTokenKind.NonWordBoundary)
                    return Partial(Project(0, input.Length, input.Length));
                return NoMatch();
            }
            var start = search + index;
            if (LeadingBoundaryAccepts(leading, compiled.Request, input, start))
            {
                var result = ProbeTrailingAssertion(input, partialMode, start, start + literal.Length, tokens[^1], compiled.Request);
                if (result.Kind != Utf8Pcre2ProbeKind.NoMatch) return result;
            }
            if (_anchored || intrinsic || !_input.TryAdvanceScalar(new Utf8BytePosition(start), out var next)) return NoMatch();
            search = next.Value;
        }
        return NoMatch();
    }

    private bool LeadingBoundaryAccepts(Pcre2CharacterToken? leading, Pcre2CompileRequest request,
        ReadOnlySpan<byte> input, int start)
    {
        if (leading is not { } assertion) return true;
        _budget.ChargeWorkspaceStep();
        return Pcre2CharacterRunner.TryMatchToken(assertion, request, input, start, _originalStart, _matchOptions, out _);
    }

    private Utf8Pcre2ProbeResult ProbeViaAnchoredExactLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, byte[] literalUtf8, Pcre2CompiledProgram compiled)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith(literalUtf8))
        {
            var end = startOffsetInBytes + literalUtf8.Length;
            var assertion = ((Pcre2CharacterDirectProgram)compiled.Operations.Match).Program.Tokens[^1];
            return ProbeTrailingAssertion(input, partialMode, startOffsetInBytes, end, assertion, compiled.Request);
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

    private Utf8Pcre2ProbeResult ProbeTrailingAssertion(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int start, int end, Pcre2CharacterToken assertion, Pcre2CompileRequest request)
    {
        _budget.ChargeWorkspaceStep();
        var full = Pcre2CharacterRunner.TryMatchToken(assertion, request, input,
            end, _originalStart, _matchOptions, out _);
        if (partialMode != Pcre2PartialMode.None)
        {
            var dollar = assertion.Kind == Pcre2CharacterTokenKind.EndOfLine;
            var multiline = (assertion.Options & Pcre2CharacterOptions.Multiline) != 0;
            // Final-newline assertions can inspect a CR while waiting for the
            // LF that would complete the configured CRLF newline. NotEol
            // affects dollar, while \Z keeps its independent semantics.
            if ((assertion.Kind == Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline ||
                 dollar && (multiline || (_matchOptions & Pcre2MatchOptions.NotEol) == 0)) &&
                request.Settings.Newline == Pcre2NewlineConvention.Crlf &&
                end + 1 == input.Length && input[end] == (byte)'\r')
                return Partial(Project(0, start, input.Length));
            if (partialMode == Pcre2PartialMode.Hard &&
                (full && (end == input.Length ||
                    assertion.Kind == Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline || dollar && !multiline) ||
                 end == input.Length && assertion.Kind == Pcre2CharacterTokenKind.NonWordBoundary))
                return Partial(Project(0, start, input.Length));
            if (!full && end == input.Length &&
                assertion.Kind is Pcre2CharacterTokenKind.WordBoundary or Pcre2CharacterTokenKind.NonWordBoundary)
                return Partial(Project(0, start, end));
        }
        return full ? Full([Project(0, start, end)]) : NoMatch();
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
                Pcre2MatchOptions.Anchored | _matchOptions, ref _budget);
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

    private bool CanStartCandidate(int start) => !_firstLine || IsFirstLineCandidate(start);

    private bool IsFirstLineCandidate(int start)
    {
        if (start < _originalStart || start > _candidateLimit) return false;
        // Scan only as far as a requested consumed start. The first newline
        // itself remains eligible, and matching keeps the complete subject.
        // Repeated literal/EOF attempts reuse this numeric progress; no eager
        // whole-line scan, subject slice, or independent resource owner.
        while (_candidateScan < start)
        {
            _budget.ChargeWorkspaceStep();
            if (!Pcre2CharacterRunner.TryAdvanceCandidate(_compiled.Request, null, ref _input,
                _candidateScan, allowAcceleration: false, out var next))
            {
                _candidateLimit = _candidateScan;
                return false;
            }
            _candidateScan = next;
        }
        return true;
    }

    private int IndexOf(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        if (_anchored)
        {
            _budget.ChargeWorkspaceStep();
            return subject.StartsWith(literal) ? 0 : -1;
        }
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
