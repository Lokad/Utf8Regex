using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ByteSafeCompilerFrameAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    private static Utf8ExecutionTree Tree(int depth)
    {
        var node = new Utf8ExecutionNode(Utf8ExecutionNodeKind.One, RegexOptions.None, [], "", 'a', 0, 1, 1);
        for (var index = 0; index < depth; index++)
            node = new(Utf8ExecutionNodeKind.Concatenate, RegexOptions.None, [node], "", '\0', 0, 1, 1);
        return new(node);
    }

    private static Utf8ByteSafeLinearVerifierStep[] Steps(int depth)
    {
        var steps = new[] { Utf8ByteSafeLinearVerifierStep.MatchText("ab"u8.ToArray()) };
        for (var index = 0; index < depth; index++)
            steps = [Utf8ByteSafeLinearVerifierStep.LoopProgram(steps, 1, 1)];
        return steps;
    }

    private static int Minimum(Utf8ByteSafeLinearVerifierStep[] steps)
    {
        var type = typeof(Utf8ByteSafeLinearVerifierProgram).GetNestedType("Compiler", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        var compiler = Activator.CreateInstance(type);
        var method = type.GetMethod("GetMinimumConsumedLength", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        try { return (int)(method.Invoke(compiler, [steps]) ?? throw new InvalidOperationException()); }
        catch (TargetInvocationException failure) when (failure.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static void Run(int route, Utf8ExecutionTree tree, Utf8ByteSafeLinearVerifierStep[] steps)
    {
        if (route == 0) Assert.True(Utf8ByteSafeLinearVerifierProgram.Compile(tree).Succeeded);
        else Assert.Equal(2, Minimum(steps));
    }

    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 2; route++)
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }

    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialPrecedesScratchAcquisitionAndRestoresParent(int route, int denial)
    {
        var tree = Tree(0);
        var steps = Steps(0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 112ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, tree, steps));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void EveryWorkCutoffReleasesScratchWithoutRefundingWork(int route)
    {
        // Compile's flat case has no fresh program-array keys in its memo.
        // Minimum reuses the same borrowed array identity for every replay.
        var tree = Tree(3);
        var steps = Steps(3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Run(route, tree, steps);
            work = measure.Budget.WorkSteps;
            Assert.Equal(0ul, measure.Budget.ReservedBytes);
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Record.Exception(() => Run(route, tree, steps));
            if (cutoff < work)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                Assert.True(breach.Requested > cutoff + 2);
            }
            else Assert.Null(failure);
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(17, parent.Value);
            Assert.True(owner.Budget.WorkSteps <= cutoff + 2);
            Assert.True(owner.Budget.WorkSteps >= 2);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ChildDepthDenialAllowsRecoveryInTheSameOwner(int route)
    {
        var tree = Tree(32);
        var steps = Steps(32);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, tree, steps));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps;
        Run(route, Tree(0), Steps(0));
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ChildArrayGrowthDenialRestoresParentLoans(int route)
    {
        var tree = Tree(128);
        var steps = Steps(128);
        using var owner = new Utf8ConstructionScope(0, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, tree, steps));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void SuccessfulNestedCompilationAndMinimumReleaseTemporaryMapsAndDescriptors()
    {
        var tree = Tree(40);
        var steps = Steps(40);
        var analysis = Utf8FrontEnd.Compile(@"^(?:ab[0-9]){2}$", RegexOptions.CultureInvariant);
        Assert.NotNull(analysis.ExecutionTree);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Run(0, tree, steps);
        Run(1, tree, steps);
        var result = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree);
        Assert.True(result.Succeeded);
        Assert.True(result.Program.TryMatch("ab1ab2"u8, 0, out var length, out var defer));
        Assert.Equal(6, length);
        Assert.False(defer);
        Assert.True(owner.Budget.PeakBytes > 2048);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void DeclineAndNestedMinimumOverflowRestoreParentAndPermitRecovery()
    {
        var unsupported = new Utf8ExecutionTree(new(Utf8ExecutionNodeKind.Unsupported, RegexOptions.None, [], "", '\0', 0, 0, 0));
        var overflowing = new[] { Utf8ByteSafeLinearVerifierStep.LoopByte((byte)'a', int.MaxValue, int.MaxValue),
            Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'b') };
        var steps = new[] { Utf8ByteSafeLinearVerifierStep.LoopProgram(overflowing, 0, 0) };
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var outcome = Utf8ByteSafeLinearVerifierProgram.Compile(unsupported);
        Assert.False(outcome.Succeeded);
        Assert.Equal(Utf8ByteSafeLinearCompileFailureKind.UnsupportedNode, outcome.FailureKind);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Throws<OverflowException>(() => Minimum(steps));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps;
        Run(0, Tree(0), Steps(0));
        Run(1, Tree(0), Steps(0));
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void EmptyTreeDeclinesWithoutAdmittingScratch()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        var outcome = Utf8ByteSafeLinearVerifierProgram.Compile(null);
        Assert.False(outcome.Succeeded);
        Assert.Equal(Utf8ByteSafeLinearCompileFailureKind.EmptyTree, outcome.FailureKind);
        Assert.Equal(0ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.PeakBytes);
    }
}
