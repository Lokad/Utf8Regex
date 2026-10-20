using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparationCacheOwnershipTests
{
    [Fact]
    public async Task ClearDuringPreparationCannotRetainTheDetachedGeneration()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(2, StringComparer.Ordinal);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var value = new object();
        var worker = RunBlocking(() => cache.GetOrAdd("old", 0, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return value;
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
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
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(1, StringComparer.Ordinal);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var worker = RunBlocking(() => cache.GetOrAdd("pending", 0, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return new object();
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            _ = cache.GetOrAdd("second", 0, (_, _) =>
            {
                Assert.InRange(cache.Count, 0, 1);
                return new object();
            }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
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
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(1, StringComparer.Ordinal);
        _ = cache.GetOrAdd("old", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var value = new object();
        var worker = RunBlocking(() => cache.GetOrAdd("new", 0, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return value;
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
        try { Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); cache.Clear(); }
        finally { release.Set(); }
        Assert.Same(value, await worker);
        Assert.Equal(0, cache.Count);
        Assert.Equal(Utf8BoundedPreparationCache<string, object>.ObjectBytes, cache.MetadataBytes);
    }

    [Fact]
    public async Task DetachedFailureCannotRemoveANewGenerationAfterShrink()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(1, StringComparer.Ordinal);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var error = new InvalidOperationException("original");
        var worker = RunBlocking(() => Record.Exception(() => cache.GetOrAdd("key", 0, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            throw error;
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>))));
        object retained;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            cache.Capacity = 0; cache.Capacity = 1;
            retained = cache.GetOrAdd("key", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        }
        finally { release.Set(); }
        Assert.Same(error, await worker);
        Assert.Same(retained, cache.GetOrAdd("key", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
    }

    [Fact]
    public async Task AWaitingCallerCanCancelWithoutAbortingTheSharedPreparation()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(1, StringComparer.Ordinal);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var waiting = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var value = new object();
        var owner = RunBlocking(() => cache.GetOrAdd("key", 0, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return value;
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            var probe = new WaitProbe(waiting);
            var follower = RunBlocking(() => Record.Exception(() => cache.GetOrAdd("key", 0, static (_, _) => throw new InvalidOperationException("recomputed"), new WaitControl(cancellation.Token, probe), default(Utf8CacheTestStorage<object>))));
            Assert.True(waiting.Wait(TimeSpan.FromSeconds(10)));
            cancellation.Cancel();
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(await follower).CancellationToken);
            Assert.Equal(1, cache.Count);
        }
        finally { release.Set(); }
        Assert.Same(value, await owner);
        Assert.Same(value, cache.GetOrAdd("key", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
    }

    [Fact]
    public async Task FollowersReceiveTheOriginalFailureAndTheNextGenerationCanRecover()
    {
        var cache = Utf8BoundedPreparationCache<string, object>.CreateRetained(1, StringComparer.Ordinal);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var waiting = new ManualResetEventSlim();
        var error = new InvalidOperationException("identity");
        var owner = RunBlocking(() => Record.Exception(() => cache.GetOrAdd("key", 0, (_, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            throw error;
        }, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>))));
        Task<Exception?> follower;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            follower = RunBlocking(() => Record.Exception(() => cache.GetOrAdd("key", 0, static (_, _) => throw new InvalidOperationException("recomputed"), new WaitControl(default, new WaitProbe(waiting)), default(Utf8CacheTestStorage<object>))));
            Assert.True(waiting.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { release.Set(); }
        Assert.Same(error, await owner);
        Assert.Same(error, await follower);
        Assert.Equal(0, cache.Count);
        var retained = cache.GetOrAdd("key", 0, static (_, _) => new object(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>));
        Assert.Same(retained, cache.GetOrAdd("key", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<object>)));
    }

    [Fact]
    public void ReentrantPreparationFailsExplicitlyAndDoesNotRetainItsFailure()
    {
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(1, StringComparer.Ordinal);
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("key", 0, (key, _) => cache.GetOrAdd(key, 0, static (_, _) => "wrong", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
        Assert.Equal(0, cache.Count);
        Assert.Equal("recovered", cache.GetOrAdd("key", 0, static (_, _) => "recovered", default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string>)));
    }

    [Fact]
    public void AReadyNullValueIsSharedWithoutRecomputation()
    {
        var cache = Utf8BoundedPreparationCache<string, string?>.CreateRetained(1, StringComparer.Ordinal);
        Assert.Null(cache.GetOrAdd("key", 0, static (_, _) => null, default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string?>)));
        Assert.Null(cache.GetOrAdd("key", 0, static (_, _) => throw new InvalidOperationException(), default(Utf8ConstructionTextNoControl), default(Utf8CacheTestStorage<string?>)));
    }

    // These workers deliberately block on test handshakes. Do not depend on
    // ThreadPool injection while the test or other fixtures occupy its workers.
    private static Task<T> RunBlocking<T>(Func<T> work) => Task.Factory.StartNew(work,
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

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
