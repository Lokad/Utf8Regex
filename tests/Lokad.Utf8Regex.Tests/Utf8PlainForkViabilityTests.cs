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
    [InlineData(@"\A(?:(?<x>ab)|(?<y>a)c|(?<z>😀))d\z", "acd")]
    [InlineData(@"\A(?:(?<x>ab)|(?<y>a)c|(?<z>😀))d\z", "😀d")]
    public void RequiredLiteralsKeepOrderingCapturesAndOpaqueAlternatives(string pattern, string text)
    {
        foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
            Compare(pattern, text, option);
    }

    [Fact]
    public void NonBacktrackingKeepsItsOwnCaptureAndAlternativeRules()
        => Compare(@"^(?<x>ab|cd)+e$", "abcdabe", RegexOptions.NonBacktracking);

    [Theory]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "record Token", 1)]
    [InlineData(@"\b(?:public|private|internal)\s+class\b", "public class", 1)]
    [InlineData(@"\b(?:public|private|internal)\s+class\b", "public sealed class", 0)]
    public void DisprovedNestedForkKeepsWorkingAdmissionSmall(string pattern, string subject, int count)
    {
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 256, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(count, regex.Count(input, token.Token));
        Assert.Equal(count != 0, regex.IsMatch(input, token.Token));
        Assert.Equal(count != 0, regex.Match(input, cancellationToken: token.Token).Success);
    }

    [Theory]
    [InlineData(@"\b(?:public|private|internal)\s+class\b", "épublic class public\tclass private\u00a0class 😀internal class internal classX")]
    [InlineData(@"\b(?:public|private|internal)\s+class\b", "public               class private                class internal                \u00a0class public classX public ")]
    [InlineData(@"\b(?:public|private|internal)[ c]+class\b", "public cclass private class internal ccclass public classX")]
    [InlineData(@"\b(?:pub|public|private)\s+class\b", "épublic class public class pub\u00a0class private class")]
    public void LiteralFamilyCountKeepsBoundariesAndOverlappingArms(string pattern, string tail)
    {
        var text = new string('.', 256) + tail;
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 256, MaxDepth = 16, MaxWorkingBytes = 1024 }
        });
        Assert.Equal(3, new Regex(pattern).Count(text));
        Assert.Equal(3, regex.Count(input, token.Token));
    }

    [Fact]
    public void ImportsWithoutPendingAlternativesFitSmallWorkingCapacity()
    {
        const string pattern = """^import\s+(?<shared>shared\s+)?"(?<path>(\.|[^\\"]*))".*$""";
        var input = Encoding.UTF8.GetBytes("import \"_Common\" as Common\nimport shared \"_Calendar\" as Calendar\n");
        var regex = new Utf8Regex(pattern, options: RegexOptions.Multiline, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100_000,
                MaxDepth = 1,
                MaxWorkingBytes = 256,
                MaxResultBytes = 1
            }
        });
        using var live = new CancellationTokenSource();
        Assert.Equal(2, regex.Count(input, live.Token));
        Assert.Equal(2, regex.Count(input));
        live.Cancel();
        Assert.Equal(live.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, live.Token)).CancellationToken);
    }

    private static void Compare(string pattern, string text, RegexOptions option)
    {
        var oracle = new Regex(pattern, option);
        var regex = new Utf8Regex(pattern, options: option);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = oracle.Match(text);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Value, System.Text.Encoding.UTF8.GetString(actual.GetValueMemory().Span));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
        Assert.Equal(oracle.Count(text), regex.Count(input));
    }
}
