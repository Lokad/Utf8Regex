using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionFrameAdmissionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RootFrameDenialPrecedesStorageAcquisition(int denial)
    {
        var descriptor = Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<(Utf8ConstructionBudget, bool, int[], int)>());
        using var owner = new Utf8ConstructionScope(ulong.MaxValue,
            denial == 1 ? ulong.MaxValue : denial == 2 ? 48 + descriptor : 48,
            ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8ConstructionFrames<int>.Create(owner.Budget, 42));
        Assert.Equal(denial == 1 ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(denial == 2 ? 48 + descriptor : 48, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(denial == 2 ? 3ul : 1ul, owner.Budget.WorkSteps);

        owner.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void ChildDepthDenialPrecedesGrowthAndCleanupRestoresParent()
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 1);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        using (var child = Utf8ConstructionScope.Enter())
        {
            using var frames = Utf8ConstructionFrames<int>.Create(child.Budget, 7);
            var peak = owner.Budget.PeakBytes;
            var work = owner.Budget.WorkSteps;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => frames.Push(8));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
            Assert.Equal(peak, owner.Budget.PeakBytes);
            Assert.Equal(work, owner.Budget.WorkSteps);
            Assert.Equal(1, frames.Count);
            Assert.Equal(7, frames[0]);
            Assert.Equal(1ul, owner.Budget.Depth);
        }
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);

        owner.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void ConstructionVectorEnumerationAcquiresNoHeapDescriptor()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var values = Utf8ConstructionBuffer<int>.Create(owner.Budget);
        values.Add(3);
        values.Add(5);
        values.Add(7);
        var sum = 0;
        for (var warm = 0; warm < 1000; warm++) foreach (var value in values) sum += value;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var repeat = 0; repeat < 1000; repeat++) foreach (var value in values) sum += value;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(30000, sum);
        Assert.Equal(0, allocated);
    }
}
