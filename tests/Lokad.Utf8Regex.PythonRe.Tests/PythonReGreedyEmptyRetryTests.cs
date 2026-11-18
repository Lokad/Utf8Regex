using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReGreedyEmptyRetryTests
{
    [Theory]
    [InlineData("x*", "xé中😀yx", 0)]
    [InlineData("x*", "xé中😀yx", 1)]
    [InlineData("x{0,2}", "xxxé😀y", 0)]
    [InlineData("[a-z]*", "aé中😀b", 0)]
    [InlineData(@"\d*", "1é中😀2", 0)]
    [InlineData(@"\w*", "a😀 b", 0)]
    [InlineData("[^x]*", "xé中😀x", 0)]
    [InlineData(".*", "\n😀\n", 0)]
    [InlineData("x*", "", 0)]
    [InlineData("x*", "😀", 4)]
    [InlineData("x*?", "xé😀", 0)]
    [InlineData("(?:x*|é)", "é😀", 0)]
    [InlineData("(x*)", "xé中😀yx", 0)]
    [InlineData("x*(?=y)|é", "éxy😀", 0)]
    public void GlobalOperationsKeepEmptyRetriesAndScalarRanges(string pattern, string text, int start)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = Encoding.UTF8.GetBytes(text);
        // The empty assertion preserves the language while keeping the reference
        // on the general retry path, including its distinct assertion workspace.
        var reference = new Utf8PythonRegex("(?=)(?:" + pattern + ")");
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = reference.CreateMatchCursor(input, start, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var expected = collectedRanges1.ToArray();
        using var token = new CancellationTokenSource();
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000,MaxDepth = 1024,            MaxWorkingBytes = 1 << 20,MaxResultBytes = 1 << 20        } })
        {
            var regex = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = policy
            });
            var collectedRanges2 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor2 = regex.CreateMatchCursor(input, start, token.Token))
            {
                while (rangeCursor2.MoveNext(out var rangeMatch2))
                    collectedRanges2.Add(rangeMatch2);
            }
            var actual = collectedRanges2.ToArray();
            Assert.Equal(expected.Select(m => m.Value), actual.Select(m => m.Value));
            Assert.Equal(expected.Length, regex.Count(input, start, token.Token));
            ReadOnlyMemory<byte> snapshots1Input = input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = reference.CreateMatchCursor(snapshots1Input, start, default))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            ReadOnlyMemory<byte> snapshots2Input = input;
            var snapshots2 = new List<Utf8PythonRangeMatch>();
            using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, start, token.Token))
            {
                while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                    snapshots2.Add(snapshots2Match);
            }
            Assert.Equal(snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray(),
                            snapshots2.Select(captureSnapshot2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
            ReadOnlyMemory<byte> snapshots3Input = input;
            var snapshots3 = new List<Utf8PythonRangeMatch>();
            using (var snapshots3Cursor = reference.CreateMatchCursor(snapshots3Input, start, default))
            {
                while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                    snapshots3.Add(snapshots3Match);
            }
            ReadOnlyMemory<byte> snapshots4Input = input;
            var snapshots4 = new List<Utf8PythonRangeMatch>();
            using (var snapshots4Cursor = regex.CreateMatchCursor(snapshots4Input, start, token.Token))
            {
                while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                    snapshots4.Add(snapshots4Match);
            }
            Assert.Equal(snapshots3.Select(captureSnapshot3 => captureSnapshot3.GetGroupMemory(captureSnapshot3.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Convert.ToHexString),
                            snapshots4.Select(captureSnapshot4 => captureSnapshot4.GetGroupMemory(captureSnapshot4.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Convert.ToHexString));
            Assert.Equal(PythonReSplitAssert.Decode(input, reference.SplitRanges(input, 0, start, default)), PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, start, default)));
            Assert.Equal(System.Text.Encoding.UTF8.GetString(reference.Replace(input, "!"u8, 0, start)), System.Text.Encoding.UTF8.GetString(regex.Replace(input, "!"u8, 0, start)));

            using var cursor = regex.CreateMatchCursor(input, start);
            var alias = cursor;
            Utf8PythonRangeMatch? first = null;
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.True(alias.MoveNext(out cursorRange));
                var match = cursorRange;
                first ??= match;
                PythonReCaptureAssert.Equal(expected[i], match);
            }
            Assert.False(cursor.MoveNext(out cursorRange));
            Assert.False(alias.MoveNext(out cursorRange));
            if (first is { } retained)
            {
                PythonReCaptureAssert.Equal(expected[0], retained);
            }
        }
    }
}
