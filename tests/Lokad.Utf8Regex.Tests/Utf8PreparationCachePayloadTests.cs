using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparationCachePayloadTests
{
    private static Utf8BoundedPreparationCache<string, byte[]> Create(int count, ulong bytes) =>
        Utf8BoundedPreparationCache<string, byte[]>.CreateRetained(count, StringComparer.Ordinal,
            Utf8BoundedPreparationCache<string, byte[]>.DefaultMaximumMetadataBytes, bytes);

    private static byte[] Add(Utf8BoundedPreparationCache<string, byte[]> cache, string key, byte[] value) =>
        cache.GetOrAdd(key, value, static (_, bytes) => bytes, default(Utf8ConstructionTextNoControl), default(ByteStorage));

    [Fact]
    public void SharedReferentsAreChargedPerEntryAndReleaseWithoutReferenceCounts()
    {
        var cache = Create(2, 4096);
        var shared = new byte[100];
        _ = Add(cache, "a", shared);
        var first = Utf8ConstructionBudget.StringBytes(1) + Utf8ConstructionBudget.ArrayBytes<byte>(100);
        Assert.Equal(first, cache.PayloadBytes);
        _ = Add(cache, "b", shared);
        var both = 2 * first;
        Assert.Equal(both, cache.PayloadBytes);
        cache.Capacity = 1;
        Assert.Equal(first, cache.PayloadBytes);
        Assert.Same(shared, Add(cache, "b", new byte[500]));
        cache.Clear();
        Assert.Equal(0ul, cache.PayloadBytes);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void EqualContentsDoNotMergeDifferentObjectIdentities()
    {
        var cache = Create(2, 4096);
        _ = Add(cache, "a", new byte[100]);
        _ = Add(cache, "b", new byte[100]);
        Assert.Equal(2 * (Utf8ConstructionBudget.StringBytes(1) + Utf8ConstructionBudget.ArrayBytes<byte>(100)), cache.PayloadBytes);
    }

    [Fact]
    public void OversizeCompletedPayloadDoesNotEvictUsefulValues()
    {
        var cache = Create(2, 1024);
        var good = Add(cache, "good", new byte[64]);
        var before = cache.PayloadBytes;
        var large = new byte[2000];
        Assert.Same(large, Add(cache, "large", large));
        Assert.Equal(1, cache.Count);
        Assert.Equal(before, cache.PayloadBytes);
        Assert.Same(good, Add(cache, "good", large));
        cache.Clear(); Assert.Equal(0ul, cache.PayloadBytes);
    }

    [Fact]
    public void ByteCapCanEvictSeveralReadyEntriesBeforeSuccessfulPublication()
    {
        var cache = Create(8, 1800);
        for (var index = 0; index < 8; index++)
        {
            _ = Add(cache, index.ToString(), new byte[200]);
            Assert.InRange(cache.PayloadBytes, 1ul, 1800ul);
        }
        Assert.Equal(6, cache.Count);
        var large = new byte[1000];
        Assert.Same(large, Add(cache, "last", large));
        Assert.Equal(3, cache.Count);
        Assert.Same(large, Add(cache, "last", []));
        Assert.InRange(cache.PayloadBytes, 1ul, 1800ul);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PendingKeyOwnershipIsReleasedByClearOrShrink(bool shrink)
    {
        var cache = Create(2, 4096);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var worker = Task.Run(() => cache.GetOrAdd("pending", 0, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return new byte[100];
        }, default(Utf8ConstructionTextNoControl), default(ByteStorage)));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, cache.Count); Assert.InRange(cache.PayloadBytes, 1ul, 4096ul);
            if (shrink) cache.Capacity = 0; else cache.Clear();
            Assert.Equal(0ul, cache.PayloadBytes);
        }
        finally { release.Set(); }
        Assert.Equal(100, (await worker).Length);
        Assert.Equal(0, cache.Count); Assert.Equal(0ul, cache.PayloadBytes);
    }

    [Fact]
    public void OriginalFactoryFailureReleasesKeyAndManifest()
    {
        var cache = Create(2, 4096);
        var failure = new InvalidOperationException("original");
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("bad", failure,
            static (_, error) => throw error, default(Utf8ConstructionTextNoControl), default(ByteStorage))));
        Assert.Equal(0, cache.Count); Assert.Equal(0ul, cache.PayloadBytes);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > 0);
    }

    [Fact]
    public void CancellationAfterFactoryKeepsUsefulEntriesAndRestoresParentLoans()
    {
        var cache = Create(1, 4096);
        var good = Add(cache, "good", new byte[100]);
        var before = cache.PayloadBytes;
        using var cancellation = new CancellationTokenSource();
        using var owner = Utf8ConstructionScope.Enter();
        var failure = Assert.Throws<OperationCanceledException>(() => cache.GetOrAdd("bad", cancellation,
            static (_, token) => { token.Cancel(); return new byte[100]; }, new TokenControl(cancellation.Token), default(ByteStorage)));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(before, cache.PayloadBytes); Assert.Same(good, Add(cache, "good", []));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StageAdmissionDenialsKeepThePreviousGenerationAndSpentWork(bool work)
    {
        var probe = Create(1, 4096); _ = Add(probe, "good", new byte[100]);
        ulong maximum;
        using (var owner = Utf8ConstructionScope.Enter())
        { _ = Add(probe, "new", new byte[100]); maximum = work ? owner.Budget.WorkSteps : owner.Budget.PeakBytes; }
        // Identity hashes have process-dependent collisions. Exercise every
        // bounded work cutoff and classify the actual outcome rather than
        // assuming a different object's successful probe count is identical.
        var capacities = work ? Enumerable.Range(0, checked((int)maximum + 129)).Select(static value => (ulong)value)
            : new[] { 0ul, maximum - 1, maximum };
        var failures = 0;
        var successes = 0;
        foreach (var capacity in capacities)
        {
            var cache = Create(1, 4096); var good = Add(cache, "good", new byte[100]);
            var before = cache.PayloadBytes;
            using var parent = new Utf8ConstructionScope(ulong.MaxValue,
                work ? ulong.MaxValue : 128 + capacity, work ? 2 + capacity : ulong.MaxValue, ulong.MaxValue);
            _ = parent.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = parent.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Add(cache, "new", new byte[100]));
                if (failure is not null)
                {
                    failures++;
                    Assert.Equal(work ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(before, cache.PayloadBytes);
                    Assert.Same(good, Add(cache, "good", []));
                }
                else successes++;
                if (!work && capacity < maximum) Assert.NotNull(failure);
                if (!work && capacity == maximum) Assert.Null(failure);
            }
            Assert.Equal(128ul, parent.Budget.ReservedBytes); Assert.Equal(checkpoint, parent.Budget.ReservedBytes);
            Assert.InRange(parent.Budget.WorkSteps, 2ul, work ? 2 + capacity : ulong.MaxValue);
        }
        Assert.True(failures > 0); Assert.True(successes > 0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClearFromAnyPostFactoryControlCannotResurrectAStage(bool full)
    {
        var probeCache = Create(full ? 1 : 2, 4096);
        if (full) _ = Add(probeCache, "old", new byte[100]);
        var probe = new ClearProbe(probeCache, int.MaxValue);
        _ = probeCache.GetOrAdd("new", probe, static (_, state) => { state.Armed = true; return new byte[100]; },
            new ClearControl(probe), default(ByteStorage));
        Assert.True(probe.Checks > 0);
        for (var cutoff = 1; cutoff <= probe.Checks; cutoff++)
        {
            var cache = Create(full ? 1 : 2, 4096);
            if (full) _ = Add(cache, "old", new byte[100]);
            var state = new ClearProbe(cache, cutoff);
            Assert.Equal(100, cache.GetOrAdd("new", state,
                static (_, item) => { item.Armed = true; return new byte[100]; }, new ClearControl(state), default(ByteStorage)).Length);
            Assert.True(state.Cleared); Assert.Equal(0, cache.Count); Assert.Equal(0ul, cache.PayloadBytes);
        }
    }

    private sealed class ClearProbe(Utf8BoundedPreparationCache<string, byte[]> cache, int cutoff)
    {
        internal bool Armed;
        internal int Checks;
        internal bool Cleared;
        internal void Check()
        {
            if (!Armed) return;
            if (++Checks == cutoff) { cache.Clear(); Cleared = true; }
        }
    }
    private readonly struct ClearControl(ClearProbe probe) : IUtf8ConstructionTextControl
    { public void Check() => probe.Check(); }

    [Fact]
    public void UncertifiedGraphsCannotEnterAPayloadBoundedCache()
    {
        var cache = Create(2, 4096);
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("key", static _ => new byte[100]));
        Assert.True(cache.IsPayloadBounded); Assert.Equal(0, cache.Count); Assert.Equal(0ul, cache.PayloadBytes);
        var globalControl = Utf8BoundedPreparationCache<string, byte[]>.Create(2);
        Assert.False(globalControl.IsPayloadBounded);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EntryChargeBoundaryAdmitsOrDeclinesWithoutTruncatingTheValue(bool exact)
    {
        var charge = Utf8ConstructionBudget.StringBytes(1) + Utf8ConstructionBudget.ArrayBytes<byte>(100);
        var cache = Create(1, exact ? charge : charge - 1);
        var value = new byte[100]; value[99] = 42;
        Assert.Same(value, Add(cache, "a", value));
        Assert.Equal(exact ? 1 : 0, cache.Count);
        Assert.Equal(exact ? charge : 0UL, cache.PayloadBytes);
        Assert.Equal(42, value[99]);
        cache.Clear(); Assert.Equal(0UL, cache.PayloadBytes);
    }

    [Fact]
    public void SharedStorageCanEvictEarlierWhileReturnedAliasesRemainValid()
    {
        var charge = Utf8ConstructionBudget.StringBytes(1) + Utf8ConstructionBudget.ArrayBytes<byte>(100);
        var cache = Create(2, 2 * charge - 1);
        var shared = new byte[100]; shared[99] = 42;
        var first = Add(cache, "a", shared);
        var second = Add(cache, "b", shared);
        Assert.Equal(1, cache.Count); Assert.Equal(charge, cache.PayloadBytes);
        Assert.Same(first, second); Assert.Same(shared, Add(cache, "b", new byte[1000]));
        cache.Clear(); Assert.Equal(42, first[99]); Assert.Equal(42, second[99]);
        Assert.Null(cache.GetType().GetField("_payloadLedger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
    }

    [Fact]
    public void NearUlongMaximumChargesEvictWithoutWrappingTheAggregate()
    {
        var cache = Utf8BoundedPreparationCache<int, object>.CreateRetained(2, EqualityComparer<int>.Default, 65536, ulong.MaxValue);
        var first = new object(); var second = new object();
        _ = cache.GetOrAdd(1, first, static (_, value) => value, default(Utf8ConstructionTextNoControl), new HugeStorage(ulong.MaxValue - 32));
        Assert.Equal(ulong.MaxValue - 32, cache.PayloadBytes);
        _ = cache.GetOrAdd(2, second, static (_, value) => value, default(Utf8ConstructionTextNoControl), new HugeStorage(64));
        Assert.Equal(1, cache.Count); Assert.Equal(64UL, cache.PayloadBytes);
        Assert.Same(second, cache.GetOrAdd(2, first, static (_, value) => value, default(Utf8ConstructionTextNoControl), new HugeStorage(64)));
        cache.Clear(); Assert.Equal(0UL, cache.PayloadBytes);
    }

    private readonly struct HugeStorage(ulong charge) : IUtf8RetainedStorage<int, object>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public void CollectKey<TControl>(int key, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl { }
        public void CollectValue<TControl>(object value, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => storage.Add(value, charge);
    }

    private readonly struct ByteStorage : IUtf8RetainedStorage<string, byte[]>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public void CollectKey<TControl>(string key, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => storage.Text(key);
        public void CollectValue<TControl>(byte[] value, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl { storage.Array(value); storage.Array(value); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CollectorScopeCannotBeReinterpretedAsAnotherOwnershipProof(bool descendants)
    {
        var cache = descendants
            ? Utf8BoundedPreparationCache<string, byte[]>.CreateRootAndDescendantRetained(2, StringComparer.Ordinal, 65536, 4096)
            : Create(2, 4096);
        var called = false;
        if (descendants)
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("key", 0,
                (_, _) => { called = true; return new byte[100]; }, default(Utf8ConstructionTextNoControl), default(ByteStorage)));
        else
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("key", 0,
                (_, _) => { called = true; return new byte[100]; }, default(Utf8ConstructionTextNoControl), default(DescendantByteStorage)));
        Assert.False(called);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0ul, cache.PayloadBytes);
    }

    private readonly struct DescendantByteStorage : IUtf8RetainedStorage<string, byte[]>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.RootsAndBoundedDescendants;
        public void CollectKey<TControl>(string key, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => storage.Text(key);
        public void CollectValue<TControl>(byte[] value, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => storage.Array(value);
    }
    private readonly struct TokenControl(CancellationToken token) : IUtf8ConstructionTextControl
    { public void Check() => token.ThrowIfCancellationRequested(); }
}
