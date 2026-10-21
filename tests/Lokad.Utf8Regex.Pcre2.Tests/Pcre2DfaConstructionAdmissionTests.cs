using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2DfaConstructionAdmissionTests
{
    private static Pcre2AsciiDfaInput Literal() => new([
        Pcre2AsciiDfaStep.MatchAsciiClass(AsciiCharClass.ForByte((byte)'a')),
        Pcre2AsciiDfaStep.MatchAsciiClass(AsciiCharClass.ForByte((byte)'b')),
        Pcre2AsciiDfaStep.Accept()]);

    private static Pcre2AsciiDfaInput Ambiguous() => new([
        Pcre2AsciiDfaStep.LoopAsciiClass(AsciiCharClass.ForByte((byte)'a'), 0, 1),
        Pcre2AsciiDfaStep.MatchAsciiClass(new(0, (1UL << ('a' - 64)) | (1UL << ('c' - 64)))),
        Pcre2AsciiDfaStep.MatchAsciiClass(new(0, (1UL << ('b' - 64)) | (1UL << ('d' - 64)))),
        Pcre2AsciiDfaStep.Accept()]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulGraphsReachTheParentConstructionOwnerAndKeepTheirResults(bool ambiguous)
    {
        using var parent = Utf8ConstructionScope.Enter();
        var input = ambiguous ? Ambiguous() : Literal();
        var outcome = Pcre2AsciiDfaVerifierProgram.Compile(input);
        Assert.True(outcome.Succeeded);
        Assert.Equal(Pcre2AsciiDfaCompileFailureKind.None, outcome.FailureKind);
        Assert.True(parent.Budget.PeakBytes > 0);
        Assert.True(parent.Budget.ReservedBytes > 0);
        Assert.True(parent.Budget.WorkSteps > 0);
        Assert.True(parent.Budget.PeakDepth > 0);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.True(outcome.Program.TryMatch("abx"u8, 0, out var width));
        Assert.Equal(2, width);
        Assert.False(outcome.Program.TryMatch("ax"u8, 0, out _));
        Assert.Equal(ambiguous, outcome.Program.TryMatch("cd"u8, 0, out width));
        Assert.Equal(ambiguous ? 2 : 0, width);
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
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Pcre2AsciiDfaVerifierProgram.Compile(input));
        Assert.Equal(kind, failure.ResourceKind);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.True(failure.Requested > failure.Limit);
    }

    [Fact]
    public void OptimizerStateDeclineReturnsCapacityButKeepsWorkSpent()
    {
        var input = new Pcre2AsciiDfaInput(Enumerable.Repeat(
            Pcre2AsciiDfaStep.MatchAsciiClass(AsciiCharClass.ForByte((byte)'a')), 256)
            .Append(Pcre2AsciiDfaStep.Accept()).ToArray());
        using var parent = Utf8ConstructionScope.Enter();
        var outcome = Pcre2AsciiDfaVerifierProgram.Compile(input);
        Assert.False(outcome.Succeeded);
        Assert.Equal(Pcre2AsciiDfaCompileFailureKind.StateBudgetExceeded, outcome.FailureKind);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.True(parent.Budget.WorkSteps > 0);
        Assert.True(parent.Budget.PeakBytes > 0);
        var recovered = Pcre2AsciiDfaVerifierProgram.Compile(Literal());
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
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Pcre2AsciiDfaVerifierProgram.Compile(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(checkpoint, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);
        parent.Budget.ReleaseArray(existing, false);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        var outcome = Pcre2AsciiDfaVerifierProgram.Compile(input);
        Assert.True(outcome.Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullCompilationUnwindsAtEveryWorkAndDepthCutoff(bool ambiguous)
    {
        var input = ambiguous ? Ambiguous() : Literal();
        ulong work, depth, bytes, slots;
        using (var baseline = Utf8ConstructionScope.Enter())
        {
            Assert.True(Pcre2AsciiDfaVerifierProgram.Compile(input).Succeeded);
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
        var recovered = Pcre2AsciiDfaVerifierProgram.Compile(input);
        Assert.True(recovered.Succeeded);
        Assert.True(recovered.Program.TryMatch("abx"u8, 0, out var width));
        Assert.Equal(2, width);
    }

    private static void AssertDenied(Pcre2AsciiDfaInput input, Utf8ResourceLimitKind kind, ulong cutoff)
    {
        using var parent = new Utf8ConstructionScope(
            kind == Utf8ResourceLimitKind.ConstructionInstructionCapacity ? cutoff : Utf8ConstructionScope.DefaultInstructionCapacity,
            kind == Utf8ResourceLimitKind.ConstructionBytes ? cutoff : Utf8ConstructionScope.DefaultBufferBytes,
            kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? cutoff : Utf8ConstructionScope.DefaultWorkSteps,
            kind == Utf8ResourceLimitKind.ConstructionDepth ? cutoff : Utf8ConstructionScope.DefaultDepth);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Pcre2AsciiDfaVerifierProgram.Compile(input));
        Assert.Equal(kind, failure.ResourceKind);
        Assert.Equal(cutoff, failure.Limit);
        Assert.True(failure.Requested > failure.Limit);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Depth);

    }
}
