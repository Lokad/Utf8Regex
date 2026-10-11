using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DfaConstructionAdmissionTests
{
    private static Utf8ByteSafeLinearVerifierProgram Literal() => new([
        Utf8ByteSafeLinearVerifierStep.MatchText("ab"u8.ToArray()),
        Utf8ByteSafeLinearVerifierStep.Accept()]);

    private static Utf8ByteSafeLinearVerifierProgram Alternatives() => new([
        Utf8ByteSafeLinearVerifierStep.MatchAnyText(["ab"u8.ToArray(), "cd"u8.ToArray()]),
        Utf8ByteSafeLinearVerifierStep.Accept()]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulGraphsReachTheParentConstructionOwnerAndKeepTheirResults(bool alternatives)
    {
        using var parent = Utf8ConstructionScope.Enter();
        var input = alternatives ? Alternatives() : Literal();
        var outcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(input);
        Assert.True(outcome.Succeeded);
        Assert.Equal(Utf8ByteSafeLazyDfaCompileFailureKind.None, outcome.FailureKind);
        Assert.True(parent.Budget.PeakBytes > 0);
        Assert.True(parent.Budget.ReservedBytes > 0);
        Assert.True(parent.Budget.WorkSteps > 0);
        Assert.True(parent.Budget.PeakDepth > 0);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.True(outcome.Program.TryMatch("abx"u8, 0, out var width));
        Assert.Equal(2, width);
        Assert.False(outcome.Program.TryMatch("ax"u8, 0, out _));
        Assert.Equal(alternatives, outcome.Program.TryMatch("cd"u8, 0, out width));
        Assert.Equal(alternatives ? 2 : 0, width);
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData(Utf8ResourceLimitKind.ConstructionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ConstructionDepth)]
    [InlineData(Utf8ResourceLimitKind.ConstructionInstructionCapacity)]
    public void SharedAdmissionFailuresPropagateAndRestoreTheParent(Utf8ResourceLimitKind kind)
    {
        var input = Literal();
        using var parent = new Utf8ConstructionScope(
            kind == Utf8ResourceLimitKind.ConstructionInstructionCapacity ? 0 : Utf8ConstructionScope.DefaultInstructionCapacity,
            kind == Utf8ResourceLimitKind.ConstructionBytes ? 1 : Utf8ConstructionScope.DefaultBufferBytes,
            kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 0 : Utf8ConstructionScope.DefaultWorkSteps,
            kind == Utf8ResourceLimitKind.ConstructionDepth ? 0 : Utf8ConstructionScope.DefaultDepth);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8ByteSafeLazyDfaVerifierProgram.Compile(input));
        Assert.Equal(kind, failure.ResourceKind);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.True(failure.Requested > failure.Limit);
        Assert.True(input.TryMatch("ab"u8, 0, out var width));
        Assert.Equal(2, width);
    }

    [Fact]
    public void OptimizerStateDeclineReturnsCapacityButKeepsWorkSpent()
    {
        var input = new Utf8ByteSafeLinearVerifierProgram([
            Utf8ByteSafeLinearVerifierStep.MatchText(Enumerable.Repeat((byte)'a', 256).ToArray()),
            Utf8ByteSafeLinearVerifierStep.Accept()]);
        using var parent = Utf8ConstructionScope.Enter();
        var outcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(input);
        Assert.False(outcome.Succeeded);
        Assert.Equal(Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded, outcome.FailureKind);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.True(parent.Budget.WorkSteps > 0);
        Assert.True(parent.Budget.PeakBytes > 0);
        Assert.True(input.TryMatch(Enumerable.Repeat((byte)'a', 256).ToArray(), 0, out var width));
        Assert.Equal(256, width);
        var recovered = Utf8ByteSafeLazyDfaVerifierProgram.Compile(Literal());
        Assert.True(recovered.Succeeded);
        Assert.True(parent.Budget.ReservedBytes > 0);
    }

    [Fact]
    public void FailedCompilationPreservesAnExistingParentArrayLoan()
    {
        using var parent = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 4096);
        var existing = parent.Budget.AllocateArray<byte>(3840, false);
        var checkpoint = parent.Budget.ReservedBytes;
        var input = Literal();
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8ByteSafeLazyDfaVerifierProgram.Compile(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(checkpoint, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);
        parent.Budget.ReleaseArray(existing, false);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        var outcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(input);
        Assert.True(outcome.Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullCompilationUnwindsAtEveryWorkAndDepthCutoff(bool alternatives)
    {
        var input = alternatives ? Alternatives() : Literal();
        ulong work, depth, bytes, slots;
        using (var baseline = Utf8ConstructionScope.Enter())
        {
            Assert.True(Utf8ByteSafeLazyDfaVerifierProgram.Compile(input).Succeeded);
            work = baseline.Budget.WorkSteps;
            depth = baseline.Budget.PeakDepth;
            bytes = baseline.Budget.PeakBytes;
            slots = baseline.Budget.Peak;
        }

        for (var cutoff = 0UL; cutoff < work; cutoff++)
            AssertDenied(input, Utf8ResourceLimitKind.ConstructionWorkSteps, cutoff);
        for (var cutoff = 0UL; cutoff < depth; cutoff++)
            AssertDenied(input, Utf8ResourceLimitKind.ConstructionDepth, cutoff);
        AssertDenied(input, Utf8ResourceLimitKind.ConstructionBytes, bytes - 1);
        AssertDenied(input, Utf8ResourceLimitKind.ConstructionInstructionCapacity, slots - 1);

        using var recovery = Utf8ConstructionScope.Enter();
        var recovered = Utf8ByteSafeLazyDfaVerifierProgram.Compile(input);
        Assert.True(recovered.Succeeded);
        Assert.True(recovered.Program.TryMatch("abx"u8, 0, out var width));
        Assert.Equal(2, width);
    }

    private static void AssertDenied(Utf8ByteSafeLinearVerifierProgram input, Utf8ResourceLimitKind kind, ulong cutoff)
    {
        using var parent = new Utf8ConstructionScope(
            kind == Utf8ResourceLimitKind.ConstructionInstructionCapacity ? cutoff : Utf8ConstructionScope.DefaultInstructionCapacity,
            kind == Utf8ResourceLimitKind.ConstructionBytes ? cutoff : Utf8ConstructionScope.DefaultBufferBytes,
            kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? cutoff : Utf8ConstructionScope.DefaultWorkSteps,
            kind == Utf8ResourceLimitKind.ConstructionDepth ? cutoff : Utf8ConstructionScope.DefaultDepth);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8ByteSafeLazyDfaVerifierProgram.Compile(input));
        Assert.Equal(kind, failure.ResourceKind);
        Assert.Equal(cutoff, failure.Limit);
        Assert.True(failure.Requested > failure.Limit);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);

        Assert.True(input.TryMatch("ab"u8, 0, out var width));
        Assert.Equal(2, width);
    }
}
