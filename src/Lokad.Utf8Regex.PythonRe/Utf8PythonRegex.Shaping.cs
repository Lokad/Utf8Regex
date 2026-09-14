using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: match shaping helpers, split from the Python facade along the shaping ownership boundary.
/// <summary>Match shaping helpers for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    private Utf8PythonMatchContext CreateMatchContext(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap)
    {
        if (!match.Success)
        {
            return default;
        }

        return CreateMatchSnapshot(match, indexMap, 0).ToContext(input, _nameEntries);
    }

    private Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap)
    {
        if (!match.Success)
        {
            return default;
        }

        return CreateDetailedMatchData(input, match, indexMap, _nameEntries, 0);
    }

    private Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap,
        PythonReNameEntry[] nameEntries) => CreateDetailedMatchData(input, match, indexMap, nameEntries, 0);

    private Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap,
        PythonReNameEntry[] nameEntries,
        int utf16BaseOffset)
    {
        if (!match.Success)
        {
            return default;
        }

        // PLAN.md C07: project Python slots; managed order coincides only when the permutation is the identity.
        var projectedGroups = new Utf8PythonGroupMatchData[_managedGroupNumbersByPythonGroup.Length];
        for (var pythonGroup = 0; pythonGroup < projectedGroups.Length; pythonGroup++)
        {
            projectedGroups[pythonGroup] = CreateGroupMatchData(
                input,
                PythonReGroupData.FromUtf16(pythonGroup, match.Groups[_managedGroupNumbersByPythonGroup[pythonGroup]], indexMap, utf16BaseOffset));
        }

        return new Utf8PythonDetailedMatchData
        {
            Groups = projectedGroups,
            NameEntries = nameEntries is { Length: > 0 } ? (PythonReNameEntry[])nameEntries.Clone() : nameEntries,
        };
    }

    private Utf8PythonMatchContext CreateMatchContextFromUtf8(
        ReadOnlySpan<byte> input,
        Utf8MatchContext match)
    {
        if (!match.Success)
        {
            return default;
        }

        // PLAN.md C07: the native engine numbers like managed (.NET order); project Python slots.
        var groups = new PythonReGroupData[_managedGroupNumbersByPythonGroup.Length];
        for (var pythonGroup = 0; pythonGroup < groups.Length; pythonGroup++)
        {
            groups[pythonGroup] = PythonReGroupData.FromUtf8Group(pythonGroup, match.GetGroup(_managedGroupNumbersByPythonGroup[pythonGroup]));
        }

        return new Utf8PythonMatchContext(input, groups, _nameEntries);
    }

    private Utf8PythonDetailedMatchData CreateDetailedMatchDataFromUtf8(
        ReadOnlySpan<byte> input,
        Utf8MatchContext match)
    {
        if (!match.Success)
        {
            return default;
        }

        // PLAN.md C07: the native engine numbers like managed (.NET order); project Python slots.
        var groups = new PythonReGroupData[_managedGroupNumbersByPythonGroup.Length];
        for (var pythonGroup = 0; pythonGroup < groups.Length; pythonGroup++)
        {
            groups[pythonGroup] = PythonReGroupData.FromUtf8Group(pythonGroup, match.GetGroup(_managedGroupNumbersByPythonGroup[pythonGroup]));
        }

        return CreateDetailedMatchData(input, groups, _nameEntries);
    }

    private PythonReManagedMatchSnapshot CreateMatchSnapshot(
        Match match,
        PythonReUtf8IndexMap indexMap) => CreateMatchSnapshot(match, indexMap, 0);

    private PythonReManagedMatchSnapshot CreateMatchSnapshot(
        Match match,
        PythonReUtf8IndexMap indexMap,
        int utf16BaseOffset)
    {
        // PLAN.md C07: snapshot Python slots for every consumer below.
        var groups = new PythonReGroupData[_managedGroupNumbersByPythonGroup.Length];
        for (var pythonGroup = 0; pythonGroup < groups.Length; pythonGroup++)
        {
            groups[pythonGroup] = PythonReGroupData.FromUtf16(pythonGroup, match.Groups[_managedGroupNumbersByPythonGroup[pythonGroup]], indexMap, utf16BaseOffset);
        }

        return new PythonReManagedMatchSnapshot(groups);
    }

    private static Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        PythonReGroupData[]? groups,
        PythonReNameEntry[]? nameEntries)
    {
        if (groups is null)
        {
            return default;
        }

        var projectedGroups = new Utf8PythonGroupMatchData[groups.Length];
        for (var i = 0; i < groups.Length; i++)
        {
            projectedGroups[i] = CreateGroupMatchData(input, groups[i]);
        }

        return new Utf8PythonDetailedMatchData
        {
            Groups = projectedGroups,
            NameEntries = nameEntries is { Length: > 0 } ? (PythonReNameEntry[])nameEntries.Clone() : (nameEntries ?? []),
        };
    }

    private static Utf8PythonValueMatch Utf8PythonValueMatchFromUtf8Regex(
        ReadOnlySpan<byte> input,
        Utf8ValueMatch match)
    {
        if (!match.Success)
        {
            return default;
        }

        return Utf8PythonValueMatch.Create(input, PythonReGroupData.FromUtf8Match(match));
    }

    private void AppendSplitMatch(
        List<string?> parts,
        string subject,
        Match match,
        ref int lastIndex,
        int utf16BaseOffset)
    {
        var absoluteStart = utf16BaseOffset + match.Index;
        var absoluteEnd = absoluteStart + match.Length;
        parts.Add(subject[lastIndex..absoluteStart]);
        // PLAN.md C07: split captures follow Python slot order.
        for (var pythonGroup = 1; pythonGroup < _managedGroupNumbersByPythonGroup.Length; pythonGroup++)
        {
            var managedSplitGroup = match.Groups[_managedGroupNumbersByPythonGroup[pythonGroup]];
            parts.Add(managedSplitGroup.Success ? managedSplitGroup.Value : null);
        }

        lastIndex = absoluteEnd;
    }

    private void AppendSplitDetailedMatch(
        List<Utf8PythonSplitItem> parts,
        string subject,
        Match match,
        ref int lastIndex,
        int utf16BaseOffset)
    {
        var absoluteStart = utf16BaseOffset + match.Index;
        var absoluteEnd = absoluteStart + match.Length;
        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = subject[lastIndex..absoluteStart],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        // PLAN.md C07: split captures follow Python slot order.
        for (var pythonGroup = 1; pythonGroup < _managedGroupNumbersByPythonGroup.Length; pythonGroup++)
        {
            parts.Add(new Utf8PythonSplitItem
            {
                ValueText = match.Groups[_managedGroupNumbersByPythonGroup[pythonGroup]].Success ? match.Groups[_managedGroupNumbersByPythonGroup[pythonGroup]].Value : null,
                IsCapture = true,
                CaptureGroupNumber = pythonGroup,
            });
        }

        lastIndex = absoluteEnd;
    }

    private string GetFindAllGroupValue(Match match, int groupNumber)
    {
        var managedGroupNumber = _managedGroupNumbersByPythonGroup[groupNumber];
        return match.Groups[managedGroupNumber].Success
            ? match.Groups[managedGroupNumber].Value
            : string.Empty;
    }

    private static byte[] GetFindAllGroupBytes(ReadOnlySpan<byte> input, PythonReGroupData[] groups, int groupNumber)
    {
        return (uint)groupNumber < (uint)groups.Length && groups[groupNumber].Success
            ? PythonReValueTextExtractor.GetValueBytes(input, groups[groupNumber])
            : [];
    }

    private static Utf8PythonGroupMatchData CreateGroupMatchData(ReadOnlySpan<byte> input, PythonReGroupData group)
    {
        return new Utf8PythonGroupMatchData
        {
            Number = group.Number,
            Success = group.Success,
            StartOffsetInBytes = group.Success ? group.StartOffsetInBytes : 0,
            EndOffsetInBytes = group.Success ? group.EndOffsetInBytes : 0,
            StartOffsetInUtf16 = group.Success ? group.StartOffsetInUtf16 : 0,
            EndOffsetInUtf16 = group.Success ? group.EndOffsetInUtf16 : 0,
            HasContiguousByteRange = group.HasContiguousByteRange,
            ValueText = PythonReValueTextExtractor.GetValueString(input, group),
        };
    }
}
