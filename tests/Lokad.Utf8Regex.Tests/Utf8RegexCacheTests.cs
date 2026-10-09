using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8RegexCacheTests
{
    [Fact]
    public void StaticCallsPopulateCacheBySemanticKey()
    {
        Utf8RegexCache.ResetForTests();
        var before = Utf8RegexCache.EntryCount;

        Assert.True(Utf8Regex.IsMatch("xxabcxx"u8, "abc", RegexOptions.CultureInvariant));
        Assert.True(Utf8Regex.IsMatch("yyabczz"u8, "abc", RegexOptions.CultureInvariant));

        Assert.Equal(before + 1, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void CacheDistinguishesDifferentTimeouts()
    {
        Utf8RegexCache.ResetForTests();
        var before = Utf8RegexCache.EntryCount;

        _ = Utf8Regex.IsMatch("abc"u8, "abc", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        _ = Utf8Regex.IsMatch("abc"u8, "abc", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

        Assert.Equal(before + 2, Utf8RegexCache.EntryCount);
    }

    // PLAN.md D07: Compiled is execution-mode identity, not a shareable semantic
    // option. Ordinary and compiled static requests keep separate entries.
    [Fact]
    public void CacheSeparatesCompiledExecutionMode()
    {
        Utf8RegexCache.ResetForTests();

        _ = Utf8Regex.IsMatch("xxabcxx"u8, "abc", RegexOptions.CultureInvariant);
        _ = Utf8Regex.IsMatch("xxabcxx"u8, "abc", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Assert.Equal(2, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void CacheUsesBoundedCapacity()
    {
        Utf8RegexCache.ResetForTests();
        Utf8RegexCache.MaxEntries = 2;

        _ = Utf8Regex.IsMatch("a"u8, "a", RegexOptions.CultureInvariant);
        _ = Utf8Regex.IsMatch("b"u8, "b", RegexOptions.CultureInvariant);
        _ = Utf8Regex.IsMatch("c"u8, "c", RegexOptions.CultureInvariant);

        Assert.Equal(2, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void CacheCanBeDisabled()
    {
        Utf8RegexCache.ResetForTests();
        Utf8RegexCache.MaxEntries = 0;

        _ = Utf8Regex.IsMatch("a"u8, "a", RegexOptions.CultureInvariant);
        _ = Utf8Regex.IsMatch("a"u8, "a", RegexOptions.CultureInvariant);

        Assert.Equal(0, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public async Task ConcurrentSameKeyRequestsRetainOnePreparedInstance()
    {
        Utf8RegexCache.ResetForTests();
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => Utf8RegexCache.GetOrAdd(
                "ab[0-9]{2}",
                RegexOptions.CultureInvariant,
                Regex.InfiniteMatchTimeout)))
            .ToArray();

        var instances = await Task.WhenAll(requests);

        Assert.All(instances, instance => Assert.Same(instances[0], instance));
        Assert.Equal(1, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void RepeatedDistinctStaticFailuresLeaveNoEntries()
    {
        Utf8RegexCache.ResetForTests();

        for (var i = 0; i < 100; i++)
        {
            Utf8PatternFailureAssert.Throws(() => Utf8RegexCache.GetOrAdd($"(unterminated-{i}", RegexOptions.CultureInvariant));
        }

        Assert.Equal(0, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void StaticSuccessesRemainUsableAfterFailures()
    {
        Utf8RegexCache.ResetForTests();

        Assert.True(Utf8Regex.IsMatch("a"u8, "a", RegexOptions.CultureInvariant));
        for (var i = 0; i < 50; i++)
        {
            Utf8PatternFailureAssert.Throws(() => Utf8RegexCache.GetOrAdd($"(unterminated-{i}", RegexOptions.CultureInvariant));
        }

        Assert.True(Utf8Regex.IsMatch("a"u8, "a", RegexOptions.CultureInvariant));
        Assert.Equal(1, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void CompileFailureIsNotRetained()
    {
        Utf8RegexCache.ResetForTests();

        Utf8PatternFailureAssert.Throws(() => Utf8RegexCache.GetOrAdd("(", RegexOptions.CultureInvariant));
        Assert.Equal(0, Utf8RegexCache.EntryCount);
        Utf8PatternFailureAssert.Throws(() => Utf8RegexCache.GetOrAdd("(", RegexOptions.CultureInvariant));
        Assert.Equal(0, Utf8RegexCache.EntryCount);
    }
    [Fact]
    public void StaticCacheServesUnlimitedPolicyOnly()
    {
        // R06 cache quarantine: the static cache key carries no policy because
        // the only cache factory builds default (Unlimited) instances. A future
        // policy-bearing cache entry would fail here first.
        Utf8RegexCache.ResetForTests();

        Assert.True(Utf8Regex.IsMatch("xxabcxx"u8, "abc", RegexOptions.CultureInvariant));

        var cached = Utf8RegexCache.GetOrAdd("abc", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(Utf8ResourcePolicy.Unlimited, cached.ResourcePolicy);
        Assert.Equal(1, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void PolicyBearingConstructionBypassesStaticCache()
    {
        // R06 cache quarantine: explicit-policy construction never consults or
        // populates the static cache, so a policy can neither leak into a
        // shared cached instance nor be relaxed by reusing one.
        Utf8RegexCache.ResetForTests();

        _ = new Utf8Regex("abc", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited);
        Assert.Throws<NotSupportedException>(() => new Utf8Regex("abc", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, new Utf8ResourcePolicy { MaxWorkSteps = 1ul }));

        Assert.Equal(0, Utf8RegexCache.EntryCount);
    }
}
