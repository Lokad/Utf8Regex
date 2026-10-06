using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.Execution;


internal static partial class Utf8ExecutionInterpreter
{
    public static int FindNextSimplePattern(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        in Utf8SearchPlan searchPlan,
        in AsciiSimplePatternPlan plan,
        int startIndex,
        out int matchedLength)
    {
        return FindNextSimplePattern(input, program, searchPlan, plan, startIndex, captures: null, budget: Utf8ExecutionDeadline.Infinite, out matchedLength);
    }

    // PLAN.md D06: single owner for the interpreter-backed simple-pattern Count loop; both compiled runtimes reach it only after their route-specific preconditions.
    internal static int CountSimplePattern(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        in Utf8SearchPlan searchPlan,
        in AsciiSimplePatternPlan plan,
        Utf8ExecutionDeadline budget)
    {
        return CountSimplePatternFrom(input, program, searchPlan, plan, 0, budget);
    }

    // FromOffset counting starts the shared loop at the start unit; anchors still see
    // the whole input, so line edges stay exact.
    internal static int CountSimplePatternFrom(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        in Utf8SearchPlan searchPlan,
        in AsciiSimplePatternPlan plan,
        int startIndex,
        Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeAsciiSimplePattern);
        var count = 0;
        var index = startIndex;
        while (index <= input.Length)
        {
            var found = FindNextSimplePattern(
                input,
                program,
                searchPlan,
                plan,
                index,
                captures: null,
                budget,
                out var matchedLength);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public static int FindNextSimplePattern(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        in Utf8SearchPlan searchPlan,
        in AsciiSimplePatternPlan plan,
        int startIndex,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int matchedLength)
    {
        matchedLength = 0;
        if (program is null)
        {
            return -1;
        }

        if (captures is null &&
            TryFindNextSingleBranchFixedLengthSimplePattern(input, searchPlan, plan, startIndex, budget, out var fastCandidate, out matchedLength))
        {
            return fastCandidate;
        }

        if (CanUseCompiledSearchPlan(searchPlan, plan))
        {
            for (var candidate = Utf8SearchExecutor.FindNext(searchPlan, input, startIndex);
                candidate >= 0;
                candidate = Utf8SearchExecutor.FindNext(searchPlan, input, candidate + 1))
            {
                budget.Step();
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();

                if (!MatchesFixedLiterals(input, candidate, plan))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountFixedCheckReject();
                    continue;
                }

                Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
                if (TryMatchSimplePatternAt(input, program, plan, candidate, captures, budget, out matchedLength))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                    return candidate;
                }
            }

            matchedLength = 0;
            return -1;
        }

        if (plan.SearchLiterals.Length > 0)
        {
            var searchFrom = startIndex + plan.SearchLiteralOffset;
            while (searchFrom <= input.Length)
            {
                budget.Step();

                var relative = plan.SearchLiterals.Length == 1
                    ? (plan.IgnoreCase
                        ? Internal.Search.AsciiSearch.IndexOfIgnoreCase(input[searchFrom..], plan.SearchLiterals[0])
                        : Internal.Search.AsciiSearch.IndexOfExact(input[searchFrom..], plan.SearchLiterals[0]))
                    : Internal.Search.AsciiSearch.IndexOfAnyExact(input[searchFrom..], plan.SearchLiterals);
                if (relative < 0)
                {
                    matchedLength = 0;
                    return -1;
                }

                var absoluteAnchor = searchFrom + relative;
                var candidate = absoluteAnchor - plan.SearchLiteralOffset;
                if (candidate >= startIndex)
                {
                    Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                    if (!MatchesFixedLiterals(input, candidate, plan))
                    {
                        Utf8SearchDiagnosticsSession.Current?.CountFixedCheckReject();
                    }
                    else
                    {
                        Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
                        if (TryMatchSimplePatternAt(input, program, plan, candidate, captures, budget, out matchedLength))
                        {
                            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                            return candidate;
                        }
                    }
                }

                searchFrom = absoluteAnchor + 1;
            }

            matchedLength = 0;
            return -1;
        }

        for (var index = startIndex; index <= input.Length; index++)
        {
            budget.Step();
            if (TryMatchSimplePatternAt(input, program, plan, index, captures, budget, out matchedLength))
            {
                return index;
            }
        }

        matchedLength = 0;
        return -1;
    }

    private static bool TryFindNextSingleBranchFixedLengthSimplePattern(
        ReadOnlySpan<byte> input,
        Utf8SearchPlan searchPlan,
        AsciiSimplePatternPlan plan,
        int startIndex,
        Utf8ExecutionDeadline budget,
        out int candidate,
        out int matchedLength)
    {
        candidate = -1;
        matchedLength = 0;

        if (plan.Branches.Length != 1 ||
            !plan.IsFixedLength ||
            (plan.IsStartAnchored && startIndex != 0))
        {
            return false;
        }

        if (!TryGetSingleBranchAnchor(searchPlan, plan, out var anchorLiteral, out var anchorOffset, out var ignoreCase))
        {
            return false;
        }

        var branch = plan.Branches[0];
        var searchFrom = startIndex + anchorOffset;
        var maxCandidate = input.Length - branch.Length;
        if (searchFrom < 0 || startIndex > maxCandidate)
        {
            return false;
        }

        while (searchFrom <= input.Length - anchorLiteral.Length)
        {
            budget.Step();

            var relative = ignoreCase
                ? Internal.Search.AsciiSearch.IndexOfIgnoreCase(input[searchFrom..], anchorLiteral)
                : Internal.Search.AsciiSearch.IndexOfExact(input[searchFrom..], anchorLiteral);
            if (relative < 0)
            {
                return false;
            }

            var absoluteAnchor = searchFrom + relative;
            var branchCandidate = absoluteAnchor - anchorOffset;
            // Absolute anchors pin candidates at offset 0; line anchors accept
            // offset 0 and positions right after a line feed.
            var holdsStartAnchor = !plan.IsStartAnchored ||
                branchCandidate == 0 ||
                (!plan.IsAbsoluteStartAnchored && (uint)branchCandidate <= (uint)input.Length && branchCandidate > 0 && input[branchCandidate - 1] == (byte)'\n');
            if (branchCandidate >= startIndex &&
                holdsStartAnchor &&
                branchCandidate <= maxCandidate)
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                if (!MatchesFixedLiterals(input, branchCandidate, plan))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountFixedCheckReject();
                    searchFrom = absoluteAnchor + 1;
                    continue;
                }

                Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
                if (TryMatchSimplePatternBranch(input, branch, branchCandidate, plan, out matchedLength))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                    candidate = branchCandidate;
                    return true;
                }
            }

            searchFrom = absoluteAnchor + 1;
        }

        return false;
    }

    private static bool TryGetSingleBranchAnchor(
        Utf8SearchPlan searchPlan,
        AsciiSimplePatternPlan plan,
        out byte[] anchorLiteral,
        out int anchorOffset,
        out bool ignoreCase)
    {
        anchorLiteral = [];
        anchorOffset = 0;
        ignoreCase = plan.IgnoreCase;

        if (CanUseCompiledSearchPlan(searchPlan, plan) &&
            searchPlan.Kind is Utf8SearchKind.FixedDistanceAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiChar or Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.AsciiFoldedByteLiteral &&
            searchPlan.LiteralUtf8 is { Length: > 0 } searchLiteral)
        {
            anchorLiteral = searchLiteral;
            anchorOffset = searchPlan.Kind is Utf8SearchKind.FixedDistanceAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiChar ? searchPlan.Distance : 0;
            ignoreCase = searchPlan.Kind == Utf8SearchKind.AsciiFoldedByteLiteral;
            return true;
        }

        if (plan.SearchLiterals.Length == 1)
        {
            anchorLiteral = plan.SearchLiterals[0];
            anchorOffset = plan.SearchLiteralOffset;
            return true;
        }

        return false;
    }

    private static bool CanUseCompiledSearchPlan(Utf8SearchPlan searchPlan, AsciiSimplePatternPlan plan)
    {
        return searchPlan.Kind switch
        {
            Utf8SearchKind.FixedDistanceAsciiLiteral => plan.SearchLiteralOffset == searchPlan.Distance &&
                ContainsSearchLiteral(plan, searchPlan.LiteralUtf8),
            Utf8SearchKind.FixedDistanceAsciiChar => searchPlan.LiteralUtf8 is { Length: 1 } literal &&
                MatchesFixedLiteralByteAtOffset(plan, searchPlan.Distance, literal[0]),
            Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.AsciiFoldedByteLiteral => plan.SearchLiteralOffset == 0 &&
                plan.SearchLiterals.Length == 1 &&
                ContainsSearchLiteral(plan, searchPlan.LiteralUtf8),
            Utf8SearchKind.ExactAsciiLiterals => plan.SearchLiteralOffset == 0 &&
                plan.SearchLiterals.Length > 1 &&
                searchPlan.AlternateLiteralsUtf8 is { Length: > 1 },
            _ => false,
        };
    }

    private static bool ContainsSearchLiteral(AsciiSimplePatternPlan plan, byte[]? literal)
    {
        if (literal is null)
        {
            return false;
        }

        foreach (var candidate in plan.SearchLiterals)
        {
            if (candidate.AsSpan().SequenceEqual(literal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesFixedLiteralByteAtOffset(AsciiSimplePatternPlan plan, int offset, byte value)
    {
        foreach (var branch in plan.Branches)
        {
            if (offset < 0 || offset >= branch.Length)
            {
                return false;
            }

            var token = branch[offset];
            if (token.Kind != AsciiSimplePatternTokenKind.Literal || token.Literal != value)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryMatchPrefix(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        int startIndex,
        out int matchedLength)
    {
        return TryMatchPrefix(input, program, startIndex, captures: null, budget: Utf8ExecutionDeadline.Infinite, out matchedLength);
    }

    public static bool TryMatchPrefix(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        int startIndex,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int matchedLength)
    {
        return Utf8ByteSafeLinearVerifierRunner.TryMatchPrefix(input, program, startIndex, captures, budget, out matchedLength);
    }

    public static bool TryMatchSimplePatternAt(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        AsciiSimplePatternPlan plan,
        int startIndex,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int matchedLength)
    {
        if (captures is null && TryMatchSimplePatternWithoutCaptures(input, plan, startIndex, out matchedLength))
        {
            return true;
        }

        if (captures is not null &&
            TryMatchDeterministicSimplePatternWithCaptures(input, program, plan, startIndex, captures, budget, out matchedLength))
        {
            return true;
        }

        if (plan.IsStartAnchored || plan.IsEndAnchored)
        {
            matchedLength = 0;
            return false;
        }

        return TryMatchPrefix(input, program, startIndex, captures, budget, out matchedLength);
    }

    private static bool TryMatchDeterministicSimplePatternWithCaptures(
        ReadOnlySpan<byte> input, Utf8ExecutionProgram? program, AsciiSimplePatternPlan plan,
        int startIndex, Utf8CaptureSlots captures, Utf8ExecutionDeadline deadline, out int matchedLength)
    {
        matchedLength = 0;
        if (program is null || !TryGetDeterministicCaptureLayout(program, plan, out _)) return false;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, CancellationToken.None);
        return TryMatchDeterministicProgram(input, program, startIndex, captures, ref invocation, out matchedLength, out _);
    }

    internal static bool TryMatchProgramAt(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram program,
        int instructionIndex,
        int startIndex,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int endIndex)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        var matched = TryMatchProgram(input, program, instructionIndex, startIndex, captures, ref invocation, out endIndex, out var breach);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned byte-safe matching breached an unlimited invocation budget.");
        return matched;
    }

    private static bool IsDeterministicCaptureProgram(Utf8ExecutionProgram program)
    {
        foreach (var instruction in program.Instructions)
        {
            if (instruction.Kind != Utf8ExecutionInstructionKind.Enter)
            {
                continue;
            }

            switch (instruction.NodeKind)
            {
                case Utf8ExecutionNodeKind.Capture:
                case Utf8ExecutionNodeKind.Group:
                case Utf8ExecutionNodeKind.Concatenate:
                case Utf8ExecutionNodeKind.One:
                case Utf8ExecutionNodeKind.Multi:
                case Utf8ExecutionNodeKind.Set:
                case Utf8ExecutionNodeKind.Bol:
                case Utf8ExecutionNodeKind.Beginning:
                case Utf8ExecutionNodeKind.Start:
                case Utf8ExecutionNodeKind.Eol:
                case Utf8ExecutionNodeKind.EndZ:
                case Utf8ExecutionNodeKind.End:
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    public static bool TryGetDeterministicCaptureLayout(
        Utf8ExecutionProgram? program, AsciiSimplePatternPlan plan, out Utf8DeterministicCaptureLayout layout)
    {
        layout = default;
        if (program?.DeterministicCaptureLayout is not { } cached ||
            plan.Branches.Length != 1 || !plan.IsFixedLength) return false;
        layout = cached;
        return true;
    }

    internal static Utf8DeterministicCaptureLayout? CreateDeterministicCaptureLayout(Utf8ExecutionProgram program)
    {
        if (program.Instructions.Count == 0 || !IsDeterministicCaptureProgram(program)) return null;
        var captures = new Dictionary<int, Utf8DeterministicCaptureSlice>();
        var frames = new Stack<(int Enter, int Start, int NextChild)>();
        frames.Push((0, 0, -1));
        var offset = 0;
        while (frames.TryPop(out var frame))
        {
            var instruction = program.Instructions[frame.Enter];
            if (instruction.Kind != Utf8ExecutionInstructionKind.Enter) return null;
            var container = instruction.NodeKind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group or Utf8ExecutionNodeKind.Concatenate;
            var next = frame.NextChild < 0 ? frame.Enter + 1 : frame.NextChild;
            if (container && next < instruction.PartnerIndex)
            {
                frames.Push((frame.Enter, frame.Start, program.Instructions[next].PartnerIndex + 1));
                frames.Push((next, offset, -1));
                continue;
            }
            if (instruction.NodeKind == Utf8ExecutionNodeKind.Capture)
                captures[instruction.CaptureNumber] = new Utf8DeterministicCaptureSlice(frame.Start, offset - frame.Start);
            if (!container)
            {
                var width = instruction.NodeKind switch
                {
                    Utf8ExecutionNodeKind.One or Utf8ExecutionNodeKind.Set => 1,
                    Utf8ExecutionNodeKind.Multi => instruction.Text?.Length ?? 0,
                    _ => 0,
                };
                // A specialization decline cannot reject an otherwise valid pattern.
                if (offset > int.MaxValue - width) return null;
                offset += width;
            }
        }
        return new Utf8DeterministicCaptureLayout(offset, captures);
    }

    // Fixed deterministic discovery borrows immutable preparation and caller-owned
    // slots. There is no invocation-owned continuation array, capture stack or
    // journal to admit. Final slots are projected only after the whole attempt
    // succeeds, using the cached final values for shared capture numbers.
    internal static bool TryMatchDeterministicProgram(
        ReadOnlySpan<byte> input, Utf8ExecutionProgram program, int startIndex, Utf8CaptureSlots captures,
        ref Utf8SliceInvocation invocation, out int matchedLength, out Utf8ExecutionLimitKind breach)
    {
        matchedLength = 0;
        breach = Utf8ExecutionLimitKind.None;
        var index = startIndex;
        var successful = false;
        try
        {
            breach = captures.TryClear(ref invocation);
            if (breach != Utf8ExecutionLimitKind.None || program.DeterministicCaptureLayout is not { } layout ||
                (uint)startIndex > (uint)input.Length) return false;
            breach = invocation.Budget.TryNoteDepth(1);
            if (breach != Utf8ExecutionLimitKind.None) return false;
            for (var pc = 0; pc <= program.Instructions[0].PartnerIndex;)
            {
                breach = invocation.TryChargeStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                var instruction = program.Instructions[pc];
                if (instruction.Kind == Utf8ExecutionInstructionKind.Exit ||
                    instruction.NodeKind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group or Utf8ExecutionNodeKind.Concatenate)
                {
                    pc++;
                    continue;
                }
                var hit = instruction.NodeKind switch
                {
                    Utf8ExecutionNodeKind.One => TryMatchOne(input, instruction, index, out index),
                    Utf8ExecutionNodeKind.Set => TryMatchSet(input, instruction, index, out index),
                    Utf8ExecutionNodeKind.Multi => TryMatchProgramText(input, instruction, index, ref invocation, out index, out breach),
                    Utf8ExecutionNodeKind.Bol => IsBeginningOfLine(input, index, instruction.Options),
                    Utf8ExecutionNodeKind.Beginning => index == 0,
                    Utf8ExecutionNodeKind.Start => index == (invocation.AnchorOriginByte < 0 ? startIndex : invocation.AnchorOriginByte),
                    Utf8ExecutionNodeKind.Eol => IsEndAnchorMatch(input, index, instruction.Options),
                    Utf8ExecutionNodeKind.EndZ => index == input.Length || index == input.Length - 1 && input[index] == (byte)'\n',
                    Utf8ExecutionNodeKind.End => index == input.Length,
                    _ => false,
                };
                if (!hit) return false;
                pc = instruction.PartnerIndex + 1;
            }
            foreach (var capture in layout.Captures)
            {
                breach = invocation.TryChargeStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                captures.Set(capture.Key, startIndex + capture.Value.Offset, capture.Value.Length);
            }
            matchedLength = index - startIndex;
            successful = true;
            return true;
        }
        finally
        {
            if (!successful) captures.Clear();
        }

    }

    private static bool MatchesFixedLiterals(ReadOnlySpan<byte> input, int candidateStart, AsciiSimplePatternPlan plan)
    {
        foreach (var check in plan.FixedLiteralChecks)
        {
            var offset = candidateStart + check.Offset;
            if ((uint)offset > (uint)input.Length || offset + check.Literal.Length > input.Length)
            {
                return false;
            }

            if (!input.Slice(offset, check.Literal.Length).SequenceEqual(check.Literal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchSimplePatternWithoutCaptures(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternPlan plan,
        int startIndex,
        out int matchedLength)
    {
        matchedLength = 0;
        if ((uint)startIndex > (uint)input.Length ||
            (plan.IsAbsoluteStartAnchored && startIndex != 0) ||
            (plan.IsStartAnchored && !plan.IsAbsoluteStartAnchored && startIndex != 0 &&
                input[startIndex - 1] != (byte)'\n'))
        {
            return false;
        }

        foreach (var branch in plan.Branches)
        {
            if (TryMatchSimplePatternBranch(input, branch, startIndex, plan, out matchedLength))
            {
                return true;
            }
        }

        matchedLength = 0;
        return false;
    }

    private static bool TryMatchSimplePatternBranch(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternToken[] branch,
        int startIndex,
        AsciiSimplePatternPlan plan,
        out int matchedLength)
    {
        matchedLength = 0;
        // Line end anchors (`(?m:$)`) also accept right before any line feed.
        var branchEnd = startIndex + branch.Length;
        if (branchEnd > input.Length ||
            (plan.IsEndAnchored && branchEnd != input.Length &&
                !(!plan.IsAbsoluteEndAnchored && (uint)branchEnd < (uint)input.Length && input[branchEnd] == (byte)'\n')))
        {
            return false;
        }

        for (var i = 0; i < branch.Length; i++)
        {
            var token = branch[i];
            var value = input[startIndex + i];
            switch (token.Kind)
            {
                case AsciiSimplePatternTokenKind.Literal:
                    if (plan.IgnoreCase)
                    {
                        value = Internal.Search.AsciiSearch.FoldCase(value);
                    }

                    if (value != token.Literal)
                    {
                        return false;
                    }

                    break;

                case AsciiSimplePatternTokenKind.Dot:
                    break;

                case AsciiSimplePatternTokenKind.CharClass when !token.CharClass.IsEmpty:
                    if (!token.CharClass.Contains(plan.IgnoreCase ? Internal.Search.AsciiSearch.FoldCase(value) : value))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        matchedLength = branch.Length;
        return true;
    }

    public static bool TryMatchLiteralPrefix(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        bool ignoreCase,
        Utf8ExecutionDeadline budget,
        out int matchedLength)
    {
        matchedLength = 0;
        if (program is null)
        {
            return false;
        }

        var offset = 0;
        foreach (var instruction in program.Instructions)
        {
            budget.Step();

            if (instruction.Kind != Utf8ExecutionInstructionKind.Enter)
            {
                continue;
            }

            switch (instruction.NodeKind)
            {
                case Utf8ExecutionNodeKind.Capture:
                case Utf8ExecutionNodeKind.Concatenate:
                case Utf8ExecutionNodeKind.Group:
                    continue;

                case Utf8ExecutionNodeKind.One:
                    if ((uint)offset >= (uint)input.Length)
                    {
                        matchedLength = 0;
                        return false;
                    }

                    var actual = input[offset];
                    var expected = (byte)instruction.Ch;
                    if (ignoreCase)
                    {
                        actual = Internal.Search.AsciiSearch.FoldCase(actual);
                        expected = Internal.Search.AsciiSearch.FoldCase(expected);
                    }

                    if (actual != expected)
                    {
                        matchedLength = 0;
                        return false;
                    }

                    offset++;
                    continue;

                case Utf8ExecutionNodeKind.Multi when instruction.Text is not null:
                    if (!TryMatchLiteralText(input, offset, instruction.Text, ignoreCase))
                    {
                        matchedLength = 0;
                        return false;
                    }

                    offset += instruction.Text.Length;
                    continue;

                case Utf8ExecutionNodeKind.Set when instruction.Text is not null:
                    if ((uint)offset >= (uint)input.Length ||
                        !Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)input[offset], instruction.Text))
                    {
                        matchedLength = 0;
                        return false;
                    }

                    offset++;
                    continue;

                default:
                    matchedLength = 0;
                    return false;
            }
        }

        matchedLength = offset;
        return true;
    }

    private static bool IsBeginningOfLine(ReadOnlySpan<byte> input, int index, RegexOptions options)
    {
        if (index == 0)
        {
            return true;
        }

        return (options & RegexOptions.Multiline) != 0 &&
            index <= input.Length &&
            input[index - 1] == (byte)'\n';
    }

    private static bool IsEndAnchorMatch(ReadOnlySpan<byte> input, int index, RegexOptions options)
    {
        return index == input.Length ||
            (index == input.Length - 1 && input[index] == (byte)'\n') ||
            ((options & RegexOptions.Multiline) != 0 &&
                (uint)index < (uint)input.Length &&
                input[index] == (byte)'\n');
    }

    private static bool TryMatchLiteralText(ReadOnlySpan<byte> input, int offset, string text, bool ignoreCase)
    {
        if (offset + text.Length > input.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var actual = input[offset + i];
            var expected = (byte)text[i];
            if (ignoreCase)
            {
                actual = Internal.Search.AsciiSearch.FoldCase(actual);
                expected = Internal.Search.AsciiSearch.FoldCase(expected);
            }

            if (actual != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchOne(
        ReadOnlySpan<byte> input,
        Utf8ExecutionInstruction instruction,
        int index,
        out int endIndex)
    {
        endIndex = 0;
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        var actual = input[index];
        var expected = (byte)instruction.Ch;
        if ((instruction.Options & RegexOptions.IgnoreCase) != 0)
        {
            actual = Internal.Search.AsciiSearch.FoldCase(actual);
            expected = Internal.Search.AsciiSearch.FoldCase(expected);
        }

        if (actual != expected)
        {
            return false;
        }

        endIndex = index + 1;
        return true;
    }

    private static bool TryMatchProgramText(
        ReadOnlySpan<byte> input, Utf8ExecutionInstruction instruction, int index,
        ref Utf8SliceInvocation invocation, out int endIndex, out Utf8ExecutionLimitKind breach)
    {
        endIndex = 0;
        breach = Utf8ExecutionLimitKind.None;
        var text = instruction.Text;
        if (text is null || text.Length > input.Length - index) return false;
        var ignoreCase = (instruction.Options & RegexOptions.IgnoreCase) != 0;
        for (var offset = 0; offset < text.Length;)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            var end = offset + Math.Min(2048, text.Length - offset);
            for (; offset < end; offset++)
            {
                var actual = input[index + offset];
                var expected = (byte)text[offset];
                if (ignoreCase)
                {
                    actual = Internal.Search.AsciiSearch.FoldCase(actual);
                    expected = Internal.Search.AsciiSearch.FoldCase(expected);
                }
                if (actual != expected) return false;
            }
        }
        endIndex = index + text.Length;
        return true;
    }

    private static bool TryMatchSet(
        ReadOnlySpan<byte> input,
        Utf8ExecutionInstruction instruction,
        int index,
        out int endIndex)
    {
        endIndex = 0;
        if ((uint)index >= (uint)input.Length || instruction.Text is null)
        {
            return false;
        }

        var ch = (char)input[index];
        if (!Internal.FrontEnd.Utf8OwnedCharClass.CharInClass(ch, instruction.Text))
        {
            if ((instruction.Options & RegexOptions.IgnoreCase) == 0 || input[index] > 0x7F)
            {
                return false;
            }

            var folded = (char)Internal.Search.AsciiSearch.FoldCase(input[index]);
            if (!Internal.FrontEnd.Utf8OwnedCharClass.CharInClass(folded, instruction.Text))
            {
                return false;
            }
        }

        endIndex = index + 1;
        return true;
    }
}
