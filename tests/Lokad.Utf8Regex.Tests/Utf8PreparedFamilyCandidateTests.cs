using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparedFamilyCandidateTests
{
    private static Utf8SearchPlan Plan(byte[][] literals) => Utf8SearchPlan.Prepare(
        Utf8SearchFacts.Create(Utf8SearchKind.ExactUtf8Literals, new Utf8SearchFactData {
            AlternateLiteralsUtf8 = literals, TrailingLiteralUtf8 = "!"u8.ToArray()
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
                var end = Math.Max(start, input.Length - trim); var expected = -1; var ordinal = -1;
                for (var id = 0; id < literals.Length; id++)
                {
                    var relative = input[start..end].IndexOf(literals[id]);
                    if (relative >= 0 && (expected < 0 || start + relative < expected))
                    { expected = start + relative; ordinal = id; }
                }
                var invocation = new Utf8SliceInvocation(new(1000000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
                Assert.True(invocation.Ledger.TryReserve(7));
                Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
                    plan, input, start, end, ref invocation, out var match, out var found));
                Assert.Equal(expected >= 0, found);
                if (found) Assert.Equal((expected, literals[ordinal].Length, ordinal), (match.Index, match.Length, match.LiteralId));
                else Assert.Equal(default, match);
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
            plan, input, 0, input.Length, ref invocation, out _, out var found));
        Assert.Equal(hit, found);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        for (ulong cap = 1; cap < invocation.Budget.Steps; cap++)
        {
            var limited = new Utf8SliceInvocation(new(cap, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
            Assert.True(limited.Ledger.TryReserve(7));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(
                    plan, input, 0, input.Length, ref limited, out var match, out found));
                Assert.False(found); Assert.Equal(default, match);
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
        Exception? failure = null; var match = default(PreparedSearchMatch); var found = false;
        try { _ = Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(plan, input, 0, input.Length,
            ref invocation, out match, out found); }
        catch (Exception error) { failure = error; }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.False(found); Assert.Equal(default, match); Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }
}
