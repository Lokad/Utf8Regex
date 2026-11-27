using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2InvalidUtf8RunnerTests
{
    [Theory]
    [InlineData(@"id=\d+", "FF69643D3432", 0, Pcre2MatchOptions.None, 1, 6)]
    [InlineData(@"\x{FFFD}", "FF69643D3432", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"\x{FFFD}", "FFEFBFBD", 0, Pcre2MatchOptions.None, 1, 4)]
    [InlineData(@"(?<=.)id", "FF69643D3432", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"c.d", "616263FF646566", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"\C", "FF", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"(?:)", "FF", 0, Pcre2MatchOptions.None, 0, 0)]
    [InlineData(@"\Adef", "616263FF646566", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"abc\z", "616263FF646566", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"abc\Z", "6162630AFF646566", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"abc$", "616263FF646566", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"def\z", "616263FF646566", 0, Pcre2MatchOptions.None, 4, 7)]
    [InlineData(@"(?m)^def", "616263FF646566", 0, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"\bdef\b", "616263FF646566", 0, Pcre2MatchOptions.None, 4, 7)]
    [InlineData(@"\b.", "618062", 2, Pcre2MatchOptions.None, 2, 3)]
    [InlineData(@"\b.", "FFC3A962", 1, Pcre2MatchOptions.None, 3, 4)]
    [InlineData(@"\Gdef", "616263FF646566", 3, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"\Gdef", "616263FF646566", 4, Pcre2MatchOptions.None, 4, 7)]
    [InlineData(@"def", "616263FF646566", 0, Pcre2MatchOptions.Anchored, 4, 7)]
    [InlineData(@"abc", "616263FF646566", 0, Pcre2MatchOptions.EndAnchored, 0, 3)]
    [InlineData(@".", "C3A941", 1, Pcre2MatchOptions.None, 2, 3)]
    [InlineData(@"(?<=é)A", "C3A941", 1, Pcre2MatchOptions.None, -1, -1)]
    [InlineData(@"(?<=é)A", "C3A941", 2, Pcre2MatchOptions.None, 2, 3)]
    public void MatchesKeepNativeBoundsAndOriginalOffsets(string pattern, string hex, int start,
        Pcre2MatchOptions options, int expectedStart, int expectedEnd)
    {
        var program = Compile(pattern);
        var input = Convert.FromHexString(hex);
        Span<Utf8Pcre2CaptureRange> ranges = stackalloc Utf8Pcre2CaptureRange[1];
        foreach (var materialization in new[] { Pcre2CaptureMaterialization.None, Pcre2CaptureMaterialization.ByteFinalSlots })
        {
            var budget = new Pcre2ResourceBudget(program.Request, CancellationToken.None);
            var result = Pcre2BacktrackingRunner.MatchInvalidUtf8(program, input, new Utf8BytePosition(start),
                options, materialization, ref budget, ranges);
            Assert.Equal(expectedStart >= 0, result.Success);
            if (result.Success)
            {
                Assert.Equal((expectedStart, expectedEnd), (result.StartOffsetInBytes, result.EndOffsetInBytes));
                if (materialization == Pcre2CaptureMaterialization.ByteFinalSlots)
                    Assert.Equal((expectedStart, expectedEnd), (ranges[0].StartOffsetInBytes, ranges[0].EndOffsetInBytes));
            }
            Assert.Equal(0ul, budget.WorkingInvocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void CaptureBeforeAnIsolatedContinuationUsesTheAdmittedFragmentEdge()
    {
        var program = Compile("(A|B)");
        var input = Convert.FromHexString("418042");
        var budget = new Pcre2ResourceBudget(program.Request, CancellationToken.None);
        Span<Utf8Pcre2CaptureRange> ranges = stackalloc Utf8Pcre2CaptureRange[2];

        var result = Pcre2BacktrackingRunner.MatchInvalidUtf8(program, input, default, default,
            Pcre2CaptureMaterialization.ByteFinalSlots, ref budget, ranges);

        Assert.True(result.Success);
        Assert.Equal((0, 1), (ranges[1].StartOffsetInBytes, ranges[1].EndOffsetInBytes));
        Assert.True(ranges[1].IsUtf8SliceWellFormed);
        Assert.Equal(0ul, budget.WorkingInvocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void FragmentScanKeepsAValidScalarThatCrossesItsCheckpointBlock()
    {
        var program = Compile("😀");
        var budget = new Pcre2ResourceBudget(program.Request, CancellationToken.None);
        var input = new byte[2054];
        input.AsSpan(0, 2047).Fill((byte)'a');
        "😀"u8.CopyTo(input.AsSpan(2047));
        input[2051] = 0xff;

        var result = Pcre2BacktrackingRunner.MatchInvalidUtf8(program, input, default, default,
            Pcre2CaptureMaterialization.None, ref budget);

        Assert.True(result.Success);
        Assert.Equal((2047, 2051), (result.StartOffsetInBytes, result.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void UcpBoundaryHonorsTheReadFloorAfterSkippingAContinuation(int start, bool expected)
    {
        var program = Compile(@"\bA", Pcre2CompileOptions.Ucp);
        var budget = new Pcre2ResourceBudget(program.Request, CancellationToken.None);

        var result = Pcre2BacktrackingRunner.MatchInvalidUtf8(program, "éA"u8, new Utf8BytePosition(start),
            default, Pcre2CaptureMaterialization.None, ref budget);

        Assert.Equal(expected, result.Success);
    }

    [Fact]
    public void CaptureAdmissionAfterABarrierRefundsTheSameInvocationOnDenial()
    {
        var program = Compile("(?<id>id=[0-9]+)");
        var limits = new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 };
        var budget = new Pcre2ResourceBudget(program.Request with { DefaultLimits = limits }, CancellationToken.None);
        var input = Convert.FromHexString("FF69643D3432");

        var failure = Assert.Throws<Pcre2MatchException>(() =>
            Pcre2BacktrackingRunner.MatchInvalidUtf8(program, input, default, default,
                Pcre2CaptureMaterialization.ByteFinalSlots, ref budget));

        Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
        Assert.Equal(0ul, budget.WorkingInvocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongFragmentScanRetainsOriginalInterruptionAuthority(bool timeout)
    {
        var program = Compile("a");
        var input = new byte[64 * 1024];
        input.AsSpan().Fill((byte)'b');
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 9) return 0;
            if (timeout) return 50;
            cancellation.Cancel();
            return 0;
        });
        var budget = new Pcre2ResourceBudget(
            program.Request with { MatchTimeout = TimeSpan.FromMilliseconds(20) }, cancellation.Token);

        var failure = Record.Exception(() =>
            Pcre2BacktrackingRunner.MatchInvalidUtf8(program, input, default, default,
                Pcre2CaptureMaterialization.None, ref budget));

        if (timeout) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(0ul, budget.WorkingInvocation.Ledger.ReservedBytes);
    }

    private static Pcre2BacktrackingProgram Compile(string pattern, Pcre2CompileOptions options = default)
        => Pcre2BacktrackingCompiler.Compile(new Pcre2CompileRequest(
            pattern, options, new Utf8Pcre2CompileSettings { BackslashC = Pcre2BackslashCPolicy.Allow },
            default, Timeout.InfiniteTimeSpan))!.Program;
}
