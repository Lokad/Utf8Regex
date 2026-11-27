using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SearchExecutor
{
    public static bool TryFindNextMatch(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, out PreparedSearchMatch match)
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
            Utf8SearchKind.AsciiFoldedByteLiterals when plan.HasPreparedSearcher => TryFindNextPreparedMatch(plan, input, startIndex, out match),
            Utf8SearchKind.ExactUtf8Literals when plan.HasPreparedSearcher => TryFindNextPreparedMatch(plan, input, startIndex, out match),
            _ => TryFindFallbackMatch(plan, input, startIndex, reverse: false, out match),
        };
    }

    public static int FindFirst(in Utf8SearchPlan plan, ReadOnlySpan<byte> input)
    {
        var literal = plan.LiteralUtf8;
        return plan.Kind switch
        {
            Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.AsciiFoldedByteLiteral when literal is not null => FindLiteralStart(plan, input, 0, reverse: false),
            Utf8SearchKind.ExactAsciiLiterals when plan.HasPreparedSearcher => plan.PreparedSearcher.FindFirst(input),
            Utf8SearchKind.AsciiFoldedByteLiterals when plan.HasPreparedSearcher => plan.HasBoundaryRequirements
                ? FindAnyIgnoreCaseLiteralWithBoundaries(plan, input, 0)
                : plan.PreparedSearcher.FindFirst(input),
            Utf8SearchKind.ExactUtf8Literals when plan.HasPreparedSearcher => plan.HasBoundaryRequirements
                ? FindAnyLiteralWithBoundaries(plan, input, 0)
                : FindAnyLiteralWithTrailingRequirement(plan, input, 0),
            _ => -1,
        };
    }

    private static int FindNext(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
    {
        if (plan.Kind is Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.AsciiFoldedByteLiteral)
            return FindLiteralStart(plan, input, startIndex, reverse: false);
        var relative = FindFirst(plan, input[startIndex..]);
        return relative < 0 ? -1 : startIndex + relative;
    }

    public static int FindLast(in Utf8SearchPlan plan, ReadOnlySpan<byte> input)
    {
        var literal = plan.LiteralUtf8;
        return plan.Kind switch
        {
            Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.AsciiFoldedByteLiteral when literal is not null => FindLiteralStart(plan, input, input.Length, reverse: true),
            Utf8SearchKind.ExactAsciiLiterals when plan.HasPreparedSearcher => plan.PreparedSearcher.FindLast(input),
            Utf8SearchKind.AsciiFoldedByteLiterals when plan.HasPreparedSearcher => plan.HasBoundaryRequirements
                ? FindLastAnyIgnoreCaseLiteralWithBoundaries(plan, input, input.Length)
                : plan.PreparedSearcher.FindLast(input),
            Utf8SearchKind.ExactUtf8Literals when plan.HasPreparedSearcher => plan.HasBoundaryRequirements
                ? FindLastAnyLiteralWithBoundaries(plan, input, input.Length)
                : FindLastAnyLiteralWithTrailingRequirement(plan, input, input.Length),
            _ => -1,
        };
    }

    public static bool TryFindLastMatch(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, out PreparedSearchMatch match)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        if (startIndex > input.Length)
        {
            startIndex = input.Length;
        }

        return plan.Kind switch
        {
            Utf8SearchKind.ExactAsciiLiterals when plan.HasPreparedSearcher => TryFindLastPreparedMatch(plan, input, startIndex, out match),
            Utf8SearchKind.AsciiFoldedByteLiterals when plan.HasPreparedSearcher => TryFindLastPreparedMatch(plan, input, startIndex, out match),
            Utf8SearchKind.ExactUtf8Literals when plan.HasPreparedSearcher => TryFindLastPreparedMatch(plan, input, startIndex, out match),
            _ => TryFindFallbackMatch(plan, input, startIndex, reverse: true, out match),
        };
    }

    private static int FindLiteralStart(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int index, bool reverse)
    {
        var start = reverse ? 0 : index;
        var end = reverse ? Math.Min(index, input.Length) : input.Length;
        if (!plan.HasBoundaryRequirements && !plan.HasTrailingLiteralRequirement)
        {
            if (reverse)
                return plan.PreparedSearcher.FindLast(input[..end]);
            var relative = plan.PreparedSearcher.FindFirst(input[start..]);
            return relative < 0 ? -1 : start + relative;
        }

        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Utf8ExecutionDeadline.Infinite, CancellationToken.None);
        var breach = TryFindLiteral(plan, input, start, end, reverse, ref invocation, out var matchIndex);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
        return matchIndex;
    }

    private static bool TryFindNextPreparedMatch(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, out PreparedSearchMatch match)
    {
        if (plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement)
            return TryFindConfirmedFamilyMatch(plan, input, startIndex, input.Length, reverse: false, out match);

        if (plan.PreparedSearcher.TryFindFirstMatch(input[startIndex..], out var relative))
            return TryTranslateMatch(relative, startIndex, out match);

        match = default;
        return false;
    }

    private static bool TryFindLastPreparedMatch(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int endIndex, out PreparedSearchMatch match)
    {
        if (plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement)
            return TryFindConfirmedFamilyMatch(plan, input, 0, endIndex, reverse: true, out match);

        return plan.PreparedSearcher.TryFindLastMatch(input[..endIndex], out match);
    }

    private static bool TryFindConfirmedFamilyMatch(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int start, int end,
        bool reverse, out PreparedSearchMatch match)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Utf8ExecutionDeadline.Infinite, CancellationToken.None);
        var breach = TryFindLiteralFamily(plan, input, start, end, reverse, ref invocation, out match, out var found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal-family execution exceeded {breach}.");
        return found;
    }

    private static bool TryFindFallbackMatch(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, bool reverse, out PreparedSearchMatch match)
    {
        var index = reverse ? FindLast(plan, input[..startIndex]) : FindNext(plan, input, startIndex);
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

    private static int FindAnyLiteralWithBoundaries(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
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

    private static int FindAnyIgnoreCaseLiteralWithBoundaries(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
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

    private static int FindAnyLiteralWithTrailingRequirement(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
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

    private static int FindLastAnyLiteralWithBoundaries(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
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

    private static int FindLastAnyIgnoreCaseLiteralWithBoundaries(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
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

    private static int FindLastAnyLiteralWithTrailingRequirement(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex)
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

    internal static bool MatchesBoundaryRequirements(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, int literalLength)
    {
        return Utf8ScalarWordBoundary.MatchesRequirement(plan.LeadingBoundary, input, startIndex) &&
            MatchesTrailingLiteralRequirement(in plan, input, startIndex + literalLength) &&
            Utf8ScalarWordBoundary.MatchesRequirement(plan.TrailingBoundary, input, startIndex + literalLength);
    }

    private static bool MatchesTrailingLiteralRequirement(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int byteOffset)
    {
        var trailingLiteral = plan.TrailingLiteralUtf8;
        return trailingLiteral is null || input[byteOffset..].StartsWith(trailingLiteral);
    }
}
