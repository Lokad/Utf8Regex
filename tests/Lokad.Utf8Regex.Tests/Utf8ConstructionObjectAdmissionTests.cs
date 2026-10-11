using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ConstructionObjectAdmissionTests
{
    [Theory]
    [InlineData(0ul, ulong.MaxValue)]
    [InlineData(127ul, ulong.MaxValue)]
    [InlineData(128ul, 0ul)]
    [InlineData(128ul, 1ul)]
    public void DenialPrecedesFactoryAndLeavesNoCapacityLoan(ulong bytes, ulong work)
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, bytes, work, ulong.MaxValue);
        var called = false;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            scope.Budget.AllocateObject(1, value => { called = true; return new Payload(value); }));
        Assert.False(called);
        Assert.Equal(bytes < 128 ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.True(failure.Requested > failure.Limit);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.PeakBytes);
        Assert.Equal(0ul, scope.Budget.WorkSteps);

    }

    [Fact]
    public void ThrowingFactoryPreservesParentObjectsAndKeepsAdmittedWorkSpent()
    {
        using var scope = Utf8ConstructionScope.Enter();
        var parent = scope.Budget.AllocateObject(42, static value => new Payload(value));
        var checkpoint = scope.Budget.ReservedBytes;
        var held = scope.Budget.ReservedBytes;
        var work = scope.Budget.WorkSteps;
        var failure = Assert.Throws<InvalidOperationException>(() =>
            scope.Budget.AllocateObject<int, Payload>(1, static _ => throw new InvalidOperationException("factory")));
        Assert.Equal("factory", failure.Message);
        Assert.Equal(held, scope.Budget.ReservedBytes);
        Assert.Equal(checkpoint, scope.Budget.ReservedBytes);
        Assert.Equal(42, parent.Value);
        Assert.Equal(work + 2, scope.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ChildCompletionTransfersObjectsAndFailureRestoresTheParentCheckpoint(bool completed)
    {
        using var parent = Utf8ConstructionScope.Enter();
        _ = parent.Budget.AllocateObject(1, static value => new Payload(value));
        var checkpoint = parent.Budget.ReservedBytes;
        var held = parent.Budget.ReservedBytes;
        using (var child = Utf8ConstructionScope.Enter())
        {
            Assert.Same(parent.Budget, child.Budget);
            _ = child.Budget.AllocateObject(2, static value => new Payload(value));
            Assert.Equal(checkpoint + 128ul, child.Budget.ReservedBytes);
            if (completed) child.Complete();
        }
        Assert.Equal(completed ? held * 2 : held, parent.Budget.ReservedBytes);
        Assert.Equal(completed ? checkpoint + 128ul : checkpoint, parent.Budget.ReservedBytes);
        Assert.Equal(4ul, parent.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClosingOwnerKeepsCompletedNumericChargesWithoutRefundingWork(bool completed)
    {
        var scope = Utf8ConstructionScope.Enter();
        _ = scope.Budget.AllocateObject(1, static value => new Payload(value));
        Assert.True(scope.Budget.ReservedBytes > 0);
        if (completed) scope.Complete();
        scope.Dispose();
        Assert.Equal(completed ? 128ul : 0ul, scope.Budget.ReservedBytes);
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

    }

    private sealed class Payload(int value)
    {
        internal int Value { get; } = value;
    }

    [Fact]
    public void NumericAdmissionDoesNotKeepDiscardedHelpersAlive()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var weak = AcquireDiscardedHelper(owner.Budget);
        var held = owner.Budget.ReservedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        Assert.Equal(128ul, held);
        Assert.Equal(held, owner.Budget.ReservedBytes);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Null(typeof(Utf8ConstructionBudget).GetNestedType("ObjectAllocation", BindingFlags.NonPublic));
        Assert.Null(typeof(Utf8ConstructionBudget).GetProperty("ObjectCheckpoint", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Payload> AcquireDiscardedHelper(Utf8ConstructionBudget budget)
        => new(budget.AllocateObject(17, static value => new Payload(value)));

    [Fact]
    public void RetainedAliasesRemainValidAfterFailedChildAndOwnerClosure()
    {
        Payload retained;
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateObject(17, static value => new Payload(value));
        using (var child = Utf8ConstructionScope.Enter())
        {
            retained = child.Budget.AllocateObject(42, static value => new Payload(value));
        }
        var alias = retained;
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(4ul, owner.Budget.WorkSteps);
        owner.Dispose();
        GC.Collect();
        Assert.Same(retained, alias);
        Assert.Equal(42, alias.Value);
        Assert.Equal(17, parent.Value);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(4ul, owner.Budget.WorkSteps);
    }
}
