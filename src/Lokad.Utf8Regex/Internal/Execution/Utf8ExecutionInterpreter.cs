using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
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
        return FindNextSimplePattern(input, program, searchPlan, plan, startIndex, budget: Utf8ExecutionDeadline.Infinite, out matchedLength);
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
        // The fallback prefix family is immutable for the whole Count. Prepare
        // it lazily once, rather than rebuilding its maps for every match.
        PreparedLiteralSetSearch? literalSearch = null;
        while (index <= input.Length)
        {
            var found = FindNextSimplePattern(
                input,
                program,
                searchPlan,
                plan,
                index,
                budget,
                ref literalSearch,
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
        Utf8ExecutionDeadline budget,
        out int matchedLength)
    {
        PreparedLiteralSetSearch? literalSearch = null;
        return FindNextSimplePattern(input, program, searchPlan, plan, startIndex,
            budget, ref literalSearch, out matchedLength);
    }

    private static int FindNextSimplePattern(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        in Utf8SearchPlan searchPlan,
        in AsciiSimplePatternPlan plan,
        int startIndex,
        Utf8ExecutionDeadline budget,
        ref PreparedLiteralSetSearch? literalSearch,
        out int matchedLength)
    {
        matchedLength = 0;
        if (program is null)
        {
            return -1;
        }

        if (TryFindNextSingleBranchFixedLengthSimplePattern(input, searchPlan, plan, startIndex, budget, out var fastCandidate, out matchedLength))
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
                if (TryMatchSimplePatternAt(input, plan, candidate, out matchedLength))
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
                    : (literalSearch ??= new PreparedLiteralSetSearch(
                        AsciiSearch.CreateExactLiteralSearchData(plan.SearchLiterals))).IndexOf(input[searchFrom..]);
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
                        if (TryMatchSimplePatternAt(input, plan, candidate, out matchedLength))
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
            if (TryMatchSimplePatternAt(input, plan, index, out matchedLength))
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

    // Finite branches are the complete ordered ASCII language admitted by the
    // lowerer. A rejected start is a miss, not a reason to retry a second VM.
    internal static bool TryMatchSimplePatternAt(
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
        // Absolute `$`/`\Z` accept one final LF; line anchors also accept an
        // internal LF. Keep the prepared newline fact separate from absoluteness.
        var branchEnd = startIndex + branch.Length;
        if (branchEnd > input.Length ||
            (plan.IsEndAnchored && branchEnd != input.Length &&
                !(plan.AllowsTrailingNewlineBeforeEnd && branchEnd + 1 == input.Length && input[branchEnd] == (byte)'\n') &&
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

}
