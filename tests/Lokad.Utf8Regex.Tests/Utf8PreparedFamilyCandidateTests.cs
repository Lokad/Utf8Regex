using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparedFamilyCandidateTests
{
    private static Utf8SearchPlan Plan(byte[][] literals, bool withSuffix = true) => Utf8SearchPlan.Prepare(
        Utf8SearchFacts.Create(Utf8SearchKind.ExactUtf8Literals, new Utf8SearchFactData {
            AlternateLiteralsUtf8 = literals, TrailingLiteralUtf8 = withSuffix ? "!"u8.ToArray() : []
        }));

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void PreparedCandidatesKeepBoundsOrderAndDoNotApplyCoreSuffixPredicates(int size)
    {
        foreach (var pair in new[] { new[] { "target", "targeted" }, ["targeted", "target"],
            ["😀é", "😀"], ["éx", "中"], ["L" + new string('a', 1024) + "z", "a"] })
        foreach (var padding in new[] { 0, 2042, 2043, 2047, 2048, 4095 })
        {
            var literals = pair.Concat(Enumerable.Range(2, size - 2).Select(i => "missing" + i))
                .Select(Encoding.UTF8.GetBytes).ToArray();
            var plan = Plan(literals);
            Assert.NotNull(plan.AlternateLiteralSearch);
            var bytes = Encoding.UTF8.GetBytes(new string('x', padding) + pair[0] + "?" + pair[1]);
            var storage = new byte[bytes.Length + 8]; bytes.CopyTo(storage, 3);
            var input = storage.AsSpan(3, bytes.Length);
            foreach (var start in new[] { 0, padding, input.Length })
            foreach (var trim in new[] { 0, 1, Math.Min(input.Length - start, literals[0].Length) })
            {
                var end = Math.Max(start, input.Length - trim); var expected = -1;
                for (var id = 0; id < literals.Length; id++)
                {
                    var relative = input[start..end].IndexOf(literals[id]);
                    if (relative >= 0 && (expected < 0 || start + relative < expected))
                    { expected = start + relative; }
                }
                var invocation = new Utf8SliceInvocation(new(1000000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
                Assert.True(invocation.Ledger.TryReserve(7));
                Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
                    plan, input, start, end, ref invocation, out var candidate));
                Assert.Equal(expected, candidate);
                Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            }
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ConfirmedAndFalsePrefixesKeepEveryWorkCutoffAndCallerLoan(bool hit)
    {
        var literals = Enumerable.Range(0, 10).Select(i => Encoding.UTF8.GetBytes("item-" + i)).ToArray();
        var plan = Plan(literals);
        var input = Encoding.UTF8.GetBytes(new string('x', 5000) + "item-x " + (hit ? "item-12" : "item-?"));
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
            plan, input, 0, input.Length, ref invocation, out var candidate));
        Assert.Equal(hit, candidate >= 0);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        for (ulong cap = 1; cap < invocation.Budget.Steps; cap++)
        {
            var limited = new Utf8SliceInvocation(new(cap, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
            Assert.True(limited.Ledger.TryReserve(7));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
                    plan, input, 0, input.Length, ref limited, out candidate));
                Assert.Equal(-1, candidate);
                Assert.Equal(7UL, limited.Ledger.ReservedBytes);
            }
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void PreparedWindowMissKeepsOriginalInterruption(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start(); using var cancellation = new CancellationTokenSource();
        var plan = Plan(Enumerable.Range(0, 10).Select(i => Encoding.UTF8.GetBytes("item-" + i)).ToArray());
        var input = Encoding.UTF8.GetBytes(new string('x', 320000));
        var invocation = new Utf8SliceInvocation(new(1000000, 1, 7, 1),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, cancellation.Token);
        Assert.True(invocation.Ledger.TryReserve(7)); var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) clock.Advance(250); else cancellation.Cancel(); };
        Exception? failure = null; var candidate = -1;
        try { _ = Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(plan, input, 0, input.Length,
            ref invocation, out candidate); }
        catch (Exception error) { failure = error; }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(-1, candidate); Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("item-", 0)] [InlineData("item-", 4097)]
    [InlineData("😀префикс-", 0)] [InlineData("😀префикс-", 4097)]
    public void UniqueColumnStillRequiresEveryPrefixAndTailByte(string prefix, int tailLength)
    {
        var tail = new string('q', tailLength);
        var literals = Enumerable.Range(0, 10).Select(i => Encoding.UTF8.GetBytes(prefix + i + tail)).ToArray();
        var plan = Plan(literals, false);
        Assert.True(plan.AlternateLiteralSearch!.Value.SearchData.Buckets[0].PrefixDiscriminator.HasValue);
        foreach (var padding in new[] { 0, 2043, 2047, 2048, 4095 })
        {
            var wrong = prefix + "3" + (tailLength == 0 ? "" : new string('x', tailLength));
            if (tailLength == 0) wrong = prefix[..^1] + "!3";
            var text = new string('x', padding) + wrong + "?" + prefix + "?" + tail + "?" + prefix + "7" + tail;
            var bytes = Encoding.UTF8.GetBytes(text);
            var storage = new byte[bytes.Length + 9]; bytes.CopyTo(storage, 3);
            var input = storage.AsSpan(3, bytes.Length);
            foreach (var start in new[] { 0, padding, input.Length })
            foreach (var trim in new[] { 0, 1, Math.Min(input.Length - start, literals[0].Length) })
            {
                var end = Math.Max(start, input.Length - trim); var expected = -1;
                foreach (var literal in literals)
                {
                    var relative = input[start..end].IndexOf(literal);
                    if (relative >= 0 && (expected < 0 || start + relative < expected)) expected = start + relative;
                }
                var invocation = new Utf8SliceInvocation(new(1000000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
                Assert.True(invocation.Ledger.TryReserve(7));
                Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
                    plan, input, start, end, ref invocation, out var candidate));
                Assert.Equal(expected, candidate); Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SelectedLongNeedleKeepsOriginalInterruptionDuringVerification(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start(); using var cancellation = new CancellationTokenSource();
        var tail = new string('q', 5000);
        var literals = Enumerable.Range(0, 10).Select(i => Encoding.UTF8.GetBytes("item-" + i + tail)).ToArray();
        var plan = Plan(literals, false); var input = literals[7];
        var invocation = new Utf8SliceInvocation(new(1000000, 1, 7, 1),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, cancellation.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        // The window search and first comparison chunk precede checkpoint64.
        for (var step = 0; step < 61; step++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeStep());
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) clock.Advance(250); else cancellation.Cancel(); };
        Exception? failure = null; var candidate = -1;
        try { _ = Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(plan, input, 0, input.Length, ref invocation, out candidate); }
        catch (Exception error) { failure = error; }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(-1, candidate); Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CollidingColumnsKeepAllBranchesAndDifferentWidths(bool shortFirst)
    {
        var pair = shortFirst ? new[] { "item-a", "item-ab" } : new[] { "item-ab", "item-a" };
        var literals = pair.Concat(Enumerable.Range(0, 8).Select(i => "item-" + i)).Select(Encoding.UTF8.GetBytes).ToArray();
        var plan = Plan(literals);
        Assert.False(plan.AlternateLiteralSearch!.Value.SearchData.Buckets[0].PrefixDiscriminator.HasValue);
        var input = "item-x item-ab"u8;
        var invocation = new Utf8SliceInvocation(new(1000000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
            plan, input, 0, input.Length, ref invocation, out var candidate));
        Assert.Equal(7, candidate); Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }
}
