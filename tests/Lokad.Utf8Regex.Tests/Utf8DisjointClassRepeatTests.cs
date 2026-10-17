using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DisjointClassRepeatTests
{
    [Theory]
    [InlineData("([a-z]+)-([0-9]+)", "abc-12 other-345")]
    [InlineData("([a-z]{2,3})-", "a- abc- abcde-")]
    [InlineData("([a-z]{0,3})-", "- abc- abcde-")]
    [InlineData("([a-z]{0})-", "abc--")]
    [InlineData("(?:([a-z]+)-x|([a-z]+)-y)", "abc-y abc-x")]
    [InlineData("(([a-z]+)-)*X", "abc-def-X")]
    [InlineData("(?=([a-z]+)-)([a-z]+)-", "abc-")]
    [InlineData("(?i:([a-z]+))-", "AbC- dEF-")]
    [InlineData(@"(\w+)-", "abc- def-")]
    [InlineData(@"(\p{L}+)-", "abc- def-")]
    [InlineData("([a-z]+)a", "baaa")]
    [InlineData("([a-z]+)(?i:A)", "baaa baA")]
    [InlineData("([^0-9]+)-", "abc-def-")]
    [InlineData("([a-z]+?)-", "abc-")]
    [InlineData("(([a-z]+))-", "abc-")]
    [InlineData("([a-z])+-", "abc-")]
    public void CapturesAndFallbackKeepOverlappingBclSemantics(string pattern, string text)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
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

    [Theory]
    [InlineData("([é😀]{1,2})-", "é😀-", "é😀")]
    [InlineData("([é😀]{2,3})-", "é-", null)]
    [InlineData("([é😀]*)-", "-", "")]
    public void RepeatBoundsCountCompleteScalars(string pattern, string text, string? expected)
    {
        var match = new Utf8Regex(pattern).MatchDetailed(Encoding.UTF8.GetBytes(text));
        Assert.Equal(expected is not null, match.Success);
        if (expected is not null)
        {
            Assert.Equal(text, match.GetValueString());
            Assert.Equal(expected, match.GetGroup(1).GetValueString());
            Assert.Equal(Encoding.UTF8.GetByteCount(text), match.LengthInBytes);
        }
    }

    [Fact]
    public void RightToLeftKeepsItsExistingTraversalAndCapture()
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft;
        var match = new Utf8Regex("([a-z]+)-", options).MatchDetailed("abc- def-"u8);
        Assert.Equal("def-", match.GetValueString());
        Assert.Equal("def", match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void OnlyProvedUselessAlternativesLoseTheirDepthRequirement()
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = 1, MaxWorkSteps = 100000,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 };
        var text = Encoding.UTF8.GetBytes(new string('a', 2048) + "-");
        Assert.Equal(1, new Utf8Regex("([a-z]+)-", RegexOptions.CultureInvariant,
            Timeout.InfiniteTimeSpan, policy).Count(text));
        var overlapping = new Utf8Regex("([a-z]+)a", RegexOptions.CultureInvariant,
            Timeout.InfiniteTimeSpan, policy);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => overlapping.Count(text));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, error.ResourceKind);
    }

    [Fact]
    public void EveryWorkCutoffDeniesWithoutLeakingTheParentLoan()
    {
        var regex = new Utf8Regex(@"(\p{L}{2,3})-");
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var input = "éé- AB-"u8.ToArray();
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(2, result.Count);
        Assert.Equal(13ul, baseline.Ledger.ReservedBytes);
        for (ulong limit = 1; limit < baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionInsideScanReleasesScratch(bool timeout)
    {
        var regex = new Utf8Regex(@"(\p{L}+)-");
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var input = Encoding.UTF8.GetBytes(new string('é', 2048) + "-");
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
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
            Assert.True(invocation.Budget.Steps >= 64);
        }
        finally { session.Complete(); }
    }
}
