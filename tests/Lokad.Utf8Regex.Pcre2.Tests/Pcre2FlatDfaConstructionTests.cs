using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2FlatDfaConstructionTests
{
    private static object Compiler(Utf8ConstructionBudget budget) => Activator.CreateInstance(
        typeof(Pcre2AsciiDfaVerifierProgram).GetNestedType("Pcre2DfaCompiler", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing DFA compiler."), budget)
        ?? throw new InvalidOperationException("Missing DFA compiler instance.");

    private static object? Call(object compiler, string name, params object?[] arguments) =>
        (compiler.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(name)).Invoke(compiler, arguments);

    private static Pcre2AsciiDfaStep[] Flat(int width) => Enumerable.Repeat(
        Pcre2AsciiDfaStep.MatchAsciiClass(AsciiCharClass.ForByte((byte)'a')), width)
        .Append(Pcre2AsciiDfaStep.Accept()).ToArray();

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    [InlineData(300, false)]
    [InlineData(16385, false)]
    public void DeterministicConstructionAdmitsAtMost256States(int width, bool accepted)
    {
        var outcome = Pcre2AsciiDfaVerifierProgram.Compile(new(Flat(width)));
        Assert.Equal(accepted, outcome.Succeeded);
        Assert.Equal(accepted ? Pcre2AsciiDfaCompileFailureKind.None : Pcre2AsciiDfaCompileFailureKind.StateBudgetExceeded, outcome.FailureKind);
        if (accepted) Assert.Equal(256, outcome.Program.StateCount);
    }

    [Theory]
    [InlineData(2047, true)]
    [InlineData(2048, false)]
    public void NfaConstructionAdmitsAtMost2048States(int width, bool accepted)
    {
        using var parent = Utf8ConstructionScope.Enter();
        var compiler = Compiler(parent.Budget);
        object?[] arguments = [Flat(width), null, false, false];
        Assert.Equal(accepted, Assert.IsType<bool>(Call(compiler, "TryCompileNfa", arguments)));
        Assert.Equal(0UL, parent.Budget.Depth);
        if (accepted)
        {
            using var graph = Assert.IsAssignableFrom<IDisposable>(arguments[1]);
            Assert.True(parent.Budget.ReservedBytes > 0);
        }
        else
        {
            Assert.Equal(Pcre2AsciiDfaCompileFailureKind.StateBudgetExceeded,
                compiler.GetType().GetField("_failureKind", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(compiler));
            Assert.Equal(0UL, parent.Budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlatNfaInterruptionReleasesScratchAndPreservesTheParent(bool expire)
    {
        long clock = 0;
        using var clockScope = Utf8ExecutionDeadline.UseClockForDiagnostics(() => clock);
        using var parent = Utf8ConstructionScope.Enter();
        using var cancellation = new CancellationTokenSource();
        parent.Budget.Deadline = Utf8ExecutionDeadline.Start(expire ? TimeSpan.FromSeconds(30) : Regex.InfiniteMatchTimeout);
        parent.Budget.CancellationToken = cancellation.Token;
        var loan = parent.Budget.AllocateArray<byte>(7, false);
        loan[0] = 37;
        var checkpoint = parent.Budget.ReservedBytes;
        var entered = false;
        parent.Budget.CheckpointHook = work =>
        {
            if (work < 100) return;
            entered = true;
            if (expire) clock = 30_001;
            else cancellation.Cancel();
        };
        object?[] arguments = [Flat(512), null, false, false];
        var failure = Assert.Throws<TargetInvocationException>(() => Call(Compiler(parent.Budget), "TryCompileNfa", arguments));
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure.InnerException);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure.InnerException).CancellationToken);
        Assert.True(entered);
        Assert.Equal(checkpoint, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.Equal(37, loan[0]);
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
        Assert.Equal("Pcre2DfaCompilationBudgetExceededException", failure.InnerException?.GetType().Name);
        Assert.Equal(4096, sets.Count);
        Assert.Equal(4096, targets.Count);
        Assert.Equal(checkpoint, parent.Budget.ReservedBytes);
    }
}
