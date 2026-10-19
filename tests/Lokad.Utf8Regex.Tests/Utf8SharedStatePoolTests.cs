using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8SharedStatePoolTests
{
    private readonly record struct ReferenceState(object? Graph, int Value);
    private readonly record struct WideState(long A, long B, long C, long D, long E, long F, long G, long H);
    private readonly record struct DomainState<T>(WideState Payload, T Marker) where T : struct;
    private readonly record struct Marker<T>(T Value) where T : struct;

    [Fact]
    public void RetentionTransfersAcrossThreadsWithoutAnotherDomainTable()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var original = Utf8ExactStatePool<ReferenceState>.Rent(16, true);
            original[0] = new(new object(), 23);
            Utf8ExactStatePool<ReferenceState>.Return(original, true);
            var before = Inspect();
            ReferenceState[]? borrowed = null;
            var thread = new Thread(() => borrowed = Utf8ExactStatePool<ReferenceState>.Rent(16, false));
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
            Assert.Same(original, borrowed);
            Assert.All(borrowed!, value => Assert.Equal(default, value));
            Assert.Equal(before.Bytes - Physical(original), Inspect().Bytes);
            Utf8ExactStatePool<ReferenceState>.Return(borrowed!, false);
            Assert.Equal(0, Inspect().Leaves);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void FiniteLoansCannotInitializeOrGrowAnyTypeDomain()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var finite = Utf8ExactStatePool<int>.Rent(16, false);
            Utf8ExactStatePool<int>.Return(finite, false);
            Assert.Equal(0, Inspect().Bytes);
            var retained = Utf8ExactStatePool<long>.Rent(32, true);
            Utf8ExactStatePool<long>.Return(retained, true);
            var before = Inspect();
            finite = Utf8ExactStatePool<int>.Rent(16, false);
            Utf8ExactStatePool<int>.Return(finite, false);
            Assert.Equal(before.Bytes, Inspect().Bytes);
            Assert.Equal(before.Leaves, Inspect().Leaves);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void ReferenceArrayLoansKeepExactElementTypesAndClearPublishedLeaves()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            // A covariant return must not admit a string[] as writable object[].
            var covariant = new string[16];
            covariant[0] = "kept";
            Utf8ExactStatePool<object>.Return(covariant, true);
            Assert.Equal(0, Utf8ExactStatePool.RetainedBytes);
            Assert.Equal("kept", covariant[0]);

            var strings = Utf8ExactStatePool<string>.Rent(16, true);
            strings[0] = "capture";
            Utf8ExactStatePool<string>.Return(strings, true);
            Assert.Equal(0, Utf8ExactStatePool<object>.RetainedBytes);
            var objects = Utf8ExactStatePool<object>.Rent(16, false);
            Assert.NotSame(strings, objects);
            objects[0] = new object();
            Utf8ExactStatePool<object>.Return(objects, true);

            Assert.Same(strings, Utf8ExactStatePool<string>.Rent(16, false));
            Assert.All(strings, value => Assert.Null(value));
            Assert.Same(objects, Utf8ExactStatePool<object>.Rent(16, false));
            Assert.All(objects, value => Assert.Null(value));
            Assert.Equal(0, Inspect().Leaves);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void TableAndDistinctLeafLoansMatchTheActualRetainedGraph()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var small = Utf8ExactStatePool<int>.Rent(16, true);
            var wide = Utf8ExactStatePool<long>.Rent(128, true);
            Utf8ExactStatePool<int>.Return(small, true);
            Utf8ExactStatePool<long>.Return(wide, true);
            var actual = Inspect();
            Assert.Equal(2, actual.Leaves);
            Assert.Equal(Physical(small), Utf8ExactStatePool<int>.RetainedBytes);
            Assert.Equal(Physical(wide), Utf8ExactStatePool<long>.RetainedBytes);
            Assert.Equal(Utf8ExactStatePool.TableBytes + Physical(small) + Physical(wide), actual.Bytes);
            var replacement = new int[16];
            Utf8ExactStatePool<int>.Return(replacement, true);
            Assert.Equal(actual.Bytes + Physical(replacement), Inspect().Bytes);
            Assert.Equal(3, Inspect().Leaves);
            Assert.Same(small, Utf8ExactStatePool<int>.Rent(16, false));
            Assert.Same(replacement, Utf8ExactStatePool<int>.Rent(16, false));
            Assert.Equal(1, Inspect().Leaves);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void FullArrayExtentsFitWithinThePerTypeCap()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            foreach (var capacity in new[] { 16, 32, 64, 128, 256 })
            {
                var items = Utf8ExactStatePool<WideState>.Rent(capacity, true);
                Assert.Equal(capacity, items.Length);
                Utf8ExactStatePool<WideState>.Return(items, true);
                Assert.InRange(Utf8ExactStatePool<WideState>.RetainedBytes, 0, Utf8ExactStatePool<WideState>.MaximumRetainedBytes);
                Inspect();
            }
            Assert.Equal(15488, Utf8ExactStatePool<WideState>.RetainedBytes);
            var oversized = Utf8ExactStatePool<WideState>.Rent(256, false);
            var before = Inspect();
            Utf8ExactStatePool<WideState>.Return(oversized, true);
            Assert.Equal(before.Bytes, Inspect().Bytes);
            Assert.NotSame(oversized, Utf8ExactStatePool<WideState>.Rent(256, false));
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void IdenticalBucketsStillRespectTheCompletePerTypeAndSlotBounds()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var leaves = new WideState[20][];
            for (var i = 0; i < leaves.Length; i++) leaves[i] = Utf8ExactStatePool<WideState>.Rent(16, true);
            foreach (var leaf in leaves) Utf8ExactStatePool<WideState>.Return(leaf, true);
            var expectedLeaves = Utf8ExactStatePool<WideState>.MaximumRetainedBytes / Physical(leaves[0]);
            var actual = Inspect();
            Assert.Equal(expectedLeaves, actual.Leaves);
            Assert.Equal(expectedLeaves * Physical(leaves[0]), Utf8ExactStatePool<WideState>.RetainedBytes);
            Assert.Equal(Utf8ExactStatePool.TableBytes + expectedLeaves * Physical(leaves[0]), actual.Bytes);
            var borrowed = new HashSet<WideState[]>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < expectedLeaves; i++) Assert.True(borrowed.Add(Utf8ExactStatePool<WideState>.Rent(16, false)));
            Assert.Equal(0, Inspect().Leaves);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void DuplicateReturnCannotPublishAnAliasedWorkingLeaf()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var leaf = Utf8ExactStatePool<int>.Rent(16, true);
            Utf8ExactStatePool<int>.Return(leaf, true);
            var before = Inspect();
            Utf8ExactStatePool<int>.Return(leaf, true);
            Assert.Equal(before, Inspect());
            Assert.Same(leaf, Utf8ExactStatePool<int>.Rent(16, false));
            Assert.NotSame(leaf, Utf8ExactStatePool<int>.Rent(16, false));
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SlotAndByteBoundsCoverManyTypesTogether(bool large)
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var attemptedBytes = 0;
            var attemptedLeaves = 0;
            Fill<byte>(); Fill<sbyte>(); Fill<short>(); Fill<ushort>();
            Fill<int>(); Fill<uint>(); Fill<long>(); Fill<ulong>();
            Fill<float>(); Fill<double>(); Fill<bool>(); Fill<char>();
            Fill<DateTime>(); Fill<Guid>(); Fill<decimal>(); Fill<TimeSpan>();
            Fill<Marker<byte>>(); Fill<Marker<sbyte>>(); Fill<Marker<short>>(); Fill<Marker<ushort>>();
            Fill<Marker<int>>(); Fill<Marker<uint>>(); Fill<Marker<long>>(); Fill<Marker<ulong>>();
            Fill<Marker<float>>(); Fill<Marker<double>>(); Fill<Marker<bool>>(); Fill<Marker<char>>();
            Fill<Marker<DateTime>>(); Fill<Marker<Guid>>(); Fill<Marker<decimal>>(); Fill<Marker<TimeSpan>>();
            var actual = Inspect();
            Assert.True(attemptedBytes > Utf8ExactStatePool.MaximumRetainedBytes);
            Assert.True(attemptedLeaves > actual.Leaves);
            if (!large) Assert.Equal(Utf8ExactStatePool.MaximumEntries, actual.Leaves);

            void Fill<T>() where T : struct
            {
                foreach (var capacity in large ? new[] { 128 } : new[] { 16, 32, 64 })
                {
                    var items = Utf8ExactStatePool<DomainState<T>>.Rent(capacity, true);
                    attemptedBytes += Physical(items); attemptedLeaves++;
                    Utf8ExactStatePool<DomainState<T>>.Return(items, true);
                    var snapshot = Inspect();
                    Assert.InRange(snapshot.Bytes, 0, Utf8ExactStatePool.MaximumRetainedBytes);
                    Assert.InRange(snapshot.Leaves, 0, Utf8ExactStatePool.MaximumEntries);
                    Assert.InRange(Utf8ExactStatePool<DomainState<T>>.RetainedBytes, 0, Utf8ExactStatePool<DomainState<T>>.MaximumRetainedBytes);
                }
            }
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void ConcurrentBorrowersHaveExclusiveArraysAndReturnClearedGraphs()
    {
        Utf8ExactStatePool.Clear();
        using var ready = new CountdownEvent(8);
        using var release = new ManualResetEventSlim();
        var arrays = new ReferenceState[8][];
        var failures = new ConcurrentQueue<Exception>();
        var threads = Enumerable.Range(0, 8).Select(index => new Thread(() =>
        {
            try
            {
                var items = Utf8ExactStatePool<ReferenceState>.Rent(128, true);
                Array.Fill(items, new ReferenceState(new object(), index));
                arrays[index] = items;
                ready.Signal();
                release.Wait();
                Assert.All(items, item => Assert.Equal(index, item.Value));
                Utf8ExactStatePool<ReferenceState>.Return(items, true);
            }
            catch (Exception failure) { failures.Enqueue(failure); }
        })).ToArray();
        try
        {
            foreach (var thread in threads) thread.Start();
            Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(8, arrays.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.Equal(0, Inspect().Leaves);
        }
        finally
        {
            release.Set();
            foreach (var thread in threads) Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
            Utf8ExactStatePool.Clear();
        }
        Assert.Empty(failures);
        Assert.All(arrays, items => Assert.All(items, value => Assert.Equal(default, value)));
    }

    [Fact]
    public void ClearReleasesTheOwnerWithoutInvalidatingBorrowedArrays()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var items = Utf8ExactStatePool<ReferenceState>.Rent(32, true);
            items[0] = new(new object(), 73);
            Utf8ExactStatePool.Clear();
            Assert.Equal(73, items[0].Value);
            Utf8ExactStatePool<ReferenceState>.Return(items, false);
            Assert.Equal(0, Inspect().Bytes);
            Utf8ExactStatePool<ReferenceState>.Return(items, true);
            Assert.Equal(0, Inspect().Bytes);
            Assert.Equal(default, items[0]); // Return relinquishes its working graph.
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void ContendedRetentionBypassesWithoutWaitingOrMutatingTheOwner()
    {
        Utf8ExactStatePool.Clear();
        var field = typeof(Utf8ExactStatePool).GetField("s_gate", BindingFlags.Static | BindingFlags.NonPublic)!;
        var originalGate = field.GetValue(null);
        var held = new SpinLock(enableThreadOwnerTracking: false);
        var taken = false;
        Thread? worker = null;
        Exception? failure = null;
        try
        {
            var original = Utf8ExactStatePool<ReferenceState>.Rent(16, true);
            Utf8ExactStatePool<ReferenceState>.Return(original, true);
            var bytes = Inspect().Bytes;
            held.Enter(ref taken);
            field.SetValue(null, held);
            worker = new Thread(() =>
            {
                try
                {
                    var items = Utf8ExactStatePool<ReferenceState>.Rent(16, true);
                    Assert.NotSame(original, items);
                    items[0] = new(new object(), 17);
                    Utf8ExactStatePool<ReferenceState>.Return(items, true);
                    Assert.Equal(default, items[0]);
                    var replacementRegex = new Utf8Regex("foo");
                    var replacementInput = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("foo-", 128)));
                    var replacementDestination = new byte[replacementInput.Length];
                    _ = replacementRegex.TryReplace(replacementInput, "bar", replacementDestination, out _);
                    var replacementBefore = GC.GetAllocatedBytesForCurrentThread();
                    for (var index = 0; index < 100; index++)
                        Assert.Equal(System.Buffers.OperationStatus.Done,
                            replacementRegex.TryReplace(replacementInput, "bar", replacementDestination, out _));
                    Assert.True(GC.GetAllocatedBytesForCurrentThread() - replacementBefore > 128 * 100);
                }
                catch (Exception error) { failure = error; }
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
            Assert.Null(failure);
            field.SetValue(null, originalGate);
            Assert.Equal(bytes, Inspect().Bytes);
            Assert.Same(original, Utf8ExactStatePool<ReferenceState>.Rent(16, false));
            // The live native verifier retains its zero-byte warm guard once
            // optional pool retention is available without contention.
            new Utf8AsciiStructuralRuntimeTests().WarmAsciiRuntimeBorrowsAllStorageWithoutTemporaryAllocation();
            new ReplacementInfrastructureTests().DestinationReplacementHasBoundedWarmedAllocationIndependentOfMatchCount();
        }
        finally
        {
            field.SetValue(null, originalGate);
            if (taken) held.Exit();
            if (worker is not null) Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
            Utf8ExactStatePool.Clear();
        }
    }

    [Theory]
    [InlineData(454, false)] [InlineData(455, true)]
    public void GrowthAdmitsBothFullExtentsAndPreservesBorrowedLoans(ulong maximum, bool allow)
    {
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, maximum), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(7));
        var buffer = new Utf8InvocationStateBuffer<long>();
        try
        {
            for (var index = 0; index < 16; index++) Assert.Equal(Utf8ExecutionLimitKind.None, buffer.TryPush(index, ref invocation));
            Assert.Equal(167ul, invocation.Ledger.ReservedBytes);
            Assert.Equal(allow ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, buffer.TryPush(99, ref invocation));
            Assert.Equal(allow ? 17 : 16, buffer.Count);
            for (var index = 0; index < 16; index++) Assert.Equal(index, buffer[index]);
            Assert.Equal(allow ? 295ul : 167ul, invocation.Ledger.ReservedBytes);
            if (allow) Assert.Equal(455ul, invocation.Budget.PeakWorkingBytes);
        }
        finally { buffer.Release(ref invocation); }
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
    }

    private static (int Bytes, int Leaves) Inspect()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(Utf8ExactStatePool);
        Assert.Equal(4, type.GetFields(flags).Count(field => !field.IsLiteral));
        var table = (Array?)type.GetField("s_items", flags)!.GetValue(null);
        if (table is null) { Assert.Equal(0, Utf8ExactStatePool.RetainedBytes); return (0, 0); }
        Assert.Equal(Utf8ExactStatePool.MaximumEntries, table.Length);
        var entryType = table.GetType().GetElementType()!;
        Assert.Equal(2, entryType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Length);
        var valueProperty = entryType.GetProperty("Value")!;
        var bytesProperty = entryType.GetProperty("Bytes")!;
        var bytes = Physical(table);
        var identities = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var entry in table)
        {
            if (valueProperty.GetValue(entry) is not Array leaf) { Assert.Equal(0, bytesProperty.GetValue(entry)); continue; }
            Assert.True(identities.Add(leaf));
            var extent = Physical(leaf);
            Assert.Equal(extent, bytesProperty.GetValue(entry));
            bytes += extent;
        }
        Assert.Equal(bytes, Utf8ExactStatePool.RetainedBytes);
        Assert.InRange(bytes, 0, Utf8ExactStatePool.MaximumRetainedBytes);
        return (bytes, identities.Count);
    }

    private static int Physical(Array array)
    {
        var method = typeof(Utf8SharedStatePoolTests).GetMethod(nameof(Stride), BindingFlags.Static | BindingFlags.NonPublic)!;
        var stride = (int)method.MakeGenericMethod(array.GetType().GetElementType()!).Invoke(null, null)!;
        return checked(32 + (array.Length * stride + 15) / 16 * 16);
    }

    private static int Stride<T>() => Unsafe.SizeOf<T>();
}
