using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// The exact-zero warm lane requires a quiet shared pool. Parallel fixtures can
// borrow its working leaves; the held byte interval and threshold stay intact.
[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ReversibleMatchStateTests
{
    [Fact]
    public void CaptureJournalRollsBackOnlyMutationsAfterCheckpoint()
    {
        var captures = new Utf8CaptureSlots(3);
        captures.BeginInvocation();
        try
        {
            captures.Set(1, 2, 3);
            var outer = captures.CreateCheckpoint();
            captures.Set(1, 5, 7);
            captures.Set(2, 11, 13);
            var inner = captures.CreateCheckpoint();
            captures.Set(2, 17, 19);

            captures.Rollback(inner);
            Assert.True(captures.TryGet(2, out var innerStart, out var innerLength));
            Assert.Equal(11, innerStart);
            Assert.Equal(13, innerLength);

            captures.Rollback(outer);
            Assert.True(captures.TryGet(1, out var outerStart, out var outerLength));
            Assert.Equal(2, outerStart);
            Assert.Equal(3, outerLength);
            Assert.False(captures.TryGet(2, out _, out _));
        }
        finally
        {
            captures.EndInvocation();
        }
    }

    [Fact]
    public void HugeSyntacticRepeatUsesReachableSubjectBound()
    {
        var prepared = Utf8FrontEnd.Compile("((?:a|b){0,2147483647})z", RegexOptions.CultureInvariant);
        var captures = new Utf8CaptureSlots(2);

        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix(
            "abz"u8,
            prepared.ExecutionProgram,
            0,
            captures,
            Utf8ExecutionDeadline.Infinite,
            out var matchedLength));
        Assert.Equal(3, matchedLength);
        Assert.True(captures.TryGet(1, out var captureStart, out var captureLength));
        Assert.Equal(0, captureStart);
        Assert.Equal(2, captureLength);
    }

    [Fact]
    public void FailedAlternativeRestoresCaptureSlots()
    {
        var prepared = Utf8FrontEnd.Compile("(?:(a)c|(b))d", RegexOptions.CultureInvariant);
        var captures = new Utf8CaptureSlots(3);

        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix(
            "bd"u8,
            prepared.ExecutionProgram,
            0,
            captures,
            Utf8ExecutionDeadline.Infinite,
            out var matchedLength));
        Assert.Equal(2, matchedLength);
        Assert.False(captures.TryGet(1, out _, out _));
        Assert.True(captures.TryGet(2, out var captureStart, out var captureLength));
        Assert.Equal(0, captureStart);
        Assert.Equal(1, captureLength);
    }

    [Fact]
    public void TimeoutReturnsJournalAndAllowsCaptureStateReuse()
    {
        var prepared = Utf8FrontEnd.Compile("((?:a|b)*)z", RegexOptions.CultureInvariant);
        var captures = new Utf8CaptureSlots(2);
        var longInput = new byte[64 * 1024];
        Array.Fill(longInput, (byte)'a');

        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8ExecutionInterpreter.TryMatchPrefix(
                longInput,
                prepared.ExecutionProgram,
                0,
                captures,
                Utf8ExecutionDeadline.Start(TimeSpan.FromTicks(1)),
                out _));

        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix(
            "abz"u8,
            prepared.ExecutionProgram,
            0,
            captures,
            Utf8ExecutionDeadline.Infinite,
            out var matchedLength));
        Assert.Equal(3, matchedLength);
    }

    [Fact]
    public void WarmCaptureBacktrackingHasNoTemporaryAllocation()
    {
        var prepared = Utf8FrontEnd.Compile("(?:(a)c|(b))d", RegexOptions.CultureInvariant);
        var captures = new Utf8CaptureSlots(3);

        for (var i = 0; i < 16; i++)
        {
            Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix(
                "bd"u8,
                prepared.ExecutionProgram,
                0,
                captures,
                Utf8ExecutionDeadline.Infinite,
                out _));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++)
        {
            Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix(
                "bd"u8,
                prepared.ExecutionProgram,
                0,
                captures,
                Utf8ExecutionDeadline.Infinite,
                out _));
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
