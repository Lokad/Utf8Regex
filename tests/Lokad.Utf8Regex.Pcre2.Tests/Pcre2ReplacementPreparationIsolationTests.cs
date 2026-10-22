using System.Buffers;
using System.Reflection;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementPreparationIsolationTests
{
    public static IEnumerable<object[]> CancellationRoutes()
    {
        foreach (var route in new[] { "Bytes", "Text", "Utf8Bytes", "Destination" })
        foreach (var live in new[] { false, true }) yield return [route, live];
    }

    [Theory] [MemberData(nameof(CancellationRoutes))]
    public void ForeignCancellationDoesNotCancelAWaitingReplacement(string route, bool live)
    {
        var result = RunContended(route, "CancelOwner", live);
        Assert.Equal(result.OwnerToken, Assert.IsType<OperationCanceledException>(result.OwnerFailure).CancellationToken);
        Assert.Null(result.FollowerFailure);
        Assert.Equal(new string('a', 128), result.FollowerOutput);
        Assert.Equal(1, result.CacheCountAfterRetry);
    }

    [Theory] [InlineData("TimeoutOwner")] [InlineData("WorkOwner")]
    public void ForeignInvocationFailureUsesTheWaitersOwnAllowance(string failure)
    {
        var result = RunContended("Bytes", failure, true);
        Assert.Equal(failure == "TimeoutOwner" ? Pcre2ErrorKind.Timeout : Pcre2ErrorKind.MatchLimit,
            Assert.IsType<Pcre2MatchException>(result.OwnerFailure).ErrorKind);
        Assert.Null(result.FollowerFailure);
        Assert.Equal(new string('a', 128), result.FollowerOutput);
    }

    [Theory] [InlineData("CancelFollower")] [InlineData("TimeoutFollower")]
    public void AWaiterStillObservesItsOwnInterruption(string failure)
    {
        var result = RunContended("Bytes", failure, true);
        Assert.Null(result.OwnerFailure);
        if (failure == "CancelFollower")
            Assert.Equal(result.FollowerToken, Assert.IsType<OperationCanceledException>(result.FollowerFailure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(result.FollowerFailure).ErrorKind);
        Assert.Equal(1, result.CacheCountAfterRetry);
    }

    [Fact]
    public void SuccessfulPreparationsRemainShared()
    {
        var result = RunContended("Text", "Success", true);
        Assert.Null(result.OwnerFailure);
        Assert.Null(result.FollowerFailure);
        Assert.Equal(new string('a', 128), result.FollowerOutput);
        Assert.Equal(1, result.CacheCountAfterRetry);
    }

    [Fact]
    public void GenuineTemplateErrorsRemainErrors()
    {
        var regex = new Utf8Pcre2Regex("(a)");
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Equal(Pcre2ErrorKind.UnknownReplacementGroup,
                Assert.Throws<Pcre2SubstitutionException>(() => regex.Replace("a"u8, "${missing}")).ErrorKind);
        Assert.Equal("a", Encoding.UTF8.GetString(regex.Replace("a"u8, "$1")));
    }

    private static (Exception? OwnerFailure, Exception? FollowerFailure, string? FollowerOutput,
        CancellationToken OwnerToken, CancellationToken FollowerToken, int CacheCountAfterRetry)
        RunContended(string route, string failure, bool live)
    {
        var finiteDeadline = failure is "TimeoutOwner" or "TimeoutFollower";
        var regex = new Utf8Pcre2Regex("(a)", default, default,
            new() { MatchLimit = failure == "WorkOwner" ? 100_000u : 0 },
            finiteDeadline ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan);
        var template = string.Concat(Enumerable.Repeat("$1", 128));
        var input = "a"u8.ToArray();
        using var ownerCancellation = new CancellationTokenSource();
        using var followerCancellation = new CancellationTokenSource();
        using var paused = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Exception? ownerFailure = null, followerFailure = null;
        string? followerOutput = null;
        long ownerTicks = 0, followerTicks = 0;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var component = typeof(Utf8Pcre2Regex).GetField("_replacementComponent", fields)!.GetValue(regex)!;
        var cache = component.GetType().GetField("_plans", fields)!.GetValue(component)!;
        var first = cache.GetType().GetField("_first", fields)!;
        var stopped = false;
        var owner = new Thread(() =>
        {
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => Volatile.Read(ref ownerTicks));
            using var construction = Utf8ConstructionScope.Enter();
            construction.Budget.CheckpointHook = _ =>
            {
                var entry = first.GetValue(cache);
                if (stopped || entry is null || (int)entry.GetType().GetField("Status", fields)!.GetValue(entry)! != 0 ||
                    (int)entry.GetType().GetField("OwnerThread", fields)!.GetValue(entry)! != Environment.CurrentManagedThreadId) return;
                stopped = true;
                paused.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Owner release timed out.");
                // Spend this owner's actual invocation allowance. No exception
                // is injected and the waiter retains a separate, ample budget.
                if (failure == "WorkOwner")
                    for (var step = 0; step < 100_001; step++) Pcre2ReplacementPreparationControl.Check();
            };
            try { _ = Run(regex, input, template, route, ownerCancellation.Token); }
            catch (Exception error) { ownerFailure = error; }
            finally { construction.Budget.CheckpointHook = null; }
        }) { IsBackground = true };
        var follower = new Thread(() =>
        {
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => Volatile.Read(ref followerTicks));
            try { followerOutput = Run(regex, input, template, route, live ? followerCancellation.Token : default); }
            catch (Exception error) { followerFailure = error; }
        }) { IsBackground = true };
        owner.Start();
        var followerStarted = false;
        try
        {
            Assert.True(paused.Wait(TimeSpan.FromSeconds(10)), "Owner did not publish a pending preparation.");
            follower.Start(); followerStarted = true;
            Assert.True(SpinWait.SpinUntil(() => (follower.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(10)), "Follower did not wait behind the pending preparation.");
            switch (failure)
            {
                case "CancelOwner": ownerCancellation.Cancel(); break;
                case "TimeoutOwner": Volatile.Write(ref ownerTicks, 25); break;
                case "CancelFollower": followerCancellation.Cancel(); break;
                case "TimeoutFollower": Volatile.Write(ref followerTicks, 25); break;
            }
            if (failure is "CancelFollower" or "TimeoutFollower")
                Assert.True(follower.Join(TimeSpan.FromSeconds(10)), "The waiter ignored its own interruption.");
        }
        finally
        {
            release.Set();
            Assert.True(owner.Join(TimeSpan.FromSeconds(10)), "Owner did not complete.");
            if (followerStarted) Assert.True(follower.Join(TimeSpan.FromSeconds(10)), "Follower did not complete.");
        }
        using var retryClock = Utf8ExecutionDeadline.UseClockForDiagnostics(static () => 0);
        Assert.Equal(new string('a', 128), Run(regex, input, template, route, default));
        return (ownerFailure, followerFailure, followerOutput, ownerCancellation.Token,
            followerCancellation.Token, regex.DebugReplacementCacheEntryCount);
    }

    private static string Run(Utf8Pcre2Regex regex, byte[] input, string template, string route, CancellationToken token)
    {
        switch (route)
        {
            case "Bytes": return Encoding.UTF8.GetString(regex.Replace(input, template, 0, default, default, token));
            case "Text": return regex.ReplaceToString(input, template, 0, default, default, token);
            case "Utf8Bytes": return Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(template), 0, default, default, token));
            default:
                var destination = new byte[128];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination,
                    out var written, 0, default, default, token));
                return Encoding.UTF8.GetString(destination.AsSpan(0, written));
        }
    }
}
