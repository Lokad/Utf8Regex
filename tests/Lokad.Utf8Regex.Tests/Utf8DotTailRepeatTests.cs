using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DotTailRepeatTests
{
    private static Utf8ResourcePolicy Policy(uint depth = 128, ulong result = 1 << 20) => new()
    {
        MaxWorkSteps = 1_000_000,
        MaxDepth = depth,
        MaxWorkingBytes = 1 << 20,
        MaxResultBytes = result
    };

    [Theory]
    [InlineData(@"(?<head>.*?),", 2047, "😀,é,,\nq,")]
    [InlineData(@"(?<head>.*?),", 0, "😀é\nq,")]
    [InlineData(@"(?s:(?<head>.*?),)", 0, "😀é\nq,")]
    [InlineData(@"(?<head>.*?)\n", 2048, "😀é\n\n")]
    [InlineData(@"(?<head>.*?),X", 0, "😀no,éyes,X")]
    [InlineData(@"(?<head>.+?),", 0, ",😀,é")]
    public void LazyDotKeepsShortestReachableEndAndCapture(string pattern, int padding, string tail)
    {
        var text = new string('x', padding) + tail;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy { ResourcePolicy = Policy() });
        var expected = oracle.Match(text);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
        Assert.Equal(oracle.Count(text), regex.Count(input, token.Token));
    }

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    public void CompleteGreedySinglelineEndNeedsNoRewindDepth(RegexOptions mode)
    {
        var text = new string('a', 4096) + "😀\né\0\r\n";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("(?s:^(?<body>.+)$)", options: mode, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy(1)
        });
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(retained.Success);
        Assert.Equal(input.Length, retained.Value.EndOffsetInBytes - retained.Value.StartOffsetInBytes);
        Assert.Equal(text, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        using var token = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, token.Token));
        Assert.Equal(text, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Fact]
    public void SinglelineLazyAndConsumingTailsKeepTheirEarlierEndpointChoices()
    {
        var tail = new Utf8Regex("(?sm:^(?<body>.*)$\\nX)");
        Assert.Equal("a", Encoding.UTF8.GetString(Utf8MatchSequence.First(tail.CreateMatchCursor("a\nX"u8.ToArray())).GetGroupMemory(1).Span));
        var lazy = new Utf8Regex("(?sm:^(?<body>.*?)$)");
        Assert.Equal("a", Encoding.UTF8.GetString(Utf8MatchSequence.First(lazy.CreateMatchCursor("a\nb"u8.ToArray())).GetGroupMemory(1).Span));
        Assert.Equal(2, lazy.Count("a\nb"u8));
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void LongLineScanKeepsScalarCapturesAcrossBlocksAndOffsetStorage(int prefixLength)
    {
        var line = new string('a', prefixLength) + "😀é\0\r";
        var storage = Encoding.UTF8.GetBytes("pad" + line + "\nZpad");
        var memory = storage.AsMemory(3, Encoding.UTF8.GetByteCount(line) + 2);
        var input = memory.Span;
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("(?m:^(?<line>.*)$)", options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy()
            });
            var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(memory));
            Assert.True(retained.Success);
            Assert.Equal(0, retained.Value.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(line), retained.Value.EndOffsetInBytes - retained.Value.StartOffsetInBytes);
            Assert.Equal(line, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
            using var token = new CancellationTokenSource();
            Assert.Equal(2, regex.Count(input, token.Token));
            Assert.Equal(line, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        }
    }

    [Theory]
    [InlineData("(.*)$", "abc\n")]
    [InlineData("(.+)$", "abc\nxyz")]
    [InlineData(@"(.*)\z", "abc\nxyz")]
    [InlineData(@"(.*)\Z", "abc\n")]
    [InlineData(@"(.*)\n", "abc\nxyz\n")]
    [InlineData("(?m:^(.{0,3})$)", "abc\nabcd\n\n")]
    [InlineData("(?m:^([a-z]+)$)", "abc\nxyz\n")]
    [InlineData("(?sm:(.{1,2})$)", "a\nb")]
    [InlineData(@"(?s:(.*)\n)", "a\nb\nc")]
    [InlineData("(.+?)$", "abc\n")]
    [InlineData("((.+))$", "abc\n")]
    [InlineData(@"(?:a(.+)\n)+Z", "abc\nade\nZ")]
    public void PublicResultsAndCaptureHistoryAgreeOnOverlappingBmpSemantics(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy()
            });
            var expected = oracle.Match(text);
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(expected.Success, regex.IsMatch(input));
            Assert.Equal(expected.Success, regex.Match(input).Success);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    public void DotBoundsCountScalarsAndPreserveCapturedByteRanges(RegexOptions mode)
    {
        var regex = new Utf8Regex("(?m:^(.{1,2})$)", options: mode, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy(1)
        });
        var input = "😀é\n中"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var first = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal((0, 6), (first.Value.StartOffsetInBytes, first.Value.EndOffsetInBytes - first.Value.StartOffsetInBytes));
        Assert.Equal("😀é", Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal(1, first.GetCaptures(1).Length);
        Assert.Equal(2, regex.Count("AB\nXY"u8));
        Assert.Equal("😀é", Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }

    [Fact]
    public void UselessDotAlternativesLoseDepthButOverlappingTailsStillNeedIt()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 2048) + "\n");
        var policy = Policy(1, 1);
        Assert.Equal(1, new Utf8Regex("(?m:^(.+)$)", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        }).Count(input));
        var overlapping = new Utf8Regex("(?m:^(.+)a$)", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => overlapping.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, error.ResourceKind);
    }

    [Theory]
    [InlineData("(?<=(.*))X", "😀éX", "😀é")]
    [InlineData("(?s:(?<=(.*))X)", "abc\nX", "abc\n")]
    [InlineData("(?m:(?<=(.*)$)X)", "abc\nX", null)]
    public void LookbehindStillRequiresItsExactEndpoint(string pattern, string text, string? captured)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var match = Utf8MatchSequence.First(new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy()
            })                .CreateMatchCursor(Encoding.UTF8.GetBytes(text)));
            Assert.Equal(captured is not null, match.Success);
            if (match.Success) Assert.Equal(captured, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        }
    }

    [Fact]
    public void EveryWorkCutoffPreservesTheBorrowedLoan()
    {
        var program = new Utf8Regex("(?m:^(.+)$)").Inspection.AvailableOwnedSliceProgram!;
        var input = "😀é\n中"u8.ToArray();
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(2, Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline).Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work <= baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            Assert.Equal(work == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DotScanObservesOriginalInterruptionAndReleasesScratch(bool timeout, bool singleline)
    {
        var program = new Utf8Regex(singleline ? "(?sm:^(.+)$)" : "(?m:^(.+)$)").Inspection.AvailableOwnedSliceProgram!;
        // Reach the diagnostic interruption quantum even with bounded byte scans.
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", 65536)));
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (timeout) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            var failure = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.True(invocation.Budget.Steps >= 64);
        }
        finally { session.Complete(); }
    }
}
