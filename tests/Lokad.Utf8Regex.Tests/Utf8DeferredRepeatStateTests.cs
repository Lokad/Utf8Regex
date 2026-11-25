using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeferredRepeatStateTests
{
    private static Utf8SliceProgram Program(string pattern) =>
        new Utf8Regex(pattern).Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();

    [Theory]
    [InlineData("x*|y", "y", 0, 2)]
    [InlineData("x*|y", "xx😀y", 2, 4)]
    [InlineData("é*|😀", "😀", 0, 2)]
    public void APreferredTerminalRepeatCanFinishWithoutAnOuterRetry(string pattern, string text, int end, int count)
    {
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 1000, MaxWorkingBytes = 1 }
        });
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(new Utf8CaptureRange(0, end), regex.Match(input));
        Assert.Equal(count, regex.Count(input));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AnEmptyFirstArmStillRetainsARequiredConsumingAlternative(bool nonEmpty, bool full)
    {
        var program = Program("x*|y");
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        try
        {
            var result = Utf8SliceRunner.TryMatchAt(program, "y"u8, 0, [], [], false, nonEmpty, true,
                ref workspace, ref invocation, requireFullInput: full);
            Assert.True(result.IsMatch);
            Assert.Equal(1, result.EndIndex);
        }
        finally { workspace.Release(ref invocation); }

        using var withSuffix = new Utf8Regex("(x*|y)z");
        Assert.Equal(new Utf8CaptureRange(0, 2), withSuffix.Match("yz"u8));
    }

    [Fact]
    public void BooleanTerminalLoopCanAcceptWithoutRepeatingForExtent()
    {
        using var funded = new Utf8Regex("(ab|cd)+", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 1000, MaxWorkingBytes = 1 }
        });
        Assert.True(funded.IsMatch("xxabcdcdyy"u8));
        Assert.False(funded.IsMatch("xxaxcyy"u8));

        using var regex = new Utf8Regex("(ab|cd)+");
        var captures = new Utf8CaptureRange[2];
        Assert.Equal(new Utf8CaptureRange(2, 8), regex.Match("xxabcdcdyy"u8, captures: captures));
        Assert.Equal(new Utf8CaptureRange(6, 8), captures[1]);
        Assert.Equal(1, regex.Count("xxabcdcdyy"u8));
    }

    [Fact]
    public void TerminalLiteralChoicesKeepTheirGreedyRangesWithoutRepeatStorage()
    {
        using var regex = new Utf8Regex("(ab|cd)+", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 1000, MaxWorkingBytes = 1 }
        });
        Assert.Equal(new Utf8CaptureRange(4, 10), regex.Match("😀abcdcdc"u8));
        Assert.Equal(2, regex.Count("😀abcdcdc ab"u8));
    }

    [Theory]
    [InlineData(@"\A([A-Za-z]+)\b")]
    [InlineData(@"\A([A-Za-z]+?)\b")]
    public void TerminalWordRepeatsSkipImpossibleShorterEndpoints(string pattern)
    {
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 256 }
        });
        var prefix = new string('a', 4096);
        var hit = System.Text.Encoding.UTF8.GetBytes(prefix + "😀");
        var miss = System.Text.Encoding.UTF8.GetBytes(prefix + "é");
        var captures = new Utf8CaptureRange[2];

        Assert.Equal(new Utf8CaptureRange(0, prefix.Length), regex.Match(hit, captures: captures));
        Assert.Equal(new Utf8CaptureRange(0, prefix.Length), captures[1]);
        Assert.False(regex.IsMatch(miss));
        Assert.Equal(0, regex.Count(miss));
    }

    [Theory]
    [InlineData(@"\A(\w+)\b", "\u200c")]
    [InlineData(@"\A(\p{L}+?)\b", "\u0301")]
    public void TerminalWordCategoryRepeatsRespectTheBoundaryOnWideInput(string pattern, string wordSuffix)
    {
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 6000 }
        });
        var prefix = string.Concat(Enumerable.Repeat("éa", 2048));
        var hit = System.Text.Encoding.UTF8.GetBytes(prefix + "😀");
        var miss = System.Text.Encoding.UTF8.GetBytes(prefix + wordSuffix);
        var end = System.Text.Encoding.UTF8.GetByteCount(prefix);
        var captures = new Utf8CaptureRange[2];

        Assert.Equal(new Utf8CaptureRange(0, end), regex.Match(hit, captures: captures));
        Assert.Equal(new Utf8CaptureRange(0, end), captures[1]);
        Assert.False(regex.IsMatch(miss));
        Assert.Equal(0, regex.Count(miss));
    }

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    public void WordCategoryScanResumesAtTheFirstWideScalar(int asciiLength)
    {
        using var regex = new Utf8Regex(@"\A(\w+)\b", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 64 }
        });
        var prefix = new string('a', asciiLength) + "é界";
        var hit = System.Text.Encoding.UTF8.GetBytes(prefix + "😀");
        var miss = System.Text.Encoding.UTF8.GetBytes(prefix + "\u200c");
        var end = System.Text.Encoding.UTF8.GetByteCount(prefix);
        var captures = new Utf8CaptureRange[2];

        Assert.Equal(new Utf8CaptureRange(0, end), regex.Match(hit, captures: captures));
        Assert.Equal(new Utf8CaptureRange(0, end), captures[1]);
        Assert.False(regex.IsMatch(miss));
        Assert.Equal(0, regex.Count(miss));
    }

    [Theory]
    [InlineData(@"\A([a.]+)\b", "a..", 1)]
    [InlineData(@"\A([\w.]+)\b", "é..", 2)]
    [InlineData(@"\A([\w.]+)\b", "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz.é!", 55)]
    [InlineData(@"\A(\p{M}+)\b", "\u0301\u0903.", 2)]
    public void MixedClassesPreserveTheirOrderedBoundaryEndpoints(string pattern, string text, int end)
    {
        using var regex = new Utf8Regex(pattern);
        var captures = new Utf8CaptureRange[2];
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        Assert.Equal(new Utf8CaptureRange(0, end), regex.Match(input, captures: captures));
        Assert.Equal(new Utf8CaptureRange(0, end), captures[1]);
    }

    [Fact]
    public void FusedImportCountNeedsNoLocalWorkingBuffers()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 10000, MaxDepth = 1,
            MaxWorkingBytes = 1, MaxResultBytes = 1 };
        var regex = new Utf8Regex("""^import\s+(?<shared>shared\s+)?"(?<path>(\.|[^\\"]*))".*$""", options:             RegexOptions.Multiline, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        using var token = new CancellationTokenSource();
        Assert.Equal(2, regex.Count("import \"x\";\nimport shared \"y\";"u8, token.Token));
    }

    [Fact]
    public void UnfusedRepeatTablesAreReusedBetweenAttempts()
    {
        var program = Program(@"a{1,3}?a\z");
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        try
        {
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
    public void WorkDenialDuringBalanceSetupRefundsThePartialWorkspace()
    {
        var program = Program(@"^(?<x>a)(?<-x>b)");
        var invocation = new Utf8SliceInvocation(new(1, 128, 1024), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));

        var denied = Utf8SliceRunner.TryFindFirst(program, "ab"u8, 0, [], [], false, ref invocation);

        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
        Assert.True(invocation.Budget.PeakWorkingBytes > 13);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Fact]
    public void AssertionRootsRetainTheirTablesAcrossChildResumeAndHistoryPublication()
    {
        var program = Program(@"(?<x>a{1,3}?)(?=b{1,2}c)b+c");
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        var results = new Utf8InvocationResultLedger();
        try
        {
            var first = Utf8SliceRunner.TryMatchAt(program, "aaabbc"u8, 0, slots.Starts, slots.Ends, true, false, true,
                ref workspace, ref invocation);
            Assert.True(first.IsMatch);
            Assert.Equal(6, first.EndIndex);
            Assert.Equal(0, slots.Starts[0]); Assert.Equal(3, slots.Ends[0]);
            var counts = workspace.Counts; var positions = workspace.Positions;
            Assert.NotEmpty(counts);
            var firstHistory = workspace.Stacks.SnapshotHistory("aaabbc"u8.ToArray(), program.GroupNumbers, 0, first.EndIndex,
                ref results, ref invocation);
            Assert.Equal(new[] { (0, 3) }, Utf8SliceHistoryAdmissionTests.CaptureEdges(firstHistory, 1));
            results.Release(ref invocation);
            var reserved = invocation.Ledger.ReservedBytes;
            var second = Utf8SliceRunner.TryMatchAt(program, "abc"u8, 0, slots.Starts, slots.Ends, true, false, true,
                ref workspace, ref invocation);
            Assert.True(second.IsMatch);
            Assert.Equal(3, second.EndIndex);
            Assert.Equal(0, slots.Starts[0]); Assert.Equal(1, slots.Ends[0]);
            Assert.Same(counts, workspace.Counts); Assert.Same(positions, workspace.Positions);
            var secondHistory = workspace.Stacks.SnapshotHistory("abc"u8.ToArray(), program.GroupNumbers, 0, second.EndIndex,
                ref results, ref invocation);
            Assert.Equal(new[] { (0, 1) }, Utf8SliceHistoryAdmissionTests.CaptureEdges(secondHistory, 1));
            results.Release(ref invocation);
            Assert.Equal(reserved, invocation.Ledger.ReservedBytes);
        }
        finally
        {
            results.Release(ref invocation);
            workspace.Release(ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(@"a{1,3}?a\z", "aaaa")]
    [InlineData("a{1,3}(?=b{1,2}c)b+c", "aaabbc")]
    [InlineData(@"\A(?:AB|(?:(?!STOP|HALT)[\s\S]))+ΩSTOP", "é😀ΩSTOP")]
    [InlineData(@"\A(?:AB|(?:(?!STOP|HALT)[\s\S]))*ΩSTOP", "é😀aaΩSTOP")]
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
    public void InterruptionDuringGuardedBlockScanRefundsTheEnclosingLoan(bool expire)
    {
        var program = Program(@"\A(?:AB|(?!STOP|HALT)[\s\S])*STOP");
        var input = System.Text.Encoding.UTF8.GetBytes(new string('x', 65536) + "STOP");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(10000, 1000, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        invocation.CheckpointHook = _ => { if (expire) ticks = 25; else token.Cancel(); };
        Assert.True(invocation.Ledger.TryReserve(13));
        var failure = Record.Exception(() => Utf8SliceRunner.TryMatchAt(program, input, 0, [], [], true, ref invocation));
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptionDuringInitialStateAcquisitionRefundsTheEnclosingLoan(bool expire)
    {
        var program = Program(@"a{1,3}?a\z");
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
