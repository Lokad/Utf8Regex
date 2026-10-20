using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparationCacheKeyAdmissionTests
{
    private static readonly string Oversized = new('x', 1024);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ImpossibleKeysNeverReachHashOrComparisonAndPreservePublishedEntries(bool fail)
    {
        var comparer = new BoundedComparer();
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(2, comparer, 65536, 1024);
        var held = new object();
        Assert.Same(held, cache.GetOrAdd("ok", held, static (_, value) => value,
            default(Utf8ConstructionTextNoControl), default(Storage)));
        var retained = cache.PayloadBytes;
        var calls = comparer.HashCalls;
        var created = new object();
        using var parent = Utf8ConstructionScope.Enter();
        parent.Budget.ReserveBytes(7);
        var factoryCalls = 0;
        object Prepare(string key, object value)
        {
            factoryCalls++;
            Assert.Equal(Oversized, key);
            Assert.Same(parent.Budget, Utf8ConstructionScope.CurrentBudget);
            if (fail) throw new FormatException("failed preparation");
            return value;
        }
        var failure = Record.Exception(() => Assert.Same(created, cache.GetOrAdd(Oversized, created,
            Prepare, default(Utf8ConstructionTextNoControl), default(Storage))));
        if (fail) Assert.IsType<FormatException>(failure);
        else Assert.Null(failure);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(calls, comparer.HashCalls);
        Assert.Equal(1, cache.Count);
        Assert.Equal(retained, cache.PayloadBytes);
        Assert.Equal(7ul, parent.Budget.ReservedBytes);
        Assert.True(parent.Budget.WorkSteps > 0);
        Assert.Same(held, cache.GetOrAdd("ok", held, static (_, _) => throw new InvalidOperationException("recomputed"),
            default(Utf8ConstructionTextNoControl), default(Storage)));
    }

    [Fact]
    public void OversizedKeyStillUsesOriginalConstructionWorkAdmission()
    {
        var comparer = new BoundedComparer();
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(2, comparer, 65536, 1024);
        using var parent = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        parent.Budget.ReserveBytes(7);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => cache.GetOrAdd(Oversized, new object(),
            static (_, _) => throw new InvalidOperationException("factory must not run"),
            default(Utf8ConstructionTextNoControl), default(Storage)));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
        Assert.Equal(1ul, error.Requested);
        Assert.Equal(0, comparer.HashCalls);
        Assert.Equal(0ul, parent.Budget.WorkSteps);
        Assert.Equal(7ul, parent.Budget.ReservedBytes);
        Assert.Same(parent.Budget, Utf8ConstructionScope.CurrentBudget);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void CancellationPrecedesOversizedKeyAndFactoryWork()
    {
        var comparer = new BoundedComparer();
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(2, comparer, 65536, 1024);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => cache.GetOrAdd(Oversized, new object(),
            static (_, _) => throw new InvalidOperationException("factory must not run"),
            new TokenControl(cancellation.Token), default(Storage)));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(0, comparer.HashCalls);
        Assert.Equal(0, cache.Count);
    }

    private sealed class BoundedComparer : IEqualityComparer<string>
    {
        internal int HashCalls;
        public int GetHashCode(string value)
        {
            HashCalls++;
            if (value.Length > 16) throw new InvalidOperationException("Oversized key reached hashing.");
            return StringComparer.Ordinal.GetHashCode(value);
        }
        public bool Equals(string? left, string? right)
        {
            if (left?.Length > 16 || right?.Length > 16)
                throw new InvalidOperationException("Oversized key reached comparison.");
            return StringComparer.Ordinal.Equals(left, right);
        }
    }

    private readonly struct Storage : IUtf8RetainedStorage<string, object>
    {
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);
        public bool TryGetValueCharge<TControl>(object value, ulong maximum, TControl control, out ulong charge)
            where TControl : struct, IUtf8ConstructionTextControl
        { control.Check(); charge = 128; return charge <= maximum; }
    }

    private readonly struct TokenControl(CancellationToken token) : IUtf8ConstructionTextControl
    { public void Check() => token.ThrowIfCancellationRequested(); }
}
