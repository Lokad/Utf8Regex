using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTemplateAuthorityTests
{
    private const string Template = @"<\g<x>>";

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ColdTemplateDeadlineStartsBeforePreparation(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(200));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        Action<ulong> hook = _ => { if (++checkpoints == 8) ticks = 250; };
        construction.Budget.CheckpointHook = hook;
        var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, result, CancellationToken.None));
        Assert.Equal(TimeSpan.FromMilliseconds(200), error.MatchTimeout);
        Assert.Equal("😀a", error.Input);
        Assert.Equal(regex.Pattern, error.Pattern);
        Assert.Equal(8, checkpoints);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal(0ul, regex.DebugReplacementPlanCachePayloadBytes);
        Assert.Same(hook, construction.Budget.CheckpointHook);
        Assert.Equal(0ul, construction.Budget.ReservedBytes);
        ticks = 0;
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, Template));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ColdTemplateCancellationStopsPreparationAndAllowsFreshCall(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)");
        using var cancellation = new CancellationTokenSource();
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        Action<ulong> hook = _ => { if (++checkpoints == 8) cancellation.Cancel(); };
        construction.Budget.CheckpointHook = hook;
        var error = Assert.Throws<OperationCanceledException>(() => Run(regex, result, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(8, checkpoints);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Same(hook, construction.Budget.CheckpointHook);
        Assert.Equal(0ul, construction.Budget.ReservedBytes);
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, Template));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void WarmCachedPlanObservesNewDeadlineWithoutInvalidatingRetainedResults(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(200));
        var retained = regex.Subn("😀a"u8, Template);
        var payload = regex.DebugReplacementPlanCachePayloadBytes;
        var reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads == 1 ? 0 : 250))
        {
            _ = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, result, CancellationToken.None));
            Assert.Equal(3, reads); // Entry, cache check, own-authority check before fallback.
        }
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal(payload, regex.DebugReplacementPlanCachePayloadBytes);
        Assert.Equal("<😀><a>"u8.ToArray(), retained.ResultBytes);
        Assert.Equal(2, retained.ReplacementCount);
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, Template));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void PreparationKeepsSeparateNumericBackstopAndFreshExecutionAllowance(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(regex, result, CancellationToken.None));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(1ul, error.Limit);
        Assert.Equal(2ul, error.Requested);
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        var again = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(regex, result, CancellationToken.None));
        Assert.Equal(error.ResourceKind, again.ResourceKind);
        Assert.Equal(error.Requested, again.Requested);
    }

    [Fact]
    public void ColdPlanKeepsEnclosingConstructionTokenAndAllowsRecovery()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)", PythonReCompileOptions.None, TimeSpan.FromSeconds(30));
        using var parent = new CancellationTokenSource();
        using var operation = new CancellationTokenSource();
        using (var construction = Utf8ConstructionScope.Enter())
        {
            construction.Budget.CancellationToken = parent.Token;
            var checkpoints = 0;
            Action<ulong> hook = _ => { if (++checkpoints == 8) parent.Cancel(); };
            construction.Budget.CheckpointHook = hook;
            var error = Assert.Throws<OperationCanceledException>(() => Run(regex, 0, operation.Token));
            Assert.Equal(parent.Token, error.CancellationToken);
            Assert.False(operation.IsCancellationRequested);
            Assert.Same(hook, construction.Budget.CheckpointHook);
            Assert.Equal(parent.Token, construction.Budget.CancellationToken);
            Assert.Equal(0ul, construction.Budget.ReservedBytes);
            Assert.Equal(0ul, construction.Budget.Depth);
            Assert.True(construction.Budget.WorkSteps > 0);
        }
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, Template));
    }

    [Fact]
    public async Task WaitingCallerCanCancelWithoutInvalidatingPendingPreparation()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)", PythonReCompileOptions.None, TimeSpan.FromSeconds(30));
        var preparing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var owner = Task.Run(() =>
        {
            using var construction = Utf8ConstructionScope.Enter();
            var checkpoints = 0;
            construction.Budget.CheckpointHook = _ =>
            {
                if (++checkpoints != 8) return;
                preparing.SetResult();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)), "Release pending preparation.");
            };
            return regex.ReplaceToString("😀a"u8, Template);
        });
        try
        {
            await preparing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
            var waiter = Task.Run(() =>
            {
                var reads = 0;
                using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
                {
                    if (++reads == 3) waiting.SetResult();
                    return 0;
                });
                return Record.Exception(() => Run(regex, 0, cancellation.Token));
            });
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            var error = Assert.IsType<OperationCanceledException>(await waiter.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.False(owner.IsCompleted);
            Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        }
        finally { release.Set(); }
        Assert.Equal("<😀><a>", await owner.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, Template));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PreparingCallerInterruptionDoesNotBecomeHealthyWaiterFailure(bool timeout)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(200));
        var preparing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        long ownerTicks = 0;
        var owner = Task.Run(() =>
        {
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => Volatile.Read(ref ownerTicks));
            using var construction = Utf8ConstructionScope.Enter();
            var checkpoints = 0;
            construction.Budget.CheckpointHook = _ =>
            {
                if (++checkpoints != 8) return;
                preparing.SetResult();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            };
            return Record.Exception(() => Run(regex, 0, cancellation.Token));
        });
        try
        {
            await preparing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var waiter = Task.Run(() =>
            {
                var reads = 0;
                using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
                {
                    if (++reads == 3) waiting.SetResult();
                    return 0;
                });
                return regex.ReplaceToString("😀a"u8, Template);
            });
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (timeout) Volatile.Write(ref ownerTicks, 250);
            else cancellation.Cancel();
            release.Set();
            Assert.Equal("<😀><a>", await waiter.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally { release.Set(); }
        var ownerError = await owner.WaitAsync(TimeSpan.FromSeconds(10));
        if (timeout) Assert.IsType<RegexMatchTimeoutException>(ownerError);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(ownerError).CancellationToken);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, Template));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Fact]
    public async Task UnlimitedWaiterDoesNotInheritAnotherCallersCancellation()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)");
        var preparing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        string? result = null;
        Exception? failure = null;
        var owner = Task.Run(() =>
        {
            using var construction = Utf8ConstructionScope.Enter();
            var checkpoints = 0;
            construction.Budget.CheckpointHook = _ =>
            {
                if (++checkpoints != 8) return;
                preparing.SetResult();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            };
            return Record.Exception(() => Run(regex, 0, cancellation.Token));
        });
        var waiter = new Thread(() =>
        {
            try { result = regex.ReplaceToString("😀a"u8, Template); }
            catch (Exception error) { failure = error; }
            finally { completed.SetResult(); }
        }) { IsBackground = true };
        try
        {
            await preparing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            waiter.Start();
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            // No finite deadline reads exist in this lane. The dedicated thread
            // has only the cache's pending Monitor.Wait as a blocking operation.
            while ((waiter.ThreadState & ThreadState.WaitSleepJoin) == 0)
            {
                Assert.False(completed.Task.IsCompleted);
                Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(10));
                await Task.Delay(5);
            }
            cancellation.Cancel();
            release.Set();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Null(failure);
            Assert.Equal("<😀><a>", result);
        }
        finally { release.Set(); }
        var ownerError = Assert.IsType<OperationCanceledException>(await owner.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(cancellation.Token, ownerError.CancellationToken);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, Template));
    }

    private static void Run(Utf8PythonRegex regex, int result, CancellationToken token)
    {
        if (result == 0) _ = regex.Replace("😀a"u8, Template, 0, 0, token);
        else if (result == 1) _ = regex.ReplaceToString("😀a"u8, Template, 0, 0, token);
        else if (result == 2) _ = regex.Subn("😀a"u8, Template, 0, 0, token);
        else _ = regex.SubnToString("😀a"u8, Template, 0, 0, token);
    }
}
