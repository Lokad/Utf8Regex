using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PairCandidateInvocationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(2042)]
    [InlineData(2043)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4094)]
    [InlineData(4095)]
    public void RawPairsKeepOffsetsTailsEarliestStartsAndBranchOrder(int padding)
    {
        foreach (var pair in new[] { new[] { "target", "needle" }, ["target", "targeted"],
            ["targeted", "target"], ["a", "ab"], ["😀é", "😀"], ["éx", "中"],
            [new string('a', 1023) + "z", new string('b', 1023) + "y"],
            [new string('a', 1024) + "z", "b"],
            [new string('a', 4097) + "z", "b"] })
        {
            var literals = pair.Select(Encoding.UTF8.GetBytes).ToArray();
            var text = new string('x', padding) + pair[0][..^1] + "?" + pair[1] + " " + pair[0];
            var bytes = Encoding.UTF8.GetBytes(text);
            var storage = new byte[bytes.Length + 8];
            bytes.CopyTo(storage, 3);
            var input = storage.AsSpan(3, bytes.Length);
            foreach (var start in new[] { 0, padding, Math.Min(input.Length, padding + 1), input.Length })
            foreach (var trim in new[] { 0, 1, Math.Min(input.Length - start, literals[0].Length) })
            {
                var end = Math.Max(start, input.Length - trim);
                var expected = -1;
                var ordinal = -1;
                for (var id = 0; id < literals.Length; id++)
                {
                    var relative = input[start..end].IndexOf(literals[id]);
                    if (relative >= 0 && (expected < 0 || start + relative < expected))
                    { expected = start + relative; ordinal = id; }
                }
                var invocation = new Utf8SliceInvocation(new(1000000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
                Assert.True(invocation.Ledger.TryReserve(7));
                Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteralFamilyCandidate(
                    literals, false, input, start, end, false, ref invocation, out var match, out var found));
                Assert.Equal(expected >= 0, found);
                if (found)
                {
                    Assert.Equal(expected, match.Index);
                    Assert.Equal(ordinal, match.LiteralId);
                    Assert.Equal(literals[ordinal].Length, match.Length);
                }
                else Assert.Equal(default, match);
                Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            }
        }
    }

    [Fact]
    public void LookaheadCannotPublishAShortNeedleBeforeAnEarlierCrossingLongNeedle()
    {
        var longNeedle = Encoding.ASCII.GetBytes("L" + new string('a', 1022) + "z");
        byte[] shortNeedle = [(byte)'a'];
        byte[] input = [.. Encoding.ASCII.GetBytes(new string('x', 1025)), .. longNeedle];
        foreach (var longFirst in new[] { false, true })
        {
            byte[][] literals = longFirst ? [longNeedle, shortNeedle] : [shortNeedle, longNeedle];
            var invocation = new Utf8SliceInvocation(new(1000000, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteralFamilyCandidate(
                literals, false, input, 0, input.Length, false, ref invocation, out var match, out var found));
            Assert.True(found);
            Assert.Equal((1025, 1024, longFirst ? 0 : 1), (match.Index, match.Length, match.LiteralId));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryWorkCutoffKeepsStickyDenialUnpublishedMatchAndCallerLoan(bool hit)
    {
        byte[][] literals = ["abcd"u8.ToArray(), "bc"u8.ToArray()];
        var input = Encoding.UTF8.GetBytes(new string('x', 5000) + "abxx " + (hit ? "abcd" : "abx"));
        var funded = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteralFamilyCandidate(
            literals, false, input, 0, input.Length, false, ref funded, out _, out var expected));
        Assert.Equal(hit, expected);
        for (ulong cap = 1; cap < funded.Budget.Steps; cap++)
        {
            var limited = new Utf8SliceInvocation(new(cap, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
            Assert.True(limited.Ledger.TryReserve(7));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SearchExecutor.TryFindLiteralFamilyCandidate(
                    literals, false, input, 0, input.Length, false, ref limited, out var match, out var found));
                Assert.False(found);
                Assert.Equal(default, match);
                Assert.Equal(7UL, limited.Ledger.ReservedBytes);
            }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ScanAndFullComparisonKeepOriginalInterruption(bool compare, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        byte[][] literals = compare
            ? [Encoding.UTF8.GetBytes(new string('a', 200000) + "z"), "b"u8.ToArray()]
            : ["needle"u8.ToArray(), "target"u8.ToArray()];
        var input = Encoding.UTF8.GetBytes(new string(compare ? 'a' : 'x', 320000));
        var invocation = new Utf8SliceInvocation(new(1000000, 1, 7, 1),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var entered = false;
        invocation.CheckpointHook = _ =>
        {
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        };
        Exception? observed = null;
        var match = default(Lokad.Utf8Regex.Internal.Search.PreparedSearchMatch);
        var found = false;
        try
        {
            _ = Utf8SearchExecutor.TryFindLiteralFamilyCandidate(literals, false, input, 0, input.Length,
                false, ref invocation, out match, out found);
        }
        catch (Exception error) { observed = error; }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(observed);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.False(found);
        Assert.Equal(default, match);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }
}
