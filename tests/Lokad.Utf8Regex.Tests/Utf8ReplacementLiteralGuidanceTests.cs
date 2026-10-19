using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementLiteralGuidanceTests
{
    private static Utf8Regex Create(string pattern, RegexOptions options = RegexOptions.None,
        ulong work = 1000000, TimeSpan? timeout = null) =>
        new(pattern, options | RegexOptions.CultureInvariant, timeout ?? Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy
            {
                MaxWorkSteps = work, MaxDepth = 256,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
            });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongLiteralMissAndLateCapturesFitBoundedWork(bool compiled)
    {
        var options = compiled ? RegexOptions.Compiled : RegexOptions.None;
        var regex = Create("(cat)", options, 1024);
        var miss = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("dog fox bird ", 512)));
        Assert.Equal(0, regex.Count(miss));
        Assert.Equal(Encoding.UTF8.GetString(miss), regex.ReplaceToString(miss, "[$1]"));
        Assert.Equal(miss, regex.Replace(miss, "[$1]"u8));
        var gap = new string('x', 4096);
        var text = gap + "cat" + gap + "cat😀";
        Assert.Equal(gap + "[cat]" + gap + "[cat]😀", regex.ReplaceToString(Encoding.UTF8.GetBytes(text), "[$1]"));
    }

    [Theory]
    [InlineData("(cat|cater)", "xcater cat", "[$1]", "x[cat]er [cat]")]
    [InlineData("(cater|cat)", "xcater cat", "[$1]", "x[cater] [cat]")]
    [InlineData("(?<a>é)|(?<b>😀)", "dé😀", "${a}:${b}|", "dé:|:😀|")]
    [InlineData("(?i:(cat))", "CAT", "[$1]", "[CAT]")]
    [InlineData("(?<=(a))(cat)", "acat", "$1:$2", "aa:cat")]
    [InlineData(@"\G(cat)", "xcatcat", "[$1]", "xcatcat")]
    [InlineData("(cat)?", "😀cat", "[$1]", "[]😀[cat][]")]
    [InlineData("(?<x>cat)|(?<x>dog)", "cat dog", "${x}!", "cat! dog!")]
    [InlineData(@"(cat)\1", "catcat", "$1", "cat")]
    public void StartDiscoveryPreservesOrderedCapturesAndExcludedShapes(string pattern, string text, string template, string expected)
    {
        var regex = Create(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.ReplaceToString(input, template));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, Encoding.UTF8.GetBytes(template)));
    }

    [Fact]
    public void SupplementaryLiteralKeepsScalarStartsAndUnmatchedPrefixes()
    {
        var gap = new string('é', 2048);
        var regex = Create("(😀)", work: 1024);
        Assert.Equal(gap + "[😀]" + gap, regex.ReplaceToString(Encoding.UTF8.GetBytes(gap + "😀" + gap), "[$1]"));
    }

    [Fact]
    public void CallbackStillReceivesVerifiedCapturesAtOriginalByteOffsets()
    {
        var gap = new string('x', 2048);
        var text = gap + "écat" + gap + "cat";
        var input = Encoding.UTF8.GetBytes(text);
        var seen = new List<(int Start, string Value)>();
        var regex = Create("(cat)");
        var result = regex.ReplaceToString(input, seen,
            static (in Utf8MatchContext match, ref List<(int Start, string Value)> state) =>
            {
                state.Add((match.IndexInBytes, match.GetValueString()));
                return "[" + match.GetValueString() + "]";
            });
        Assert.Equal(gap + "é[cat]" + gap + "[cat]", result);
        Assert.Equal(new[] { (2050, "cat"), (4101, "cat") }, seen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalAuthorityInterruptsEligibleCallsAndAllowsReuse(bool timeout)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = Create("(cat)", timeout: timeout ? TimeSpan.FromMilliseconds(10) : Timeout.InfiniteTimeSpan);
        // Reach the shared diagnostic quantum even when vector search skips
        // thousands of otherwise impossible scalar attempts.
        var input = Encoding.UTF8.GetBytes(new string('x', 131072));
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            entered = true;
            if (timeout) ticks = 20; else cancellation.Cancel();
        });
        try
        {
            if (timeout) Assert.Throws<RegexMatchTimeoutException>(() => regex.ReplaceToString(input, "x"));
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.ReplaceToString(input, "x", cancellation.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.Equal("cat", regex.ReplaceToString("cat"u8, "$1"));
    }
}
