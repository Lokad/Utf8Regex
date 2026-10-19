using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DotTailRepeatTests
{
    private static Utf8ResourcePolicy Policy(uint depth = 128, ulong result = 1 << 20) => new()
    {
        MaxWorkSteps = 1_000_000, MaxDepth = depth,
        MaxWorkingBytes = 1 << 20, MaxResultBytes = result,
    };

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
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Policy());
            var expected = oracle.Match(text);
            var actual = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, regex.IsMatch(input));
            Assert.Equal(expected.Success, regex.Match(input).Success);
            Assert.Equal(expected.Success, actual.Success);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            if (!actual.Success) continue;
            Assert.Equal(expected.Value, actual.GetValueString());
            Assert.Equal((expected.Index, expected.Length), (actual.IndexInBytes, actual.LengthInBytes));
            foreach (var number in oracle.GetGroupNumbers().Where(number => number != 0))
            {
                var wanted = expected.Groups[number];
                var group = actual.GetGroup(number);
                Assert.Equal(wanted.Success, group.Success);
                Assert.Equal(wanted.Value, group.GetValueString());
                Assert.Equal(wanted.Captures.Count, group.CaptureCount);
                for (var i = 0; i < wanted.Captures.Count; i++)
                    Assert.Equal(wanted.Captures[i].Value, group.GetCapture(i).GetValueString());
            }
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    public void DotBoundsCountScalarsAndPreserveCapturedByteRanges(RegexOptions mode)
    {
        var regex = new Utf8Regex("(?m:^(.{1,2})$)", mode, Timeout.InfiniteTimeSpan, Policy(1));
        var input = "😀é\n中"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var first = regex.MatchDetailed(input);
        Assert.Equal((0, 6), (first.IndexInBytes, first.LengthInBytes));
        Assert.Equal("😀é", first.GetGroup(1).GetValueString());
        Assert.Equal(1, first.GetGroup(1).CaptureCount);
        Assert.Equal(2, regex.Count("AB\nXY"u8));
        Assert.Equal("😀é", first.GetGroup(1).GetValueString());
    }

    [Fact]
    public void UselessDotAlternativesLoseDepthButOverlappingTailsStillNeedIt()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 2048) + "\n");
        var policy = Policy(1, 1);
        Assert.Equal(1, new Utf8Regex("(?m:^(.+)$)", RegexOptions.None,
            Timeout.InfiniteTimeSpan, policy).Count(input));
        var overlapping = new Utf8Regex("(?m:^(.+)a$)", RegexOptions.None, Timeout.InfiniteTimeSpan, policy);
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
            var match = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, Policy())
                .MatchDetailed(Encoding.UTF8.GetBytes(text));
            Assert.Equal(captured is not null, match.Success);
            if (match.Success) Assert.Equal(captured, match.GetGroup(1).GetValueString());
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
    [InlineData(false)] [InlineData(true)]
    public void DotScanObservesOriginalInterruptionAndReleasesScratch(bool timeout)
    {
        var program = new Utf8Regex("(?m:^(.+)$)").Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", 2048)));
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

    [Fact]
    public void ValidFirstLineDoesNotHideMalformedUtf8Tail()
    {
        var regex = new Utf8Regex("(?m:^(.+)$)", RegexOptions.None, Timeout.InfiniteTimeSpan, Policy());
        byte[] input = [.. "abc\n"u8, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.Match(input));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailed(input));
    }
}
