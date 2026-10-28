using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PrefixCountPublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicPrefixCountNeverPublishesAfterDiscoveryCancellation(bool priorMatch)
    {
        var regex = new Utf8Regex("(ab|cd)+", RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout, new() { MaxWorkSteps = 50000000 });
        Assert.True(regex.Inspection.SearchPlan.CanGuideFallbackStarts);
        Assert.Equal(0, regex.Inspection.SearchPlan.Distance);
        var observed = 0;
        for (var blocks = 0; blocks <= 128; blocks++)
        {
            // Mixed input selects the scalar counter rather than the qualified
            // ASCII structural counter, which already retains its final check.
            var input = Encoding.UTF8.GetBytes("é" + (priorMatch ? "ab " : string.Empty)
                + new string('x', blocks * 1024));
            using var token = new CancellationTokenSource();
            var injected = false;
            var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
            {
                injected = true;
                token.Cancel();
            });
            int? published = null;
            OperationCanceledException? canceled = null;
            try { published = regex.Count(input, token.Token); }
            catch (OperationCanceledException error) { canceled = error; }
            finally { diagnostics.Complete(); }
            if (!injected) continue;
            observed++;
            Assert.False(published.HasValue, $"Published {published} after cancellation; prior={priorMatch}, bytes={input.Length}.");
            Assert.NotNull(canceled);
            Assert.Equal(token.Token, canceled.CancellationToken);
        }
        Assert.True(observed > 0);
    }

    [Theory]
    [InlineData(false, false, "(ab|cd)+")] [InlineData(false, true, "(ab|cd)+")]
    [InlineData(true, false, "(ab|cd)+")] [InlineData(true, true, "(ab|cd)+")]
    [InlineData(false, false, "#[xy]+")] [InlineData(false, true, "#[xy]+")]
    [InlineData(true, false, "#[xy]+")] [InlineData(true, true, "#[xy]+")]
    public void ExhaustedPrefixChecksOriginalAuthorityAtTheFinalAdmittedStep(bool priorMatch, bool expire, string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var plan = regex.Inspection.SearchPlan;
        var input = Encoding.UTF8.GetBytes(priorMatch ? (pattern[0] == '#' ? "#x xxxx" : "ab xxxx") : "xxxx");
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var limits = new Utf8ExecutionLimits(ulong.MaxValue, 0, 0);
        var measured = new Utf8SliceInvocation(limits, Regex.InfiniteMatchTimeout, default);
        var baseline = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref measured, plan);
        Assert.Equal(priorMatch ? 1 : 0, baseline.Count);
        Assert.InRange(measured.Budget.Steps, 1UL, 63UL);

        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(limits,
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, token.Token);
        // A borrowing caller can already have spent validation/projection work.
        // Align that cumulative owner so the final search step reaches the hook.
        var precedingWork = 64 - measured.Budget.Steps;
        for (ulong i = 0; i < precedingWork; i++)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        Assert.True(invocation.Ledger.TryReserve(13));
        var injected = false;
        invocation.CheckpointHook = _ => { injected = true; if (expire) ticks = 50; else token.Cancel(); };
        Utf8SliceCountOutcome? published = null;
        Exception? failure = null;
        try { published = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation, plan); }
        catch (Exception error) { failure = error; }
        Assert.True(injected);
        Assert.False(published.HasValue, "Published a count after cancellation at the final prefix-search step.");
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
