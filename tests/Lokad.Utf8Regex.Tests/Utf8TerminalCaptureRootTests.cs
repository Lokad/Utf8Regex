using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TerminalCaptureRootTests
{
    [Theory]
    [InlineData("item-([0-9]+)")] [InlineData("([a-z]+)")]
    [InlineData("X([A-Z]+)")] [InlineData("item-([0-9]+?)")]
    [InlineData("item-([0-9]{1,3})")] [InlineData("item-([0-9]+)x")]
    [InlineData("item-(([0-9]+))")] [InlineData("(?:item-([0-9]+))+")]
    [InlineData("(?i:item-([a-z]+))")] [InlineData(@"item-(?=([0-9]+))\d+")]
    [InlineData(@"item-([\p{L}]+)")]
    public void LatestSlotsAgreeWithHistoryEnabledExecutionAtEveryScalarStart(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
        {
            var program = new Utf8Regex(pattern, options).Inspection.AvailableOwnedSliceProgram;
            foreach (var text in new[] { "", "item-", "item-12", "item-123x", "xitem-345!",
                "é😀item-12a", "XABC", "item-aé", "item-123item-45" })
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                for (var start = 0; start <= bytes.Length; start++)
                {
                    if (start < bytes.Length && (bytes[start] & 0xc0) == 0x80) continue;
                    var expected = Run(program, bytes, start, true);
                    var actual = Run(program, bytes, start, false);
                    Assert.Equal(expected.Found, actual.Found);
                    Assert.Equal(expected.End, actual.End);
                    Assert.Equal(expected.Starts, actual.Starts);
                    Assert.Equal(expected.Ends, actual.Ends);
                }
            }
        }
    }

    [Fact]
    public void EveryWorkCutoffClearsRefusedCapturesAndPreservesCallerReservation()
    {
        var program = new Utf8Regex("item-([0-9]+)").Inspection.AvailableOwnedSliceProgram;
        var bytes = Encoding.ASCII.GetBytes("item-" + new string('1', 8193));
        var reference = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 17), Timeout.InfiniteTimeSpan, default);
        Assert.True(reference.Ledger.TryReserve(17));
        var starts = new[] { -1 }; var ends = new[] { -1 };
        var result = Utf8SliceRunner.TryMatchAt(program, bytes, 0, starts, ends, false, true, ref reference);
        Assert.True(result.IsMatch); Assert.Equal(5, starts[0]); Assert.Equal(bytes.Length, ends[0]);
        for (var cap = 1UL; cap < reference.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 1, 17), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(17)); starts[0] = ends[0] = 99;
            var refused = Utf8SliceRunner.TryMatchAt(program, bytes, 0, starts, ends, false, true, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, refused.Breach);
            Assert.False(refused.IsMatch); Assert.Equal(-1, starts[0]); Assert.Equal(-1, ends[0]);
            Assert.Equal(17UL, invocation.Ledger.ReservedBytes);
        }
        Assert.Equal(17UL, reference.Ledger.ReservedBytes);
    }

    private static (bool Found, int End, int[] Starts, int[] Ends) Run(Utf8SliceProgram program, byte[] input, int start, bool history)
    {
        var invocation = new Utf8SliceInvocation(new(1000000, 1000, 1 << 20), Timeout.InfiniteTimeSpan, default);
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var result = Utf8SliceRunner.TryMatchAt(program, input, start, starts, ends, history, true, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Utf8SliceRunner.ReleaseHistory(result.CaptureStarts, result.CaptureLengths, ref invocation);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        return (result.IsMatch, result.EndIndex, starts, ends);
    }
}
