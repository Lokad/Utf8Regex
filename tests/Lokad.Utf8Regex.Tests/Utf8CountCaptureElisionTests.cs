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
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 128,
                MaxWorkingBytes = 4096,
                MaxResultBytes = 1
            }
        });

        Assert.Equal(2, regex.Count(input));
        Assert.Equal(2, regex.Count(input, cancellation.Token));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(input))).ResourceKind);
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
    public void CountAndValuesFitWithoutUnobservedCaptureScratch(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(text, 16)));
        var expected = new Regex(pattern).Count(Encoding.UTF8.GetString(input));
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            // Allow the value-only frame/journal stacks, while the additional
            // observed capture trail still needs its own admitted capacity.
            // This public allowance must not shrink with an elided trail.
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1000000,
                    MaxDepth = 128,
                    MaxWorkingBytes = 1280,
                    MaxResultBytes = 1
                }
            });
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected, regex.Count(input, cancellation.Token));
            Assert.True(regex.IsMatch(input, cancellation.Token));
            Assert.True(regex.Match(input, cancellationToken: cancellation.Token).Success);
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(input))).ResourceKind);
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
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 100000,
                    MaxDepth = 1,
                    MaxWorkingBytes = 64,
                    MaxResultBytes = 1
                }
            });
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
            var tiny = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1000000,
                    MaxDepth = 128,
                    MaxWorkingBytes = (ulong)Utf8SliceRunner.TrailReserveBytes - 1,
                    MaxResultBytes = 1000000
                }
            });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => tiny.Count(input)).ResourceKind);
            var funded = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1000000,
                    MaxDepth = 128,
                    MaxWorkingBytes = 1000000,
                    MaxResultBytes = 1000000
                }
            });
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
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(oracle.Count(text), regex.Count(input));
        foreach (var number in oracle.GetGroupNumbers())
        {
            if (number == 0) continue;
            Assert.True(retained.TryGetGroup(number, out var group));
            Assert.Equal(expected.Groups[number].Value, System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(number).Span));
            Assert.Equal(expected.Groups[number].Captures.Count, retained.GetCaptures(number).Length);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CountKeepsOriginalInterruption(bool expire)
    {
        const string pattern = "(?:(a)|b)+c";
        var input = Encoding.UTF8.GetBytes(new string('a', 32768) + "c");
        var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 10000000,
                MaxDepth = 100000,
                MaxWorkingBytes = 16777216,
                MaxResultBytes = 1
            }
        });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= 64) return;
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
