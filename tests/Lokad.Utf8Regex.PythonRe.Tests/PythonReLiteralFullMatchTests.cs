using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLiteralFullMatchTests
{
    [Theory]
    [InlineData("cat", "cat", 0, true)]
    [InlineData("cat", "xcat", 0, false)]
    [InlineData("cat", "catx", 0, false)]
    [InlineData("cat", "xcat", 1, true)]
    [InlineData("café😀", "πcafé😀", 2, true)]
    [InlineData("😀", "é😀", 2, true)]
    [InlineData("😀", "é😀x", 2, false)]
    [InlineData("😀", "éx😀", 2, false)]
    [InlineData("", "", 0, true)]
    [InlineData("", "😀", 0, false)]
    [InlineData("", "😀", 4, true)]
    public void ValueAndStringFullMatchKeepSuffixEndpointsAndScalarCoordinates(
        string pattern, string text, int start, bool success)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var controlled in new[] { false, true })
        {
            var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = controlled ? new()
                {
                    MaxWorkSteps = 100000,
                    MaxDepth = 128,
                    MaxWorkingBytes = 1000000,
                    MaxResultBytes = 1000000
                }

    : default
            });
            using var live = new CancellationTokenSource();
            var match = regex.Match(input, start, cancellationToken: live.Token, mode: Utf8PythonMatchMode.FullMatch);
            Assert.Equal(success, match.Success);
            PythonReMatchAssert.Equal(success ? pattern : null, regex.Match(input, start, cancellationToken: live.Token, mode: Utf8PythonMatchMode.FullMatch));
            if (success)
            {
                Assert.Equal((start, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
                Assert.Equal(pattern, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
            }
        }
    }

    [Theory]
    [InlineData("cat")]
    [InlineData("é😀")]
    [InlineData("")]
    public void BorrowedLiteralResultNeedsNoVmPreparationOrMajorWorkingResultCapacity(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        using var deniedPreparation = new Utf8ConstructionScope(0, 0, 0, 0);
        var input = Encoding.UTF8.GetBytes(pattern);
        using var live = new CancellationTokenSource();
        Assert.True(regex.Match(input, 0, cancellationToken: live.Token, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(0ul, deniedPreparation.Budget.WorkSteps);
        Assert.Equal(0ul, deniedPreparation.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(4096)]
    public void LiteralLengthBoundariesKeepAnchoredValuesAndText(int length)
    {
        var pattern = new string('x', length);
        var regex = new Utf8PythonRegex(pattern);
        foreach (var prefix in new[] { "", "π😀" })
        {
            var start = Encoding.UTF8.GetByteCount(prefix);
            var input = Encoding.UTF8.GetBytes(prefix + pattern);
            var full = regex.Match(input, start, mode: Utf8PythonMatchMode.FullMatch);
            Assert.True(full.Success);
            Assert.Equal((start, input.Length), (full.StartOffsetInBytes, full.EndOffsetInBytes));
            PythonReMatchAssert.Equal(pattern, regex.Match(input, start, mode: Utf8PythonMatchMode.FullMatch));
            var tail = Encoding.UTF8.GetBytes(prefix + pattern + "!");
            var match = regex.Match(tail, start);
            Assert.True(match.Success);
            Assert.Equal(input.Length, match.EndOffsetInBytes);
            PythonReMatchAssert.Equal(pattern, regex.Match(tail, start));
            Assert.False(regex.Match(tail, start, mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.False(regex.Match(tail, start, mode: Utf8PythonMatchMode.FullMatch).Success);
        }
    }

    [Theory]
    [InlineData("foo(?=bar)", "foo", false, false)]
    [InlineData("foo(?=bar)", "foobar", true, false)]
    [InlineData(@"foo\b", "foobar", false, false)]
    [InlineData(@"foo\b", "foo!", true, false)]
    [InlineData("(?i:k)", "K", true, true)]
    public void AnchoredLiteralResultsKeepAssertionsAndProfileFolding(string pattern,
        string subject, bool matched, bool fullMatched)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(matched, regex.Match(input).Success);
        Assert.Equal(fullMatched, regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void WorkDenialAndPreCancellationStayDistinctFromNoMatch()
    {
        var regex = new Utf8PythonRegex("cat", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1
            }
        });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match("dog"u8.ToArray(), 0, cancellationToken: default, mode: Utf8PythonMatchMode.FullMatch));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(1ul, error.Limit);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => regex.Match([0xff], 0, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.FullMatch));
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(256)]
    public void BoundedFixedStartComparisonChargesEveryWorkCutoff(int length)
    {
        var pattern = new string('x', length);
        var input = Encoding.UTF8.GetBytes(pattern);
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        ulong steps = 0;
        for (ulong limit = 1; limit <= 64; limit++)
        {
            var limited = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = limit,
                    MaxDepth = 1,
                    MaxWorkingBytes = 1,
                    MaxResultBytes = 1
                }
            });
            try { Assert.True(limited.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success); steps = limit; break; }
            catch (Utf8ResourceLimitExceededException failure)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind); Assert.Equal(limit, failure.Limit); }
        }
        Assert.InRange(steps, 2UL, 64UL);
        using var token = new CancellationTokenSource();
        Assert.True(regex.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.FullMatch).Success);

        Assert.False(regex.Match([.. input, (byte)'!'], mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData("^cat", "xcat", 1, false)]
    [InlineData(@"\Acat", "xcat", 1, false)]
    [InlineData(@"\bcat", "xcat", 1, false)]
    [InlineData("(?<=x)cat", "xcat", 1, true)]
    [InlineData("cat$", "cat\n", 0, false)]
    [InlineData("cat|cater", "cater", 0, true)]
    [InlineData("(?P<x>cat)", "cat", 0, true)]
    [InlineData("(?i:k)", "K", 0, true)]
    public void ExcludedShapesKeepTheirFullSubjectContextAndGeneralProgram(
        string pattern, string text, int start, bool success)
    {
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 128,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            }
        });
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(success, regex.Match(input, start, mode: Utf8PythonMatchMode.FullMatch).Success);
        PythonReMatchAssert.Equal(success ? Encoding.UTF8.GetString(input.AsSpan(start)) : null, regex.Match(input, start, mode: Utf8PythonMatchMode.FullMatch));
    }



    [Fact]
    public void LongMatchObservesInFlightCancellationAndFreshInvocationRecovers()
    {
        var pattern = new string('x', 8192) + "cat";
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromSeconds(30)
        });
        var input = Encoding.UTF8.GetBytes(new string('x', 65536) + pattern);
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == 6) cancellation.Cancel();
            return 0;
        }))
        {
            var error = Assert.Throws<OperationCanceledException>(() => regex.Match(input, 65536, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.FullMatch));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.True(reads >= 2);
        }
        regex.Match(input, 65536, cancellationToken: default, mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(regex.Match(input, 65536, mode: Utf8PythonMatchMode.FullMatch).Success);
        PythonReMatchAssert.Equal(pattern, regex.Match(input, 65536, mode: Utf8PythonMatchMode.FullMatch));
    }

    [Fact]
    public void OriginalDeadlineAndByteBoundedTimeoutOmitSubject()
    {
        var regex = new Utf8PythonRegex("cat", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1),
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        var input = Enumerable.Repeat((byte)'x', 65536).Append((byte)0xff).ToArray();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads < 2 ? 0 : 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, 0, cancellationToken: default, mode: Utf8PythonMatchMode.FullMatch));
        Assert.Empty(error.Input);
        Assert.Equal(regex.Pattern, error.Pattern);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalLiteralPublicationKeepsOriginalTokenAndDeadline(bool expire)
    {
        var regex = new Utf8PythonRegex("cat", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = "cat"u8.ToArray();
        var total = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { total++; return 0; }))
            regex.Match(input, 0, cancellationToken: default, mode: Utf8PythonMatchMode.FullMatch);
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == total) { if (expire) ticks = 50; else cancellation.Cancel(); }
            return ticks;
        });
        if (expire)
            Assert.Equal(regex.Pattern, Assert.Throws<RegexMatchTimeoutException>(() =>
                regex.Match(input, 0, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.FullMatch)).Pattern);
        else
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Match(input, 0, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.FullMatch)).CancellationToken);
    }
}
