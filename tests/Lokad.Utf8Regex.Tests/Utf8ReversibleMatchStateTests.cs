using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ReversibleMatchStateTests
{
    [Fact]
    public void ScalarRollbackKeepsEarlierCapturesAndDiscardsFailedBranchHistory()
    {
        var regex = new Utf8Regex("(?<x>😀)(?:(?<x>𐐨)(?<y>a)q|(?<x>𐐨)(?<y>a))z", RegexOptions.CultureInvariant);
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor("😀𐐨az"u8.ToArray()));
        Assert.True(match.Success);
        Assert.Equal((0, 10), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
        Assert.True(match.TryGetGroup("x", out var x));
        Assert.Equal(2, match.GetCaptures(regex.GroupNumberFromName("x")).Length);
        Assert.Equal((0, 4), (match.GetCaptures(regex.GroupNumberFromName("x"))[0].StartOffsetInBytes, (match.GetCaptures(regex.GroupNumberFromName("x"))[0].EndOffsetInBytes - match.GetCaptures(regex.GroupNumberFromName("x"))[0].StartOffsetInBytes)));
        Assert.Equal((4, 4), (match.GetCaptures(regex.GroupNumberFromName("x"))[1].StartOffsetInBytes, (match.GetCaptures(regex.GroupNumberFromName("x"))[1].EndOffsetInBytes - match.GetCaptures(regex.GroupNumberFromName("x"))[1].StartOffsetInBytes)));
        Assert.True(match.TryGetGroup("y", out var y));
        Assert.Equal(1, match.GetCaptures(regex.GroupNumberFromName("y")).Length);
        Assert.Equal((8, 1), (y.StartOffsetInBytes, (y.EndOffsetInBytes - y.StartOffsetInBytes)));
    }

    [Fact]
    public void HugeSyntacticRepeatUsesReachableSubjectBound()
    {
        var regex = new Utf8Regex("((?:a|b){0,2147483647})z", RegexOptions.CultureInvariant);
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor("abz"u8.ToArray()));
        Assert.True(match.Success);
        Assert.Equal(3, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));
        Assert.Equal((0, 2), ((match.TryGetGroup(1, out var range1) ? range1.StartOffsetInBytes : default), ((match.TryGetGroup(1, out var range2) ? range2.EndOffsetInBytes : default) - (match.TryGetGroup(1, out var range3) ? range3.StartOffsetInBytes : default))));
    }

    [Fact]
    public void FailedAlternativeRestoresScalarCaptures()
    {
        var regex = new Utf8Regex("(?:(a)c|(b))d", RegexOptions.CultureInvariant);
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor("bd"u8.ToArray()));
        Assert.True(match.Success);
        Assert.Equal(2, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));
        Assert.False((match.TryGetGroup(1, out var range4) ? range4.Success : default));
        Assert.Equal(0, match.GetCaptures(1).Length);
        Assert.Equal((0, 1), ((match.TryGetGroup(2, out var range5) ? range5.StartOffsetInBytes : default), ((match.TryGetGroup(2, out var range6) ? range6.EndOffsetInBytes : default) - (match.TryGetGroup(2, out var range7) ? range7.StartOffsetInBytes : default))));
    }

    [Fact]
    public void TimeoutReturnsScalarWorkingStorageAndAllowsProgramReuse()
    {
        var program = Utf8ScalarCaptureWitness.Lower("((?:a|b)*)z", RegexOptions.CultureInvariant, 0, 0);
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var input = new byte[64 * 1024]; Array.Fill(input, (byte)'a');
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            TimeSpan.FromTicks(1), CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(7));
        ticks = 50;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, true, true, ref invocation));
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
        Utf8ScalarCaptureWitness.AssertAsciiAt(program, new Regex("((?:a|b)*)z"), "abz", 0);
    }

}
