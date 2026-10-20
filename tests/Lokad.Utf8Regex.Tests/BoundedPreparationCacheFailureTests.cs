using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

// PLAN.md C03: failed preparations removed the dictionary entry but left the
// queued record (and faulted Lazy) behind, so failures accumulated unbounded
// queue state while the successful-entry count stayed within capacity.
public sealed class BoundedPreparationCacheFailureTests
{
    [Fact]
    public void DistinctFailuresDoNotAccumulateQueueRecords()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(3, StringComparer.Ordinal);

        for (var i = 0; i < 100; i++)
        {
            var key = $"key-{i}";
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        }

        Assert.Equal(0, cache.Count);
        Assert.True(cache.QueueLengthForTests <= 3, $"Queue holds {cache.QueueLengthForTests} records after 100 distinct failures.");
    }

    [Fact]
    public void RepeatedIdenticalFailuresDoNotAccumulateQueueRecords()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(3, StringComparer.Ordinal);

        for (var i = 0; i < 50; i++)
        {
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("bad", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        }

        Assert.Equal(0, cache.Count);
        Assert.True(cache.QueueLengthForTests <= 3, $"Queue holds {cache.QueueLengthForTests} records after 50 identical failures.");
    }

    [Fact]
    public void SuccessesRemainCachedAndEvictOldestAfterFailures()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal);
        Assert.Equal("a!", cache.GetOrAdd("a", 0, static (key, _) => key + "!", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        Assert.Equal("b!", cache.GetOrAdd("b", 0, static (key, _) => key + "!", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        for (var i = 0; i < 10; i++)
        {
            var key = $"bad-{i}";
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        }

        Assert.Equal("c!", cache.GetOrAdd("c", 0, static (key, _) => key + "!", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));

        Assert.Equal(2, cache.Count);
        Assert.True(cache.QueueLengthForTests <= 2, $"Queue holds {cache.QueueLengthForTests} records.");
        Assert.Equal("b!", cache.GetOrAdd("b", 0, static (_, _) => throw new InvalidOperationException("recomputed"), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        Assert.Equal("c!", cache.GetOrAdd("c", 0, static (_, _) => throw new InvalidOperationException("recomputed"), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
    }

    [Fact]
    public void CapacityShrinkStaysBoundedAfterFailures()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(4, StringComparer.Ordinal);
        Assert.Equal("a!", cache.GetOrAdd("a", 0, static (key, _) => key + "!", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        Assert.Equal("b!", cache.GetOrAdd("b", 0, static (key, _) => key + "!", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        for (var i = 0; i < 10; i++)
        {
            var key = $"bad-{i}";
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        }

        cache.Capacity = 1;

        Assert.True(cache.Count <= 1, $"Dictionary holds {cache.Count} entries.");
        Assert.True(cache.QueueLengthForTests <= 1, $"Queue holds {cache.QueueLengthForTests} records.");
    }

    [Fact]
    public async Task ConcurrentFailuresShareOneGenerationWithoutQueueGrowth()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(8, StringComparer.Ordinal);
        var attempts = 0;
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                try
                {
                    cache.GetOrAdd("bad", 0, (_, _) =>
                    {
                        Interlocked.Increment(ref attempts);
                        throw new InvalidOperationException();
                    }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>));
                }
                catch (InvalidOperationException)
                {
                }
            }))
            .ToArray();

        await Task.WhenAll(requests);

        Assert.Equal(0, cache.Count);
        Assert.True(cache.QueueLengthForTests <= 8, $"Queue holds {cache.QueueLengthForTests} records.");
    }

    [Fact]
    public async Task ConcurrentSuccessesPrepareOnceAndQueueOnce()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(8, StringComparer.Ordinal);
        var preparations = 0;
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => cache.GetOrAdd("good", 0, (_, _) =>
            {
                Interlocked.Increment(ref preparations);
                return "good!";
            }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>))))
            .ToArray();

        var values = await Task.WhenAll(requests);

        Assert.All(values, static value => Assert.Equal("good!", value));
        Assert.Equal(1, preparations);
        Assert.Equal(1, cache.Count);
        Assert.Equal(1, cache.QueueLengthForTests);
    }
}
