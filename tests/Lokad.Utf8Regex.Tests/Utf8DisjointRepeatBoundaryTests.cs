using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DisjointRepeatBoundaryTests
{
    [Theory]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    public void RequiredAssertionEndCannotInventAnEarlierLiteralBoundary(bool lazy, bool success, bool captureTail)
    {
        var mask = new byte[16];
        for (var scalar = 'a'; scalar <= 'z'; scalar++) mask[scalar >> 3] |= (byte)(1 << (scalar & 7));
        var body = new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.CaptureNode(0, new Utf8SliceNode.RepeatNode(
                new Utf8SliceNode.SetNode(mask, false, [], false), 1, long.MaxValue, lazy)),
            captureTail ? new Utf8SliceNode.CaptureNode(1, new Utf8SliceNode.LiteralNode('-', false))
                : new Utf8SliceNode.LiteralNode('-', false)]);
        var behind = new Utf8SliceNode.LookbehindNode(body, true) { ForwardScalarCount = 3 };
        var root = new Utf8SliceNode.ConcatNode([behind, new Utf8SliceNode.LiteralNode(success ? 'x' : '-', false)]);
        var program = Utf8SliceLowerer.Lower(root, null, false, false, false)!;
        var invocation = new Utf8SliceInvocation(new(10000, 32, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var outcome = Utf8SliceRunner.TryMatchAt(program, success ? "ab-x"u8 : "abc-x"u8,
            3, starts, ends, true, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        Assert.Equal(success, outcome.IsMatch);
        if (success)
        {
            Assert.Equal((0, 2), (starts[0], ends[0]));
            if (captureTail) Assert.Equal((2, 3), (starts[1], ends[1]));
        }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Theory]
    [InlineData(32, false)] [InlineData(33, false)]
    [InlineData(31, true)] [InlineData(32, true)] [InlineData(33, true)]
    public void CaptureMarkerProofBoundKeepsEveryCapture(int groups, bool captureTail)
    {
        var pattern = captureTail ? "([a-z]+)" + new string('(', groups) + "-" + new string(')', groups)
            : new string('(', groups) + "[a-z]+" + new string(')', groups) + "-";
        var regex = new Utf8Regex(pattern);
        var match = regex.MatchDetailed("é😀abc-"u8);
        Assert.True(match.Success);
        Assert.Equal("abc-", match.GetValueString());
        for (var group = 1; group <= (captureTail ? groups + 1 : groups); group++)
        {
            Assert.Equal(captureTail && group > 1 ? "-" : "abc", match.GetGroup(group).GetValueString());
            Assert.Equal(1, match.GetGroup(group).CaptureCount);
        }
    }
}
