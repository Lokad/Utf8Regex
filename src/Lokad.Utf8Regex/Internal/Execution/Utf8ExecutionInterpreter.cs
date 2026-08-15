using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.Execution;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static class Utf8ExecutionInterpreter
{
    public static int FindNextSimplePattern(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        Utf8SearchPlan searchPlan,
        AsciiSimplePatternPlan plan,
        int startIndex,
        out int matchedLength)
    {
        return FindNextSimplePattern(input, program, searchPlan, plan, startIndex, captures: null, budget: Utf8ExecutionDeadline.Infinite, out matchedLength);
    }

    public static int FindNextSimplePattern(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        Utf8SearchPlan searchPlan,
        AsciiSimplePatternPlan plan,
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
            if (branchCandidate >= startIndex &&
                (!plan.IsStartAnchored || branchCandidate == 0) &&
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
            TryMatchDeterministicSimplePatternWithCaptures(input, program, plan, startIndex, captures, out matchedLength))
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
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        AsciiSimplePatternPlan plan,
        int startIndex,
        Utf8CaptureSlots captures,
        out int matchedLength)
    {
        matchedLength = 0;
        if (program is null ||
            plan.Branches.Length != 1 ||
            !plan.IsFixedLength ||
            !IsDeterministicCaptureProgram(program))
        {
            return false;
        }

        captures.Clear();
        if (!TryMatchDeterministicNode(input, program, 0, startIndex, captures, out var endIndex))
        {
            captures.Clear();
            return false;
        }

        matchedLength = endIndex - startIndex;
        return true;
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
        return TryMatchProgramNode(input, program, instructionIndex, startIndex, captures, budget, out endIndex);
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
        Utf8ExecutionProgram? program,
        AsciiSimplePatternPlan plan,
        out Utf8DeterministicCaptureLayout layout)
    {
        layout = default;
        if (program is null ||
            plan.Branches.Length != 1 ||
            !plan.IsFixedLength ||
            !IsDeterministicCaptureProgram(program))
        {
            return false;
        }

        var captures = new Dictionary<int, Utf8DeterministicCaptureSlice>();
        if (!TryAnalyzeDeterministicNode(program, 0, 0, captures, out var endOffset))
        {
            return false;
        }

        layout = new Utf8DeterministicCaptureLayout(endOffset, captures);
        return true;
    }

    private static bool TryMatchDeterministicNode(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram program,
        int enterIndex,
        int index,
        Utf8CaptureSlots captures,
        out int endIndex)
    {
        var instruction = program.Instructions[enterIndex];
        if (instruction.Kind != Utf8ExecutionInstructionKind.Enter)
        {
            endIndex = 0;
            return false;
        }

        switch (instruction.NodeKind)
        {
            case Utf8ExecutionNodeKind.Capture:
                if (TryMatchDeterministicSequence(input, program, enterIndex + 1, instruction.PartnerIndex, index, captures, out endIndex))
                {
                    captures.Set(instruction.CaptureNumber, index, endIndex - index);
                    return true;
                }

                endIndex = 0;
                return false;

            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Concatenate:
                return TryMatchDeterministicSequence(input, program, enterIndex + 1, instruction.PartnerIndex, index, captures, out endIndex);

            case Utf8ExecutionNodeKind.One:
                return TryMatchOne(input, instruction, index, out endIndex);

            case Utf8ExecutionNodeKind.Multi:
                return TryMatchMulti(input, instruction, index, out endIndex);

            case Utf8ExecutionNodeKind.Set:
                return TryMatchSet(input, instruction, index, out endIndex);

            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                endIndex = index;
                return IsBeginningOfLine(input, index, instruction.Options);

            case Utf8ExecutionNodeKind.Eol:
            case Utf8ExecutionNodeKind.EndZ:
                endIndex = index;
                return IsEndAnchorMatch(input, index, instruction.Options);

            case Utf8ExecutionNodeKind.End:
                endIndex = index;
                return index == input.Length;

            default:
                endIndex = 0;
                return false;
        }
    }

    private static bool TryAnalyzeDeterministicNode(
        Utf8ExecutionProgram program,
        int enterIndex,
        int offset,
        Dictionary<int, Utf8DeterministicCaptureSlice> captures,
        out int endOffset)
    {
        var instruction = program.Instructions[enterIndex];
        if (instruction.Kind != Utf8ExecutionInstructionKind.Enter)
        {
            endOffset = 0;
            return false;
        }

        switch (instruction.NodeKind)
        {
            case Utf8ExecutionNodeKind.Capture:
                if (TryAnalyzeDeterministicSequence(program, enterIndex + 1, instruction.PartnerIndex, offset, captures, out endOffset))
                {
                    captures[instruction.CaptureNumber] = new Utf8DeterministicCaptureSlice(offset, endOffset - offset);
                    return true;
                }

                endOffset = 0;
                return false;

            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Concatenate:
                return TryAnalyzeDeterministicSequence(program, enterIndex + 1, instruction.PartnerIndex, offset, captures, out endOffset);

            case Utf8ExecutionNodeKind.One:
            case Utf8ExecutionNodeKind.Set:
                endOffset = offset + 1;
                return true;

            case Utf8ExecutionNodeKind.Multi:
                endOffset = offset + (instruction.Text?.Length ?? 0);
                return true;

            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
            case Utf8ExecutionNodeKind.Eol:
            case Utf8ExecutionNodeKind.EndZ:
            case Utf8ExecutionNodeKind.End:
                endOffset = offset;
                return true;

            default:
                endOffset = 0;
                return false;
        }
    }

    private static bool TryAnalyzeDeterministicSequence(
        Utf8ExecutionProgram program,
        int currentIndex,
        int exitIndex,
        int offset,
        Dictionary<int, Utf8DeterministicCaptureSlice> captures,
        out int endOffset)
    {
        while (currentIndex < exitIndex)
        {
            if (!TryAnalyzeDeterministicNode(program, currentIndex, offset, captures, out offset))
            {
                endOffset = 0;
                return false;
            }

            currentIndex = program.Instructions[currentIndex].PartnerIndex + 1;
        }

        endOffset = offset;
        return true;
    }

    private static bool TryMatchDeterministicSequence(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram program,
        int currentIndex,
        int exitIndex,
        int index,
        Utf8CaptureSlots captures,
        out int endIndex)
    {
        while (currentIndex < exitIndex)
        {
            if (!TryMatchDeterministicNode(input, program, currentIndex, index, captures, out index))
            {
                endIndex = 0;
                return false;
            }

            currentIndex = program.Instructions[currentIndex].PartnerIndex + 1;
        }

        endIndex = index;
        return true;
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
            (plan.IsStartAnchored && startIndex != 0))
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
        if (startIndex + branch.Length > input.Length ||
            (plan.IsEndAnchored && startIndex + branch.Length != input.Length))
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
                        !RuntimeFrontEnd.RegexCharClass.CharInClass((char)input[offset], instruction.Text))
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

    private static bool TryMatchProgramNode(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram program,
        int enterIndex,
        int index,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int endIndex)
    {
        budget.Step();

        var instruction = program.Instructions[enterIndex];
        if (instruction.Kind != Utf8ExecutionInstructionKind.Enter)
        {
            endIndex = 0;
            return false;
        }

        switch (instruction.NodeKind)
        {
            case Utf8ExecutionNodeKind.Empty:
                endIndex = index;
                return true;

            case Utf8ExecutionNodeKind.Capture:
                var captureCheckpoint = captures?.CreateCheckpoint() ?? default;
                if (TryMatchProgramSequence(input, program, enterIndex + 1, instruction.PartnerIndex, index, captures, budget, out endIndex))
                {
                    captures?.Set(instruction.CaptureNumber, index, endIndex - index);
                    return true;
                }

                captures?.Rollback(captureCheckpoint);

                endIndex = 0;
                return false;

            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Concatenate:
                return TryMatchProgramSequence(input, program, enterIndex + 1, instruction.PartnerIndex, index, captures, budget, out endIndex);

            case Utf8ExecutionNodeKind.Alternate:
                for (var childIndex = enterIndex + 1; childIndex < instruction.PartnerIndex; childIndex = program.Instructions[childIndex].PartnerIndex + 1)
                {
                    var alternateCheckpoint = captures?.CreateCheckpoint() ?? default;
                    if (TryMatchProgramNode(input, program, childIndex, index, captures, budget, out endIndex))
                    {
                        return true;
                    }

                    captures?.Rollback(alternateCheckpoint);
                }

                endIndex = 0;
                return false;

            case Utf8ExecutionNodeKind.One:
                return TryMatchOne(input, instruction, index, out endIndex);

            case Utf8ExecutionNodeKind.Multi:
                return TryMatchMulti(input, instruction, index, out endIndex);

            case Utf8ExecutionNodeKind.Set:
                return TryMatchSet(input, instruction, index, out endIndex);

            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                endIndex = index;
                return IsBeginningOfLine(input, index, instruction.Options);

            case Utf8ExecutionNodeKind.Eol:
            case Utf8ExecutionNodeKind.EndZ:
                endIndex = index;
                return IsEndAnchorMatch(input, index, instruction.Options);

            case Utf8ExecutionNodeKind.End:
                endIndex = index;
                return index == input.Length;

            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                return TryMatchStandaloneProgramLoop(input, program, enterIndex, instruction, index, captures, budget, out endIndex);

            default:
                endIndex = 0;
                return false;
        }
    }

    private static bool TryMatchProgramSequence(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram program,
        int currentIndex,
        int exitIndex,
        int index,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int endIndex)
    {
        if (currentIndex >= exitIndex)
        {
            endIndex = index;
            return true;
        }

        var instruction = program.Instructions[currentIndex];
        if (instruction.NodeKind == Utf8ExecutionNodeKind.Alternate)
        {
            for (var branchIndex = currentIndex + 1; branchIndex < instruction.PartnerIndex; branchIndex = program.Instructions[branchIndex].PartnerIndex + 1)
            {
                var branchCheckpoint = captures?.CreateCheckpoint() ?? default;
                if (TryMatchProgramNode(input, program, branchIndex, index, captures, budget, out var branchEnd) &&
                    TryMatchProgramSequence(input, program, instruction.PartnerIndex + 1, exitIndex, branchEnd, captures, budget, out endIndex))
                {
                    return true;
                }

                captures?.Rollback(branchCheckpoint);
            }

            endIndex = 0;
            return false;
        }

        if (instruction.NodeKind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop)
        {
            return TryMatchProgramLoopInSequence(input, program, currentIndex, exitIndex, index, captures, budget, out endIndex);
        }

        var sequenceCheckpoint = captures?.CreateCheckpoint() ?? default;
        if (!TryMatchProgramNode(input, program, currentIndex, index, captures, budget, out var nextIndex))
        {
            captures?.Rollback(sequenceCheckpoint);

            endIndex = 0;
            return false;
        }

        if (TryMatchProgramSequence(input, program, instruction.PartnerIndex + 1, exitIndex, nextIndex, captures, budget, out endIndex))
        {
            return true;
        }

        captures?.Rollback(sequenceCheckpoint);

        endIndex = 0;
        return false;
    }

    private static bool TryMatchProgramLoopInSequence(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram program,
        int enterIndex,
        int exitIndex,
        int index,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int endIndex)
    {
        var loop = program.Instructions[enterIndex];
        if (loop.Max < loop.Min)
        {
            endIndex = 0;
            return false;
        }

        var maximumRepeats = Math.Min(loop.Max, input.Length - index);
        if (loop.Min > maximumRepeats)
        {
            endIndex = 0;
            return false;
        }

        var stateCount = checked(maximumRepeats + 1);
        var positions = new Utf8PooledStateStack<int>(stateCount);
        var captureCheckpoints = new Utf8PooledStateStack<Utf8CaptureCheckpoint>(captures is null ? 0 : stateCount);
        try
        {
            PushState(ref positions, index);
            if (captures is not null)
            {
                PushState(ref captureCheckpoints, captures.CreateCheckpoint());
            }

            var count = 0;
            while (count < maximumRepeats &&
                TryMatchProgramLoopBody(input, program, enterIndex, loop, positions[count], captures, budget, out var nextIndex) &&
                nextIndex > positions[count])
            {
                count++;
                PushState(ref positions, nextIndex);
                if (captures is not null)
                {
                    PushState(ref captureCheckpoints, captures.CreateCheckpoint());
                }
            }

            if (count < loop.Min)
            {
                RollbackCapture(captures, captureCheckpoints, 0);
                endIndex = 0;
                return false;
            }

            if (loop.NodeKind == Utf8ExecutionNodeKind.LazyLoop)
            {
                for (var repeats = loop.Min; repeats <= count; repeats++)
                {
                    RollbackCapture(captures, captureCheckpoints, repeats);

                    if (TryMatchProgramSequence(input, program, loop.PartnerIndex + 1, exitIndex, positions[repeats], captures, budget, out endIndex))
                    {
                        return true;
                    }
                }
            }
            else
            {
                for (var repeats = count; repeats >= loop.Min; repeats--)
                {
                    RollbackCapture(captures, captureCheckpoints, repeats);

                    if (TryMatchProgramSequence(input, program, loop.PartnerIndex + 1, exitIndex, positions[repeats], captures, budget, out endIndex))
                    {
                        return true;
                    }
                }
            }

            RollbackCapture(captures, captureCheckpoints, 0);
            endIndex = 0;
            return false;
        }
        finally
        {
            captureCheckpoints.Dispose();
            positions.Dispose();
        }
    }

    private static bool TryMatchStandaloneProgramLoop(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram program,
        int enterIndex,
        Utf8ExecutionInstruction loop,
        int index,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int endIndex)
    {
        if (loop.Max < loop.Min)
        {
            endIndex = 0;
            return false;
        }

        var maximumRepeats = Math.Min(loop.Max, input.Length - index);
        var initialCheckpoint = captures?.CreateCheckpoint() ?? default;
        if (loop.Min > maximumRepeats)
        {
            endIndex = 0;
            return false;
        }

        var stateCount = checked(maximumRepeats + 1);
        var captureCheckpoints = new Utf8PooledStateStack<Utf8CaptureCheckpoint>(captures is null ? 0 : stateCount);
        try
        {
            endIndex = index;
            if (captures is not null)
            {
                PushState(ref captureCheckpoints, initialCheckpoint);
            }

            for (var i = 0; i < loop.Min; i++)
            {
                if (!TryMatchProgramLoopBody(input, program, enterIndex, loop, endIndex, captures, budget, out var nextIndex) || nextIndex <= endIndex)
                {
                    captures?.Rollback(initialCheckpoint);
                    endIndex = 0;
                    return false;
                }

                endIndex = nextIndex;
                if (captures is not null)
                {
                    PushState(ref captureCheckpoints, captures.CreateCheckpoint());
                }
            }

            var repeats = loop.Min;
            for (var i = loop.Min; i < maximumRepeats; i++)
            {
                if (!TryMatchProgramLoopBody(input, program, enterIndex, loop, endIndex, captures, budget, out var nextIndex) || nextIndex <= endIndex)
                {
                    break;
                }

                endIndex = nextIndex;
                repeats++;
                if (captures is not null)
                {
                    PushState(ref captureCheckpoints, captures.CreateCheckpoint());
                }
            }

            RollbackCapture(captures, captureCheckpoints, repeats);
            return true;
        }
        finally
        {
            captureCheckpoints.Dispose();
        }
    }

    private static void PushState<T>(ref Utf8PooledStateStack<T> states, T value) where T : struct
    {
        if (!states.TryPush(value))
        {
            throw new InvalidOperationException("The bounded backtracking state stack is full.");
        }
    }

    private static void RollbackCapture(
        Utf8CaptureSlots? captures,
        Utf8PooledStateStack<Utf8CaptureCheckpoint> checkpoints,
        int index)
    {
        if (captures is not null)
        {
            captures.Rollback(checkpoints[index]);
        }
    }

    private static bool TryMatchProgramLoopBody(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram program,
        int enterIndex,
        Utf8ExecutionInstruction loop,
        int index,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int endIndex)
    {
        if (enterIndex + 1 < loop.PartnerIndex)
        {
            return TryMatchProgramSequence(input, program, enterIndex + 1, loop.PartnerIndex, index, captures, budget, out endIndex);
        }

        if (loop.Text is not null)
        {
            return TryMatchSet(input, loop, index, out endIndex);
        }

        return TryMatchOne(input, loop, index, out endIndex);
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

    private static bool TryMatchMulti(
        ReadOnlySpan<byte> input,
        Utf8ExecutionInstruction instruction,
        int index,
        out int endIndex)
    {
        endIndex = 0;
        if (instruction.Text is null || !TryMatchLiteralText(input, index, instruction.Text, (instruction.Options & RegexOptions.IgnoreCase) != 0))
        {
            return false;
        }

        endIndex = index + instruction.Text.Length;
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
        if (!RuntimeFrontEnd.RegexCharClass.CharInClass(ch, instruction.Text))
        {
            if ((instruction.Options & RegexOptions.IgnoreCase) == 0 || input[index] > 0x7F)
            {
                return false;
            }

            var folded = (char)Internal.Search.AsciiSearch.FoldCase(input[index]);
            if (!RuntimeFrontEnd.RegexCharClass.CharInClass(folded, instruction.Text))
            {
                return false;
            }
        }

        endIndex = index + 1;
        return true;
    }
}
