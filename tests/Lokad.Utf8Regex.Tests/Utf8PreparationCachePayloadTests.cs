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

    private static ulong Registry(int count) => Utf8ConstructionBudget.ArrayBytes<
        Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Utf8RetainedPayloadLedger.Reference>.Entry>(count);

    [Fact]
    public void SharedReferentsHaveOnePhysicalLoanAcrossEntriesAndReleasePrecisely()
    {
        var cache = Create(2, 4096);
        var shared = new byte[100];
        _ = Add(cache, "a", shared);
        var first = Utf8ConstructionBudget.StringBytes(1) + Utf8ConstructionBudget.ArrayBytes<byte>(100)
            + Utf8ConstructionBudget.ArrayBytes<Utf8RetainedPayloadExtent>(2) + Registry(8);
        Assert.Equal(first, cache.PayloadBytes);
        _ = Add(cache, "b", shared);
        var both = 2 * Utf8ConstructionBudget.StringBytes(1) + Utf8ConstructionBudget.ArrayBytes<byte>(100)
            + 2 * Utf8ConstructionBudget.ArrayBytes<Utf8RetainedPayloadExtent>(2) + Registry(16);
        Assert.Equal(both, cache.PayloadBytes);
        cache.Capacity = 1;
        Assert.Equal(first - Registry(8) + Registry(16), cache.PayloadBytes);
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
        Assert.Equal(2 * (Utf8ConstructionBudget.StringBytes(1) + Utf8ConstructionBudget.ArrayBytes<byte>(100)
            + Utf8ConstructionBudget.ArrayBytes<Utf8RetainedPayloadExtent>(2)) + Registry(16), cache.PayloadBytes);
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
        // A pending key can enlarge the registry; its slots remain charged until
        // a new stage compacts them or the last entry is detached.
        Assert.InRange(cache.PayloadBytes, before, 1024ul);
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
        Assert.InRange(cache.Count, 1, 3);
        var large = new byte[1000];
        Assert.Same(large, Add(cache, "last", large));
        Assert.Equal(1, cache.Count);
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
        Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Null(owner.Budget.ObjectCheckpoint);
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
        Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Null(owner.Budget.ObjectCheckpoint);
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
                work ? ulong.MaxValue : 112 + capacity, work ? 2 + capacity : ulong.MaxValue, ulong.MaxValue);
            _ = parent.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = parent.Budget.ObjectCheckpoint;
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
            Assert.Equal(112ul, parent.Budget.ReservedBytes); Assert.Same(checkpoint, parent.Budget.ObjectCheckpoint);
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

    private readonly struct ByteStorage : IUtf8RetainedStorage<string, byte[]>
    {
        public bool IsCertified => true;
        public void CollectKey<TControl>(string key, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => storage.Text(key);
        public void CollectValue<TControl>(byte[] value, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl { storage.Array(value); storage.Array(value); }
    }
    private readonly struct TokenControl(CancellationToken token) : IUtf8ConstructionTextControl
    { public void Check() => token.ThrowIfCancellationRequested(); }
}
