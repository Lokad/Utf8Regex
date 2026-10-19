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
        var retained = new List<string>();
        var state = (Regex: regex, Captures: retained);
        var result = regex.ReplaceToString("aaa xaa aa"u8, state,
            static (in Utf8MatchContext match, ref (Utf8Regex Regex, List<string> Captures) state) =>
            {
                Assert.Equal("a", state.Regex.ReplaceToString("aa"u8, "$1"));
                var group = match.GetGroup(1);
                var values = new string[group.CaptureCount];
                for (var i = 0; i < values.Length; i++) values[i] = group.GetCapture(i).GetValueString();
                state.Captures.Add(string.Concat(values));
                return "[" + match.GetValueString() + "]";
            });
        Assert.Equal("[aaa] x[aa] [aa]", result);
        Assert.Equal(new[] { "aaa", "aa", "aa" }, retained);
        Assert.Equal("a", regex.ReplaceToString("aaaa"u8, "$1"));
        Assert.Equal(new[] { "aaa", "aa", "aa" }, retained);
    }

    [Fact]
    public void CallbackFailureUnwindsAndTheExpressionCanBeReused()
    {
        var regex = Create("(a)+");
        Assert.Throws<InvalidOperationException>(() => regex.ReplaceToString("aa aa aa"u8, 0,
            static (in Utf8MatchContext match, ref int count) =>
            {
                if (++count == 2) throw new InvalidOperationException("Expected callback failure.");
                return match.GetValueString();
            }));
        Assert.Equal("[a] [a] [a]", regex.ReplaceToString("aa aa aa"u8, "[$1]"));
    }
}
