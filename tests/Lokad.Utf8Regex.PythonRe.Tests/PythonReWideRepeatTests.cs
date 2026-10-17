using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// CPython 3.13.2 observations: artifacts/python-wide-repeats-20261018/cpython-oracle.json.
public sealed class PythonReWideRepeatTests
{
    [Theory]
    [InlineData("a{2147483647}", "aaa", false, 0, null)]
    [InlineData("a{2147483648}", "aaa", false, 0, null)]
    [InlineData("a{2147483648,}", "aaa", false, 0, null)]
    [InlineData("a{4294967294}", "aaa", false, 0, null)]
    [InlineData("a{2147483648,4294967294}?", "aaa", false, 0, null)]
    [InlineData("a{0,2147483648}", "aaa", true, 2, null)]
    [InlineData("a{0,4294967294}?a", "aaa", true, 3, null)]
    [InlineData("(?P<g>😀){0,4294967294}+é", "😀😀é", true, 1, "😀")]
    [InlineData("(?P<g>a?){0,2147483648}b", "aab", true, 1, "")]
    [InlineData("(?P<g>a?){0,4294967294}?b", "aab", true, 1, "a")]
    [InlineData("(?:(?P<g>a)?){0,4294967294}+b", "aab", true, 1, "a")]
    [InlineData("(?=(a{0,4294967294}))a", "a", true, 1, "a")]
    [InlineData("(?<=a{2147483648})b", "ab", false, 0, null)]
    [InlineData("(?<=a{4294967294})b", "ab", false, 0, null)]
    [InlineData(@"\w{2147483648}", "éé", false, 0, null)]
    [InlineData(@"\w{1,4294967294}", "éé", true, 1, null)]
    [InlineData("(?<!a{4294967294})b", "b", true, 1, null)]
    public void WideBoundsPreserveScalarResults(string pattern, string text, bool success, int count, string? capture)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var cancellation = new CancellationTokenSource();
        foreach (var finite in new[] { false, true })
        foreach (var bytes in new[] { false, true })
        {
            var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 256,
                MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 } : default;
            var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy)
                : new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            var match = regex.FullMatchDetailedData(input, 0, cancellation.Token);
            Assert.Equal(success, match.Success);
            Assert.Equal(count, regex.Count(input, 0, cancellation.Token));
            if (!success) continue;
            Assert.Equal(text, match.Value.ValueText);
            Assert.Equal(0, match.Value.StartOffsetInBytes);
            Assert.Equal(input.Length, match.Value.EndOffsetInBytes);
            if (capture is not null)
            {
                Assert.Equal(capture, match.Groups[1].ValueText);
                Assert.Equal(1, match.LastIndex);
            }
        }
    }

    [Theory]
    [InlineData("a{4294967295}")]
    [InlineData("a{0,4294967295}")]
    [InlineData("a{9999999999999999999999}")]
    public void BoundsAtOrAbovePythonSentinelArePatternFailures(string pattern)
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Equal("the repetition number is too large", error.Message);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void ReversedWideBoundsKeepPatternFailure()
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("a{4294967294,2147483648}"));
        Assert.Equal("min repeat greater than max repeat", error.Message);
        Assert.Equal(2, error.Position);
    }

    [Theory]
    [InlineData("(?:){2147483648}")]
    [InlineData("(?:){4294967294,}")]
    [InlineData("(?P<g>a?){2147483648,4294967294}")]
    public void LargeMandatoryNullableWorkRemainsBounded(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000, MaxDepth = 256, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(""u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WideMandatoryIterationsKeepOriginalInterruption(bool expire)
    {
        const string pattern = "(?:){4294967294,}";
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20),
            new() { MaxWorkSteps = 1000000, MaxDepth = 256, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            entered = true;
            if (expire) ticks = 50;
            else cancellation.Cancel();
        });
        try
        {
            if (expire)
            {
                var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(""u8));
                Assert.Equal(pattern, error.Pattern);
                Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
            }
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(""u8, 0, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
    }

    [Fact]
    public void WiderRepeatWorkspaceStillRequiresWorkingAdmission()
    {
        var regex = new Utf8PythonRegex("(?:){2147483648}", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1, MaxResultBytes = 1000000 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(""u8)).ResourceKind);
    }
}
