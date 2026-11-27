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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = oracle.Match(text);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
        Assert.Equal(oracle.Count(text), regex.Count(input));
    }

    [Theory]
    [InlineData("([é😀]+)", "é😀é", "é😀é")]
    [InlineData("([é😀]{2})", "é😀é", "é😀")]
    [InlineData("([é😀]*)", "", "")]
    public void TerminalBoundsCountCompleteScalars(string pattern, string text, string expected)
    {
        var match = Utf8MatchSequence.First(new Utf8Regex(pattern).CreateMatchCursor(Encoding.UTF8.GetBytes(text)));
        Assert.True(match.Success);
        Assert.Equal(expected, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal(Encoding.UTF8.GetByteCount(expected), match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes);
    }

    [Fact]
    public void EmptyAsciiRunsAdvanceAcrossCompleteSupplementaryScalars()
    {
        var regex = new Utf8Regex("([a-z]{0,4})");
        var input = "😀abcde é"u8;
        var ranges = new List<(int Start, int Length)>();
        foreach (var match in regex.EnumerateMatches(input))
            ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(new[] { (0, 0), (4, 4), (8, 1), (9, 0), (10, 0), (12, 0) }, ranges);
        Assert.Equal(6, regex.Count(input));
    }

    [Theory]
    [InlineData(1L, long.MaxValue, true)]
    [InlineData(2L, 2L, true)]
    [InlineData(3L, 3L, false)]
    public void RequiredAssertionEndKeepsOnlyItsValidEndpoints(long minimum, long maximum, bool success)
    {
        // The assertion caller requires edge2; a greedy body can first reach3.
        // Accept alone must not discard the alternative ending at the caller's edge.
        Utf8SliceNode body = new Utf8SliceNode.CaptureNode(0,
            new Utf8SliceNode.RepeatNode(new Utf8SliceNode.ShorthandNode(Utf8SliceShorthand.Word), minimum, maximum, false));
        var behind = new Utf8SliceNode.LookbehindNode(body, true) { ForwardScalarCount = 2 };
        var root = new Utf8SliceNode.ConcatNode([behind, new Utf8SliceNode.LiteralNode('x', false)]);
        var program = Utf8SliceLowerer.Lower(root, null, false, false, false)!;
        Assert.False(program.Assertions[0].Body.RightToLeft);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var invocation = new Utf8SliceInvocation(new(32, 1 << 20), Timeout.InfiniteTimeSpan, default);
        var result = Utf8SliceRunner.TryFindFirst(program, "abx"u8, 2, starts, ends, false, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(success, result.Found);
        if (!success) return;
        Assert.Equal((2, 3), (result.StartByte, result.EndByte));
        Assert.Equal((0, 2), (starts[0], ends[0]));
    }

    [Fact]
    public void RightToLeftKeepsItsExistingCapture()
    {
        var regex = new Utf8Regex("([a-z]+)", options: RegexOptions.CultureInvariant | RegexOptions.RightToLeft);
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor("abc def"u8.ToArray()));
        Assert.Equal("def", Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
    }

    [Fact]
    public void CountCanAvoidFramesButCaptureSnapshotsStillRequireResultAdmission()
    {
        var regex = new Utf8Regex("([a-z]+)", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 1,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }
        });
        var input = Encoding.UTF8.GetBytes(new string('a', 2048));
        Assert.Equal(1, regex.Count(input));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(input)));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
    }

    [Theory]
    [InlineData(@"(\p{L}{1,3})", "éé A𐐀!")]
    [InlineData("([a-z]{1,3})", "abc de!")]
    [InlineData("([ace]+)", "ace b aaa")]
    [InlineData("(x{1,3})", "xx x!")]
    [InlineData("(é{1,3})", "éé é!")]
    public void EveryWorkCutoffPreservesTheParentLoan(string pattern, string text)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = new Utf8Regex(pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(text);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(1, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        Assert.Equal(2, outcome.Count);
        Assert.Equal(13ul, baseline.Ledger.ReservedBytes);
        Assert.Equal(0u, baseline.Budget.PeakDepth);
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    public void OriginalInterruptionDuringCountAndMatchingPreservesParentLoan(bool timeout, bool ascii, bool count)
    {
        var program = new Utf8Regex(ascii ? "([a-z]+)" : @"(\p{L}+)").Inspection.AvailableOwnedSliceProgram!;
        // ASCII ranges charge bounded 2 KiB blocks; keep enough input to reach
        // the diagnostic hook's 64-step cadence inside either repeat loop.
        var input = Encoding.UTF8.GetBytes(new string(ascii ? 'a' : 'é', ascii ? 262144 : 2048));
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            // Direct runner calls start at execution, so the first hook is live
            // matching work regardless of which eligible counter/repeat runs.
            if (entered) return;
            entered = true;
            if (timeout) ticks = 25; else token.Cancel();
        });
        var invocation = new Utf8SliceInvocation(new(1, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            var failure = Record.Exception(() =>
            {
                if (count) Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
                else Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, true, ref invocation);
            });
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.True(entered);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }
}
