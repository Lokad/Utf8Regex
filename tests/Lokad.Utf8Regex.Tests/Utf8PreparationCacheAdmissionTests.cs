using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparationCacheAdmissionTests
{
    private static ulong Descriptor => Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>();

    [Fact]
    public void CompleteCacheAndEntryStateHasExactFundedMetadataLoans()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var cache = Utf8BoundedPreparationCache<string, string>.Create(2, StringComparer.Ordinal);
        _ = cache.GetOrAdd("a", static _ => "value");
        _ = cache.GetOrAdd("b", static _ => "value");
        Assert.Equal(Utf8BoundedPreparationCache<string, string>.ObjectBytes
            + 2 * Utf8BoundedPreparationCache<string, string>.EntryBytes, cache.MetadataBytes);
        Assert.Equal(cache.MetadataBytes + 3 * Descriptor, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
        var types = new List<Type>();
        for (var allocation = owner.Budget.ObjectCheckpoint; allocation is not null; allocation = allocation.Previous)
            types.Add(allocation.Value.GetType());
        Assert.Equal(3, types.Count);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly;
        Assert.All(types, type => Assert.Equal(7, type.GetFields(flags).Length));
        cache.Clear();
        Assert.Equal(Utf8BoundedPreparationCache<string, string>.ObjectBytes, cache.MetadataBytes);
        // The still-live construction owner retains its independent tracking
        // roots until it closes; cache detachment removes only cache ownership.
        Assert.Equal(3, types.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MetadataByteDenialsAndFailedFactoriesRestoreTheParent(bool entry)
    {
        var cache = Utf8BoundedPreparationCache<string, string>.Create(2);
        var loan = (entry ? Utf8BoundedPreparationCache<string, string>.EntryBytes : Utf8BoundedPreparationCache<string, string>.ObjectBytes) + Descriptor;
        foreach (var capacity in new[] { 0ul, loan - 1, loan })
        {
            using var parent = new Utf8ConstructionScope(ulong.MaxValue, 112 + capacity, ulong.MaxValue, ulong.MaxValue);
            _ = parent.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = parent.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() =>
                {
                    if (entry) _ = cache.GetOrAdd("key", static _ => "value");
                    else _ = Utf8BoundedPreparationCache<string, string>.Create(2);
                });
                if (capacity < loan) Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(112ul, parent.Budget.ReservedBytes);
            Assert.Same(checkpoint, parent.Budget.ObjectCheckpoint);
            cache.Clear();
        }
        using var owner = Utf8ConstructionScope.Enter();
        var error = new InvalidOperationException("identity");
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("bad", _ => throw error)));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Null(owner.Budget.ObjectCheckpoint);
        Assert.True(owner.Budget.WorkSteps > 0);
        Assert.Equal(0, cache.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryMetadataWorkCutoffRollsBackWithoutRefundingWork(bool entry)
    {
        var cache = Utf8BoundedPreparationCache<string, string>.Create(2);
        for (var capacity = 0ul; capacity <= 2; capacity++)
        {
            using var parent = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 2 + capacity, ulong.MaxValue);
            _ = parent.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = parent.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() =>
                {
                    if (entry) _ = cache.GetOrAdd("key", static _ => "value");
                    else _ = Utf8BoundedPreparationCache<string, string>.Create(2);
                });
                if (capacity < 2) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(112ul, parent.Budget.ReservedBytes);
            Assert.Same(checkpoint, parent.Budget.ObjectCheckpoint);
            Assert.Equal(capacity == 2 ? 4ul : 2ul, parent.Budget.WorkSteps);
            cache.Clear();
        }
    }

    [Fact]
    public void RetainedMetadataCapEvictsReadyEntriesAndCanBypassRetention()
    {
        var own = Utf8BoundedPreparationCache<string, object>.ObjectBytes;
        var entry = Utf8BoundedPreparationCache<string, object>.EntryBytes;
        var cache = Utf8BoundedPreparationCache<string, object>.Create(100, StringComparer.Ordinal, own + entry);
        _ = cache.GetOrAdd("one", static _ => new object());
        var retained = cache.GetOrAdd("two", static _ => new object());
        Assert.Equal(1, cache.Count);
        Assert.Equal(own + entry, cache.MetadataBytes);
        Assert.Same(retained, cache.GetOrAdd("two", static _ => throw new InvalidOperationException()));
        cache.Clear();
        Assert.Equal(own, cache.MetadataBytes);
        var noEntries = Utf8BoundedPreparationCache<string, object>.Create(100, StringComparer.Ordinal, own);
        Assert.NotSame(noEntries.GetOrAdd("key", static _ => new object()), noEntries.GetOrAdd("key", static _ => new object()));
        Assert.Equal(0, noEntries.Count);
        Assert.Equal(own, noEntries.MetadataBytes);
    }

    [Fact]
    public void AFailedFullCacheMissPreservesUsefulEntries()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.Create(1);
        var retained = cache.GetOrAdd("good", static _ => new object());
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("bad", static _ => throw new InvalidOperationException()));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Same(retained, cache.GetOrAdd("good", static _ => throw new InvalidOperationException()));
    }
}
