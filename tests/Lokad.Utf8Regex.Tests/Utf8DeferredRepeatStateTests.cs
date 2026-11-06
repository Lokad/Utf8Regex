using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeferredRepeatStateTests
{
    private static Utf8SliceProgram Program(string pattern) =>
        new Utf8Regex(pattern).Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();

    [Fact]
    public void FusedImportCountNeedsNoLocalWorkingBuffers()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 10000, MaxDepth = 1,
            MaxWorkingBytes = 1, MaxResultBytes = 1 };
        var regex = new Utf8Regex("""^import\s+(?<shared>shared\s+)?"(?<path>(\.|[^\\"]*))".*$""",
            RegexOptions.Multiline, Regex.InfiniteMatchTimeout, policy);
        using var token = new CancellationTokenSource();
        Assert.Equal(2, regex.Count("import \"x\";\nimport shared \"y\";"u8, token.Token));
    }

    [Fact]
    public void UnfusedRepeatTablesAreReusedBetweenAttempts()
    {
        var program = Program("a{1,3}a");
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        try
        {
            Assert.Empty(workspace.Counts);
            var first = Utf8SliceRunner.TryMatchAt(program, "aaaa"u8, 0, [], [], false, false, true,
                ref workspace, ref invocation);
            Assert.True(first.IsMatch);
            Assert.Equal(4, first.EndIndex);
            var counts = workspace.Counts; var positions = workspace.Positions;
            Assert.Equal(program.RepeatCount, counts.Length);
            Assert.NotEmpty(counts);
            var reserved = invocation.Ledger.ReservedBytes;
            var second = Utf8SliceRunner.TryMatchAt(program, "aaa"u8, 0, [], [], false, false, true,
                ref workspace, ref invocation);
            Assert.True(second.IsMatch);
            Assert.Equal(3, second.EndIndex);
            Assert.Same(counts, workspace.Counts);
            Assert.Same(positions, workspace.Positions);
            Assert.Equal(reserved, invocation.Ledger.ReservedBytes);
        }
        finally { workspace.Release(ref invocation); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void AssertionRootsRetainTheirTablesAcrossChildResumeAndHistoryPublication()
    {
        var program = Program(@"(?<x>a{1,3})(?=b{1,2}c)b+c");
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var first = Utf8SliceRunner.TryMatchAt(program, "aaabbc"u8, 0, slots.Starts, slots.Ends, true, false, true,
                ref workspace, ref invocation);
            Assert.True(first.IsMatch);
            Assert.Equal(6, first.EndIndex);
            Assert.Equal(0, slots.Starts[0]); Assert.Equal(3, slots.Ends[0]);
            var counts = workspace.Counts; var positions = workspace.Positions;
            Assert.NotEmpty(counts);
            Utf8SliceRunner.ReleaseHistory(first.CaptureStarts, first.CaptureLengths, ref invocation);
            var reserved = invocation.Ledger.ReservedBytes;
            var second = Utf8SliceRunner.TryMatchAt(program, "abc"u8, 0, slots.Starts, slots.Ends, true, false, true,
                ref workspace, ref invocation);
            Assert.True(second.IsMatch);
            Assert.Equal(3, second.EndIndex);
            Assert.Equal(0, slots.Starts[0]); Assert.Equal(1, slots.Ends[0]);
            Assert.Same(counts, workspace.Counts); Assert.Same(positions, workspace.Positions);
            Utf8SliceRunner.ReleaseHistory(second.CaptureStarts, second.CaptureLengths, ref invocation);
            Assert.Equal(reserved, invocation.Ledger.ReservedBytes);
        }
        finally
        {
            workspace.Release(ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("a{1,3}a", "aaaa")]
    [InlineData("a{1,3}(?=b{1,2}c)b+c", "aaabbc")]
    public void EveryWorkCutoffAndWorkingDenialRefundsTheEnclosingLoan(string pattern, string text)
    {
        var program = Program(pattern); var input = System.Text.Encoding.UTF8.GetBytes(text);
        var baseline = new Utf8SliceInvocation(new(10000, 1000, 1 << 20), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach); Assert.Equal(1, result.Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 1000, 1 << 20), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        var succeeded = false; var deniedWorking = false;
        foreach (ulong maximum in new ulong[] { 14, 32, 64, 96, 128, 512, 2048, 4096 })
        {
            var invocation = new Utf8SliceInvocation(new(10000, 1000, maximum), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            if (outcome.Breach == Utf8ExecutionLimitKind.None) { Assert.Equal(1, outcome.Count); succeeded = true; }
            else { Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, outcome.Breach); deniedWorking = true; }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        Assert.True(succeeded); Assert.True(deniedWorking);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptionDuringInitialStateAcquisitionRefundsTheEnclosingLoan(bool expire)
    {
        var program = Program("a{1,3}a");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { entered = true; if (expire) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(10000, 1000, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            for (var i = 0; i < 60; i++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeStep());
            var failure = Record.Exception(() => Utf8SliceRunner.TryMatchAt(program, "aaaa"u8, 0, [], [], false, false, true,
                ref workspace, ref invocation));
            if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.True(entered);
        }
        finally { workspace.Release(ref invocation); session.Complete(); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
