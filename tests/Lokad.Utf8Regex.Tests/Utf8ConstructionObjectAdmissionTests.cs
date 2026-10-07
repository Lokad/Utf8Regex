using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionObjectAdmissionTests
{
    [Theory]
    [InlineData(0ul, ulong.MaxValue)]
    [InlineData(111ul, ulong.MaxValue)]
    [InlineData(112ul, 0ul)]
    [InlineData(112ul, 1ul)]
    public void DenialPrecedesFactoryAndLeavesNoObjectOrDescriptorLoan(ulong bytes, ulong work)
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, bytes, work, ulong.MaxValue);
        var called = false;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            scope.Budget.AllocateObject(1, value => { called = true; return new Payload(value); }));
        Assert.False(called);
        Assert.Equal(bytes < 112 ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.True(failure.Requested > failure.Limit);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.PeakBytes);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Null(scope.Budget.ObjectCheckpoint);
    }

    [Fact]
    public void ThrowingFactoryPreservesParentObjectsAndKeepsAdmittedWorkSpent()
    {
        using var scope = Utf8ConstructionScope.Enter();
        var parent = scope.Budget.AllocateObject(42, static value => new Payload(value));
        var checkpoint = scope.Budget.ObjectCheckpoint;
        var held = scope.Budget.ReservedBytes;
        var work = scope.Budget.WorkSteps;
        var failure = Assert.Throws<InvalidOperationException>(() =>
            scope.Budget.AllocateObject<int, Payload>(1, static _ => throw new InvalidOperationException("factory")));
        Assert.Equal("factory", failure.Message);
        Assert.Equal(held, scope.Budget.ReservedBytes);
        Assert.Same(checkpoint, scope.Budget.ObjectCheckpoint);
        Assert.Same(parent, scope.Budget.ObjectCheckpoint?.Value);
        Assert.Equal(work + 2, scope.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ChildCompletionTransfersObjectsAndFailureRestoresTheParentCheckpoint(bool completed)
    {
        using var parent = Utf8ConstructionScope.Enter();
        _ = parent.Budget.AllocateObject(1, static value => new Payload(value));
        var checkpoint = parent.Budget.ObjectCheckpoint;
        var held = parent.Budget.ReservedBytes;
        using (var child = Utf8ConstructionScope.Enter())
        {
            Assert.Same(parent.Budget, child.Budget);
            _ = child.Budget.AllocateObject(2, static value => new Payload(value));
            Assert.Same(checkpoint, child.Budget.ObjectCheckpoint?.Previous);
            if (completed) child.Complete();
        }
        Assert.Equal(completed ? held * 2 : held, parent.Budget.ReservedBytes);
        if (completed) Assert.Same(checkpoint, parent.Budget.ObjectCheckpoint?.Previous);
        else Assert.Same(checkpoint, parent.Budget.ObjectCheckpoint);
        Assert.Equal(4ul, parent.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClosingRootOwnerDropsTrackingRootsWithoutRefundingWork(bool completed)
    {
        var scope = Utf8ConstructionScope.Enter();
        _ = scope.Budget.AllocateObject(1, static value => new Payload(value));
        Assert.NotNull(scope.Budget.ObjectCheckpoint);
        if (completed) scope.Complete();
        scope.Dispose();
        Assert.Null(scope.Budget.ObjectCheckpoint);
        Assert.Equal(2ul, scope.Budget.WorkSteps);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void SaturatedByteOwnerDeniesWithoutCallingTheFactoryOrWrappingTheRequest()
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        scope.Budget.ReserveBytes(ulong.MaxValue);
        var called = false;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            scope.Budget.AllocateObject(1, value => { called = true; return new Payload(value); }));
        Assert.False(called);
        Assert.Equal(ulong.MaxValue, failure.Requested);
        Assert.Equal(ulong.MaxValue, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Null(scope.Budget.ObjectCheckpoint);
    }

    private sealed class Payload(int value)
    {
        internal int Value { get; } = value;
    }
}
