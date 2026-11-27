using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionMetadataMapTests
{

    [Fact]
    public void FrozenCollisionChainsKeepEveryEntryAndDoNotAliasLaterGrowth()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var arena = Utf8ConstructionArena.Create(owner.Budget);
        using var map = Utf8ConstructionMap<int, int>.Create(owner.Budget, new Collisions());
        for (var value = 0; value < 129; value++) map.Add(value, value * 3);
        var snapshot = map.Freeze(arena);
        for (var value = 0; value < 129; value++) map.Add(value, -value);
        Assert.Equal(129, snapshot.Count);
        for (var value = 0; value < 129; value++) Assert.Equal(value * 3, snapshot[value]);
        Assert.False(snapshot.ContainsKey(999));
        Assert.Equal(129, snapshot.ToArray().Length);
        arena.Complete();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OwnedCollectionDescriptorsFitMeasuredAcquisitionIncludingTheirTrackingObjects(bool map)
    {
        using var owner = Utf8ConstructionScope.Enter();
        if (map) _ = Utf8ConstructionMap<int, int>.Create(owner.Budget, EqualityComparer<int>.Default);
        else _ = Utf8ConstructionBuffer<int>.Create(owner.Budget);
        var held = owner.Budget.ReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        object acquired = map ? Utf8ConstructionMap<int, int>.Create(owner.Budget, EqualityComparer<int>.Default)
            : Utf8ConstructionBuffer<int>.Create(owner.Budget);
        var allocated = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.NotNull(acquired);
        Assert.True(owner.Budget.ReservedBytes - held >= 128ul);
        Assert.True(allocated > 0);
        Assert.True(allocated <= owner.Budget.ReservedBytes - held);
    }

    [Fact]
    public void MetadataEnumerationPreservesParentLoans()
    {
        Utf8ConstructionReadOnlyMap<int, int> snapshot;
        using (var original = Utf8ConstructionScope.Enter())
        {
            using var arena = Utf8ConstructionArena.Create(original.Budget);
            using var map = Utf8ConstructionMap<int, int>.Create(original.Budget, EqualityComparer<int>.Default);
            map.Add(1, 2); map.Add(3, 4); snapshot = map.Freeze(arena); arena.Complete();
            original.Complete();
        }
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        using (var child = Utf8ConstructionScope.Enter())
        {
            Assert.Equal(new[] { 2, 4 }, snapshot.Values.OrderBy(static value => value).ToArray());
        }
        Assert.Equal(48ul, owner.Budget.ReservedBytes);

        owner.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void TemporaryViewsUseNoByteLoans()
    {
        var snapshot = CreateSnapshot();
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 0, ulong.MaxValue);
        using var keys = snapshot.Keys.GetEnumerator();
        using var values = snapshot.Values.GetEnumerator();
        using var pairs = snapshot.GetEnumerator();
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TemporaryViewsAndEnumeratorsKeepOriginalInterruptionPreflights(bool timeout)
    {
        var snapshot = CreateSnapshot();
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 0, ulong.MaxValue);
        var keys = snapshot.Keys;
        var values = snapshot.Values;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = cancellation.Token;
        if (timeout) ticks = 25; else cancellation.Cancel();

        foreach (var acquire in new Func<object>[]
        {
            () => snapshot.Keys, () => snapshot.Values, () => snapshot.GetEnumerator(),
            () => keys.GetEnumerator(), () => values.GetEnumerator(),
        })
        {
            var failure = Record.Exception(() => acquire());
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }

    private static Utf8ConstructionReadOnlyMap<int, int> CreateSnapshot()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var arena = Utf8ConstructionArena.Create(owner.Budget);
        using var map = Utf8ConstructionMap<int, int>.Create(owner.Budget, EqualityComparer<int>.Default);
        map.Add(1, 2); map.Add(3, 4);
        var snapshot = map.Freeze(arena);
        arena.Complete(); owner.Complete();
        return snapshot;
    }

    private sealed class Collisions : IEqualityComparer<int>
    {
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value) => 0;
    }
}
