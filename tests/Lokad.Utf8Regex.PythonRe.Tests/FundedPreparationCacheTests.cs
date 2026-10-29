using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class FundedPreparationCacheTests
{
    [Fact]
    public void DetachedPendingEntryTransfersFundingToItsAlreadyWaitingUsers()
    {
        var allowance = new Allowance(1 << 20); Assert.True(allowance.TryReserve(13));
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(1, StringComparer.Ordinal);
        cache.EnableFunding(allowance);
        using var preparing = new ManualResetEventSlim(); using var finishPreparation = new ManualResetEventSlim();
        using var ownerReady = new ManualResetEventSlim(); using var followerReady = new ManualResetEventSlim();
        using var finishOwner = new ManualResetEventSlim(); using var finishFollower = new ManualResetEventSlim();
        var payload = new string('x', 16384); Exception? ownerError = null, followerError = null;
        var owner = new Thread(() =>
        {
            try
            {
                var value = cache.GetOrAdd("key", payload, (_, text) =>
                {
                    preparing.Set();
                    if (!finishPreparation.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Preparation was not released.");
                    return Utf8ConstructionScope.CurrentBudget.CopyText(text);
                }, default(Utf8ConstructionTextNoControl), default(TextStorage), out var use);
                using (use)
                {
                    Assert.Equal(payload, value); ownerReady.Set();
                    if (!finishOwner.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Owner was not released.");
                }
            }
            catch (Exception error) { ownerError = error; ownerReady.Set(); }
        }) { IsBackground = true };
        var follower = new Thread(() =>
        {
            try
            {
                var value = cache.GetOrAdd("key", payload, static (_, _) => throw new InvalidOperationException("Unexpected second factory."),
                    default(Utf8ConstructionTextNoControl), default(TextStorage), out var use);
                using (use)
                {
                    Assert.Equal(payload, value); followerReady.Set();
                    if (!finishFollower.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Follower was not released.");
                }
            }
            catch (Exception error) { followerError = error; followerReady.Set(); }
        }) { IsBackground = true };
        var followerStarted = false; owner.Start();
        try
        {
            Assert.True(preparing.Wait(TimeSpan.FromSeconds(30)));
            follower.Start(); followerStarted = true;
            Assert.True(SpinWait.SpinUntil(() => (follower.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(30)));
            cache.Capacity = 0; cache.Clear();
            finishPreparation.Set();
            Assert.True(ownerReady.Wait(TimeSpan.FromSeconds(30))); Assert.True(followerReady.Wait(TimeSpan.FromSeconds(30)));
            Assert.Null(ownerError); Assert.Null(followerError); Assert.Equal(0, cache.Count);
            var held = allowance.Used; Assert.True(held > 13);
            finishOwner.Set(); Assert.True(owner.Join(TimeSpan.FromSeconds(30))); Assert.Equal(held, allowance.Used);
            finishFollower.Set(); Assert.True(follower.Join(TimeSpan.FromSeconds(30))); Assert.Equal(13UL, allowance.Used);
        }
        finally
        {
            finishPreparation.Set(); finishOwner.Set(); finishFollower.Set();
            Assert.True(owner.Join(TimeSpan.FromSeconds(30)));
            if (followerStarted) Assert.True(follower.Join(TimeSpan.FromSeconds(30)));
            cache.Clear();
        }
        Assert.Null(ownerError); Assert.Null(followerError); allowance.Release(13);
    }

    [Fact]
    public void ZeroCapacityStillFundsUncachedValueUntilItsUseEnds()
    {
        var allowance = new Allowance(1 << 20);
        var cache = Utf8BoundedPreparationCache<string, string>.CreateRetained(0, StringComparer.Ordinal);
        cache.EnableFunding(allowance);
        var value = cache.GetOrAdd("key", 0, static (_, _) => Utf8ConstructionScope.CurrentBudget.CopyText("payload"),
            default(Utf8ConstructionTextNoControl), default(TextStorage), out var use);
        Assert.Equal("payload", value); Assert.Equal(0, cache.Count); Assert.True(allowance.Used > 0);
        use!.Dispose(); use.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    private readonly struct TextStorage : IUtf8RetainedStorage<string, string>
    {
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);
        public bool TryGetValueCharge<TControl>(string value, ulong maximum, TControl control, out ulong charge)
            where TControl : struct, IUtf8ConstructionTextControl
        { control.Check(); charge = Utf8ConstructionBudget.StringBytes(value.Length); return charge <= maximum; }
    }
}
