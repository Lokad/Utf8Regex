using System.Reflection;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFundedReplacementTests
{
    private static Utf8PythonRegex Compile(Allowance allowance, TimeSpan? timeout = null) => Utf8PythonRegex.Compile(
        "(?P<x>a)", default, timeout ?? Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, allowance);

    [Fact]
    public void WarmPlansStayFundedAndAreReusedUntilProgramDisposal()
    {
        var allowance = new Allowance(4 << 20); using var regex = Compile(allowance);
        var program = allowance.Used;
        Assert.Equal("a!", regex.SubnToString("a"u8, @"\g<x>!").ResultText);
        var retained = allowance.Used; Assert.True(retained > program);
        var reservations = allowance.Reservations;
        Assert.Equal("a!a!", regex.SubnToString("aa"u8, @"\g<x>!").ResultText);
        Assert.Equal(retained, allowance.Used); Assert.Equal(reservations, allowance.Reservations);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void InvalidCanceledAndDeniedPreparationsPreserveValidEntries(bool warm)
    {
        var allowance = new Allowance(4 << 20); using var regex = Compile(allowance);
        if (warm) Assert.Equal("a!", regex.SubnToString("a"u8, @"\g<x>!").ResultText);
        var retained = allowance.Used;
        var invalid = new string('x', 131072) + @"\q";
        Assert.Throws<PythonRePatternException>(() => regex.SubnToString(""u8, invalid));
        Assert.Equal(retained, allowance.Used);
        using var canceled = new CancellationTokenSource(); allowance.AfterGrant = canceled.Cancel;
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.SubnToString("a"u8, @"new\g<x>", 0, 0, canceled.Token)).CancellationToken);
        allowance.AfterGrant = null; Assert.Equal(retained, allowance.Used);
        allowance.Maximum = retained + 256;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.SubnToString("a"u8, new string('x', 65536)));
        Assert.Equal(retained, allowance.Used);
        allowance.Maximum = 4 << 20;
        Assert.Equal("a!", regex.SubnToString("a"u8, @"\g<x>!").ResultText);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void ReplacingManyTemplatesDoesNotAccumulateHistoricalCharges()
    {
        var allowance = new Allowance(4 << 20); using var regex = Compile(allowance);
        ulong saturated = 0;
        for (var i = 0; i < 256; i++)
        {
            var prefix = i.ToString("D4");
            Assert.Equal(prefix + "a", regex.SubnToString("a"u8, prefix + @"\g<x>").ResultText);
            if (i == 63) saturated = allowance.Used;
            if (i >= 64) Assert.Equal(saturated, allowance.Used);
        }
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public async Task ConcurrentTemplateReuseLeavesOnlyRetainedFunding()
    {
        var allowance = new Allowance(4 << 20); using var regex = Compile(allowance);
        for (var i = 0; i < 4; i++) regex.SubnToString("a"u8, i + @"\g<x>");
        var retained = allowance.Used;
        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            using var token = new CancellationTokenSource();
            for (var i = 0; i < 32; i++)
                Assert.Equal((i % 4) + "a", regex.SubnToString("a"u8, (i % 4) + @"\g<x>", 0, 0, token.Token).ResultText);
        })));
        Assert.Equal(retained, allowance.Used);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ActiveSubstitutionKeepsAnEvictedOrDisposedPlanFunded(bool disposeProgram)
    {
        var allowance = new Allowance(4 << 20); using var regex = Compile(allowance, TimeSpan.FromSeconds(30));
        var cache = Cache(regex);
        using var paused = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        Exception? error = null; string? output = null; var stopped = false;
        var worker = new Thread(() =>
        {
            // The completed status is observed only to coordinate a real public
            // substitution after plan acquisition. No production hook is added.
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            {
                if (!stopped && !Monitor.IsEntered(cache) && FirstStatus(cache) == 1)
                {
                    stopped = true; paused.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Active use was not released.");
                }
                return 0;
            });
            try { output = regex.SubnToString("a"u8, @"\g<x>!").ResultText; }
            catch (Exception failure) { error = failure; }
        }) { IsBackground = true };
        worker.Start();
        ulong held = 0;
        try
        {
            Assert.True(paused.Wait(TimeSpan.FromSeconds(30)), "Substitution did not reach its acquired plan.");
            if (disposeProgram)
            {
                held = allowance.Used; regex.Dispose(); Assert.Equal(held, allowance.Used);
            }
            else
            {
                for (var i = 0; i < 64; i++)
                {
                    var prefix = i.ToString("D4");
                    Assert.Equal(prefix + "a", regex.SubnToString("a"u8, prefix + @"\g<x>").ResultText);
                }
                held = allowance.Used;
            }
        }
        finally { release.Set(); Assert.True(worker.Join(TimeSpan.FromSeconds(30)), "Active substitution did not finish."); }
        Assert.Null(error); Assert.Equal("a!", output);
        if (disposeProgram) Assert.Equal(0UL, allowance.Used);
        else Assert.True(allowance.Used < held, "The detached active plan was not refunded at completion.");
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void AWaitingCallerDoesNotInheritForeignCancellationOrUnfundedRetry()
    {
        var allowance = new Allowance(4 << 20); using var regex = Compile(allowance); var program = allowance.Used;
        var cache = Cache(regex);
        using var ownerToken = new CancellationTokenSource(); using var followerToken = new CancellationTokenSource();
        using var paused = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        Exception? ownerError = null, followerError = null; string? output = null; var stopped = false;
        var ownerId = 0;
        allowance.AfterGrant = () =>
        {
            if (Environment.CurrentManagedThreadId != ownerId || stopped || FirstStatus(cache) != 0) return;
            stopped = true; paused.Set();
            if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Pending preparation was not released.");
        };
        var owner = new Thread(() =>
        {
            ownerId = Environment.CurrentManagedThreadId;
            try { regex.SubnToString("a"u8, @"\g<x>!", 0, 0, ownerToken.Token); }
            catch (Exception failure) { ownerError = failure; }
        }) { IsBackground = true };
        var follower = new Thread(() =>
        {
            try { output = regex.SubnToString("a"u8, @"\g<x>!", 0, 0, followerToken.Token).ResultText; }
            catch (Exception failure) { followerError = failure; }
        }) { IsBackground = true };
        var followerStarted = false; owner.Start();
        try
        {
            Assert.True(paused.Wait(TimeSpan.FromSeconds(30)), "Owner did not publish pending preparation.");
            follower.Start(); followerStarted = true;
            Assert.True(SpinWait.SpinUntil(() => (follower.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(30)), "Follower did not wait for pending preparation.");
            ownerToken.Cancel();
        }
        finally
        {
            release.Set(); Assert.True(owner.Join(TimeSpan.FromSeconds(30)), "Owner did not finish.");
            if (followerStarted) Assert.True(follower.Join(TimeSpan.FromSeconds(30)), "Follower did not finish.");
            allowance.AfterGrant = null;
        }
        Assert.Equal(ownerToken.Token, Assert.IsType<OperationCanceledException>(ownerError).CancellationToken);
        Assert.Null(followerError); Assert.Equal("a!", output);
        Assert.Equal(program, allowance.Used); // Failed owner removed; independent retry is uncached and refunded.
        Assert.Equal("a!", regex.SubnToString("a"u8, @"\g<x>!").ResultText);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static object Cache(Utf8PythonRegex regex) => typeof(Utf8PythonRegex).GetField("_replacementPlanCache", Fields)!.GetValue(regex)!;
    private static int FirstStatus(object cache)
    {
        var entry = cache.GetType().GetField("_first", Fields)!.GetValue(cache);
        return entry is null ? -1 : (int)entry.GetType().GetField("Status", Fields)!.GetValue(entry)!;
    }
}
