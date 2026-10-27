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
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
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
        var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, result, CancellationToken.None));
        Assert.Equal(TimeSpan.FromMilliseconds(200), error.MatchTimeout);
        Assert.Equal("😀a", error.Input);
        Assert.Equal(CopyCheckpoint, checkpoints);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        AssertRestored(construction, hook);
        // Interrupted preparation must not poison future invocations.
        ticks = 0;
        Assert.Equal("x", regex.FullMatchDetailedData("😀a"u8).LastGroup);
        Assert.Equal(1, regex.FullMatchDetailedData("😀a"u8).LastIndex);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ColdPreparationCancellationUsesOriginalTokenAndAllowsFreshCall(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+");
        using var cancellation = new CancellationTokenSource();
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        Action<ulong> hook = _ => { if (++checkpoints == CopyCheckpoint) cancellation.Cancel(); };
        construction.Budget.CheckpointHook = hook;
        var error = Assert.Throws<OperationCanceledException>(() => Run(regex, result, cancellation.Token));
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
        Assert.Equal("x", regex.FullMatchDetailedData("😀a"u8).LastGroup);
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
                var match = regex.FullMatchDetailedData("😀a"u8);
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

    private static void Run(Utf8PythonRegex regex, int result, CancellationToken token)
    {
        if (result == 0) _ = regex.FullMatch("😀a"u8, 0, token);
        else if (result == 1) _ = regex.FullMatchDetailed("😀a"u8, 0, token);
        else if (result == 2) _ = regex.FullMatchDetailedData("😀a"u8, 0, token);
        else _ = regex.FullMatchToString("😀a"u8, 0, token);
    }
}
