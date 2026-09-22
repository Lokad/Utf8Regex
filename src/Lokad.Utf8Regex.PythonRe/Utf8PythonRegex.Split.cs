using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: split entry points, split from the Python facade along the split ownership boundary.
/// <summary>Split entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Splits the complete input with unlimited Python-style splits.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input) => SplitToStrings(input, 0, 0);

    /// <summary>Splits the input at most the requested number of times.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input, int maxSplit) =>
        SplitToStrings(input, maxSplit, 0);

    /// <summary>Splits after a zero-based UTF-8 byte offset and returns strings and captures.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        if (_canCountAsciiWordBoundariesDirectly &&
            MatchTimeout == Timeout.InfiniteTimeSpan &&
            startOffsetInBytes == 0 &&
            maxSplit >= 0)
        {
            return SplitAsciiWordBoundaries(subject, maxSplit);
        }

        // Regex.Split advances empty matches by UTF-16 code unit, whereas Python advances by scalar.
        if (_groupNumberingIsIdentity &&
            _canUseManagedSplitFastPath &&
            (!_canMatchEmpty || input.IndexOfAnyInRange((byte)0xF0, (byte)0xF4) < 0) &&
            startOffsetInBytes == 0 &&
            maxSplit >= 0)
        {
            return maxSplit is 0 or int.MaxValue
                ? _managedRegex.Value.Split(subject)
                : _managedRegex.Value.Split(subject, maxSplit + 1);
        }

        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var parts = new List<string?>();
        var lastIndex = startOffsetInUtf16;
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            var splitCount = 0;
            while ((maxSplit == 0 || splitCount < maxSplit) && cursor.TryMoveNext(out var match))
            {
                AppendSplitMatch(parts, subject, match, ref lastIndex, 0);
                splitCount++;
            }

        parts.Add(subject[lastIndex..]);
        return parts.ToArray();
    }

    private static string?[] SplitAsciiWordBoundaries(string subject, int maxSplit)
    {
        var parts = new List<string?>();
        var lastIndex = 0;
        var splitCount = 0;
        var leftIsWord = false;
        for (var position = 0; position <= subject.Length; position++)
        {
            var rightIsWord = position < subject.Length && IsAsciiWord(subject[position]);
            if (leftIsWord != rightIsWord)
            {
                parts.Add(subject[lastIndex..position]);
                lastIndex = position;
                splitCount++;
                if (maxSplit > 0 && splitCount >= maxSplit)
                {
                    break;
                }
            }

            leftIsWord = rightIsWord;
        }

        parts.Add(subject[lastIndex..]);
        return parts.ToArray();
    }

    /// <summary>Splits the complete input and describes values and captured separators.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input) => SplitDetailed(input, 0, 0);

    /// <summary>Performs a bounded detailed split.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit) =>
        SplitDetailed(input, maxSplit, 0);

    /// <summary>Performs a detailed split after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        if (_canCountAsciiWordBoundariesDirectly &&
            MatchTimeout == Timeout.InfiniteTimeSpan &&
            startOffsetInBytes == 0 &&
            maxSplit >= 0)
        {
            var values = SplitAsciiWordBoundaries(subject, maxSplit);
            var items = new Utf8PythonSplitItem[values.Length];
            for (var index = 0; index < items.Length; index++)
            {
                items[index] = new Utf8PythonSplitItem
                {
                    ValueText = values[index],
                    IsCapture = false,
                    CaptureGroupNumber = 0,
                };
            }

            return items;
        }

        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var parts = new List<Utf8PythonSplitItem>();
        var lastIndex = startOffsetInUtf16;
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            var splitCount = 0;
            while ((maxSplit == 0 || splitCount < maxSplit) && cursor.TryMoveNext(out var match))
            {
                AppendSplitDetailedMatch(parts, subject, match, ref lastIndex, 0);
                splitCount++;
            }

        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = subject[lastIndex..],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        return parts.ToArray();
    }
}
