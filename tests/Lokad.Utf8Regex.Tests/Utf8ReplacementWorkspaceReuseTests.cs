using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementWorkspaceReuseTests
{
    private static Utf8Regex Create(string pattern, RegexOptions options = RegexOptions.None, ulong working = 1 << 20) =>
        new(pattern, options | RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxWorkingBytes = working, MaxResultBytes = 1 << 20,
            });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedCapturesReleaseScratchBeforeFinalProjection(bool compiled)
    {
        var text = string.Concat(Enumerable.Repeat("dog fox bird ", 512));
        var input = Encoding.UTF8.GetBytes(text);
        var regex = Create("(dog)", compiled ? RegexOptions.Compiled : RegexOptions.None, 24000);
        Assert.Equal(text, regex.ReplaceToString(input, "$1"));
        Assert.Equal(input, regex.Replace(input, "$1"u8));
    }

    [Theory]
    [InlineData("(a|ab)+c", "zzabc -- ac -- aabc -- abc", "{$1:$&}")]
    [InlineData(@"(?<x>a)+b\k<x>", "aaba -- aaabaa -- aba", "${x}|$&")]
    [InlineData("(?<x>a)+(?<-x>b)+(?(x)(?!))", "aabb -- ab -- aaabbb", "$&!")]
    public void SearchesResetBacktrackingRepeatAndCaptureState(string pattern, string text, string template)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
        {
            var oracle = new Regex(pattern, options | RegexOptions.CultureInvariant);
            var regex = Create(pattern, options);
            for (var iteration = 0; iteration < 3; iteration++)
                Assert.Equal(oracle.Replace(text, template), regex.ReplaceToString(input, template));
        }
    }

    [Fact]
    public void CaptureHistoriesSurviveNestedOperationsAndSubsequentMatches()
    {
        var regex = Create("(a)+");
        var input = "aaa xaa aa"u8.ToArray();
        var retained = new List<Utf8RangeMatch>();
        using (var cursor = regex.CreateMatchCursor(input))
            while (cursor.MoveNext(out var match))
            {
                Assert.Equal("a", regex.ReplaceToString("aa"u8, "$1"));
                retained.Add(match);
            }
        Assert.Equal("a", regex.ReplaceToString("aaaa"u8, "$1"));
        Assert.Equal(new[] { "aaa", "aa", "aa" },
            retained.Select(match => Encoding.UTF8.GetString(match.GetValueMemory().Span)));
        for (var index = 0; index < retained.Count; index++)
        {
            var captures = retained[index].GetCaptures(1);
            Assert.Equal(index == 0 ? 3 : 2, captures.Length);
            foreach (var capture in captures)
                Assert.Equal("a"u8.ToArray(), input.AsSpan(capture.StartOffsetInBytes,
                    capture.EndOffsetInBytes - capture.StartOffsetInBytes).ToArray());
        }
    }

}
