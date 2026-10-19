using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeCountPublicationTests
{
    [Theory]
    [InlineData("", 0, false)] [InlineData("", 0, true)]
    [InlineData("miss", 0, false)] [InlineData("miss", 0, true)]
    [InlineData("needle needle", 2, false)] [InlineData("needle needle", 2, true)]
    public void FinalCounterCheckpointCannotPublishAfterInterruption(string text, int expected, bool expire)
    {
        var plan = new Utf8Regex("needle").Inspection.SearchPlan;
        var input = Encoding.UTF8.GetBytes(text);
        var limits = new Utf8ExecutionLimits(10000, 0, 1000);
        var measured = new Utf8SliceInvocation(limits, Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None,
            Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(plan, input, false, ref measured, out var count));
        Assert.Equal(expected, count);
        Assert.InRange(measured.Budget.Steps, 1UL, 63UL);

        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(limits, TimeSpan.FromMilliseconds(20), token.Token);
        for (ulong i = 0; i < 64 - measured.Budget.Steps; i++)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        Assert.True(invocation.Ledger.TryReserve(13));
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else token.Cancel(); };
        Utf8ExecutionLimitKind? returned = null;
        Exception? failure = null;
        var published = -1;
        try { returned = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(plan, input, false, ref invocation, out published); }
        catch (Exception error) { failure = error; }
        Assert.True(entered);
        Assert.Null(returned);
        Assert.Equal(0, published);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(64UL, invocation.Budget.Steps);
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { "a", "(a)", "😀" })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        foreach (var finite in new[] { false, true })
        foreach (var expire in new[] { false, true })
            yield return [pattern, mode, finite, expire];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicCountKeepsOriginalInterruptionThroughFinalPublication(
        string pattern, RegexOptions mode, bool finite, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var policy = finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1,
        } : Utf8ResourcePolicy.Unlimited;
        var regex = new Utf8Regex(pattern, mode | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20), policy);
        byte[] input = [];
        var expected = 0;
        ulong terminal = 0;
        // Find an actual production checkpoint boundary, without pinning the
        // current version's numerical work requirement.
        for (var count = 1; count <= 128; count++)
        {
            input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(pattern == "😀" ? "😀" : "a", count)));
            var measured = new Utf8SliceInvocation(new(100000, 1, 1, 1), regex.MatchTimeout, default);
            Assert.Equal(count, regex.CountForResult(input, 0, ref measured));
            terminal = measured.Budget.Steps;
            if ((terminal & 63) == 0) { expected = count; break; }
        }
        Assert.True(expected > 0, "No bounded native terminal checkpoint witness found.");
        var entered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps != terminal) return;
            entered = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        int? published = null;
        try
        {
            var failure = Record.Exception(() => published = regex.Count(input, token.Token));
            Assert.True(entered);
            Assert.Null(published);
            if (expire)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(pattern, error.Pattern);
                Assert.Equal(regex.MatchTimeout, error.MatchTimeout);
                if (finite) Assert.Empty(error.Input);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { diagnostics.Complete(); }
        Assert.Equal(expected, regex.Count(input));
    }
}
