using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparationCacheOwnershipTests
{
    [Fact]
    public async Task ClearDuringPreparationCannotRetainTheDetachedGeneration()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.Create(2);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var value = new object();
        var worker = Task.Run(() => cache.GetOrAdd("old", _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return value;
        }));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            cache.Clear();
        }
        finally { release.Set(); }
        Assert.Same(value, await worker);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.QueueLengthForTests);
    }

    [Fact]
    public async Task DistinctPendingPreparationsNeverExceedTheRetainedEntryLimit()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.Create(1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var worker = Task.Run(() => cache.GetOrAdd("pending", _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return new object();
        }));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            _ = cache.GetOrAdd("second", _ =>
            {
                Assert.InRange(cache.Count, 0, 1);
                return new object();
            });
        }
        finally { release.Set(); }
        _ = await worker;
        Assert.InRange(cache.Count, 0, 1);
    }

    [Fact]
    public void OwnedCacheMetadataHasNoHiddenCollectionOrLazyFields()
    {
        var type = typeof(Utf8BoundedPreparationCache<string, object>);
        var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.DoesNotContain(fields, field => field.FieldType.Namespace == "System.Collections.Concurrent"
            || field.FieldType.FullName?.StartsWith("System.Lazy", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ClearDuringAnUncachedFullMissCannotPublishItsResult()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.Create(1);
        _ = cache.GetOrAdd("old", static _ => new object());
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var value = new object();
        var worker = Task.Run(() => cache.GetOrAdd("new", _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return value;
        }));
        try { Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); cache.Clear(); }
        finally { release.Set(); }
        Assert.Same(value, await worker);
        Assert.Equal(0, cache.Count);
        Assert.Equal(Utf8BoundedPreparationCache<string, object>.ObjectBytes, cache.MetadataBytes);
    }

    [Fact]
    public async Task DetachedFailureCannotRemoveANewGenerationAfterShrink()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.Create(1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var error = new InvalidOperationException("original");
        var worker = Task.Run(() => Record.Exception(() => cache.GetOrAdd("key", _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            throw error;
        })));
        object retained;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            cache.Capacity = 0; cache.Capacity = 1;
            retained = cache.GetOrAdd("key", static _ => new object());
        }
        finally { release.Set(); }
        Assert.Same(error, await worker);
        Assert.Same(retained, cache.GetOrAdd("key", static _ => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task AWaitingCallerCanCancelWithoutAbortingTheSharedPreparation()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.Create(1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var waiting = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var value = new object();
        var owner = Task.Run(() => cache.GetOrAdd("key", _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return value;
        }));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            var probe = new WaitProbe(waiting);
            var follower = Task.Run(() => Record.Exception(() => cache.GetOrAdd("key", 0,
                static (_, _) => throw new InvalidOperationException("recomputed"), new WaitControl(cancellation.Token, probe))));
            Assert.True(waiting.Wait(TimeSpan.FromSeconds(10)));
            cancellation.Cancel();
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(await follower).CancellationToken);
            Assert.Equal(1, cache.Count);
        }
        finally { release.Set(); }
        Assert.Same(value, await owner);
        Assert.Same(value, cache.GetOrAdd("key", static _ => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task FollowersReceiveTheOriginalFailureAndTheNextGenerationCanRecover()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.Create(1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var waiting = new ManualResetEventSlim();
        var error = new InvalidOperationException("identity");
        var owner = Task.Run(() => Record.Exception(() => cache.GetOrAdd("key", _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            throw error;
        })));
        Task<Exception?> follower;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            follower = Task.Run(() => Record.Exception(() => cache.GetOrAdd("key", 0,
                static (_, _) => throw new InvalidOperationException("recomputed"), new WaitControl(default, new WaitProbe(waiting)))));
            Assert.True(waiting.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { release.Set(); }
        Assert.Same(error, await owner);
        Assert.Same(error, await follower);
        Assert.Equal(0, cache.Count);
        var retained = cache.GetOrAdd("key", static _ => new object());
        Assert.Same(retained, cache.GetOrAdd("key", static _ => throw new InvalidOperationException()));
    }

    [Fact]
    public void ReentrantPreparationFailsExplicitlyAndDoesNotRetainItsFailure()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.Create(1);
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("key", key => cache.GetOrAdd(key, static _ => "wrong")));
        Assert.Equal(0, cache.Count);
        Assert.Equal("recovered", cache.GetOrAdd("key", static _ => "recovered"));
    }

    [Fact]
    public void AReadyNullValueIsSharedWithoutRecomputation()
    {
        var cache = Utf8BoundedPreparationCache<string, string?>.Create(1);
        Assert.Null(cache.GetOrAdd("key", static _ => null));
        Assert.Null(cache.GetOrAdd("key", static _ => throw new InvalidOperationException()));
    }

    private sealed class WaitProbe(ManualResetEventSlim waiting)
    {
        private int _checks;
        internal void Check() { if (Interlocked.Increment(ref _checks) >= 3) waiting.Set(); }
    }

    private readonly struct WaitControl(CancellationToken token, WaitProbe probe) : IUtf8ConstructionTextControl
    {
        public void Check() { probe.Check(); token.ThrowIfCancellationRequested(); }
    }
}
