using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

// PLAN.md C02: the static core cache ignored the construction culture, so a
// culture-sensitive instance built under one culture was reused under another.
// The oracle is System.Text.RegularExpressions, whose static cache keys the
// effective construction culture.
[Collection(Utf8RegexCacheCollection.Name)]
public sealed class StaticCacheCultureRegressionTests
{
    [Theory]
    [InlineData("en-US", "tr-TR")]
    [InlineData("tr-TR", "en-US")]
    public void StaticIsMatchRespectsConstructionCultureInBothOrders(string firstCulture, string secondCulture)
    {
        Utf8RegexCache.ResetForTests();
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(firstCulture);
            var firstExpected = Regex.IsMatch("I", "i", RegexOptions.IgnoreCase);
            var firstActual = Utf8Regex.IsMatch("I"u8, "i", RegexOptions.IgnoreCase);

            CultureInfo.CurrentCulture = new CultureInfo(secondCulture);
            var secondExpected = Regex.IsMatch("I", "i", RegexOptions.IgnoreCase);
            var secondActual = Utf8Regex.IsMatch("I"u8, "i", RegexOptions.IgnoreCase);

            Assert.Equal(firstExpected, firstActual);
            Assert.Equal(secondExpected, secondActual);
            Assert.NotEqual(firstExpected, secondExpected);
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    [Theory]
    [InlineData("en-US", "tr-TR")]
    [InlineData("tr-TR", "en-US")]
    public void StaticCachedOperationsAgreeWithDotNetAcrossCultures(string firstCulture, string secondCulture)
    {
        Utf8RegexCache.ResetForTests();
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var cultureName in new[] { firstCulture, secondCulture })
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);

                var expectedMatch = new Regex("i", RegexOptions.IgnoreCase).Match("I");
                var actualMatch = Utf8Regex.Match("I"u8, "i", RegexOptions.IgnoreCase);
                Assert.Equal(expectedMatch.Success, actualMatch.Success);

                var expectedDetailed = new Regex("i", RegexOptions.IgnoreCase).Match("I");
                var actualDetailed = Utf8Regex.MatchDetailed("I"u8, "i", RegexOptions.IgnoreCase);
                Assert.Equal(expectedDetailed.Success, actualDetailed.Success);

                Assert.Equal(
                    Regex.Count("I x i", "i", RegexOptions.IgnoreCase),
                    Utf8Regex.Count("I x i"u8, "i", RegexOptions.IgnoreCase));
                Assert.Equal(
                    Regex.IsMatch("I", "i", RegexOptions.IgnoreCase),
                    Utf8Regex.IsMatch("I"u8, "i", RegexOptions.IgnoreCase));

                var oracle = new Regex("i", RegexOptions.IgnoreCase);
                var utf8Bytes = Encoding.UTF8.GetBytes("I x i");
                Assert.Equal(
                    oracle.Matches("I x i").Select(static match => match.Value).ToList(),
                    CollectMatchValues(utf8Bytes));
                Assert.Equal(
                    oracle.Split("I x i"),
                    CollectSplitValues(utf8Bytes));
                Assert.Equal(
                    oracle.Replace("I x i", "X"),
                    Encoding.UTF8.GetString(Utf8Regex.Replace("I x i"u8, "i", "X", RegexOptions.IgnoreCase)));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    [Fact]
    public void StaticIsMatchRespectsCultureWithExplicitTimeout()
    {
        Utf8RegexCache.ResetForTests();
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.True(Utf8Regex.IsMatch("I"u8, "i", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5)));

            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.False(Utf8Regex.IsMatch("I"u8, "i", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5)));
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    [Fact]
    public void InvariantCultureStillSharesOneCacheEntry()
    {
        Utf8RegexCache.ResetForTests();
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.True(Utf8Regex.IsMatch("I"u8, "i", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.True(Utf8Regex.IsMatch("I"u8, "i", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

            Assert.Equal(1, Utf8RegexCache.EntryCount);
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    [Fact]
    public async Task ConcurrentCultureSensitiveRequestsShareOneEntryPerCulture()
    {
        Utf8RegexCache.ResetForTests();
        var culture = new CultureInfo("en-US");
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                CultureInfo.CurrentCulture = culture;
                return Utf8RegexCache.GetOrAdd("i", RegexOptions.IgnoreCase, Regex.InfiniteMatchTimeout);
            }))
            .ToArray();

        var instances = await Task.WhenAll(requests);

        Assert.All(instances, instance => Assert.Same(instances[0], instance));
        Assert.Equal(1, Utf8RegexCache.EntryCount);
    }

    [Fact]
    public void FreshTurkishInstanceAgreesWithDotNetOnAllOperations()
    {
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var oracle = new Regex("i", RegexOptions.IgnoreCase);
            var actual = new Utf8Regex("i", RegexOptions.IgnoreCase);
            var utf8Bytes = Encoding.UTF8.GetBytes("I x i");

            Assert.Equal(oracle.IsMatch("I"), actual.IsMatch("I"u8));
            Assert.Equal(oracle.Count("I x i"), actual.Count(utf8Bytes));

            var expectedMatch = oracle.Match("I");
            var actualMatch = actual.Match("I"u8);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);

            var expectedDetailed = oracle.Match("I");
            var actualDetailed = actual.MatchDetailed("I"u8);
            Assert.Equal(expectedDetailed.Success, actualDetailed.Success);

            Assert.Equal(
                oracle.Matches("I x i").Select(static match => match.Value).ToList(),
                CollectInstanceMatchValues(actual, utf8Bytes));
            Assert.Equal(
                oracle.Split("I x i"),
                CollectInstanceSplitValues(actual, utf8Bytes));
            Assert.Equal(
                oracle.Replace("I x i", "X"),
                Encoding.UTF8.GetString(actual.Replace(utf8Bytes, "X")));
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    private static List<string> CollectMatchValues(byte[] utf8)
    {
        var values = new List<string>();
        foreach (var match in Utf8Regex.EnumerateMatches(utf8, "i", RegexOptions.IgnoreCase))
        {
            values.Add(Encoding.UTF8.GetString(utf8.AsSpan(match.IndexInBytes, match.LengthInBytes)));
        }

        return values;
    }

    private static List<string> CollectSplitValues(byte[] utf8)
    {
        var values = new List<string>();
        foreach (var split in Utf8Regex.EnumerateSplits(utf8, "i", int.MaxValue, RegexOptions.IgnoreCase))
        {
            values.Add(split.GetValueString());
        }

        return values;
    }

    private static List<string> CollectInstanceMatchValues(Utf8Regex regex, byte[] utf8)
    {
        var values = new List<string>();
        foreach (var match in regex.EnumerateMatches(utf8))
        {
            values.Add(Encoding.UTF8.GetString(utf8.AsSpan(match.IndexInBytes, match.LengthInBytes)));
        }

        return values;
    }

    private static List<string> CollectInstanceSplitValues(Utf8Regex regex, byte[] utf8)
    {
        var values = new List<string>();
        foreach (var split in regex.EnumerateSplits(utf8))
        {
            values.Add(split.GetValueString());
        }

        return values;
    }
}
