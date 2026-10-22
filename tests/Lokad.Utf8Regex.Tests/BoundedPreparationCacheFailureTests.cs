using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

// Failed and detached preparations must not retain entries or displace ready
// values beyond the configured capacity. Concurrent callers retain one owner
// for a preparation and observe its actual completion or failure.
public sealed class BoundedPreparationCacheFailureTests
{
    [Fact]
    public void DistinctFailuresDoNotRetainEntries()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(3, StringComparer.Ordinal);

        for (var i = 0; i < 100; i++)
        {
            var key = $"key-{i}";
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        }

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void RepeatedIdenticalFailuresDoNotRetainEntries()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(3, StringComparer.Ordinal);

        for (var i = 0; i < 50; i++)
        {
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("bad", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        }

        Assert.Equal(0, cache.Count);
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

        Assert.True(cache.Count <= 1, $"Cache holds {cache.Count} entries.");
    }

    [Fact]
    public async Task ConcurrentFailuresDoNotRetainEntries()
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
    }

    [Fact]
    public async Task ConcurrentSuccessesPrepareOnceAndRetainOneEntry()
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
    }
}
