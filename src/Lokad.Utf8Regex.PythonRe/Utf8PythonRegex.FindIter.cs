using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: iteration entry points, split from the Python facade along the iteration ownership boundary.
/// <summary>Iteration entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Materializes detailed data for every non-overlapping match.</summary>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input) => FindIterDetailed(input, 0);

    /// <summary>Materializes detailed matches after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (!_canMatchEmpty)
        {
            if (TryFindIterDetailedOwned(input, Decode(input), GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes), out var owned))
            {
                return owned;
            }

            return FindIterDetailedNonEmpty(input, startOffsetInBytes);
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var indexMap = default(PythonReUtf8IndexMap);
        var hasIndexMap = false;
        List<Utf8PythonDetailedMatchData> matches = [];
        var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
        while (cursor.TryMoveNext(out var match))
        {
            if (!hasIndexMap)
            {
                indexMap = PythonReUtf8IndexMap.Create(input, subject);
                hasIndexMap = true;
            }

            matches.Add(CreateDetailedMatchData(input, match, indexMap));
        }

        return matches.ToArray();
    }

    // R04: literal shapes iterate on the shared owned cursor with branch-indexed group spans
    // projected to Python slots; the first hit validates the branch channel before any output,
    // so an unknown branch still declines cleanly to the managed routes.
    private bool TryFindIterDetailedOwned(
        ReadOnlySpan<byte> input,
        string subject,
        int startOffsetInUtf16,
        out Utf8PythonDetailedMatchData[] matches)
    {
        matches = [];
        if (_utf8Regex is null || _canMatchEmpty)
        {
            return false;
        }

        if (!_utf8Regex.TryCreateOwnedSplitCursor(input, out var cursor, out var map) || map is null)
        {
            return false;
        }

        var list = new List<Utf8PythonDetailedMatchData>();
        var indexMap = default(PythonReUtf8IndexMap);
        var hasIndexMap = false;
        var first = true;
        try
        {
            while (cursor.MoveNext())
            {
                var match = cursor.CurrentValueMatch;
                if (!match.Success || match.IndexInUtf16 < startOffsetInUtf16)
                {
                    continue;
                }

                var branch = cursor.LastLiteralFamilyBranchId;
                if (first)
                {
                    first = false;
                    if (branch < 0 || (uint)branch >= (uint)map.BranchGroups.Length)
                    {
                        return false;
                    }
                }

                if (!hasIndexMap)
                {
                    indexMap = PythonReUtf8IndexMap.Create(input, subject);
                    hasIndexMap = true;
                }

                list.Add(CreateOwnedDetailedMatchData(input, indexMap, match, map, branch));
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(subject, _translation.Pattern, MatchTimeout);
        }

        matches = list.ToArray();
        return true;
    }

    private Utf8PythonDetailedMatchData CreateOwnedDetailedMatchData(
        ReadOnlySpan<byte> input,
        PythonReUtf8IndexMap indexMap,
        Utf8ValueMatch match,
        Utf8LiteralFamilyCaptureMap map,
        int branch)
    {
        var groups = new PythonReGroupData[_managedGroupNumbersByPythonGroup.Length];
        groups[0] = OwnedSpanGroupData(indexMap, 0, match.IndexInUtf16, match.LengthInUtf16);
        for (var pythonGroup = 1; pythonGroup < groups.Length; pythonGroup++)
        {
            var managedGroup = _managedGroupNumbersByPythonGroup[pythonGroup];
            var matched = Array.IndexOf(map.WholeMatchGroups, managedGroup) >= 0 ||
                Array.IndexOf(map.BranchGroups[branch], managedGroup) >= 0;
            groups[pythonGroup] = matched
                ? OwnedSpanGroupData(indexMap, pythonGroup, match.IndexInUtf16, match.LengthInUtf16)
                : new PythonReGroupData { Number = pythonGroup, Success = false };
        }

        return CreateDetailedMatchData(input, groups, _nameEntries);
    }

    private static PythonReGroupData OwnedSpanGroupData(PythonReUtf8IndexMap indexMap, int number, int startOffsetInUtf16, int lengthInUtf16)
    {
        var endOffsetInUtf16 = startOffsetInUtf16 + lengthInUtf16;
        return new PythonReGroupData
        {
            Number = number,
            Success = true,
            StartOffsetInBytes = indexMap.GetByteOffset(startOffsetInUtf16),
            EndOffsetInBytes = indexMap.GetByteOffset(endOffsetInUtf16),
            StartOffsetInUtf16 = startOffsetInUtf16,
            EndOffsetInUtf16 = endOffsetInUtf16,
        };
    }

    private Utf8PythonDetailedMatchData[] FindIterDetailedNonEmpty(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var firstMatch = _managedRegex.Value.Match(subject, startOffsetInUtf16);
        if (!firstMatch.Success)
        {
            return [];
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        List<Utf8PythonDetailedMatchData> matches = [];
        for (var match = firstMatch; match.Success; match = match.NextMatch())
        {
            matches.Add(CreateDetailedMatchData(input, match, indexMap));
        }

        return matches.ToArray();
    }
}
