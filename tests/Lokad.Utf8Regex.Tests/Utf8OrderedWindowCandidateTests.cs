using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8OrderedWindowCandidateTests
{
    private const string Pattern = @"Tom.{10,25}river|river.{10,25}Tom";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 4000000,
        MaxDepth = 4096, MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 };

    private static int ScalarCount(Utf8Regex regex, byte[] input, int fromByte)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            // No prefix plan: this oracle discovers starts independently.
            var result = Utf8SliceRunner.CountFromBytes(program, input, fromByte, slots.Starts, slots.Ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            return result.Count;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Theory]
    [InlineData(Pattern)]
    [InlineData(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes")]
    [InlineData(@"(?<x>Tom).{0,2}river\k<x>|river.{0,2}Tom")]
    [InlineData(@"Tom.{0,2}river|Tommy.{0,2}river")]
    [InlineData(@"é😀.{0,2}river|river.{0,2}é😀")]
    public void PublicCountKeepsScalarFloorsCapturesAndSameStartVerification(string pattern)
    {
        var text = new string('!', 260) + "Tommy!river Tom😀riverTom river!!Tom " +
            "Holmes😀Watson Watson\nHolmes é😀!river river😀é😀 Tom" + new string('!', 10) + "river";
        var input = Encoding.UTF8.GetBytes(text);
        byte[] storage = [0xff, .. input, 0xff];
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, Funded);
            using var token = new CancellationTokenSource();
            for (var start = 255; start <= text.Length; start++)
            {
                if (start < text.Length && char.IsLowSurrogate(text[start])) continue;
                var fromByte = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
                var expected = ScalarCount(regex, input, fromByte);
                Assert.Equal(expected, regex.CountFromUtf16Offset(storage.AsSpan(1, input.Length), start));
                Assert.Equal(expected, regex.CountFromUtf16Offset(input, start, token.Token));
            }
        }
    }

    [Theory]
    [InlineData(2043)] [InlineData(2044)] [InlineData(2045)] [InlineData(4095)]
    public void CrossingWindowsKeepScalarGapBoundsAndNonBacktracking(int padding)
    {
        foreach (var gap in new[] { 9, 10, 25, 26 })
        {
            var input = Encoding.UTF8.GetBytes(new string('r', padding) + "Tom" +
                string.Concat(Enumerable.Repeat("😀", gap)) + "river");
            foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
            {
                var regex = new Utf8Regex(Pattern, mode, Timeout.InfiniteTimeSpan, Funded);
                Assert.Equal(gap is >= 10 and <= 25 ? 1 : 0, regex.Count(input));
                Assert.Equal(ScalarCount(regex, input, 0), regex.Count(input));
            }
        }
    }

    [Fact]
    public void DenseFalseFirstBytesUseBoundedWorkButStillAdmitVmWorkspace()
    {
        var input = Encoding.UTF8.GetBytes(new string('r', 100000) + "Tom!!!!!!!!!!river");
        var regex = new Utf8Regex(Pattern, RegexOptions.Compiled, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkSteps = 4096 });
        using var token = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, token.Token));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(Pattern,
                RegexOptions.None, Timeout.InfiniteTimeSpan, Funded with { MaxWorkingBytes = 1 }).Count(input)).ResourceKind);
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, token.Token)).CancellationToken);
    }

    [Fact]
    public void CountLeavesRetainedDetailedCapturesIntact()
    {
        var regex = new Utf8Regex(@"(?<x>Tom).{0,2}river\k<x>|river.{0,2}Tom");
        var input = "😀Tom!riverTom"u8.ToArray();
        var retained = regex.MatchDetailed(input);
        Assert.Equal("Tom", retained.GetGroup("x").GetValueString());
        Assert.Equal(1, regex.Count(input));
        Assert.Equal("Tom", retained.GetGroup("x").GetValueString());
    }
}
