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
        var match = regex.MatchDetailed("😀𐐨az"u8);
        Assert.True(match.Success);
        Assert.Equal((0, 10), (match.IndexInBytes, match.LengthInBytes));
        var x = match.GetGroup("x");
        Assert.Equal(2, x.CaptureCount);
        Assert.Equal((0, 4), (x.GetCapture(0).IndexInBytes, x.GetCapture(0).LengthInBytes));
        Assert.Equal((4, 4), (x.GetCapture(1).IndexInBytes, x.GetCapture(1).LengthInBytes));
        var y = match.GetGroup("y");
        Assert.Equal(1, y.CaptureCount);
        Assert.Equal((8, 1), (y.IndexInBytes, y.LengthInBytes));
    }

    [Fact]
    public void HugeSyntacticRepeatUsesReachableSubjectBound()
    {
        var regex = new Utf8Regex("((?:a|b){0,2147483647})z", RegexOptions.CultureInvariant);
        var match = regex.MatchDetailed("abz"u8);
        Assert.True(match.Success);
        Assert.Equal(3, match.LengthInBytes);
        Assert.Equal((0, 2), (match.GetGroup(1).IndexInBytes, match.GetGroup(1).LengthInBytes));
    }

    [Fact]
    public void FailedAlternativeRestoresScalarCaptures()
    {
        var regex = new Utf8Regex("(?:(a)c|(b))d", RegexOptions.CultureInvariant);
        var match = regex.MatchDetailed("bd"u8);
        Assert.True(match.Success);
        Assert.Equal(2, match.LengthInBytes);
        Assert.False(match.GetGroup(1).Success);
        Assert.Equal(0, match.GetGroup(1).CaptureCount);
        Assert.Equal((0, 1), (match.GetGroup(2).IndexInBytes, match.GetGroup(2).LengthInBytes));
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
