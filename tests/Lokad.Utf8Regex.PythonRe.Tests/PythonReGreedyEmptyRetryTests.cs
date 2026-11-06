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
        var input = Encoding.UTF8.GetBytes(text);
        // The empty assertion preserves the language while keeping the reference
        // on the general retry path, including its distinct assertion workspace.
        var reference = new Utf8PythonRegex("(?=)(?:" + pattern + ")");
        var expected = reference.FindIterDetailed(input, start);
        using var token = new CancellationTokenSource();
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 1024,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
        } })
        {
            var regex = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan, policy);
            var actual = regex.FindIterDetailed(input, start, token.Token);
            Assert.Equal(expected.Select(m => m.Value), actual.Select(m => m.Value));
            Assert.Equal(expected.Length, regex.Count(input, start, token.Token));
            Assert.Equal(reference.FindAllToStrings(input, start).ScalarValues,
                regex.FindAllToStrings(input, start, token.Token).ScalarValues);
            Assert.Equal(reference.FindAllToUtf8(input, start).ScalarValues.Select(Convert.ToHexString),
                regex.FindAllToUtf8(input, start, token.Token).ScalarValues.Select(Convert.ToHexString));
            Assert.Equal(reference.SplitToStrings(input, 0, start), regex.SplitToStrings(input, 0, start));
            Assert.Equal(reference.ReplaceToString(input, "!", 0, start), regex.ReplaceToString(input, "!", 0, start));

            using var cursor = regex.CreateMatchCursor(input, start);
            var alias = cursor;
            Utf8PythonDetailedMatchData? first = null;
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.True(alias.MoveNext());
                var match = cursor.Current;
                first ??= match;
                Assert.Equal(expected[i].Value, match.Value);
            }
            Assert.False(cursor.MoveNext());
            Assert.False(alias.MoveNext());
            if (first is { } retained) Assert.Equal(expected[0].Value, retained.Value);
        }
    }
}
