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
        var cache = new Utf8BoundedPreparationCache<string, string>(3);

        for (var i = 0; i < 100; i++)
        {
            var key = $"key-{i}";
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, static _ => throw new InvalidOperationException()));
        }

        Assert.Equal(0, cache.Count);
        Assert.True(cache.QueueLengthForTests <= 3, $"Queue holds {cache.QueueLengthForTests} records after 100 distinct failures.");
    }

    [Fact]
    public void RepeatedIdenticalFailuresDoNotAccumulateQueueRecords()
    {
        var cache = new Utf8BoundedPreparationCache<string, string>(3);

        for (var i = 0; i < 50; i++)
        {
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("bad", static _ => throw new InvalidOperationException()));
        }

        Assert.Equal(0, cache.Count);
        Assert.True(cache.QueueLengthForTests <= 3, $"Queue holds {cache.QueueLengthForTests} records after 50 identical failures.");
    }

    [Fact]
    public void SuccessesRemainCachedAndEvictOldestAfterFailures()
    {
        var cache = new Utf8BoundedPreparationCache<string, string>(2);
        Assert.Equal("a!", cache.GetOrAdd("a", static key => key + "!"));
        Assert.Equal("b!", cache.GetOrAdd("b", static key => key + "!"));
        for (var i = 0; i < 10; i++)
        {
            var key = $"bad-{i}";
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, static _ => throw new InvalidOperationException()));
        }

        Assert.Equal("c!", cache.GetOrAdd("c", static key => key + "!"));

        Assert.Equal(2, cache.Count);
        Assert.True(cache.QueueLengthForTests <= 2, $"Queue holds {cache.QueueLengthForTests} records.");
        Assert.Equal("b!", cache.GetOrAdd("b", static _ => throw new InvalidOperationException("recomputed")));
        Assert.Equal("c!", cache.GetOrAdd("c", static _ => throw new InvalidOperationException("recomputed")));
    }

    [Fact]
    public void CapacityShrinkStaysBoundedAfterFailures()
    {
        var cache = new Utf8BoundedPreparationCache<string, string>(4);
        Assert.Equal("a!", cache.GetOrAdd("a", static key => key + "!"));
        Assert.Equal("b!", cache.GetOrAdd("b", static key => key + "!"));
        for (var i = 0; i < 10; i++)
        {
            var key = $"bad-{i}";
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, static _ => throw new InvalidOperationException()));
        }

        cache.Capacity = 1;

        Assert.True(cache.Count <= 1, $"Dictionary holds {cache.Count} entries.");
        Assert.True(cache.QueueLengthForTests <= 1, $"Queue holds {cache.QueueLengthForTests} records.");
    }

    [Fact]
    public async Task ConcurrentFailuresShareOneGenerationWithoutQueueGrowth()
    {
        var cache = new Utf8BoundedPreparationCache<string, string>(8);
        var attempts = 0;
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                try
                {
                    cache.GetOrAdd("bad", _ =>
                    {
                        Interlocked.Increment(ref attempts);
                        throw new InvalidOperationException();
                    });
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
        var cache = new Utf8BoundedPreparationCache<string, string>(8);
        var preparations = 0;
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => cache.GetOrAdd("good", _ =>
            {
                Interlocked.Increment(ref preparations);
                return "good!";
            })))
            .ToArray();

        var values = await Task.WhenAll(requests);

        Assert.All(values, static value => Assert.Equal("good!", value));
        Assert.Equal(1, preparations);
        Assert.Equal(1, cache.Count);
        Assert.Equal(1, cache.QueueLengthForTests);
    }
}
