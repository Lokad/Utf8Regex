using Lokad.Utf8Regex.Internal.FrontEnd;
using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8GlobalCacheDescendantTests
{
    [Fact]
    public void RaisingEntryCapacityCannotMultiplyLazyCacheStorageWithoutBound()
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            Utf8RegexCache.MaxEntries = 4096;
            for (var index = 0; index < 32; index++)
            {
                var regex = Utf8RegexCache.GetOrAdd("a(?#owner" + index + ")", RegexOptions.CultureInvariant |
                    ((index & 1) == 0 ? RegexOptions.Compiled : RegexOptions.None));
                Assert.True(regex.IsMatch("a"u8));
            }
            Assert.InRange(Utf8RegexCache.EntryCount, 1, 15);
            var cache = GlobalCache();
            Assert.Equal(64ul * 1024 * 1024, Read<ulong>(cache, "MaximumPayloadBytes"));
            Assert.InRange(Read<ulong>(cache, "PayloadBytes"), 1ul, 64ul * 1024 * 1024);
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Fact]
    public void LazyGrowthUsesItsReservedMaximumWithoutChangingTheParentLoan()
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            var regex = Utf8RegexCache.GetOrAdd("(?<n>a)", RegexOptions.CultureInvariant);
            var cache = GlobalCache();
            var before = Read<ulong>(cache, "PayloadBytes");
            Assert.True(before > 4ul * 1024 * 1024);
            var replacementField = typeof(Utf8Regex).GetField("_replacementCache", Instance);
            Assert.NotNull(replacementField);
            var replacement = replacementField.GetValue(regex);
            Assert.NotNull(replacement);
            Assert.Equal(0ul, Read<ulong>(replacement, "PayloadBytes"));
            for (var index = 0; index < 20; index++)
            {
                var template = new string('x', 32768) + index + "${n}";
                Assert.Equal(new string('x', 32768) + index + "a", regex.ReplaceToString("a"u8, template));
                Assert.Equal(before, Read<ulong>(cache, "PayloadBytes"));
            }
            Assert.InRange(Read<ulong>(replacement, "PayloadBytes"), 1ul, 4ul * 1024 * 1024);
            Assert.Same(regex, Utf8RegexCache.GetOrAdd("(?<n>a)", RegexOptions.CultureInvariant));
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Fact]
    public void ClearDropsGlobalOwnershipWhileAnExternalRegexKeepsItsResults()
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            var regex = Utf8RegexCache.GetOrAdd("(?<n>.)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var cache = GlobalCache();
            Assert.True(Read<ulong>(cache, "PayloadBytes") > 4ul * 1024 * 1024);
            Assert.Equal("<😀>", regex.ReplaceToString("😀"u8, "<${n}>"));
            Utf8RegexCache.ResetForTests();
            Assert.Equal(0ul, Read<ulong>(cache, "PayloadBytes"));
            Assert.Equal(0, Utf8RegexCache.EntryCount);
            Assert.Equal("[𐐨]", regex.ReplaceToString("𐐨"u8, "[${n}]"));
            Assert.Equal(0ul, Read<ulong>(cache, "PayloadBytes"));
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Fact]
    public void TheSharedOwnerDeclaresItsSubgraphScope()
    {
        Utf8RegexCache.ResetForTests();
        var cache = GlobalCache();
        var field = cache.GetType().GetField("_storageScope", Instance);
        Assert.NotNull(field);
        Assert.Equal("RootsAndBoundedDescendants", field.GetValue(cache)?.ToString());
    }

    [Fact]
    public void ConservativeEntryChargesCoverEveryRootAndBoundedDescendant()
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            const string pattern = "(?<n>.)";
            var ordinary = Utf8RegexCache.GetOrAdd(pattern, RegexOptions.CultureInvariant);
            var compiled = Utf8RegexCache.GetOrAdd(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var timed = Utf8RegexCache.GetOrAdd(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            Assert.NotSame(ordinary, compiled);
            Assert.NotSame(ordinary, timed);
            var cache = GlobalCache();
            var before = InspectLoans(cache);
            Assert.Same(ordinary, Utf8RegexCache.GetOrAdd(new string(pattern.ToCharArray()), RegexOptions.CultureInvariant));
            Assert.Equal(before, InspectLoans(cache));
            Assert.Equal("<😀>", ordinary.ReplaceToString("😀"u8, "<${n}>"));
            Assert.Equal(before, InspectLoans(cache));
            Utf8RegexCache.MaxEntries = 1;
            Assert.True(InspectLoans(cache) < before);
            Assert.Equal("𐐨", timed.ReplaceToString("𐐨"u8, "${n}"));
            Assert.Equal(1, Utf8RegexCache.EntryCount);
            Utf8RegexCache.ResetForTests();
            Assert.Equal(0ul, InspectLoans(cache));
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Fact]
    public async Task ConcurrentDifferentKeysRespectFutureCapacityAndKeepReturnedRegexesUsable()
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            Utf8RegexCache.MaxEntries = 4096;
            var tasks = Enumerable.Range(0, 32).Select(index => Task.Run(() =>
            {
                var regex = Utf8RegexCache.GetOrAdd("(?<n>😀)(?#parallel" + index + ")", RegexOptions.CultureInvariant |
                    ((index & 1) == 0 ? RegexOptions.Compiled : RegexOptions.None));
                Assert.Equal("<😀>", regex.ReplaceToString("😀"u8, "<${n}>"));
                return regex;
            })).ToArray();
            var results = await Task.WhenAll(tasks);
            Assert.InRange(Utf8RegexCache.EntryCount, 1, 15);
            Assert.InRange(InspectLoans(GlobalCache()), 1ul, 64ul * 1024 * 1024);
            foreach (var regex in results) Assert.True(regex.IsMatch("😀"u8));
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Fact]
    public void TheDefaultFifteenEntriesRemainCacheableWithTheirFutureReservations()
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            var roots = Enumerable.Range(0, 15).Select(index => Utf8RegexCache.GetOrAdd(
                "a(?#default" + index + ")", RegexOptions.CultureInvariant)).ToArray();
            Assert.Equal(15, Utf8RegexCache.EntryCount);
            for (var index = 0; index < roots.Length; index++)
                Assert.Same(roots[index], Utf8RegexCache.GetOrAdd("a(?#default" + index + ")", RegexOptions.CultureInvariant));
            Assert.InRange(InspectLoans(GlobalCache()), 1ul, 64ul * 1024 * 1024);
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    // Recompute the production collector as a transient audit, then independently
    // inspect every payload below. The cache retains only the scalar charge.
    internal static Utf8RetainedPayloadExtent[] InspectEntryPayloads(object entry)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8RetainedPayloadBuilder<Utf8ConstructionTextNoControl>.Create(ulong.MaxValue, default);
        var collector = default(Utf8RegexRootAndLazyStorage);
        var key = Assert.IsType<Utf8RegexCacheKey>(Field(entry, "Key"));
        var value = Assert.IsType<ValueTuple<Utf8Regex>>(Field(entry, "Value")).Item1;
        Assert.True(collector.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out var charge));
        value.CollectCacheRoots(storage);
        Assert.False(storage.Exceeded);
        Assert.Equal(charge, storage.FinishCharge());
        Assert.Equal(checked(collector.GetKeyCharge(key) + charge), Assert.IsType<ulong>(Field(entry, "PayloadCharge")));
        // The physical field witness includes key text, even when its identity
        // also occurs in the value. Numeric admission may count it twice.
        storage.Text(key.Pattern); storage.Text(key.Culture);
        return storage.Finish();
    }

    private static ulong InspectLoans(object cache)
    {
        lock (cache)
        {
            Assert.Equal(11, cache.GetType().GetFields(Instance).Length);
            ulong bytes = 0;
            var entry = Field(cache, "_first");
            while (entry is not null)
            {
                Assert.Equal(8, entry.GetType().GetFields(Instance).Length);
                Assert.Equal(1, Assert.IsType<int>(Field(entry, "Status")));
                var manifest = InspectEntryPayloads(entry);
                var regex = Assert.IsType<ValueTuple<Utf8Regex>>(Field(entry, "Value")).Item1;
                var scalar = Utf8ScalarRetainedGraphTests.InspectScalarGraph(Utf8ScalarRetainedGraphTests.ScalarProgram(regex));
                foreach (var pair in Utf8PreparedRetainedGraphTests.InspectPreparedGraph(Utf8ExecutionRetainedGraphTests.Prepared(regex)))
                    if (!scalar.TryAdd(pair.Key, pair.Value)) Assert.Equal(scalar[pair.Key], pair.Value);
                foreach (var pair in Utf8FacadeRetainedGraphTests.InspectFacadeGraph(regex))
                    if (!scalar.TryAdd(pair.Key, pair.Value)) Assert.Equal(scalar[pair.Key], pair.Value);
                foreach (var pair in Utf8RuntimeRetainedGraphTests.InspectRuntimeGraph(regex))
                    if (!scalar.TryAdd(pair.Key, pair.Value)) Assert.Equal(scalar[pair.Key], pair.Value);
                foreach (var pair in scalar)
                    Assert.Contains(manifest, extent => ReferenceEquals(extent.Value, pair.Key) && extent.Bytes == pair.Value);
                ulong physicalBytes = 0;
                foreach (var extent in manifest)
                {
                    ulong expected;
                    if (scalar.TryGetValue(extent.Value, out var scalarExtent)) expected = scalarExtent;
                    else if (extent.Value is string text) expected = Utf8ConstructionBudget.StringBytes(text.Length);
                    else if (extent.Value is Utf8Regex) expected = Utf8Regex.CachedObjectBytes;
                    else
                    {
                        var child = Assert.IsType<Utf8ReplacementPlanCache>(extent.Value);
                        Assert.Single(child.GetType().GetFields(Instance));
                        var plans = Field(child, "_plans");
                        Assert.NotNull(plans);
                        expected = 48ul + Read<ulong>(plans, "MaximumMetadataBytes") + Read<ulong>(plans, "MaximumPayloadBytes");
                        Assert.True(Read<ulong>(plans, "MetadataBytes") + Read<ulong>(plans, "PayloadBytes") + 48 <= expected);
                    }
                    Assert.Equal(expected, extent.Bytes);
                    physicalBytes += expected;
                }
                var charge = Assert.IsType<ulong>(Field(entry, "PayloadCharge"));
                Assert.True(physicalBytes <= charge);
                bytes = checked(bytes + charge);
                entry = Field(entry, "Next");
            }
            Assert.Equal(bytes, Read<ulong>(cache, "PayloadBytes"));
            return bytes;
        }
    }

    private static object? Field(object instance, string name)
    {
        var field = instance.GetType().GetField(name, Instance);
        Assert.NotNull(field);
        return field.GetValue(instance);
    }

    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static object GlobalCache()
    {
        var field = typeof(Utf8RegexCache).GetField("s_cache", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field.GetValue(null);
        Assert.NotNull(value);
        return value;
    }

    private static T Read<T>(object instance, string name)
        => Assert.IsType<T>(Property(instance, name));

    private static object? Property(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name, Instance);
        Assert.NotNull(property);
        return property.GetValue(instance);
    }
}
