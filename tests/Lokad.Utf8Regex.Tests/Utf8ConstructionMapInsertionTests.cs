using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionMapInsertionTests
{
    private sealed class Collisions : IEqualityComparer<int>
    {
        internal int HashCalls;
        internal int EqualityCalls;
        internal int ThrowAt;
        public int GetHashCode(int value) { HashCalls++; return 0; }
        public bool Equals(int left, int right)
        {
            if (++EqualityCalls == ThrowAt) throw new InvalidOperationException("comparer");
            return left == right;
        }
    }

    [Fact]
    public void AddHashesOnceAndDuplicateCannotReplaceValueOrGrowCapacity()
    {
        var budget = new Utf8ConstructionBudget(1000, 1000000);
        var comparer = new Collisions();
        using var map = new Utf8ConstructionMap<int, int>(budget, comparer);
        for (var key = 0; key < 65; key++)
        {
            var hashes = comparer.HashCalls;
            map.Add(key, key * 3);
            Assert.Equal(hashes + 1, comparer.HashCalls);
        }
        var extent = map.StorageBytes;
        var hashesBefore = comparer.HashCalls;
        Assert.Throws<ArgumentException>(() => map.Add(64, -1));
        Assert.Equal(hashesBefore + 1, comparer.HashCalls);
        Assert.Equal(extent, map.StorageBytes);
        Assert.Equal(65, map.Count); Assert.Equal(192, map[64]);
        map[64] = -1;
        Assert.Equal(-1, map[64]); Assert.Equal(65, map.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GrowthFailureKeepsOldTableAndParentLoan(bool cancel)
    {
        var budget = new Utf8ConstructionBudget(1000, 1000000);
        budget.ReserveBytes(13);
        using var token = new CancellationTokenSource();
        budget.CancellationToken = token.Token;
        var comparer = new Collisions();
        using var map = new Utf8ConstructionMap<int, int>(budget, comparer);
        map.Add(0, 10); map.Add(1, 11);
        var held = budget.ReservedBytes; var storage = map.StorageBytes;
        // Count the old-table comparisons made before allocation on this engine.
        // The witness must reach the final probe in both baseline and candidate.
        var beforeProbeEqualities = comparer.EqualityCalls;
        var targetEquality = 0;
        var cancellationProbe = new Utf8ConstructionBudget(1000, 1000000);
        var probeComparer = new Collisions();
        using (var probe = new Utf8ConstructionMap<int, int>(cancellationProbe, probeComparer))
        {
            probe.Add(0, 10); probe.Add(1, 11);
            var previousHashes = probeComparer.HashCalls;
            probe.Add(2, 12);
            targetEquality = beforeProbeEqualities + 2 * (probeComparer.HashCalls - previousHashes) + 1;
        }
        var entered = false;
        if (cancel)
            budget.CheckpointHook = () =>
            {
                if (budget.ReservedBytes > held && comparer.EqualityCalls >= targetEquality)
                { entered = true; token.Cancel(); }
            };
        else comparer.ThrowAt = targetEquality;
        if (cancel) Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => map.Add(2, 12)).CancellationToken);
        else Assert.Equal("comparer", Assert.Throws<InvalidOperationException>(() => map.Add(2, 12)).Message);
        if (cancel) Assert.True(entered);
        Assert.Equal(held, budget.ReservedBytes); Assert.Equal(storage, map.StorageBytes); Assert.Equal(2, map.Count);
        budget.CheckpointHook = null; budget.CancellationToken = default; comparer.ThrowAt = 0;
        Assert.Equal(10, map[0]); Assert.Equal(11, map[1]);
        map.Dispose(); Assert.Equal(13UL, budget.ReservedBytes);
    }
    [Fact]
    public void CoarseCollisionBlocksCheckCancellation()
    {
        Utf8ConstructionReadOnlyMap<int, int> snapshot;
        var comparer = new Collisions();
        using (var preparation = Utf8ConstructionScope.Enter())
        {
            using var arena = Utf8ConstructionArena.Create(preparation.Budget);
            using var map = new Utf8ConstructionMap<int, int>(preparation.Budget, comparer);
            for (var key = 0; key < 160; key++) map.Add(key, key);
            snapshot = map.Freeze(arena);
            arena.Complete(); preparation.Complete();
        }
        comparer.EqualityCalls = 0;
        using var cancellation = new CancellationTokenSource();
        using (var operation = new Utf8ConstructionScope(ulong.MaxValue, 0, ulong.MaxValue))
        {
            operation.Budget.CancellationToken = cancellation.Token;
            var checkpoints = 0;
            operation.Budget.CheckpointHook = () => { if (checkpoints++ == 2) cancellation.Cancel(); };
            var failure = Record.Exception(() => snapshot.ContainsKey(-1));
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.InRange(comparer.EqualityCalls, 1, 127);
            Assert.Equal(0ul, operation.Budget.ReservedBytes);
        }
        Assert.Equal(0, snapshot[0]); Assert.Equal(159, snapshot[159]);
        using var funded = new Utf8ConstructionScope(ulong.MaxValue, 0, ulong.MaxValue);
        Assert.False(snapshot.ContainsKey(-1));
    }
}
