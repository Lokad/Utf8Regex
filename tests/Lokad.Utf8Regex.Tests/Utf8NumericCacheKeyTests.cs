using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NumericCacheKeyTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SharedKeyAndValueTextHaveIndependentExactCharges(bool allow)
    {
        var text = new string('x', 100);
        var charge = 2 * Utf8ConstructionBudget.StringBytes(text.Length);
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal,
            65536, charge - (allow ? 0UL : 1UL));
        var value = cache.GetOrAdd(text, text, static (_, value) => value,
            default(Utf8ConstructionTextNoControl), default(TextStorage));
        Assert.Same(text, value);
        Assert.Equal(allow ? 1 : 0, cache.Count);
        Assert.Equal(allow ? charge : 0UL, cache.PayloadBytes);
        cache.Clear(); Assert.Equal(text, value); Assert.Equal(0UL, cache.PayloadBytes);
    }

    [Fact]
    public void PendingKeyDoesNotAcquireAnIdentityBuilderOrMapBeforeTheFactory()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal);
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.ReserveBytes(7);
        var before = owner.Budget.ReservedBytes;
        ulong factoryBytes = 0;
        var result = cache.GetOrAdd("key", owner.Budget, (_, budget) =>
        {
            factoryBytes = budget.ReservedBytes - before;
            return string.Empty;
        }, default(Utf8ConstructionTextNoControl), default(TextStorage));
        Assert.Equal(Math.Max(128UL, Utf8BoundedPreparationCache<string, string>.EntryBytes), factoryBytes);
        Assert.Equal(string.Empty, result);
        Assert.Equal(Utf8ConstructionBudget.StringBytes(3), cache.PayloadBytes);
        Assert.True(owner.Budget.ReservedBytes >= 7);
        cache.Clear(); owner.Complete();
    }

    [Fact]
    public void OversizeKeyRunsTheFactoryWithoutPublishingPendingStorage()
    {
        var key = new string('x', 1024);
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal,
            65536, Utf8ConstructionBudget.StringBytes(key.Length) - 1);
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.ReserveBytes(7);
        var before = owner.Budget.ReservedBytes;
        var calls = 0;
        Assert.Equal("retained", cache.GetOrAdd(key, 0, (_, _) =>
        {
            calls++; Assert.Equal(before, owner.Budget.ReservedBytes);
            Assert.Equal(0, cache.Count); Assert.Equal(0UL, cache.PayloadBytes);
            return "retained";
        }, default(Utf8ConstructionTextNoControl), default(TextStorage)));
        Assert.Equal(1, calls); Assert.Equal(0, cache.Count); Assert.Equal(0UL, cache.PayloadBytes);
        Assert.Equal(before, owner.Budget.ReservedBytes); owner.Complete();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void KeyAndValueAdditionAtUlongMaximumCannotWrap(bool allow)
    {
        var cache = Utf8BoundedPreparationCache<int, object>.CreateRetained(2, EqualityComparer<int>.Default,
            65536, ulong.MaxValue);
        var value = new object();
        var storage = new BoundaryStorage(ulong.MaxValue - 32, allow ? 32UL : 33UL);
        Assert.Same(value, cache.GetOrAdd(1, value, static (_, value) => value,
            default(Utf8ConstructionTextNoControl), storage));
        Assert.Equal(allow ? 1 : 0, cache.Count);
        Assert.Equal(allow ? ulong.MaxValue : 0UL, cache.PayloadBytes);
        cache.Clear(); Assert.Equal(0UL, cache.PayloadBytes);
    }

    [Fact]
    public void ValueReentrancyCannotPublishAnOldPendingKeyAfterClear()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(2, StringComparer.Ordinal);
        var storage = new TextStorage(cache);
        Assert.Equal("retained", cache.GetOrAdd("key", "retained", static (_, value) => value,
            default(Utf8ConstructionTextNoControl), storage));
        Assert.Equal(0, cache.Count); Assert.Equal(0UL, cache.PayloadBytes);
    }

    private readonly struct TextStorage(Utf8BoundedPreparationCache<string, string>? clear = null)
        : IUtf8RetainedStorage<string, string>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);
        public bool TryGetValueCharge<TControl>(string value, ulong maximum, TControl control, out ulong charge)
            where TControl : struct, IUtf8ConstructionTextControl
        {
            using var storage = Utf8GraphInspection<TControl>.Create(maximum, control);
            clear?.Clear(); storage.Text(value);
            charge = storage.FinishCharge();
            return !storage.Exceeded;
        }
    }

    private readonly struct BoundaryStorage(ulong keyCharge, ulong valueCharge) : IUtf8RetainedStorage<int, object>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public ulong GetKeyCharge(int key) => keyCharge;
        public bool TryGetValueCharge<TControl>(object value, ulong maximum, TControl control, out ulong charge)
            where TControl : struct, IUtf8ConstructionTextControl
        {
            using var storage = Utf8GraphInspection<TControl>.Create(maximum, control);
            storage.Add(value, valueCharge);
            charge = storage.FinishCharge();
            return !storage.Exceeded;
        }
    }
}
