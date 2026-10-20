using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparationCacheAdmissionTests
{

    [Fact]
    public void CacheAndEntryNumericChargesRemainReservedAfterDetachment()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal);
        _ = cache.GetOrAdd("a", 0, static (_, _) => "value", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>));
        _ = cache.GetOrAdd("b", 0, static (_, _) => "value", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>));
        Assert.Equal(Utf8BoundedPreparationCache<string, string>.ObjectBytes
            + 2 * Utf8BoundedPreparationCache<string, string>.EntryBytes, cache.MetadataBytes);
        Assert.Equal(Math.Max(128ul, Utf8BoundedPreparationCache<string, string>.ObjectBytes) + 2 * Math.Max(128ul, Utf8BoundedPreparationCache<string, string>.EntryBytes), owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
        var held = owner.Budget.ReservedBytes;
        cache.Clear();
        Assert.Equal(Utf8BoundedPreparationCache<string, string>.ObjectBytes, cache.MetadataBytes);
        // Detached entries may be collected; numeric helper charges remain until scope closure.
        Assert.Equal(held, owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MetadataByteDenialsAndFailedFactoriesRestoreTheParent(bool entry)
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal);
        var loan = Math.Max(128ul, entry ? Utf8BoundedPreparationCache<string, string>.EntryBytes : Utf8BoundedPreparationCache<string, string>.ObjectBytes);
        foreach (var capacity in new[] { 0ul, loan - 1, loan })
        {
            using var parent = new Utf8ConstructionScope(ulong.MaxValue, 128 + capacity, ulong.MaxValue, ulong.MaxValue);
            _ = parent.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = parent.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() =>
                {
                    if (entry) _ = cache.GetOrAdd("key", 0, static (_, _) => "value", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>));
                    else _ = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal);
                });
                if (capacity < loan) Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(128ul, parent.Budget.ReservedBytes);
            Assert.Equal(checkpoint, parent.Budget.ReservedBytes);
            cache.Clear();
        }
        using var owner = Utf8ConstructionScope.Enter();
        var error = new InvalidOperationException("identity");
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("bad", 0, (_, _) => throw error, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>))));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);

        Assert.True(owner.Budget.WorkSteps > 0);
        Assert.Equal(0, cache.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundedMetadataWorkCutoffsRestoreCapacityWithoutRefundingWork(bool entry)
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal);
        void Acquire()
        {
            if (entry) _ = cache.GetOrAdd("key", 0, static (_, _) => "value", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>));
            else _ = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal);
        }
        ulong work;
        using (var funded = Utf8ConstructionScope.Enter()) { Acquire(); work = funded.Budget.WorkSteps; }
        cache.Clear();
        foreach (var capacity in new[] { 0UL, work - 1, work })
        {
            using var parent = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 2 + capacity, ulong.MaxValue);
            _ = parent.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = parent.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(Acquire);
                if (capacity < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(checkpoint, parent.Budget.ReservedBytes);
            Assert.InRange(parent.Budget.WorkSteps, 2UL, 2 + capacity);
            if (capacity == work) Assert.Equal(2 + work, parent.Budget.WorkSteps);
            cache.Clear();
        }
    }

    [Fact]
    public void RetainedMetadataCapEvictsReadyEntriesAndCanBypassRetention()
    {
        var own = Utf8BoundedPreparationCache<string, object>.ObjectBytes;
        var entry = Utf8BoundedPreparationCache<string, object>.EntryBytes;
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(100, StringComparer.Ordinal, own + entry, Utf8BoundedPreparationCache<string, object>.DefaultMaximumPayloadBytes);
        _ = cache.GetOrAdd("one", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        var retained = cache.GetOrAdd("two", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        Assert.Equal(1, cache.Count);
        Assert.Equal(own + entry, cache.MetadataBytes);
        Assert.Same(retained, cache.GetOrAdd("two", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
        cache.Clear();
        Assert.Equal(own, cache.MetadataBytes);
        var noEntries = Utf8BoundedPreparationCache<string, object>.CreateRetained(100, StringComparer.Ordinal, own, Utf8BoundedPreparationCache<string, object>.DefaultMaximumPayloadBytes);
        Assert.NotSame(noEntries.GetOrAdd("key", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)), noEntries.GetOrAdd("key", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
        Assert.Equal(0, noEntries.Count);
        Assert.Equal(own, noEntries.MetadataBytes);
    }

    [Fact]
    public void AFailedFullCacheMissPreservesUsefulEntries()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(1, StringComparer.Ordinal);
        var retained = cache.GetOrAdd("good", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("bad", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Same(retained, cache.GetOrAdd("good", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
    }
}
