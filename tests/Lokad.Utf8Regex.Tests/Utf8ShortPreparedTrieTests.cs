using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ShortPreparedTrieTests
{
    private static readonly byte[][] Prefixes = (from s in new[] { "S", "s", "ſ" }
        from h in new[] { "H", "h" } from e in new[] { "E", "e" }
        select Encoding.UTF8.GetBytes(s + h + e)).ToArray();

    private static Utf8SearchPlan Plan() => Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
        Utf8SearchKind.ExactUtf8Literals, new Utf8SearchFactData {
            AlternateLiteralsUtf8 = Prefixes, TrailingLiteralUtf8 = "!"u8.ToArray()
        }));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(20)] [InlineData(255)] [InlineData(256)] [InlineData(257)] [InlineData(4097)]
    public void EarliestPrefixKeepsBorrowedOffsetsAndPartialTails(int length)
    {
        var plan = Plan();
        Assert.Equal(PreparedLiteralSetStrategy.MultiLiteralTrie, plan.AlternateLiteralSearch!.Value.Strategy);
        var storage = Enumerable.Repeat((byte)'x', length + 8).ToArray();
        var content = "sxx SHE? ſhe she"u8;
        var copy = Math.Min(length, content.Length);
        content[..copy].CopyTo(storage.AsSpan(3 + length - copy));
        var input = storage.AsSpan(3, length);
        foreach (var start in new[] { 0, length / 2, length })
        foreach (var end in new[] { length, Math.Max(start, length - 1), start })
        {
            var expected = -1;
            foreach (var literal in Prefixes)
            {
                var relative = input[start..end].IndexOf(literal);
                if (relative >= 0 && (expected < 0 || start + relative < expected)) expected = start + relative;
            }
            var invocation = new Utf8SliceInvocation(new(1_000_000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
                plan, input, start, end, ref invocation, out var candidate));
            Assert.Equal(expected, candidate);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void EveryWorkCutoffDeniesPublicationAndKeepsCallerLoan(bool hit)
    {
        var plan = Plan(); var input = hit ? "sxx SHE?"u8 : "prefix watson suffix"u8;
        var funded = new Utf8SliceInvocation(new(1_000_000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
            plan, input, 0, input.Length, ref funded, out var found));
        Assert.Equal(hit, found >= 0);
        for (ulong cap = 1; cap < funded.Budget.Steps; cap++)
        {
            var limited = new Utf8SliceInvocation(new(cap, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
            Assert.True(limited.Ledger.TryReserve(7));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
                    plan, input, 0, input.Length, ref limited, out var candidate));
                Assert.Equal(-1, candidate);
                Assert.Equal(7UL, limited.Ledger.ReservedBytes);
            }
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ShortLookupObservesOriginalInterruptionBeforePublishing(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start(); using var cancellation = new CancellationTokenSource();
        var plan = Plan(); var invocation = new Utf8SliceInvocation(new(1_000_000, 1, 7, 1),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, cancellation.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        for (var step = 0; step < 63; step++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) clock.Advance(250); else cancellation.Cancel(); };
        Exception? failure = null; var candidate = -1;
        try { _ = Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(plan, "sxx SHE?"u8, 0, 8,
            ref invocation, out candidate); }
        catch (Exception error) { failure = error; }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(-1, candidate); Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }
}
