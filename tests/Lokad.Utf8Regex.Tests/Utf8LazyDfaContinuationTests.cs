using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LazyDfaContinuationTests
{
    // Private stage tests borrow the parent's owner; public factory/graph loans
    // are independently covered by Utf8DfaConstructionAdmissionTests.
    private static object Compiler(Utf8ConstructionBudget budget)
    {
        var type = typeof(Utf8ByteSafeLazyDfaVerifierProgram).GetNestedType("Compiler", BindingFlags.NonPublic);
        Assert.NotNull(type);
        return Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null, args: [budget], culture: null) ?? throw new InvalidOperationException();
    }

    private static object Call(object compiler, string name, params object?[] arguments)
        => compiler.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(compiler, arguments)
            ?? throw new InvalidOperationException(name);

    private static Utf8ByteSafeLazyDfaCompileFailureKind Failure(object compiler)
        => Assert.IsType<Utf8ByteSafeLazyDfaCompileFailureKind>(compiler.GetType().GetField("_failureKind", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(compiler));

    private static Utf8ByteSafeLinearVerifierStep[] Deep(int depth)
    {
        Utf8ByteSafeLinearVerifierStep[] body = [Utf8ByteSafeLinearVerifierStep.MatchText("ab"u8.ToArray())];
        for (var index = 0; index < depth; index++) body = [Utf8ByteSafeLinearVerifierStep.LoopProgram(body, 1, 1)];
        return [.. body, Utf8ByteSafeLinearVerifierStep.Accept()];
    }

    [Theory]
    [InlineData(65)]
    [InlineData(4097)]
    [InlineData(16385)]
    public void DeepNfaEmissionReachesBodiesBeforeEnforcingTheExistingStateCap(int depth)
    {
        using var parent = Utf8ConstructionScope.Enter();
        var compiler = Compiler(parent.Budget);
        object?[] arguments = [Deep(depth), null, false, false];
        var found = Assert.IsType<bool>(Call(compiler, "TryCompileNfa", arguments));
        Assert.Equal(depth == 65, found);
        // Depth now counts all live explicit pending frames, including resumes.
        Assert.True(parent.Budget.PeakDepth >= (ulong)depth + 1);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.True(parent.Budget.PeakBytes > 0);
        if (!found)
        {
            Assert.Equal(Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded, Failure(compiler));
            Assert.Equal(0UL, parent.Budget.ReservedBytes);
            Assert.Equal(0UL, parent.Budget.Reserved);
            return;
        }
        using var graph = Assert.IsAssignableFrom<IDisposable>(arguments[1]);
        var program = Assert.IsType<Utf8ByteSafeLazyDfaVerifierProgram>(Call(compiler, "BuildDfa", arguments[1], false, false));
        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("abx"u8, 0, out var length));
        Assert.Equal(2, length);
        Assert.False(program.TryMatch("ac"u8, 0, out _));
    }

    [Theory]
    [InlineData(65, true)]
    [InlineData(16385, true)]
    [InlineData(65, false)]
    [InlineData(16385, false)]
    public void OptionalFirstTransitionWalkRetainsTailAndFailureSemantics(int depth, bool consume)
    {
        Utf8ByteSafeLinearVerifierStep[] body = consume ? [Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'a')]
            : [Utf8ByteSafeLinearVerifierStep.RequireEnd(RegexOptions.None), Utf8ByteSafeLinearVerifierStep.Accept()];
        for (var index = 0; index < depth; index++) body = [Utf8ByteSafeLinearVerifierStep.LoopProgram(body, 0, 1), Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'z')];
        using var parent = Utf8ConstructionScope.Enter();
        object?[] arguments = [body, 0, null];
        Assert.Equal(consume, Assert.IsType<bool>(Call(Compiler(parent.Budget), "TryGetFirstDeterministicTransitionSet", arguments)));
        Assert.Equal(consume ? AsciiCharClass.ForByte((byte)'a') : AsciiCharClass.Empty, Assert.IsType<AsciiCharClass>(arguments[2]));
        Assert.True(parent.Budget.WorkSteps >= (ulong)depth);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);
    }

    [Fact]
    public void EveryNfaContinuationWorkDepthAndStorageCutoffReleasesItsScratch()
    {
        var steps = Deep(65);
        ulong work, depth, bytes, slots;
        using (var baseline = Utf8ConstructionScope.Enter())
        {
            object?[] arguments = [steps, null, false, false];
            Assert.True(Assert.IsType<bool>(Call(Compiler(baseline.Budget), "TryCompileNfa", arguments)));
            using var graph = Assert.IsAssignableFrom<IDisposable>(arguments[1]);
            work = baseline.Budget.WorkSteps;
            depth = baseline.Budget.PeakDepth;
            bytes = baseline.Budget.PeakBytes;
            slots = baseline.Budget.Peak;
        }
        var limits = Enumerable.Range(0, checked((int)work))
            .Select(static value => (Kind: Utf8ResourceLimitKind.ConstructionWorkSteps, Limit: (ulong)value))
            .Concat(Enumerable.Range(0, checked((int)depth)).Select(static value => (Utf8ResourceLimitKind.ConstructionDepth, (ulong)value)))
            .Concat(new[] { (Utf8ResourceLimitKind.ConstructionBytes, 1UL), (Utf8ResourceLimitKind.ConstructionBytes, bytes - 1),
                (Utf8ResourceLimitKind.ConstructionInstructionCapacity, 0UL), (Utf8ResourceLimitKind.ConstructionInstructionCapacity, slots - 1) });
        foreach (var (kind, limit) in limits)
        {
            using var parent = new Utf8ConstructionScope(
                kind == Utf8ResourceLimitKind.ConstructionInstructionCapacity ? limit : Utf8ConstructionScope.DefaultInstructionCapacity,
                kind == Utf8ResourceLimitKind.ConstructionBytes ? limit : Utf8ConstructionScope.DefaultBufferBytes,
                kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? limit : Utf8ConstructionScope.DefaultWorkSteps,
                kind == Utf8ResourceLimitKind.ConstructionDepth ? limit : Utf8ConstructionScope.DefaultDepth);
            object?[] arguments = [steps, null, false, false];
            var failure = Assert.Throws<TargetInvocationException>(() => Call(Compiler(parent.Budget), "TryCompileNfa", arguments));
            Assert.Equal(kind, Assert.IsType<Utf8ResourceLimitExceededException>(failure.InnerException).ResourceKind);
            Assert.Equal(0UL, parent.Budget.ReservedBytes);
            Assert.Equal(0UL, parent.Budget.Reserved);
            Assert.Equal(0UL, parent.Budget.Depth);
        }
        using var recovery = Utf8ConstructionScope.Enter();
        var existing = recovery.Budget.AllocateArray<byte>(7, false);
        var checkpoint = recovery.Budget.ReservedBytes;
        object?[] recoveryArguments = [steps, null, false, false];
        Assert.True(Assert.IsType<bool>(Call(Compiler(recovery.Budget), "TryCompileNfa", recoveryArguments)));
        using var recoveredGraph = Assert.IsAssignableFrom<IDisposable>(recoveryArguments[1]);
        Assert.True(recovery.Budget.ReservedBytes > checkpoint);
        Assert.Equal(0UL, recovery.Budget.Depth);
        recovery.Budget.ReleaseArray(existing, false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NfaCancellationOrDeadlineReturnsEveryPendingFrame(bool expire)
    {
        long clock = 0;
        using var clockScope = Utf8ExecutionDeadline.UseClockForDiagnostics(() => clock);
        using var parent = Utf8ConstructionScope.Enter();
        using var source = new CancellationTokenSource();
        parent.Budget.Deadline = Utf8ExecutionDeadline.Start(expire ? TimeSpan.FromSeconds(30) : Regex.InfiniteMatchTimeout);
        parent.Budget.CancellationToken = source.Token;
        var entered = false;
        parent.Budget.CheckpointHook = _ =>
        {
            if (parent.Budget.Depth < 3) return;
            entered = true;
            if (expire) clock = 30_001;
            else source.Cancel();
        };
        object?[] arguments = [Deep(4097), null, false, false];
        var failure = Assert.Throws<TargetInvocationException>(() => Call(Compiler(parent.Budget), "TryCompileNfa", arguments));
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure.InnerException);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure.InnerException).CancellationToken);
        Assert.True(entered);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.Depth);
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    [InlineData(300, false)]
    [InlineData(16385, false)]
    public void DeterministicConstructionAdmitsAtMost256States(int width, bool accepted)
    {
        var outcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(new([
            Utf8ByteSafeLinearVerifierStep.MatchText(Enumerable.Repeat((byte)'a', width).ToArray()), Utf8ByteSafeLinearVerifierStep.Accept()]));
        Assert.Equal(accepted, outcome.Succeeded);
        Assert.Equal(accepted ? Utf8ByteSafeLazyDfaCompileFailureKind.None : Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded, outcome.FailureKind);
        if (accepted) Assert.Equal(256, outcome.Program.StateCount);
    }

    [Theory]
    [InlineData(2047, true)]
    [InlineData(2048, false)]
    public void NfaConstructionAdmitsAtMost2048States(int width, bool accepted)
    {
        using var parent = Utf8ConstructionScope.Enter();
        var compiler = Compiler(parent.Budget);
        object?[] arguments = [new[] { Utf8ByteSafeLinearVerifierStep.MatchText(Enumerable.Repeat((byte)'a', width).ToArray()), Utf8ByteSafeLinearVerifierStep.Accept() }, null, false, false];
        Assert.Equal(accepted, Assert.IsType<bool>(Call(compiler, "TryCompileNfa", arguments)));
        Assert.Equal(0UL, parent.Budget.Depth);
        if (accepted)
        {
            using var graph = Assert.IsAssignableFrom<IDisposable>(arguments[1]);
            Assert.True(parent.Budget.ReservedBytes > 0);
        }
        else
        {
            Assert.Equal(Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded, Failure(compiler));
            Assert.Equal(0UL, parent.Budget.ReservedBytes);
        }
    }

    [Fact]
    public void TransitionAdmissionPrecedesExtendingThePublishedExtent()
    {
        using var parent = Utf8ConstructionScope.Enter();
        using var sets = Utf8ConstructionBuffer<AsciiCharClass>.Create(parent.Budget);
        using var targets = Utf8ConstructionBuffer<int>.Create(parent.Budget, true);
        using var offsets = Utf8ConstructionBuffer<int>.Create(parent.Budget);
        using var counts = Utf8ConstructionBuffer<int>.Create(parent.Budget);
        sets.AddRange(Enumerable.Repeat(AsciiCharClass.ForByte((byte)'a'), 4096).ToArray());
        targets.AddRange(new int[4096]);
        offsets.Add(4096);
        counts.Add(0);
        var checkpoint = parent.Budget.ReservedBytes;
        object?[] arguments = [0, AsciiCharClass.ForByte((byte)'b'), 0, offsets, counts, sets, targets];
        var failure = Assert.Throws<TargetInvocationException>(() => Call(Compiler(parent.Budget), "AddDeterministicTransition", arguments));
        Assert.Equal("CompilationBudgetExceededException", failure.InnerException?.GetType().Name);
        Assert.Equal(4096, sets.Count);
        Assert.Equal(4096, targets.Count);
        Assert.Equal(checkpoint, parent.Budget.ReservedBytes);
    }
}
