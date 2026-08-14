using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Planning;
using System.Buffers;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchExecutor
{
    public static bool TryFindNextMatch(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, out PreparedSearchMatch match)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        if (startIndex > input.Length)
        {
            match = default;
            return false;
        }

        return plan.Kind switch
        {
            Utf8SearchKind.ExactAsciiLiterals when plan.HasPreparedSearcher => TryFindNextPreparedMatch(plan, input, startIndex, out match),
            Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals when plan.HasPreparedSearcher => TryFindNextPreparedMatch(plan, input, startIndex, out match),
            Utf8SearchKind.ExactUtf8Literals when plan.HasPreparedSearcher => TryFindNextPreparedMatch(plan, input, startIndex, out match),
            _ => TryFindFallbackMatch(plan, input, startIndex, reverse: false, out match),
        };
    }

    public static int FindFirst(Utf8SearchPlan plan, ReadOnlySpan<byte> input)
    {
        var literal = plan.LiteralUtf8;
        return plan.Kind switch
        {
            Utf8SearchKind.ExactAsciiLiteral when literal is not null => FindFilteredLiteralStart(plan, input, 0, ignoreCase: false),
            Utf8SearchKind.AsciiLiteralIgnoreCase when literal is not null => FindFilteredLiteralStart(plan, input, 0, ignoreCase: true),
            Utf8SearchKind.ExactAsciiLiterals when plan.HasPreparedSearcher => plan.PreparedSearcher.FindFirst(input),
            Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals when plan.HasPreparedSearcher => plan.HasBoundaryRequirements
                ? FindAnyIgnoreCaseLiteralWithBoundaries(plan, input, 0)
                : plan.PreparedSearcher.FindFirst(input),
            Utf8SearchKind.ExactUtf8Literals when plan.HasPreparedSearcher => plan.HasBoundaryRequirements
                ? FindAnyLiteralWithBoundaries(plan, input, 0)
                : FindAnyLiteralWithTrailingRequirement(plan, input, 0),
            Utf8SearchKind.FixedDistanceAsciiLiteral when literal is not null => FindFixedDistanceLiteral(plan, input, 0),
            Utf8SearchKind.FixedDistanceAsciiChar when literal is not null => FindFixedDistanceChar(plan, input, 0),
            Utf8SearchKind.FixedDistanceAsciiSets when plan.FixedDistanceSets is not null => FindFixedDistanceSets(plan, input, 0),
            Utf8SearchKind.TrailingAnchorFixedLengthEnd => FindTrailingAnchorFixedLengthEnd(plan, input, 0),
            Utf8SearchKind.TrailingAnchorFixedLengthEndZ => FindTrailingAnchorFixedLengthEndZ(plan, input, 0),
            _ => -1,
        };
    }

    public static int FindNext(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        if (startIndex > input.Length)
        {
            return -1;
        }

        return plan.Kind switch
        {
            Utf8SearchKind.ExactAsciiLiteral => FindFilteredLiteralStart(plan, input, startIndex, ignoreCase: false),
            Utf8SearchKind.AsciiLiteralIgnoreCase => FindFilteredLiteralStart(plan, input, startIndex, ignoreCase: true),
            Utf8SearchKind.FixedDistanceAsciiLiteral => FindFixedDistanceLiteral(plan, input, startIndex),
            Utf8SearchKind.FixedDistanceAsciiChar => FindFixedDistanceChar(plan, input, startIndex),
            Utf8SearchKind.FixedDistanceAsciiSets => FindFixedDistanceSets(plan, input, startIndex),
            Utf8SearchKind.TrailingAnchorFixedLengthEnd => FindTrailingAnchorFixedLengthEnd(plan, input, startIndex),
            Utf8SearchKind.TrailingAnchorFixedLengthEndZ => FindTrailingAnchorFixedLengthEndZ(plan, input, startIndex),
            _ =>
                FindRelativeNext(plan, input, startIndex),
        };
    }

    public static int FindLast(Utf8SearchPlan plan, ReadOnlySpan<byte> input)
    {
        var literal = plan.LiteralUtf8;
        return plan.Kind switch
        {
            Utf8SearchKind.ExactAsciiLiteral when literal is not null => FindFilteredLastLiteralStart(plan, input, input.Length, ignoreCase: false),
            Utf8SearchKind.AsciiLiteralIgnoreCase when literal is not null => FindFilteredLastLiteralStart(plan, input, input.Length, ignoreCase: true),
            Utf8SearchKind.ExactAsciiLiterals when plan.HasPreparedSearcher => plan.PreparedSearcher.FindLast(input),
            Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals when plan.HasPreparedSearcher => plan.HasBoundaryRequirements
                ? FindLastAnyIgnoreCaseLiteralWithBoundaries(plan, input, input.Length)
                : plan.PreparedSearcher.FindLast(input),
            Utf8SearchKind.ExactUtf8Literals when plan.HasPreparedSearcher => plan.HasBoundaryRequirements
                ? FindLastAnyLiteralWithBoundaries(plan, input, input.Length)
                : FindLastAnyLiteralWithTrailingRequirement(plan, input, input.Length),
            _ => -1,
        };
    }

    public static int FindPrevious(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        if (startIndex > input.Length)
        {
            startIndex = input.Length;
        }

        return FindLast(plan, input[..startIndex]);
    }

    public static bool TryFindLastMatch(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, out PreparedSearchMatch match)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        if (startIndex > input.Length)
        {
            startIndex = input.Length;
        }

        return plan.Kind switch
        {
            Utf8SearchKind.ExactAsciiLiterals when plan.HasPreparedSearcher => TryFindLastPreparedMatch(plan, input, startIndex, out match),
            Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals when plan.HasPreparedSearcher => TryFindLastPreparedMatch(plan, input, startIndex, out match),
            Utf8SearchKind.ExactUtf8Literals when plan.HasPreparedSearcher => TryFindLastPreparedMatch(plan, input, startIndex, out match),
            _ => TryFindFallbackMatch(plan, input, startIndex, reverse: true, out match),
        };
    }

    private static int FindRelativeNext(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var relative = FindFirst(plan, input[startIndex..]);
        return relative < 0 ? -1 : startIndex + relative;
    }

    private static int FindFilteredLiteralStart(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, bool ignoreCase)
    {
        var literal = plan.LiteralUtf8;
        var literalSearch = plan.LiteralSearch;
        if (literal is null || !literalSearch.HasValue || startIndex > input.Length)
        {
            return -1;
        }

        if (plan.HasStructuralCandidates &&
            plan.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start &&
            (plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement))
        {
            var state = new Utf8StructuralSearchState(
                new PreparedSearchScanState(startIndex, default),
                PreparedWindowScanState.Create(0));
            while (plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate))
            {
                if (candidate.StartIndex >= startIndex)
                {
                    Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                    return candidate.StartIndex;
                }
            }

            return -1;
        }

        return plan.HasBoundaryRequirements
            ? FindLiteralWithBoundaries(plan, input, startIndex, ignoreCase)
            : FindLiteralWithTrailingRequirement(plan, input, startIndex, ignoreCase);
    }

    private static int FindFilteredLastLiteralStart(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, bool ignoreCase)
    {
        var literal = plan.LiteralUtf8;
        var literalSearch = plan.LiteralSearch;
        if (literal is null || !literalSearch.HasValue)
        {
            return -1;
        }

        if (plan.HasStructuralCandidates &&
            plan.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start &&
            (plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement))
        {
            if (plan.StructuralSearchPlan.TryFindLastCandidate(input, startIndex, out var candidate))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate.StartIndex;
            }

            return -1;
        }

        return plan.HasBoundaryRequirements
            ? FindLastLiteralWithBoundaries(plan, input, startIndex, ignoreCase)
            : FindLastLiteralWithTrailingRequirement(plan, input, startIndex, ignoreCase);
    }

    private static bool TryFindNextPreparedMatch(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, out PreparedSearchMatch match)
    {
        if (!plan.HasPreparedSearcher)
        {
            match = default;
            return false;
        }

        if (plan.HasStructuralCandidates &&
            plan.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start &&
            (plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement))
        {
            var candidateState = new Utf8StructuralSearchState(
                new PreparedSearchScanState(startIndex, default),
                PreparedWindowScanState.Create(0));
            while (plan.StructuralSearchPlan.TryFindNextCandidate(input, ref candidateState, out var candidate))
            {
                if (candidate.StartIndex < startIndex)
                {
                    continue;
                }

                match = new PreparedSearchMatch(candidate.StartIndex, candidate.MatchLength, candidate.LiteralId);
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return true;
            }

            match = default;
            return false;
        }

        if (plan.Kind is Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals or Utf8SearchKind.ExactUtf8Literals)
        {
            if (!plan.HasBoundaryRequirements && !plan.HasTrailingLiteralRequirement)
            {
                if (plan.PreparedSearcher.TryFindFirstMatch(input[startIndex..], out var relative) &&
                    TryTranslateMatch(relative, startIndex, out match))
                {
                    return true;
                }

                match = default;
                return false;
            }

            var shortestLength = plan.Kind == Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals
                ? plan.AlternateIgnoreCaseLiteralSearch?.ShortestLength ?? int.MaxValue
                : plan.AlternateLiteralSearchData?.ShortestLength ?? int.MaxValue;
            if (shortestLength == int.MaxValue)
            {
                match = default;
                return false;
            }

            var searchIndex = startIndex;
            while (searchIndex <= input.Length - shortestLength)
            {
                if (!plan.PreparedSearcher.TryFindFirstMatch(input[searchIndex..], out var relative) ||
                    !TryTranslateMatch(relative, searchIndex, out match))
                {
                    match = default;
                    return false;
                }

                if ((!plan.HasBoundaryRequirements || MatchesBoundaryRequirements(plan, input, match.Index, match.Length)) &&
                    (!plan.HasTrailingLiteralRequirement || MatchesTrailingLiteralRequirement(plan, input, match.Index + match.Length)))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                    return true;
                }

                searchIndex = match.Index + 1;
            }
        }

        match = default;
        return false;
    }

    private static bool TryFindLastPreparedMatch(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, out PreparedSearchMatch match)
    {
        if (!plan.HasPreparedSearcher)
        {
            match = default;
            return false;
        }

        if (plan.HasStructuralCandidates &&
            plan.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start &&
            (plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement))
        {
            if (plan.StructuralSearchPlan.TryFindLastCandidate(input, startIndex, out var candidate))
            {
                match = new PreparedSearchMatch(candidate.StartIndex, candidate.MatchLength, candidate.LiteralId);
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return true;
            }

            match = default;
            return false;
        }

        if (plan.Kind is Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals or Utf8SearchKind.ExactUtf8Literals)
        {
            if (!plan.HasBoundaryRequirements && !plan.HasTrailingLiteralRequirement)
            {
                if (plan.PreparedSearcher.TryFindLastMatch(input[..Math.Min(startIndex, input.Length)], out match))
                {
                    return true;
                }

                match = default;
                return false;
            }

            var shortestLength = plan.Kind == Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals
                ? plan.AlternateIgnoreCaseLiteralSearch?.ShortestLength ?? int.MaxValue
                : plan.AlternateLiteralSearchData?.ShortestLength ?? int.MaxValue;
            if (shortestLength == int.MaxValue)
            {
                match = default;
                return false;
            }

            var searchLength = Math.Min(startIndex, input.Length);
            while (searchLength >= shortestLength)
            {
                if (!plan.PreparedSearcher.TryFindLastMatch(input[..searchLength], out match))
                {
                    match = default;
                    return false;
                }

                if ((!plan.HasBoundaryRequirements || MatchesBoundaryRequirements(plan, input, match.Index, match.Length)) &&
                    (!plan.HasTrailingLiteralRequirement || MatchesTrailingLiteralRequirement(plan, input, match.Index + match.Length)))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                    return true;
                }

                searchLength = match.Index;
            }
        }

        match = default;
        return false;
    }

    private static bool TryFindFallbackMatch(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, bool reverse, out PreparedSearchMatch match)
    {
        var index = reverse ? FindPrevious(plan, input, startIndex) : FindNext(plan, input, startIndex);
        if (index >= 0 && plan.PreparedSearcher.TryGetMatchedLength(input, index, out var matchedLength))
        {
            match = new PreparedSearchMatch(index, matchedLength, 0);
            return true;
        }

        match = default;
        return false;
    }

    private static bool TryTranslateMatch(PreparedSearchMatch relative, int baseIndex, out PreparedSearchMatch absolute)
    {
        absolute = new PreparedSearchMatch(baseIndex + relative.Index, relative.Length, relative.LiteralId);
        return true;
    }

    private static int FindLiteralWithBoundaries(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, bool ignoreCase)
    {
        var literal = plan.LiteralUtf8;
        var literalSearch = plan.LiteralSearch;
        if (literal is null || !literalSearch.HasValue || startIndex > input.Length)
        {
            return -1;
        }

        var searchIndex = startIndex;
        while (searchIndex <= input.Length - literal.Length)
        {
            var relative = Utf8SearchKernel.IndexOfLiteral(input[searchIndex..], literalSearch.Value);
            if (relative < 0)
            {
                return -1;
            }

            var candidate = searchIndex + relative;
            if (MatchesBoundaryRequirements(plan, input, candidate, literal.Length))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchIndex = candidate + 1;
        }

        return -1;
    }

    private static int FindLiteralWithTrailingRequirement(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, bool ignoreCase)
    {
        var literal = plan.LiteralUtf8;
        var literalSearch = plan.LiteralSearch;
        if (literal is null || !literalSearch.HasValue || startIndex > input.Length)
        {
            return -1;
        }

        if (!plan.HasTrailingLiteralRequirement)
        {
            return plan.PreparedSearcher.FindFirst(input[startIndex..]) is var relative && relative >= 0
                ? startIndex + relative
                : -1;
        }

        var searchIndex = startIndex;
        while (searchIndex <= input.Length - literal.Length)
        {
            var relative = plan.PreparedSearcher.FindFirst(input[searchIndex..]);
            if (relative < 0)
            {
                return -1;
            }

            var candidate = searchIndex + relative;
            if (MatchesTrailingLiteralRequirement(plan, input, candidate + literal.Length))
            {
                return candidate;
            }

            searchIndex = candidate + 1;
        }

        return -1;
    }

    private static int FindLastLiteralWithBoundaries(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, bool ignoreCase)
    {
        var literal = plan.LiteralUtf8;
        var literalSearch = plan.LiteralSearch;
        if (literal is null || !literalSearch.HasValue)
        {
            return -1;
        }

        var searchLength = Math.Min(startIndex, input.Length);
        while (searchLength >= literal.Length)
        {
            var candidate = Utf8SearchKernel.LastIndexOfLiteral(input[..searchLength], literalSearch.Value);
            if (candidate < 0)
            {
                return -1;
            }

            if (MatchesBoundaryRequirements(plan, input, candidate, literal.Length))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchLength = candidate;
        }

        return -1;
    }

    private static int FindLastLiteralWithTrailingRequirement(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, bool ignoreCase)
    {
        var literal = plan.LiteralUtf8;
        var literalSearch = plan.LiteralSearch;
        if (literal is null || !literalSearch.HasValue)
        {
            return -1;
        }

        if (!plan.HasTrailingLiteralRequirement)
        {
            return plan.PreparedSearcher.FindLast(input[..Math.Min(startIndex, input.Length)]);
        }

        var searchLength = Math.Min(startIndex, input.Length);
        while (searchLength >= literal.Length)
        {
            var candidate = plan.PreparedSearcher.FindLast(input[..searchLength]);
            if (candidate < 0)
            {
                return -1;
            }

            if (MatchesTrailingLiteralRequirement(plan, input, candidate + literal.Length))
            {
                return candidate;
            }

            searchLength = candidate;
        }

        return -1;
    }

    private static int FindAnyLiteralWithBoundaries(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var literalSearch = plan.AlternateLiteralSearch;
        if (!literalSearch.HasValue || startIndex > input.Length)
        {
            return -1;
        }

        var searchIndex = startIndex;
        while (searchIndex <= input.Length - literalSearch.Value.SearchData.ShortestLength)
        {
            if (!plan.PreparedSearcher.TryFindFirstMatch(input[searchIndex..], out var match))
            {
                return -1;
            }

            var candidate = searchIndex + match.Index;
            if (MatchesBoundaryRequirements(plan, input, candidate, match.Length))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchIndex = candidate + 1;
        }

        return -1;
    }

    private static int FindAnyIgnoreCaseLiteralWithBoundaries(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var literalSearch = plan.AlternateIgnoreCaseLiteralSearch;
        if (!literalSearch.HasValue || startIndex > input.Length)
        {
            return -1;
        }

        var searchIndex = startIndex;
        while (searchIndex <= input.Length - literalSearch.Value.ShortestLength)
        {
            if (!plan.PreparedSearcher.TryFindFirstMatch(input[searchIndex..], out var match))
            {
                return -1;
            }

            var candidate = searchIndex + match.Index;
            if (MatchesBoundaryRequirements(plan, input, candidate, match.Length))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchIndex = candidate + 1;
        }

        return -1;
    }

    private static int FindAnyLiteralWithTrailingRequirement(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var literalSearch = plan.AlternateLiteralSearch;
        if (!literalSearch.HasValue || startIndex > input.Length)
        {
            return -1;
        }

        if (!plan.HasTrailingLiteralRequirement)
        {
            var relative = plan.PreparedSearcher.FindFirst(input[startIndex..]);
            return relative < 0 ? -1 : startIndex + relative;
        }

        var searchIndex = startIndex;
        while (searchIndex <= input.Length - literalSearch.Value.SearchData.ShortestLength)
        {
            if (!plan.PreparedSearcher.TryFindFirstMatch(input[searchIndex..], out var match))
            {
                return -1;
            }

            var candidate = searchIndex + match.Index;
            if (MatchesTrailingLiteralRequirement(plan, input, candidate + match.Length))
            {
                return candidate;
            }

            searchIndex = candidate + 1;
        }

        return -1;
    }

    private static int FindLastAnyLiteralWithBoundaries(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var literalSearch = plan.AlternateLiteralSearch;
        if (!literalSearch.HasValue)
        {
            return -1;
        }

        var searchLength = Math.Min(startIndex, input.Length);
        while (searchLength >= literalSearch.Value.SearchData.ShortestLength)
        {
            if (!plan.PreparedSearcher.TryFindLastMatch(input[..searchLength], out var match))
            {
                return -1;
            }

            var candidate = match.Index;
            if (MatchesBoundaryRequirements(plan, input, candidate, match.Length))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchLength = candidate;
        }

        return -1;
    }

    private static int FindLastAnyIgnoreCaseLiteralWithBoundaries(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var literalSearch = plan.AlternateIgnoreCaseLiteralSearch;
        if (!literalSearch.HasValue)
        {
            return -1;
        }

        var searchLength = Math.Min(startIndex, input.Length);
        while (searchLength >= literalSearch.Value.ShortestLength)
        {
            if (!plan.PreparedSearcher.TryFindLastMatch(input[..searchLength], out var match))
            {
                return -1;
            }

            var candidate = match.Index;
            if (MatchesBoundaryRequirements(plan, input, candidate, match.Length))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchLength = candidate;
        }

        return -1;
    }

    private static int FindLastAnyLiteralWithTrailingRequirement(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var literalSearch = plan.AlternateLiteralSearch;
        if (!literalSearch.HasValue)
        {
            return -1;
        }

        if (!plan.HasTrailingLiteralRequirement)
        {
            return plan.PreparedSearcher.FindLast(input[..Math.Min(startIndex, input.Length)]);
        }

        var searchLength = Math.Min(startIndex, input.Length);
        while (searchLength >= literalSearch.Value.SearchData.ShortestLength)
        {
            if (!plan.PreparedSearcher.TryFindLastMatch(input[..searchLength], out var match))
            {
                return -1;
            }

            var candidate = match.Index;
            if (MatchesTrailingLiteralRequirement(plan, input, candidate + match.Length))
            {
                return candidate;
            }

            searchLength = candidate;
        }

        return -1;
    }

    private static int FindFixedDistanceLiteral(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var literal = plan.LiteralUtf8;
        if (literal is null)
        {
            return -1;
        }

        var searchIndex = startIndex + plan.Distance;
        if (searchIndex < 0 || searchIndex > input.Length)
        {
            return -1;
        }

        var relative = Utf8SearchKernel.IndexOfLiteral(input[searchIndex..], literal, ignoreCase: false);
        while (relative >= 0)
        {
            var absolute = searchIndex + relative;
            var candidate = absolute - plan.Distance;
            if (candidate >= startIndex &&
                candidate >= 0 &&
                candidate <= input.Length - Math.Max(plan.MinRequiredLength, literal.Length))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchIndex = absolute + 1;
            if (searchIndex > input.Length)
            {
                break;
            }

            relative = Utf8SearchKernel.IndexOfLiteral(input[searchIndex..], literal, ignoreCase: false);
        }

        return -1;
    }

    private static int FindFixedDistanceChar(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var literal = plan.LiteralUtf8;
        if (literal is not { Length: 1 })
        {
            return -1;
        }

        var searchIndex = startIndex + plan.Distance;
        if (searchIndex < 0 || searchIndex > input.Length)
        {
            return -1;
        }

        var relative = input[searchIndex..].IndexOf(literal[0]);
        while (relative >= 0)
        {
            var absolute = searchIndex + relative;
            var candidate = absolute - plan.Distance;
            if (candidate >= startIndex &&
                candidate >= 0 &&
                candidate <= input.Length - Math.Max(plan.MinRequiredLength, 1))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchIndex = absolute + 1;
            if (searchIndex > input.Length)
            {
                break;
            }

            relative = input[searchIndex..].IndexOf(literal[0]);
        }

        return -1;
    }

    private static int FindTrailingAnchorFixedLengthEnd(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var candidate = input.Length - plan.MinRequiredLength;
        return candidate >= startIndex && candidate >= 0 ? candidate : -1;
    }

    private static int FindTrailingAnchorFixedLengthEndZ(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var candidate = input.Length - plan.MinRequiredLength;
        if (candidate >= startIndex && candidate >= 0)
        {
            return candidate;
        }

        if (input.Length > 0 && input[^1] == (byte)'\n')
        {
            candidate = input.Length - plan.MinRequiredLength - 1;
            if (candidate >= startIndex && candidate >= 0)
            {
                return candidate;
            }
        }

        return -1;
    }

    private static int FindFixedDistanceSets(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        var sets = plan.FixedDistanceSets;
        if (sets is not { Length: > 0 })
        {
            return -1;
        }

        var primary = sets[0];
        var searchIndex = startIndex + primary.Distance;
        if (searchIndex < 0 || searchIndex > input.Length)
        {
            return -1;
        }

        while (true)
        {
            var absolute = FindNextSetMatch(input, primary, searchIndex);
            if (absolute < 0)
            {
                return -1;
            }

            var candidate = absolute - primary.Distance;
            if (candidate >= startIndex &&
                candidate >= 0 &&
                candidate <= input.Length - Math.Max(1, plan.MinRequiredLength) &&
                MatchesRemainingSets(input, sets, candidate))
            {
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return candidate;
            }

            searchIndex = absolute + 1;
            if (searchIndex > input.Length)
            {
                return -1;
            }
        }
    }

    private static int FindNextSetMatch(ReadOnlySpan<byte> input, Utf8FixedDistanceSet set, int startIndex)
    {
        if ((uint)startIndex > (uint)input.Length)
        {
            return -1;
        }

        var span = input[startIndex..];
        var relative = span.IndexOfAny(set.SearchValues);
        return relative < 0 ? -1 : startIndex + relative;
    }

    private static bool MatchesRemainingSets(ReadOnlySpan<byte> input, Utf8FixedDistanceSet[] sets, int candidate)
    {
        for (var i = 1; i < sets.Length; i++)
        {
            var set = sets[i];
            var index = candidate + set.Distance;
            if ((uint)index >= (uint)input.Length || !set.Contains(input[index]))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool MatchesBoundaryRequirements(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, int literalLength)
    {
        return MatchesBoundaryRequirement(plan.LeadingBoundary, input, startIndex) &&
            MatchesTrailingLiteralRequirement(plan, input, startIndex + literalLength) &&
            MatchesBoundaryRequirement(plan.TrailingBoundary, input, startIndex + literalLength);
    }

    private static bool MatchesTrailingLiteralRequirement(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int byteOffset)
    {
        var trailingLiteral = plan.TrailingLiteralUtf8;
        return trailingLiteral is null || input[byteOffset..].StartsWith(trailingLiteral);
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
        return DotNetUtf8WordBoundary.IsBoundary(input, byteOffset);
    }
}
