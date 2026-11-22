using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

internal enum Pcre2LiteralAnchor : byte
{
    None = 0,
    AbsoluteStart = 1,
    RequestedStart = 2,
}

internal sealed class Pcre2LiteralProgram
{
    private readonly byte[] _literalUtf8;

    internal Pcre2LiteralProgram(
        byte[] literalUtf8,
        Pcre2LiteralAnchor startAnchor,
        bool endAnchored,
        bool hasExplicitEndAssertion,
        bool hasExplicitCrOrLf,
        bool ignoreCase = false)
    {
        _literalUtf8 = literalUtf8; // The compiler transfers its admitted UTF-8 buffer.
        Search = new PreparedSubstringSearch(_literalUtf8, ignoreCase);
        StartAnchor = startAnchor;
        EndAnchored = endAnchored;
        HasExplicitEndAssertion = hasExplicitEndAssertion;
        HasExplicitCrOrLf = hasExplicitCrOrLf;
    }

    internal ReadOnlySpan<byte> LiteralUtf8 => _literalUtf8;

    internal PreparedSubstringSearch Search { get; }

    internal Pcre2LiteralAnchor StartAnchor { get; }

    internal bool EndAnchored { get; }

    internal bool HasExplicitEndAssertion { get; }
    internal bool HasExplicitCrOrLf { get; }

    internal bool CanCountDirectly =>
        _literalUtf8.Length != 0 &&
        StartAnchor == Pcre2LiteralAnchor.None &&
        !EndAnchored;

    internal int CountNonOverlapping(ReadOnlySpan<byte> input) =>
        Search.Count(input);

    internal Utf8ExecutionLimitKind TryCountNonOverlapping(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out int count)
    {
        count = 0;
        var total = 0;
        var start = 0;
        var last = input.Length - _literalUtf8.Length;
        while (start <= last)
        {
            // The same bounded search and long-needle comparison used by Core.
            // No global cursor, capture or coordinate projection escapes Count.
            var breach = Utf8SearchExecutor.TryFindLiteralCandidate(Search, input, start, last,
                false, ref invocation, out var candidate);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (candidate < 0) break;
            total++;
            start = candidate + _literalUtf8.Length;
        }
        var terminal = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (terminal == Utf8ExecutionLimitKind.None) count = total;
        return terminal;
    }
}

internal readonly record struct Pcre2LiteralMatch(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int ConsumedEndOffsetInBytes)
{
    internal static Pcre2LiteralMatch NoMatch => default;

    internal static Pcre2LiteralMatch Create(int startOffsetInBytes, int lengthInBytes)
        => new(
            true,
            startOffsetInBytes,
            startOffsetInBytes + lengthInBytes,
            startOffsetInBytes + lengthInBytes);
}

internal static class Pcre2LiteralCompiler
{
    private const Pcre2CompileOptions UnsupportedOptions =
        Pcre2CompileOptions.Caseless |
        Pcre2CompileOptions.Extended |
        Pcre2CompileOptions.ExtendedMore |
        Pcre2CompileOptions.FirstLine;

    // A whole nonempty folded literal can count without the VM when each
    // token's PCRE2 case fiber is exactly its ASCII fold.
    internal static Pcre2LiteralProgram? CompileCaselessCount(IPcre2BacktrackingNode root, in Pcre2CompileRequest request)
    {
        const Pcre2CompileOptions countOptions = Pcre2CompileOptions.Caseless | Pcre2CompileOptions.Anchored |
            Pcre2CompileOptions.EndAnchored | Pcre2CompileOptions.FirstLine;
        if ((request.Options & countOptions) != Pcre2CompileOptions.Caseless) return null;

        var children = (root as Pcre2SequenceBacktrackingNode)?.Children;
        var length = children?.Length ?? 1;
        if (length is 0 or > 32) return null;
        var budget = Utf8ConstructionScope.CurrentBudget;
        budget.ChargeWork((ulong)length);
        Span<byte> literal = stackalloc byte[length];
        for (var index = 0; index < length; index++)
        {
            var node = children is null ? root : children[index];
            if (node is not Pcre2TokenBacktrackingNode
                { Token: { Kind: Pcre2CharacterTokenKind.Literal, Literal.IsAscii: true } token } ||
                (token.Options & Pcre2CharacterOptions.Caseless) == 0)
                return null;

            var value = (char)token.Literal.Value;
            var folded = AsciiSearch.FoldCase((byte)value);
            var equivalents = Pcre2CharacterSemantics.GetBmpCaseEquivalents(value);
            foreach (var equivalent in equivalents)
                if (equivalent >= 128 || AsciiSearch.FoldCase((byte)equivalent) != folded)
                    return null;
            if (char.IsAsciiLetter(value) && !equivalents.Contains((char)(value ^ 0x20)))
                return null;
            literal[index] = folded;
        }

        if (request.SkipsCrLfContinuation && literal[0] == '\n') return null;
        var bytes = budget.AllocateArray<byte>(length, false);
        literal.CopyTo(bytes);
        return budget.AllocateObject(bytes, static folded => new Pcre2LiteralProgram(folded,
            Pcre2LiteralAnchor.None, endAnchored: false, hasExplicitEndAssertion: false,
            hasExplicitCrOrLf: folded.AsSpan().IndexOfAny((byte)'\r', (byte)'\n') >= 0, ignoreCase: true));
    }

    // A fully consuming exact literal can prove a leading positive lookahead.
    // Only the Boolean route may ignore the writes of an enclosed capture.
    internal static Pcre2LiteralProgram? CompileImpliedLookahead(IPcre2BacktrackingNode root)
    {
        if (root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 2 and <= 33 } sequence ||
            sequence.Children[0] is not Pcre2AssertionBacktrackingNode { AssertionKind: Pcre2AssertionKind.PositiveLookahead } assertion)
            return null;

        var budget = Utf8ConstructionScope.CurrentBudget;
        Span<byte> literal = stackalloc byte[sequence.Children.Length - 1];
        budget.ChargeWork((ulong)literal.Length);
        for (var index = 0; index < literal.Length; index++)
        {
            if (sequence.Children[index + 1] is not Pcre2TokenBacktrackingNode
                { Token: { Kind: Pcre2CharacterTokenKind.Literal, Options: Pcre2CharacterOptions.None, Literal.IsAscii: true } token })
                return null;
            literal[index] = (byte)token.Literal.Value;
        }

        var body = assertion.Body is Pcre2CaptureBacktrackingNode capture ? capture.Body : assertion.Body;
        var offset = 0;
        if (body is Pcre2SequenceBacktrackingNode parts)
        {
            if (parts.Children.Length > literal.Length) return null;
            budget.ChargeWork((ulong)parts.Children.Length);
            foreach (var part in parts.Children)
                if (!FitsLiteral(part, literal, ref offset)) return null;
        }
        else if (!FitsLiteral(body, literal, ref offset)) return null;

        var bytes = budget.AllocateArray<byte>(literal.Length, false);
        literal.CopyTo(bytes);
        return budget.AllocateObject(bytes, static exact => new Pcre2LiteralProgram(exact,
            Pcre2LiteralAnchor.None, endAnchored: false, hasExplicitEndAssertion: false,
            hasExplicitCrOrLf: exact.AsSpan().IndexOfAny((byte)'\r', (byte)'\n') >= 0));

        static bool FitsLiteral(IPcre2BacktrackingNode node, ReadOnlySpan<byte> literal, ref int offset)
        {
            var count = 1;
            if (node is Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat && repeat.Minimum == repeat.Maximum)
            {
                node = repeat.Body;
                count = repeat.Minimum;
            }
            if (count > literal.Length - offset || node is not Pcre2TokenBacktrackingNode
                { Token: { Options: Pcre2CharacterOptions.None } atom }) return false;
            var end = offset + count;
            for (; offset < end; offset++)
            {
                var value = literal[offset];
                if (!(atom.Kind switch
                {
                    Pcre2CharacterTokenKind.Literal => atom.Literal.Value == value,
                    Pcre2CharacterTokenKind.CharacterClass => atom.CharacterClass.AsciiSet.Contains(value),
                    _ => false,
                })) return false;
            }
            return true;
        }
    }

    internal static Pcre2LiteralProgram? Compile(Pcre2CompileRequest request)
    {
        if ((request.Options & UnsupportedOptions) != 0) return null;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            // Literal preparation owns its buffers; it needs no second Core
            // compilation to supply construction bounds or group-zero metadata.
            budget.ChargeWork((ulong)request.ConstructionPattern.Length);
            if (!TryParse(request.ConstructionPattern, out var atoms, out var startAnchor, out var endAnchored))
                return null;
            try
            {
                if ((request.Options & Pcre2CompileOptions.Anchored) != 0 && startAnchor == Pcre2LiteralAnchor.None)
                    startAnchor = Pcre2LiteralAnchor.RequestedStart;
                var hasExplicitEndAssertion = endAnchored;
                endAnchored |= (request.Options & Pcre2CompileOptions.EndAnchored) != 0;
                var byteCount = 0;
                var hasExplicitCrOrLf = false;
                budget.ChargeWork((ulong)atoms.Length);
                for (var index = 0; index < atoms.Length; index++)
                {
                    if ((index & 1023) == 0) budget.ChargeWork(0);
                    byteCount = checked(byteCount + atoms[index].Utf8SequenceLength);
                    hasExplicitCrOrLf |= atoms[index].Value is '\r' or '\n';
                }
                var literalUtf8 = budget.AllocateArray<byte>(byteCount, false);
                budget.ChargeWork((ulong)byteCount);
                var written = 0;
                for (var index = 0; index < atoms.Length; index++)
                {
                    if ((index & 1023) == 0) budget.ChargeWork(0);
                    written += atoms[index].EncodeToUtf8(literalUtf8.AsSpan(written));
                }
                var program = budget.AllocateObject((literalUtf8, startAnchor, endAnchored, hasExplicitEndAssertion, hasExplicitCrOrLf),
                    static state => new Pcre2LiteralProgram(state.literalUtf8, state.startAnchor,
                        state.endAnchored, state.hasExplicitEndAssertion, state.hasExplicitCrOrLf));
                scope.Complete();
                return program;
            }
            finally { budget.ReleaseArray(atoms, true); }
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryParse(
        Utf8ConstructionPattern pattern,
        out Rune[] atoms,
        out Pcre2LiteralAnchor startAnchor,
        out bool endAnchored)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        using var parsedAtoms = new Utf8ConstructionBuffer<Rune>(budget, instructions: true);
        var position = 0;
        startAnchor = Pcre2LiteralAnchor.None;
        endAnchored = false;

        while (Pcre2CompileValidator.TrySkipParenthesizedComment(pattern, ref position)) { }
        if (pattern.StartsWithAscii(position, @"\A"))
        {
            startAnchor = Pcre2LiteralAnchor.AbsoluteStart;
            position += 2;
        }
        else if (pattern.StartsWithAscii(position, @"\G"))
        {
            startAnchor = Pcre2LiteralAnchor.RequestedStart;
            position += 2;
        }

        while (position < pattern.Length)
        {
            budget.ChargeWork(1);
            if (Pcre2CompileValidator.TrySkipParenthesizedComment(pattern, ref position)) continue;
            if (pattern.StartsWithAscii(position, @"\z"))
            {
                position += 2;
                while (Pcre2CompileValidator.TrySkipParenthesizedComment(pattern, ref position)) { }
                if (position != pattern.Length) { atoms = []; return false; }
                endAnchored = true;
                break;
            }
            if (pattern[position] == '\\')
            {
                if (position + 1 >= pattern.Length || !IsEscapedLiteralMetacharacter(pattern[position + 1]))
                {
                    atoms = [];
                    return false;
                }

                parsedAtoms.Add(new Rune(pattern[position + 1]));
                position += 2;
                continue;
            }

            if (IsUnescapedMetacharacter(pattern[position]))
            {
                atoms = [];
                return false;
            }

            var status = pattern.DecodeScalar(position, out var scalar, out var consumed);
            if (status != OperationStatus.Done)
            {
                atoms = [];
                return false;
            }

            parsedAtoms.Add(scalar);
            position += consumed;
        }

        atoms = parsedAtoms.Take();
        return true;
    }

    private static bool IsEscapedLiteralMetacharacter(char value)
        => value is '\\' or '.' or '^' or '$' or '*' or '+' or '?' or '(' or ')' or '[' or ']' or '{' or '}' or '|';

    private static bool IsUnescapedMetacharacter(char value)
        => value is '\\' or '.' or '^' or '$' or '*' or '+' or '?' or '(' or ')' or '[' or ']' or '{' or '}' or '|';
}

internal static class Pcre2LiteralRunner
{
    private const int MeteredSearchChunkLength = 4096;

    internal static Pcre2LiteralMatch Match(
        Pcre2LiteralProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition requestedStart,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
    {
        if (budget.IsUnmetered)
            return MatchCore(program, ref input, requestedStart, matchOptions, ref budget);
        budget.CheckDeadline();
        var result = MatchCore(program, ref input, requestedStart, matchOptions, ref budget);
        budget.CheckDeadline();
        return result;
    }

    private static Pcre2LiteralMatch MatchCore(
        Pcre2LiteralProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition requestedStart,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
    {
        var literal = program.LiteralUtf8;
        var startAnchor = GetEffectiveStartAnchor(program.StartAnchor, matchOptions);
        var endAnchored = program.EndAnchored || (matchOptions & Pcre2MatchOptions.EndAnchored) != 0;

        if ((matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && literal.Length == 0)
        {
            return Pcre2LiteralMatch.NoMatch;
        }

        if (startAnchor == Pcre2LiteralAnchor.AbsoluteStart)
        {
            budget.ChargeCandidate();
            return requestedStart.Value == 0 && MatchesAt(program, input.Bytes, 0, endAnchored) &&
                   !RejectsEmptyAtRequestedStart(literal, matchOptions)
                ? Pcre2LiteralMatch.Create(0, literal.Length)
                : Pcre2LiteralMatch.NoMatch;
        }

        if (startAnchor == Pcre2LiteralAnchor.RequestedStart)
        {
            budget.ChargeCandidate();
            return MatchesAt(program, input.Bytes, requestedStart.Value, endAnchored) &&
                   !RejectsEmptyAtRequestedStart(literal, matchOptions)
                ? Pcre2LiteralMatch.Create(requestedStart.Value, literal.Length)
                : Pcre2LiteralMatch.NoMatch;
        }

        if (endAnchored)
        {
            budget.ChargeCandidate();
            var candidate = input.ByteLength - literal.Length;
            return candidate >= requestedStart.Value &&
                   MatchesAt(program, input.Bytes, candidate, endAnchored: true) &&
                   !(candidate == requestedStart.Value && RejectsEmptyAtRequestedStart(literal, matchOptions))
                ? Pcre2LiteralMatch.Create(candidate, literal.Length)
                : Pcre2LiteralMatch.NoMatch;
        }

        var searchStart = requestedStart;
        if (RejectsEmptyAtRequestedStart(literal, matchOptions))
        {
            if (!input.TryAdvanceScalar(requestedStart, out searchStart))
            {
                return Pcre2LiteralMatch.NoMatch;
            }
        }

        if (literal.Length == 0)
        {
            budget.ChargeCandidate();
            return Pcre2LiteralMatch.Create(searchStart.Value, 0);
        }

        var relative = IndexOfLiteral(program, input.Bytes[searchStart.Value..], ref budget);
        return relative >= 0
            ? Pcre2LiteralMatch.Create(searchStart.Value + relative, literal.Length)
            : Pcre2LiteralMatch.NoMatch;
    }

    private static int IndexOfLiteral(
        Pcre2LiteralProgram program,
        ReadOnlySpan<byte> input,
        ref Pcre2ResourceBudget budget)
    {
        if (!budget.RequiresCandidateMetering)
        {
            return program.Search.IndexOf(input);
        }

        var literalLength = program.LiteralUtf8.Length;
        var offset = 0;
        while (offset <= input.Length - literalLength)
        {
            budget.CheckDeadline();
            budget.ChargeCandidate();
            var searchLength = Math.Min(
                input.Length - offset,
                MeteredSearchChunkLength + literalLength - 1);
            var relative = program.Search.IndexOf(input.Slice(offset, searchLength));
            if (relative >= 0)
            {
                return offset + relative;
            }

            offset += MeteredSearchChunkLength;
        }

        return -1;
    }

    private static Pcre2LiteralAnchor GetEffectiveStartAnchor(
        Pcre2LiteralAnchor compiledAnchor,
        Pcre2MatchOptions matchOptions)
    {
        if (compiledAnchor == Pcre2LiteralAnchor.AbsoluteStart)
        {
            return compiledAnchor;
        }

        return compiledAnchor == Pcre2LiteralAnchor.RequestedStart ||
               (matchOptions & Pcre2MatchOptions.Anchored) != 0
            ? Pcre2LiteralAnchor.RequestedStart
            : Pcre2LiteralAnchor.None;
    }

    private static bool MatchesAt(
        Pcre2LiteralProgram program,
        ReadOnlySpan<byte> input,
        int candidate,
        bool endAnchored)
    {
        var literal = program.LiteralUtf8;
        return candidate >= 0 &&
               candidate <= input.Length - literal.Length &&
               (!endAnchored || candidate + literal.Length == input.Length) &&
               input.Slice(candidate, literal.Length).SequenceEqual(literal);
    }

    private static bool RejectsEmptyAtRequestedStart(ReadOnlySpan<byte> literal, Pcre2MatchOptions matchOptions)
        => literal.Length == 0 && (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0;
}

internal static class Pcre2LiteralProbeRunner
{
    internal static Utf8Pcre2ProbeResult Probe(
        ReadOnlySpan<byte> subject,
        Pcre2LiteralProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition requestedStart,
        Pcre2PartialMode partialMode,
        Pcre2MatchOptions matchOptions,
        scoped ref Pcre2ResourceBudget budget)
    {
        try
        {
            var fullMatch = Pcre2LiteralRunner.Match(program, ref input, requestedStart, matchOptions, ref budget);
            if (fullMatch.Success)
            {
                var projected = Project(ref input, fullMatch.StartOffsetInBytes, fullMatch.EndOffsetInBytes, ref budget);
                if (partialMode == Pcre2PartialMode.Hard && program.HasExplicitEndAssertion &&
                    fullMatch.EndOffsetInBytes == input.ByteLength)
                {
                    budget.CheckDeadline();
                    return Utf8Pcre2ProbeResult.CreatePartial(subject, projected);
                }

                var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
                Pcre2SliceDriver.ThrowIfSliceBreached(budget.WorkingInvocation.Budget.TryChargeResultBytes(bytes));
                budget.ReserveWorking(bytes);
                try
                {
                    var groups = new[] { projected };
                    budget.CheckDeadline();
                    return Utf8Pcre2ProbeResult.CreateFullMatch(subject, groups);
                }
                finally { budget.ReleaseWorking(bytes); }
            }

            if (partialMode == Pcre2PartialMode.None || program.LiteralUtf8.IsEmpty)
            {
                // Rejecting an empty full does not reject the explicit end's
                // EOF partial. Ordinary empty patterns have no such uncertainty;
                // requested/absolute anchors cannot move to a different start.
                if (partialMode != Pcre2PartialMode.None && program.LiteralUtf8.IsEmpty &&
                    program.HasExplicitEndAssertion &&
                    (!IsEffectivelyAnchored(program, matchOptions) || requestedStart.Value == input.ByteLength) &&
                    (program.StartAnchor != Pcre2LiteralAnchor.AbsoluteStart || requestedStart.Value == 0))
                {
                    budget.ChargeCandidate();
                    var projected = Project(ref input, input.ByteLength, input.ByteLength, ref budget);
                    budget.CheckDeadline();
                    return Utf8Pcre2ProbeResult.CreatePartial(subject, projected);
                }
                budget.CheckDeadline();
                return Utf8Pcre2ProbeResult.CreateNoMatch(subject);
            }

            var literal = program.LiteralUtf8;
            var candidate = GetFirstPartialCandidate(program, input.Bytes, requestedStart, matchOptions);
            while (candidate >= 0 && candidate < input.ByteLength)
            {
                budget.ChargeCandidate();
                var suffix = input.Bytes[candidate..];
                if (suffix.Length < literal.Length && literal.StartsWith(suffix))
                {
                    var projected = Project(ref input, candidate, input.ByteLength, ref budget);
                    budget.CheckDeadline();
                    return Utf8Pcre2ProbeResult.CreatePartial(subject, projected);
                }

                if (IsEffectivelyAnchored(program, matchOptions) ||
                    !input.TryAdvanceScalar(new Utf8BytePosition(candidate), out var next)) break;
                candidate = next.Value;
            }

            budget.CheckDeadline();
            return Utf8Pcre2ProbeResult.CreateNoMatch(subject);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    private static int GetFirstPartialCandidate(
        Pcre2LiteralProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition requestedStart,
        Pcre2MatchOptions matchOptions)
    {
        if (program.StartAnchor == Pcre2LiteralAnchor.AbsoluteStart)
        {
            return requestedStart.Value == 0 ? 0 : -1;
        }

        if (IsEffectivelyAnchored(program, matchOptions))
        {
            return requestedStart.Value;
        }

        var earliestSuffix = Math.Max(requestedStart.Value, input.Length - program.LiteralUtf8.Length + 1);
        while (earliestSuffix < input.Length && (input[earliestSuffix] & 0xC0) == 0x80)
        {
            earliestSuffix++;
        }

        return earliestSuffix;
    }

    private static bool IsEffectivelyAnchored(Pcre2LiteralProgram program, Pcre2MatchOptions matchOptions)
        => program.StartAnchor != Pcre2LiteralAnchor.None ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0;

    private static Pcre2GroupData Project(
        ref Utf8ValidatedInput input, int startOffsetInBytes, int endOffsetInBytes,
        ref Pcre2ResourceBudget budget)
    {
        return Pcre2MatchPublication.Publish(startOffsetInBytes, endOffsetInBytes,
            ref input, ref budget);
    }
}
