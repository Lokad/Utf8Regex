using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CountCaptureElisionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManyUnobservedCapturesDoNotRequireBalancingStorage(bool compiled)
    {
        var pattern = string.Concat(Enumerable.Repeat("()", 512)) + "(?:(ab)|c){1,8}X";
        var input = "ababcX cabX "u8.ToArray();
        var options = compiled ? RegexOptions.Compiled : RegexOptions.None;
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 128, MaxWorkingBytes = 4096, MaxResultBytes = 1 });

        Assert.Equal(2, regex.Count(input));
        Assert.Equal(2, regex.Count(input, cancellation.Token));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.MatchDetailed(input)).ResourceKind);
        Assert.Equal(2, regex.Count(input));
    }

    [Theory]
    [InlineData("(?:(ab)|c){1,8}X", "ababcX cabX ")]
    [InlineData("((ab)|c){1,8}X", "ababcX cabX ")]
    [InlineData("(?:(a)|(b)){1,8}X", "aabX bbX ")]
    [InlineData("(?:(ab)|c){1,8}?X", "ababcX cabX ")]
    [InlineData("(?>(?:(ab)|c){1,8})X", "ababcX cabX ")]
    [InlineData("((?:ab|c){1,8})cX", "ababcX cabcX ")]
    [InlineData("(?:(😀)|é){1,8}X", "😀éX é😀X ")]
    public void CountFitsWithoutUnobservedCaptureScratch(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(text, 16)));
        var expected = new Regex(pattern).Count(Encoding.UTF8.GetString(input));
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 1000000, MaxDepth = 128, MaxWorkingBytes = 4096, MaxResultBytes = 1 });
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected, regex.Count(input, cancellation.Token));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input)).ResourceKind);
            Assert.Equal(expected, regex.Count(input));
        }
    }

    [Theory]
    [InlineData("(.)", 2)]
    [InlineData("()", 3)]
    public void ScalarAndEmptyProgressNeedNoCaptureStorage(string pattern, int expected)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.RightToLeft, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 64, MaxResultBytes = 1 });
            Assert.Equal(expected, regex.Count("😀é"u8));
        }
    }

    [Theory]
    [InlineData(@"(?:(ab)|c){1,8}\1", "ababab")]
    [InlineData(@"(?:(ab)|c){1,8}(?(1)X|Y)", "ababcX")]
    [InlineData(@"(?=((ab)|c){1,8}X)", "ababcX")]
    [InlineData(@"(?<p>a)+(?<-p>b)+(?(p)(?!))", "aabb")]
    [InlineData(@"(?<7>ab|c){1,8}X", "ababcX")]
    [InlineData(@"(?:(?<g>ab)|(?<g>c)){1,8}X", "ababcX")]
    public void ReadDependentAndExcludedProgramsKeepCaptureAdmission(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern).Count(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var tiny = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 1000000, MaxDepth = 128, MaxWorkingBytes = 4096, MaxResultBytes = 1000000 });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => tiny.Count(input)).ResourceKind);
            var funded = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 1000000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
            Assert.Equal(expected, funded.Count(input));
        }
    }

    [Theory]
    [InlineData("((ab)|c){1,8}X", "ababcX cX")]
    [InlineData("((😀)|é){1,8}X", "😀😀éX éX")]
    public void RetainedDetailedCapturesRemainValidAcrossCount(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern);
        var oracle = new Regex(pattern);
        var expected = oracle.Match(text);
        var retained = regex.MatchDetailed(input);
        Assert.Equal(oracle.Count(text), regex.Count(input));
        foreach (var number in oracle.GetGroupNumbers())
        {
            if (number == 0) continue;
            var group = retained.GetGroup(number);
            Assert.Equal(expected.Groups[number].Value, group.GetValueString());
            Assert.Equal(expected.Groups[number].Captures.Count, group.CaptureCount);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CountKeepsOriginalInterruptionAfterValidation(bool expire)
    {
        const string pattern = "(?:(a)|b)+c";
        var input = Encoding.UTF8.GetBytes(new string('a', 32768) + "c");
        var regex = new Utf8Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(20),
            new() { MaxWorkSteps = 10000000, MaxDepth = 100000, MaxWorkingBytes = 16777216, MaxResultBytes = 1 });
        var validation = new Utf8SliceInvocation(new(10000000, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= validationSteps + 64) return;
            entered = true;
            if (expire) ticks = 50;
            else cancellation.Cancel();
        });
        try
        {
            if (expire)
            {
                var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input));
                Assert.Equal(pattern, error.Pattern);
            }
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(input, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
        ticks = 0;
        Assert.Equal(1, regex.Count("abc"u8));
    }
}
