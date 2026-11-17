using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLazyFullMatchAuthorityTests
{
    // Entry, instruction-array admission, then the bounded instruction copy.
    // Interrupt after acquisition so cleanup is exercised without depending on
    // the retired AST conversion's number of helper checkpoints.
    private const int CopyCheckpoint = 3;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ColdFullMatchDoesNotRestartDeadlineAfterPreparation(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(200));
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        Action<ulong> hook = _ => { if (++checkpoints == CopyCheckpoint) ticks = 250; };
        construction.Budget.CheckpointHook = hook;
        var error = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            if (result == 0) _ = regex.FullMatch("😀a"u8);
            else
            {
                using var cursor = regex.CreateMatchCursor("😀a"u8.ToArray(), mode: Utf8PythonMatchMode.FullMatch);
                cursor.MoveNext(out _);
            }
        });
        Assert.Equal(TimeSpan.FromMilliseconds(200), error.MatchTimeout);
        Assert.Equal("😀a", error.Input);
        Assert.Equal(CopyCheckpoint, checkpoints);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        AssertRestored(construction, hook);
        // Interrupted preparation must not poison future invocations.
        ticks = 0;
        Utf8PythonRangeMatch snapshot1 = default;
        using (var captureCursor1 = regex.CreateMatchCursor(("😀a"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out snapshot1);
        }
        Assert.Equal("x", snapshot1.LastGroup);
        Utf8PythonRangeMatch snapshot2 = default;
        using (var captureCursor2 = regex.CreateMatchCursor(("😀a"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor2.MoveNext(out snapshot2);
        }
        Assert.Equal(1, snapshot2.LastIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ColdPreparationCancellationUsesOriginalTokenAndAllowsFreshCall(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+");
        using var cancellation = new CancellationTokenSource();
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        Action<ulong> hook = _ => { if (++checkpoints == CopyCheckpoint) cancellation.Cancel(); };
        construction.Budget.CheckpointHook = hook;
        var error = Assert.Throws<OperationCanceledException>(() =>
        {
            if (result == 0) _ = regex.FullMatch("😀a"u8, 0, cancellation.Token);
            else
            {
                using var cursor = regex.CreateMatchCursor("😀a"u8.ToArray(), 0, cancellation.Token, mode: Utf8PythonMatchMode.FullMatch);
                cursor.MoveNext(out _);
            }
        });
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(CopyCheckpoint, checkpoints);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        AssertRestored(construction, hook);
        Assert.True(regex.FullMatch("😀a"u8).Success);
    }

    [Fact]
    public void BorrowedInterruptionPreservesEnclosingConstructionToken()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None, TimeSpan.FromSeconds(30));
        using var parent = new CancellationTokenSource();
        using var call = new CancellationTokenSource();
        using (var construction = Utf8ConstructionScope.Enter())
        {
            construction.Budget.CancellationToken = parent.Token;
            var checkpoints = 0;
            Action<ulong> hook = _ => { if (++checkpoints == CopyCheckpoint) parent.Cancel(); };
            construction.Budget.CheckpointHook = hook;
            var error = Assert.Throws<OperationCanceledException>(() => regex.FullMatch("😀a"u8, 0, call.Token));
            Assert.Equal(parent.Token, error.CancellationToken);
            Assert.False(call.IsCancellationRequested);
            Assert.Equal(parent.Token, construction.Budget.CancellationToken);
            AssertRestored(construction, hook);
        }
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Utf8PythonRangeMatch snapshot3 = default;
        using (var captureCursor3 = regex.CreateMatchCursor(("😀a"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor3.MoveNext(out snapshot3);
        }
        Assert.Equal("x", snapshot3.LastGroup);
    }

    [Fact]
    public void FailedConstructionAdmissionIsRetryableWithoutResettingParentWork()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None, TimeSpan.FromSeconds(30));
        using (var construction = new Utf8ConstructionScope(
            Utf8ConstructionScope.DefaultInstructionCapacity, Utf8ConstructionScope.DefaultBufferBytes, 8, 65536))
        {
            var first = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatch("😀a"u8));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, first.ResourceKind);
            var spent = construction.Budget.WorkSteps;
            Assert.True(spent > 0);
            Assert.Equal(0ul, construction.Budget.ReservedBytes);
            var second = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatch("😀a"u8));
            Assert.Equal(first.ResourceKind, second.ResourceKind);
            Assert.True(construction.Budget.WorkSteps >= spent);
            Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        }
        Assert.True(regex.FullMatch("😀a"u8).Success);
    }

    [Fact]
    public async Task ConcurrentColdCallerCancellationDoesNotPoisonSuccessfulPublication()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+");
        using var bothCompiling = new CountdownEvent(2);
        using var cancellation = new CancellationTokenSource();
        // Blocking factory barriers need dedicated workers, independent of pool availability.
        Task<int> RunCold(bool cancel) => Task.Factory.StartNew(() =>
        {
            using var construction = Utf8ConstructionScope.Enter();
            var checkpoints = 0;
            Action<ulong> hook = _ =>
            {
                if (++checkpoints != CopyCheckpoint) return;
                bothCompiling.Signal();
                Assert.True(bothCompiling.Wait(TimeSpan.FromSeconds(10)), "Both cold factories must enter independently.");
                if (cancel) cancellation.Cancel();
            };
            construction.Budget.CheckpointHook = hook;
            if (cancel)
            {
                var error = Assert.Throws<OperationCanceledException>(() => regex.FullMatch("😀a"u8, 0, cancellation.Token));
                Assert.Equal(cancellation.Token, error.CancellationToken);
                AssertRestored(construction, hook);
                Assert.Equal(CopyCheckpoint, checkpoints);
            }
            else
            {
                Utf8PythonRangeMatch match = default;
                using (var captureCursor4 = regex.CreateMatchCursor(("😀a"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
                {
                    captureCursor4.MoveNext(out match);
                }
                Assert.Equal(1, match.LastIndex);
                Assert.Equal("x", match.LastGroup);
                Assert.Same(hook, construction.Budget.CheckpointHook);
            }
            return checkpoints;
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var results = await Task.WhenAll(RunCold(true), RunCold(false));
        Assert.True(results[1] > CopyCheckpoint);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
        Assert.True(regex.FullMatch("😀a"u8).Success);
    }

    private static void AssertRestored(Utf8ConstructionScope construction, Action<ulong> hook)
    {
        Assert.Same(hook, construction.Budget.CheckpointHook);
        Assert.Equal(0ul, construction.Budget.Reserved);
        Assert.Equal(0ul, construction.Budget.ReservedBytes);
        Assert.Equal(0ul, construction.Budget.Depth);
        Assert.True(construction.Budget.WorkSteps > 0);
    }

    [Fact]
    public void PreparationKeepsSeparateWorkBackstopAndFiniteExecutionOwner()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            // Fund the independent preparation byte ceiling; construction
            // work still does not consume the invocation's one work step.
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 16384, MaxResultBytes = 1 });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatch("😀a"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(1ul, error.Limit);
        Assert.Equal(2ul, error.Requested);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
        var again = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatch("😀a"u8));
        Assert.Equal(error.ResourceKind, again.ResourceKind);
        Assert.Equal(error.Requested, again.Requested);
    }

}
