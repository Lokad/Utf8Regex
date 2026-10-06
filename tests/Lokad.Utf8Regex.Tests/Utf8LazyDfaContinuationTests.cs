using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LazyDfaContinuationTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_startTickCount64")]
    private static extern ref long StartedAt(Utf8ExecutionDeadline deadline);

    private static object Compiler()
    {
        var type = typeof(Utf8ByteSafeLazyDfaVerifierProgram).GetNestedType("Compiler", BindingFlags.NonPublic);
        Assert.NotNull(type);
        return Activator.CreateInstance(type, nonPublic: true) ?? throw new InvalidOperationException();
    }

    private static object Call(object compiler, string name, params object?[] arguments)
        => compiler.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(compiler, arguments)
            ?? throw new InvalidOperationException(name);

    private static Utf8SliceInvocation Invocation(object compiler)
        => Assert.IsType<Utf8SliceInvocation>(compiler.GetType().GetField("_invocation", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(compiler));

    private static void SetInvocation(object compiler, Utf8SliceInvocation invocation)
        => (compiler.GetType().GetField("_invocation", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException()).SetValue(compiler, invocation);

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
        var compiler = Compiler();
        object?[] arguments = [Deep(depth), null, false, false];
        var found = Assert.IsType<bool>(Call(compiler, "TryCompileNfa", arguments));
        Assert.Equal(depth == 65, found);
        var invocation = Invocation(compiler);
        Assert.Equal((uint)depth + 1, invocation.Budget.PeakDepth);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.True(invocation.Budget.PeakWorkingBytes > 0);
        if (!found)
        {
            Assert.Equal(Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded, Failure(compiler));
            return;
        }
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
        var compiler = Compiler();
        object?[] arguments = [body, 0, null];
        Assert.Equal(consume, Assert.IsType<bool>(Call(compiler, "TryGetFirstDeterministicTransitionSet", arguments)));
        Assert.Equal(consume ? AsciiCharClass.ForByte((byte)'a') : AsciiCharClass.Empty, Assert.IsType<AsciiCharClass>(arguments[2]));
        Assert.True(Invocation(compiler).Budget.Steps >= (ulong)depth);
        Assert.Equal(0UL, Invocation(compiler).Ledger.ReservedBytes);
    }

    [Fact]
    public void EveryNfaContinuationWorkDepthAndStorageCutoffReleasesItsScratch()
    {
        var steps = Deep(65);
        var baseline = Compiler();
        object?[] baselineArguments = [steps, null, false, false];
        Assert.True(Assert.IsType<bool>(Call(baseline, "TryCompileNfa", baselineArguments)));
        var measured = Invocation(baseline);
        var limits = Enumerable.Range(1, checked((int)measured.Budget.Steps - 1))
            .Select(static value => new Utf8ExecutionLimits((ulong)value, 0, 0))
            .Concat(Enumerable.Range(1, checked((int)measured.Budget.PeakDepth - 1)).Select(static value => new Utf8ExecutionLimits(0, (uint)value, 0)))
            .Concat(new[] { new Utf8ExecutionLimits(0, 0, 1), new Utf8ExecutionLimits(0, 0, measured.Budget.PeakWorkingBytes - 1) });
        foreach (var limit in limits)
        {
            var compiler = Compiler();
            SetInvocation(compiler, new(limit, Regex.InfiniteMatchTimeout, CancellationToken.None));
            object?[] arguments = [steps, null, false, false];
            Assert.False(Assert.IsType<bool>(Call(compiler, "TryCompileNfa", arguments)));
            Assert.Equal(Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded, Failure(compiler));
            Assert.Equal(0UL, Invocation(compiler).Ledger.ReservedBytes);
        }
        var recovery = Compiler();
        var leased = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(leased.Ledger.TryReserve(7));
        SetInvocation(recovery, leased);
        object?[] recoveryArguments = [steps, null, false, false];
        Assert.True(Assert.IsType<bool>(Call(recovery, "TryCompileNfa", recoveryArguments)));
        Assert.Equal(7UL, Invocation(recovery).Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NfaCancellationOrDeadlineReturnsEveryPendingFrame(bool expire)
    {
        var compiler = Compiler();
        using var source = new CancellationTokenSource();
        var deadline = Utf8ExecutionDeadline.Start(expire ? TimeSpan.FromSeconds(30) : Regex.InfiniteMatchTimeout);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            deadline, source.Token);
        var entered = false;
        invocation.CheckpointHook = _ =>
        {
            entered = true;
            // Expire the actual compiler deadline after its pending frames
            // exist, without depending on scheduler or GC timing before entry.
            if (expire) StartedAt(deadline) = Environment.TickCount64 - 30_001;
            else source.Cancel();
        };
        SetInvocation(compiler, invocation);
        object?[] arguments = [Deep(4097), null, false, false];
        var failure = Assert.Throws<TargetInvocationException>(() => Call(compiler, "TryCompileNfa", arguments));
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure.InnerException);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure.InnerException).CancellationToken);
        Assert.True(entered);
        Assert.Equal(0UL, Invocation(compiler).Ledger.ReservedBytes);
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
        var compiler = Compiler();
        object?[] arguments = [new[] { Utf8ByteSafeLinearVerifierStep.MatchText(Enumerable.Repeat((byte)'a', width).ToArray()), Utf8ByteSafeLinearVerifierStep.Accept() }, null, false, false];
        Assert.Equal(accepted, Assert.IsType<bool>(Call(compiler, "TryCompileNfa", arguments)));
        Assert.Equal(0UL, Invocation(compiler).Ledger.ReservedBytes);
        if (!accepted) Assert.Equal(Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded, Failure(compiler));
    }

    [Fact]
    public void TransitionAdmissionPrecedesExtendingThePublishedExtent()
    {
        var sets = Enumerable.Repeat(AsciiCharClass.ForByte((byte)'a'), 4096).ToList();
        var targets = Enumerable.Repeat(0, 4096).ToList();
        var compiler = Compiler();
        object?[] arguments = [0, AsciiCharClass.ForByte((byte)'b'), 0, new List<int> { 4096 }, new List<int> { 0 }, sets, targets];
        var failure = Assert.Throws<TargetInvocationException>(() => Call(compiler, "AddDeterministicTransition", arguments));
        Assert.Equal("CompilationBudgetExceededException", failure.InnerException?.GetType().Name);
        Assert.Equal(4096, sets.Count);
        Assert.Equal(4096, targets.Count);
    }
}
