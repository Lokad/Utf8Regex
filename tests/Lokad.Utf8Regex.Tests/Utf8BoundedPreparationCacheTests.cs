using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BoundedPreparationCacheTests
{
    [Fact]
    public void FailedGenerationCannotEvictLaterValueForSameKey()
    {
        var cache = new Utf8BoundedPreparationCache<string, object>(2, StringComparer.Ordinal);
        var aAttempts = 0;
        var bAttempts = 0;

        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("a", _ =>
        {
            aAttempts++;
            throw new InvalidOperationException("injected");
        }));

        var retainedB = cache.GetOrAdd("b", _ =>
        {
            bAttempts++;
            return new object();
        });
        _ = cache.GetOrAdd("c", static _ => new object());
        var retainedA = cache.GetOrAdd("a", _ =>
        {
            aAttempts++;
            return new object();
        });

        Assert.Equal(2, cache.Count);
        Assert.Same(retainedA, cache.GetOrAdd("a", _ =>
        {
            aAttempts++;
            return new object();
        }));
        Assert.Equal(2, aAttempts);

        var replacementB = cache.GetOrAdd("b", _ =>
        {
            bAttempts++;
            return new object();
        });
        Assert.NotSame(retainedB, replacementB);
        Assert.Equal(2, bAttempts);
    }

    [Fact]
    public async Task ConcurrentRequestsPublishOneSuccessfulPreparation()
    {
        var cache = new Utf8BoundedPreparationCache<string, object>(4, StringComparer.Ordinal);
        var preparations = 0;
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => cache.GetOrAdd("key", _ =>
            {
                Interlocked.Increment(ref preparations);
                return new object();
            })))
            .ToArray();

        var values = await Task.WhenAll(requests);

        Assert.All(values, value => Assert.Same(values[0], value));
        Assert.Equal(1, preparations);
        Assert.Equal(1, cache.Count);
    }
}
