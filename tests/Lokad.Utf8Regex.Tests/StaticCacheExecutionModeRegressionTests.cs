using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

// PLAN.md D07: the static core cache stripped RegexOptions.Compiled, so a
// static compiled request reused the ordinary cached instance and never
// reached the compiled engine. Execution mode is part of the cache identity;
// the prepared program stays shared underneath both entries.
[Collection(Utf8RegexCacheCollection.Name)]
public sealed class StaticCacheExecutionModeRegressionTests
{
    [Fact]
    public void StaticCompiledRequestKeepsExecutionModeIdentity()
    {
        Utf8RegexCache.ResetForTests();

        var ordinary = Utf8RegexCache.GetOrAdd("a+", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var compiled = Utf8RegexCache.GetOrAdd("a+", RegexOptions.CultureInvariant | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);

        Assert.Equal(RegexOptions.CultureInvariant, ordinary.Options);
        Assert.Equal(RegexOptions.CultureInvariant | RegexOptions.Compiled, compiled.Options);
        Assert.NotSame(ordinary, compiled);
        Assert.Same(ordinary, Utf8RegexCache.GetOrAdd("a+", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout));
        Assert.Same(compiled, Utf8RegexCache.GetOrAdd("a+", RegexOptions.CultureInvariant | RegexOptions.Compiled, Regex.InfiniteMatchTimeout));
        Assert.Equal(2, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void StaticExecutionModeIdentityHoldsWhenCompiledComesFirst()
    {
        Utf8RegexCache.ResetForTests();

        var compiled = Utf8RegexCache.GetOrAdd("a+", RegexOptions.CultureInvariant | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
        var ordinary = Utf8RegexCache.GetOrAdd("a+", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        Assert.Equal(RegexOptions.CultureInvariant | RegexOptions.Compiled, compiled.Options);
        Assert.Equal(RegexOptions.CultureInvariant, ordinary.Options);
        Assert.NotSame(ordinary, compiled);
        Assert.Equal(2, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void StaticCompiledAndOrdinaryEntriesAgreeWithOracle()
    {
        Utf8RegexCache.ResetForTests();

        var ordinary = Utf8RegexCache.GetOrAdd("a+", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var compiled = Utf8RegexCache.GetOrAdd("a+", RegexOptions.CultureInvariant | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
        var ordinaryOracle = new Regex("a+", RegexOptions.CultureInvariant);
        var compiledOracle = new Regex("a+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = Encoding.UTF8.GetBytes("xxaaayya");

        Assert.Equal(ordinaryOracle.IsMatch("xxaaayya"), ordinary.IsMatch(input));
        Assert.Equal(compiledOracle.IsMatch("xxaaayya"), compiled.IsMatch(input));
        Assert.Equal(ordinaryOracle.Count("xxaaayya"), ordinary.Count(input));
        Assert.Equal(compiledOracle.Count("xxaaayya"), compiled.Count(input));
        var compiledMatch = compiled.Match(input);
        Assert.Equal(ordinaryOracle.Match("xxaaayya").Value, Encoding.UTF8.GetString(input.AsSpan(compiledMatch.IndexInBytes, compiledMatch.LengthInBytes)));
    }
}