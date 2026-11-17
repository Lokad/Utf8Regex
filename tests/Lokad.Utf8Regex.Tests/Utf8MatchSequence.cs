namespace Lokad.Utf8Regex.Tests;

// Assertions and aggregation over production byte cursors.
internal static class Utf8MatchSequence
{
    internal static Utf8RangeMatch First(Utf8RegexMatchCursor cursor)
    {
        using (cursor)
        {
            cursor.MoveNext(out var match);
            return match;
        }
    }

    internal static void AssertCaptures(System.Text.RegularExpressions.Match expected,
        Utf8RangeMatch actual, byte[] input, string text) =>
        AssertCaptures(expected, actual, input,
            index => System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, index)));

    // Only expected oracle coordinates are translated. Actual results stay bytes.
    internal static void AssertCaptures(System.Text.RegularExpressions.Match expected,
        Utf8RangeMatch actual, byte[] input, Func<int, int> byteOffset)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal((byteOffset(expected.Index), byteOffset(expected.Index + expected.Length)),
            (actual.Value.StartOffsetInBytes, actual.Value.EndOffsetInBytes));
        if (!expected.Success) return;
        Assert.Equal(expected.Groups.Count, actual.GroupNumbers.Length);
        for (var ordinal = 0; ordinal < actual.GroupNumbers.Length; ordinal++)
        {
            var number = actual.GroupNumbers[ordinal];
            var group = expected.Groups[number];
            Assert.True(actual.TryGetGroup(number, out var range));
            Assert.Equal(group.Success, range.Success);
            var start = byteOffset(group.Index);
            var end = byteOffset(group.Index + group.Length);
            Assert.Equal((start, end), (range.StartOffsetInBytes, range.EndOffsetInBytes));
            Assert.Equal(input.AsSpan(start, end - start).ToArray(), actual.GetGroupMemory(number).ToArray());
            Assert.True(actual.TryGetGroup(actual.GroupNames[ordinal], out var named));
            Assert.Equal(range, named);
            var history = actual.GetCaptures(number);
            Assert.Equal(group.Captures.Count, history.Length);
            for (var index = 0; index < history.Length; index++)
            {
                var capture = group.Captures[index];
                Assert.True(history[index].Success);
                Assert.Equal((byteOffset(capture.Index), byteOffset(capture.Index + capture.Length)),
                    (history[index].StartOffsetInBytes, history[index].EndOffsetInBytes));
            }
        }
    }

    internal static int Count(Utf8ValueMatchEnumerator matches)
    {
        var count = 0;
        while (matches.MoveNext()) count = checked(count + 1);
        return count;
    }

    internal static void EqualCaptures(Utf8RangeMatch expected, Utf8RangeMatch actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.GetValueMemory().ToArray(), actual.GetValueMemory().ToArray());
        if (!expected.Success) return;
        Assert.Equal(expected.GroupNumbers.ToArray(), actual.GroupNumbers.ToArray());
        Assert.Equal(expected.GroupNames.ToArray(), actual.GroupNames.ToArray());
        foreach (var number in expected.GroupNumbers)
        {
            Assert.True(expected.TryGetGroup(number, out var wanted));
            Assert.True(actual.TryGetGroup(number, out var found));
            Assert.Equal(wanted, found);
            Assert.Equal(expected.GetGroupMemory(number).ToArray(), actual.GetGroupMemory(number).ToArray());
            Assert.Equal(expected.GetCaptures(number).ToArray(), actual.GetCaptures(number).ToArray());
        }
    }

    internal static void AssertRange(Utf8CaptureRange actual, string text, int index, int length)
    {
        Assert.True(actual.Success);
        Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, index)),
            System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, index + length))),
            (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
    }

    internal static void EqualValue(Utf8ValueMatch expected, Utf8CaptureRange actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Success ? (expected.IndexInBytes, expected.IndexInBytes + expected.LengthInBytes) : (0, 0),
            (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
    }
}
