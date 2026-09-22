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
