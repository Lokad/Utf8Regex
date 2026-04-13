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

    [Fact]
    public void CacheTreatsCompiledOptionAsSameSemanticKey()
    {
        Utf8RegexCache.ResetForTests();

        _ = Utf8Regex.IsMatch("xxabcxx"u8, "abc", RegexOptions.CultureInvariant);
        _ = Utf8Regex.IsMatch("xxabcxx"u8, "abc", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Assert.Equal(1, Utf8RegexCache.EntryCount);
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
}
