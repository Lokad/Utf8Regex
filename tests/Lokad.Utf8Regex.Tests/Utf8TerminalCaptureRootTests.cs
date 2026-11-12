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
    [InlineData("([a-z]+)-([0-9]+)")] [InlineData("([a-z]+)X([0-9]+)")]
    [InlineData("([a-z]+)a([0-9]+)")] [InlineData("([a-z]+?)-([0-9]+)")]
    [InlineData("([a-z]+)-([0-9]+?)")] [InlineData("([a-z]{1,3})-([0-9]+)")]
    [InlineData("(([a-z]+))-([0-9]+)")] [InlineData("([a-z]+)-([0-9]+)x")]
    [InlineData("([,;])")] [InlineData("([a-z])")] [InlineData("([^,;])")]
    [InlineData("(?i:([a-z]))")] [InlineData("(([a-z]))")] [InlineData("([,;])x")]
    public void LatestSlotsAgreeWithHistoryEnabledExecutionAtEveryScalarStart(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
        {
            var program = new Utf8Regex(pattern, options).Inspection.AvailableOwnedSliceProgram;
            foreach (var text in new[] { "", "item-", "item-12", "item-123x", "xitem-345!",
                "é😀item-12a", "XABC", "item-aé", "item-123item-45", "other-345!", "aa123", "abcX12", ",;", "é😀,;x" })
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

    [Theory]
    [InlineData("([\\x00-\\x7F])", 0)]
    [InlineData("([,;])", 1)]
    [InlineData("([a-z])", 2)]
    public void SingleCapturedSetsAgreeWithScalarOracleAcrossBorrowedAsciiAndUnicode(string pattern, int predicate)
    {
        var ascii = new string(Enumerable.Range(0, 128).Select(value => (char)value).ToArray());
        var storage = Encoding.UTF8.GetBytes("!😀" + ascii + "é尾😀,;#");
        var input = storage.AsSpan(1, storage.Length - 2);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var program = new Utf8Regex(pattern, options).Inspection.AvailableOwnedSliceProgram;
            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
                // Forward roots use an independent scalar predicate. RTL has
                // its own coordinate direction and is checked against history.
                var expected = Run(program, input, start, true);
                var actual = Run(program, input, start, false);
                Assert.Equal(expected.Found, actual.Found);
                Assert.Equal(expected.End, actual.End);
                Assert.Equal(expected.Starts, actual.Starts);
                Assert.Equal(expected.Ends, actual.Ends);
                if (options == RegexOptions.RightToLeft) continue;
                var present = Rune.DecodeFromUtf8(input[start..], out var scalar, out var width) == System.Buffers.OperationStatus.Done;
                var hit = present && (predicate == 0 ? scalar.Value < 128 :
                    predicate == 1 ? scalar.Value is ',' or ';' : scalar.Value is >= 'a' and <= 'z');
                Assert.Equal(hit, actual.Found);
                Assert.Equal(hit ? start + width : -1, actual.End);
                Assert.Equal(hit ? start : -1, actual.Starts[0]);
                Assert.Equal(hit ? start + width : -1, actual.Ends[0]);
            }
        }
    }

    [Theory]
    [InlineData("item-([0-9]+)")] [InlineData("([a-z]+)-([0-9]+)")] [InlineData("([,;])")]
    public void EveryWorkCutoffClearsRefusedCapturesAndPreservesCallerReservation(string pattern)
    {
        var program = new Utf8Regex(pattern).Inspection.AvailableOwnedSliceProgram;
        var bytes = program.RepeatCount == 0 ? ";"u8.ToArray() :
            Encoding.ASCII.GetBytes((program.GroupCount == 1 ? "item" : new string('a', 8193)) + "-" + new string('1', 8193));
        var expected = Run(program, bytes, 0, true);
        var working = program.GroupCount == 1 ? 17UL : 1UL << 20;
        var reference = new Utf8SliceInvocation(new(ulong.MaxValue, 1, working), Timeout.InfiniteTimeSpan, default);
        Assert.True(reference.Ledger.TryReserve(17));
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var result = Utf8SliceRunner.TryMatchAt(program, bytes, 0, starts, ends, false, true, ref reference);
        Assert.True(result.IsMatch); Assert.Equal(expected.Starts, starts); Assert.Equal(expected.Ends, ends);
        for (var cap = 1UL; cap < reference.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 1, working), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(17)); Array.Fill(starts, 99); Array.Fill(ends, 99);
            var refused = Utf8SliceRunner.TryMatchAt(program, bytes, 0, starts, ends, false, true, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, refused.Breach);
            Assert.False(refused.IsMatch); Assert.All(starts, value => Assert.Equal(-1, value)); Assert.All(ends, value => Assert.Equal(-1, value));
            Assert.Equal(17UL, invocation.Ledger.ReservedBytes);
        }
        Assert.Equal(17UL, reference.Ledger.ReservedBytes);
    }

    private static (bool Found, int End, int[] Starts, int[] Ends) Run(Utf8SliceProgram program, ReadOnlySpan<byte> input, int start, bool history)
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
