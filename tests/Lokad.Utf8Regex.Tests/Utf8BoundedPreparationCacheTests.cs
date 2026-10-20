using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BoundedPreparationCacheTests
{
    [Fact]
    public void FailedGenerationCannotEvictLaterValueForSameKey()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(2, StringComparer.Ordinal);
        var aAttempts = 0;
        var bAttempts = 0;

        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("a", 0, (_, _) =>
        {
            aAttempts++;
            throw new InvalidOperationException("injected");
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));

        var retainedB = cache.GetOrAdd("b", 0, (_, _) =>
        {
            bAttempts++;
            return new object();
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        _ = cache.GetOrAdd("c", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        var retainedA = cache.GetOrAdd("a", 0, (_, _) =>
        {
            aAttempts++;
            return new object();
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));

        Assert.Equal(2, cache.Count);
        Assert.Same(retainedA, cache.GetOrAdd("a", 0, (_, _) =>
        {
            aAttempts++;
            return new object();
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
        Assert.Equal(2, aAttempts);

        var replacementB = cache.GetOrAdd("b", 0, (_, _) =>
        {
            bAttempts++;
            return new object();
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        Assert.NotSame(retainedB, replacementB);
        Assert.Equal(2, bAttempts);
    }

    [Fact]
    public async Task ConcurrentRequestsPublishOneSuccessfulPreparation()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(4, StringComparer.Ordinal);
        var preparations = 0;
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => cache.GetOrAdd("key", 0, (_, _) =>
            {
                Interlocked.Increment(ref preparations);
                return new object();
            }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>))))
            .ToArray();

        var values = await Task.WhenAll(requests);

        Assert.All(values, value => Assert.Same(values[0], value));
        Assert.Equal(1, preparations);
        Assert.Equal(1, cache.Count);
    }
}
