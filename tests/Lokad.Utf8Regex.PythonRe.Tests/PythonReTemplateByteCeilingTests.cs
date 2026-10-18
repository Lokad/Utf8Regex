using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTemplateByteCeilingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptySubjectCannotPrepareAnOversizedTemplate(bool liveToken)
    {
        var regex = Create("a", 4096);
        var template = new string('x', 65536);
        using var token = new CancellationTokenSource();
        using var parent = Utf8ConstructionScope.Enter();
        var held = parent.Budget.AllocateArray<byte>(17, false);
        var reserved = parent.Budget.ReservedBytes;
        var maximum = parent.Budget.MaximumBytes;
        Action<ulong> hook = _ => { };
        parent.Budget.CheckpointHook = hook;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var spent = parent.Budget.WorkSteps;
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                regex.Replace([], template, 0, 0, liveToken ? token.Token : default));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, error.ResourceKind);
            Assert.True(error.Requested > error.Limit);
            Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
            Assert.Equal(0ul, regex.DebugReplacementPlanCachePayloadBytes);
            Assert.Equal(reserved, parent.Budget.ReservedBytes);
            Assert.Equal(maximum, parent.Budget.MaximumBytes);
            Assert.Equal(0ul, parent.Budget.Depth);
            Assert.Same(hook, parent.Budget.CheckpointHook);
            Assert.True(parent.Budget.WorkSteps > spent);
            Assert.Equal(17, held.Length);
        }

        Assert.Empty(regex.Replace([], "ok"));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Fact]
    public void ColdByteDenialPrecedesExecutionWithoutCachingFailure()
    {
        var regex = new Utf8PythonRegex("a", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Replace([], "x"));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Fact]
    public void FundedPreparationAndLaterDenialPreserveScalarResults()
    {
        // Fund template-length work to reach the independent byte ceiling.
        var regex = Create("(?P<x>😀|a)", 1 << 20, work: 1024);
        Assert.Empty(regex.Replace([], new string('x', 65536)));
        var retained = regex.Subn("😀a"u8, @"<\g<x>>");
        Assert.Equal(2, retained.ReplacementCount);
        Assert.Equal("<😀><a>"u8.ToArray(), retained.ResultBytes);
        var entries = regex.DebugReplacementPlanCacheEntryCount;
        var payload = regex.DebugReplacementPlanCachePayloadBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            regex.Replace([], new string('y', 1 << 20)));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(entries, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal(payload, regex.DebugReplacementPlanCachePayloadBytes);
        Assert.Equal("<😀><a>"u8.ToArray(), retained.ResultBytes);
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, @"<\g<x>>"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedPreparationKeepsOriginalInterruptionAndRestoresParent(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8PythonRegex("(?P<x>😀|a)", PythonReCompileOptions.None,
            TimeSpan.FromMilliseconds(20), new()
            {
                MaxWorkSteps = 100000, MaxDepth = 256,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
            });
        using var token = new CancellationTokenSource();
        using var parent = Utf8ConstructionScope.Enter();
        var maximum = parent.Budget.MaximumBytes;
        var checkpoints = 0;
        Action<ulong> hook = _ =>
        {
            if (++checkpoints != 8) return;
            if (cancel) token.Cancel(); else ticks = 25;
        };
        parent.Budget.CheckpointHook = hook;
        var failure = Record.Exception(() => regex.Replace("😀a"u8, @"<\g<x>>", 0, 0, token.Token));
        if (cancel)
            Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
        {
            var timeout = Assert.IsType<RegexMatchTimeoutException>(failure);
            Assert.Equal(TimeSpan.FromMilliseconds(20), timeout.MatchTimeout);
            Assert.Empty(timeout.Input);
        }
        Assert.Equal(8, checkpoints);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(maximum, parent.Budget.MaximumBytes);
        Assert.Same(hook, parent.Budget.CheckpointHook);
        ticks = 0;
        parent.Budget.CheckpointHook = null;
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, @"<\g<x>>"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void DistinctCallerAndParentAuthoritiesRemainEffective(int authority)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8PythonRegex("(?P<x>😀|a)", PythonReCompileOptions.None,
            TimeSpan.FromMilliseconds(100), new() { MaxWorkingBytes = 1 << 20 });
        using var caller = new CancellationTokenSource();
        using var parentToken = new CancellationTokenSource();
        using var parent = Utf8ConstructionScope.Enter();
        parent.Budget.CancellationToken = parentToken.Token;
        var parentDeadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        parent.Budget.Deadline = parentDeadline;
        var checkpoints = 0;
        Action<ulong> hook = _ =>
        {
            if (++checkpoints != 8) return;
            if (authority == 0) caller.Cancel();
            else if (authority == 1) parentToken.Cancel();
            else ticks = authority == 2 ? 110 : 25;
        };
        parent.Budget.CheckpointHook = hook;
        var failure = Record.Exception(() => regex.Replace("😀a"u8, @"<\g<x>>", 0, 0, caller.Token));
        if (authority < 2)
            Assert.Equal(authority == 0 ? caller.Token : parentToken.Token,
                Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
            Assert.IsType<RegexMatchTimeoutException>(failure);
        Assert.Same(hook, parent.Budget.CheckpointHook);
        Assert.Same(parentDeadline, parent.Budget.Deadline);
        Assert.Equal(parentToken.Token, parent.Budget.CancellationToken);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(0ul, parent.Budget.Depth);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        ticks = 0;
        parent.Budget.CheckpointHook = null;
        parent.Budget.CancellationToken = default;
        parent.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, @"<\g<x>>"));
    }

    private static Utf8PythonRegex Create(string pattern, ulong working, ulong work = 128) => new(pattern,
        PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
        new() { MaxWorkSteps = work, MaxDepth = 16, MaxWorkingBytes = working, MaxResultBytes = 4096 });
}
