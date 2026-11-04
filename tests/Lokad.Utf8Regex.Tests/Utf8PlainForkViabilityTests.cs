using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PlainForkViabilityTests
{
    [Theory]
    [InlineData(@"^(?:(?<left>ab)|(?<right>cd))$", "cd")]
    [InlineData(@"^(?:(?<left>ab)|(?<right>cd))$", "ab")]
    [InlineData(@"^(?:(?<x>a)b|a)c$", "ac")]
    [InlineData(@"^(?:(?<x>a)b|c)d$", "cd")]
    [InlineData(@"^(?:(?<x>a)b|c)d$", "ad")]
    [InlineData(@"^(?:(?<x>a)|(?<y>b))+c$", "ababc")]
    [InlineData(@"^(?:(?<x>a)|(?<x>b))+c$", "ababc")]
    [InlineData(@"^(?:a|)b$", "b")]
    [InlineData(@"^(?:|a)b$", "ab")]
    [InlineData(@"^(?>ab|a)c$", "ac")]
    [InlineData(@"^(?>ab|cd)e$", "cde")]
    [InlineData(@"^(?=(?<x>a)|(?<y>b))\w$", "b")]
    [InlineData(@"^(?:(?<x>a)|b)\k<x>?$", "b")]
    [InlineData(@"^(?<stack>a)+(?:(?<-stack>b)|c)+(?(stack)(?!))$", "aabb")]
    [InlineData(@"^(?:(?i:a)b|c)d$", "Abd")]
    [InlineData(@"^(?:(?<x>😀)b|c)d$", "😀bd")]
    [InlineData(@"^(?:(?<x>a)b|c)d$", "")]
    [InlineData(@"^(?<x>ab|cd)+e$", "abcdabe")]
    public void RequiredLiteralsKeepOrderingCapturesAndOpaqueAlternatives(string pattern, string text)
    {
        foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
            Compare(pattern, text, option);
    }

    [Fact]
    public void NonBacktrackingKeepsItsOwnCaptureAndAlternativeRules()
        => Compare(@"^(?<x>ab|cd)+e$", "abcdabe", RegexOptions.NonBacktracking);

    [Fact]
    public void ImportsWithoutPendingAlternativesFitSmallWorkingCapacity()
    {
        const string pattern = """^import\s+(?<shared>shared\s+)?"(?<path>(\.|[^\\"]*))".*$""";
        var input = Encoding.UTF8.GetBytes("import \"_Common\" as Common\nimport shared \"_Calendar\" as Calendar\n");
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 100_000, MaxDepth = 1,
                MaxWorkingBytes = 256, MaxResultBytes = 1 });
        using var live = new CancellationTokenSource();
        Assert.Equal(2, regex.Count(input, live.Token));
        Assert.Equal(2, regex.Count(input));
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        live.Cancel();
        Assert.Equal(live.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, live.Token)).CancellationToken);
    }

    private static void Compare(string pattern, string text, RegexOptions option)
    {
        var oracle = new Regex(pattern, option);
        var regex = new Utf8Regex(pattern, option);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Value, actual.GetValueString());
        foreach (var number in oracle.GetGroupNumbers())
        {
            if (number == 0) continue;
            var wanted = expected.Groups[number];
            var group = actual.GetGroup(number);
            Assert.Equal(wanted.Success, group.Success);
            Assert.Equal(wanted.Value, group.GetValueString());
            Assert.Equal(wanted.Captures.Count, group.CaptureCount);
            for (var index = 0; index < wanted.Captures.Count; index++)
                Assert.Equal(wanted.Captures[index].Value, group.GetCapture(index).GetValueString());
        }
        Assert.Equal(oracle.Count(text), regex.Count(input));
    }
}
