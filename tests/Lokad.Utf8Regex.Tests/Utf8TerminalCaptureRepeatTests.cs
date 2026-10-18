using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TerminalCaptureRepeatTests
{
    [Theory]
    [InlineData("([a-z]+)", "abc def")]
    [InlineData("([a-z]{2,3})", "a abc defgh")]
    [InlineData("([a-z]*)", "ab  cd")]
    [InlineData("([a-z]{0})", "ab")]
    [InlineData("a([a-z]+)", "zaabc")]
    [InlineData("(?:z|)([a-z]{2})", "zab")]
    [InlineData("(?=([a-z]+))a", "aaa")]
    [InlineData(@"(?=([a-z]+))\1b", "ab")]
    [InlineData("(?!([a-z]+))([0-9]+)", "123")]
    [InlineData("([a-z]+?)", "abcd")]
    [InlineData("(([a-z]+))", "abcd")]
    [InlineData("([a-z])+", "abcd")]
    [InlineData("([ace]+)", "😀ace bad cabé")]
    [InlineData("([a-cx-z]{1,3})", "😀abc-xyzzéq")]
    [InlineData("([a-z]{0,4})", "éabcde é")]
    [InlineData("([\\x00-\\x7f]{1,3})", "\0\u007f\u0080\u0001😀")]
    public void TerminalAndExcludedShapesKeepCapturesAndProgression(string pattern, string text)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
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
        }
        Assert.Equal(oracle.Count(text), regex.Count(input));
    }

    [Theory]
    [InlineData("([é😀]+)", "é😀é", "é😀é")]
    [InlineData("([é😀]{2})", "é😀é", "é😀")]
    [InlineData("([é😀]*)", "", "")]
    public void TerminalBoundsCountCompleteScalars(string pattern, string text, string expected)
    {
        var match = new Utf8Regex(pattern).MatchDetailed(Encoding.UTF8.GetBytes(text));
        Assert.True(match.Success);
        Assert.Equal(expected, match.GetGroup(1).GetValueString());
        Assert.Equal(Encoding.UTF8.GetByteCount(expected), match.LengthInBytes);
    }

    [Fact]
    public void EmptyAsciiRunsAdvanceAcrossCompleteSupplementaryScalars()
    {
        var regex = new Utf8Regex("([a-z]{0,4})");
        var input = "😀abcde é"u8;
        var ranges = new List<(int Start, int Length)>();
        foreach (var match in regex.EnumerateMatches(input))
            ranges.Add((match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(new[] { (0, 0), (4, 4), (8, 1), (9, 0), (10, 0), (12, 0) }, ranges);
        Assert.Equal(6, regex.Count(input));
    }

    [Fact]
    public void RequiredAssertionEndKeepsItsShorterAlternatives()
    {
        // The assertion caller requires edge2; a greedy body can first reach3.
        // Accept alone must not discard the alternative ending at the caller's edge.
        Utf8SliceNode body = new Utf8SliceNode.CaptureNode(0,
            new Utf8SliceNode.RepeatNode(new Utf8SliceNode.ShorthandNode(Utf8SliceShorthand.Word), 1, long.MaxValue, false));
        var behind = new Utf8SliceNode.LookbehindNode(body, true) { ForwardScalarCount = 2 };
        var root = new Utf8SliceNode.ConcatNode([behind, new Utf8SliceNode.LiteralNode('x', false)]);
        var program = Utf8SliceLowerer.Lower(root, null, false, false, false)!;
        Assert.False(program.Assertions[0].Body.RightToLeft);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var invocation = new Utf8SliceInvocation(new(100000, 32, 1 << 20), Timeout.InfiniteTimeSpan, default);
        var result = Utf8SliceRunner.TryFindFirst(program, "abx"u8, 2, starts, ends, false, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.True(result.Found);
        Assert.Equal((2, 3), (result.StartByte, result.EndByte));
        Assert.Equal((0, 2), (starts[0], ends[0]));
    }

    [Fact]
    public void RightToLeftKeepsItsExistingCapture()
    {
        var regex = new Utf8Regex("([a-z]+)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft);
        var match = regex.MatchDetailed("abc def"u8);
        Assert.Equal("def", match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void CountCanAvoidFramesButDetailedOutputStillRequiresResultAdmission()
    {
        var regex = new Utf8Regex("([a-z]+)", RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(new string('a', 2048));
        Assert.Equal(1, regex.Count(input));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.MatchDetailed(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
    }

    [Theory]
    [InlineData(@"(\p{L}{1,3})", "éé A𐐀!")]
    [InlineData("([a-z]{1,3})", "abc de!")]
    [InlineData("([ace]+)", "ace b aaa")]
    public void EveryWorkCutoffPreservesTheParentLoan(string pattern, string text)
    {
        var regex = new Utf8Regex(pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(text);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        Assert.Equal(2, outcome.Count);
        Assert.Equal(13ul, baseline.Ledger.ReservedBytes);
        Assert.Equal(0u, baseline.Budget.PeakDepth);
        for (ulong limit = 1; limit < baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, result.Breach);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void OriginalInterruptionInsideTerminalRunReleasesScratch(bool timeout, bool ascii)
    {
        var program = new Utf8Regex(ascii ? "([a-z]+)" : @"(\p{L}+)").Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string(ascii ? 'a' : 'é', 2048));
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
        }
        finally { session.Complete(); }
    }
}
