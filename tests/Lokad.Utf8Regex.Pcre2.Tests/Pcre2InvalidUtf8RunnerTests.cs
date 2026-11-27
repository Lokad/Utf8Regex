using System.Buffers;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2InvalidUtf8RunnerTests
{
    [Fact]
    public void PublicOperationsAgreeOnByteRangesAtBarriers()
    {
        using var regex = new Utf8Pcre2Regex("(?<letter>A|B)", Pcre2CompileOptions.MatchInvalidUtf8);
        var input = Convert.FromHexString("418042");
        Span<Utf8Pcre2CaptureRange> slots = stackalloc Utf8Pcre2CaptureRange[2];

        Assert.True(regex.IsMatch(input));
        Assert.Equal(2, regex.Count(input));
        Assert.True(regex.Match(input).IsUtf8SliceWellFormed);
        Assert.True(regex.Match(input, captures: slots).Success);
        Assert.True(slots[1].IsUtf8SliceWellFormed);
        Assert.Equal((0, 1), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
        var afterBarrier = regex.Match(input, 1);
        Assert.Equal((2, 3), (afterBarrier.StartOffsetInBytes, afterBarrier.EndOffsetInBytes));
        var probe = regex.Probe(input, Pcre2PartialMode.None);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        Assert.Equal((0, 1), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));

        var enumerator = regex.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsUtf8SliceWellFormed);
        Assert.Equal((0, 1), (enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        Assert.True(enumerator.MoveNext());
        Assert.Equal((2, 3), (enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        Assert.False(enumerator.MoveNext());

        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out var first));
        Assert.True(alias.MoveNext(out var second));
        Assert.False(cursor.MoveNext(out _));
        Assert.True(first.Groups[1].IsUtf8SliceWellFormed);
        Assert.True(first.GetGroupMemory(1).Span.SequenceEqual("A"u8));
        Assert.True(second.GetGroupMemory(1).Span.SequenceEqual("B"u8));
    }

    [Fact]
    public void ReplacementPreservesMalformedGapsAndExpandsCaptures()
    {
        using var regex = new Utf8Pcre2Regex("(?<number>[0-9]+)", Pcre2CompileOptions.MatchInvalidUtf8);
        var input = Convert.FromHexString("31FF32");
        var expected = Convert.FromHexString("3C313EFF3C323E");
        Assert.Equal(expected, regex.Replace(input, "<${number}>"u8));
        Assert.Equal("<1><2>"u8.ToArray(), regex.Replace(input, "<${number}>"u8,
            substitutionOptions: Pcre2SubstitutionOptions.SubstituteReplacementOnly));
        Assert.Equal(Convert.FromHexString("78FF78"), regex.Replace(input, "x"u8));
        Span<byte> destination = stackalloc byte[expected.Length];
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "<${number}>"u8, destination, out var written));
        Assert.Equal(expected, destination[..written].ToArray());
    }

    [Fact]
    public void GlobalIterationRetriesAtTheReportedEmptyRangeAfterAReset()
    {
        using var regex = new Utf8Pcre2Regex(@"(?:a\K|)", Pcre2CompileOptions.MatchInvalidUtf8);
        var input = Convert.FromHexString("61FF62");
        Assert.Equal(3, regex.Count(input));
        var ranges = regex.EnumerateMatches(input);
        for (var offset = 1; offset <= 3; offset++)
        {
            Assert.True(ranges.MoveNext());
            Assert.Equal((offset, offset), (ranges.Current.StartOffsetInBytes, ranges.Current.EndOffsetInBytes));
        }
        Assert.False(ranges.MoveNext());
    }

    [Theory]
    [InlineData("(?:abc|a)", "61FF616263", Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1)]
    [InlineData("(?:abc|a)", "61FF616263", Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 5)]
    [InlineData("(?:abc|a)", "61FF7A", Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0)]
    [InlineData("(?:abc|a)", "61FF6162", Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 4)]
    [InlineData("abc", "6162FF", Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0)]
    [InlineData("abc", "61FF6162", Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 2, 4)]
    [InlineData(@"\z", "61FF", Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0)]
    [InlineData(@"\z", "FF61", Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 2)]
    public void ProbesPreserveHardPartialPriorityAndReportPartialsOnlyAtTheSubjectEnd(
        string pattern, string hex, Pcre2PartialMode mode, Utf8Pcre2ProbeKind kind, int start, int end)
    {
        using var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.MatchInvalidUtf8);
        var result = regex.Probe(Convert.FromHexString(hex), mode);
        Assert.Equal(kind, result.Kind);
        Assert.Equal((start, end), (result.Value.StartOffsetInBytes, result.Value.EndOffsetInBytes));
    }

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
    [InlineData(@"a\K", "61FF62", 0, Pcre2MatchOptions.NotEmptyAtStart, 1, 1)]
    [InlineData(@"a\K", "61FF62", 0, Pcre2MatchOptions.NotEmpty, -1, -1)]
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

    [Fact]
    public void RepeatedMatchesReuseTheExtentOfTheSameSubject()
    {
        var program = Compile("a");
        var input = new byte[64 * 1024];
        input.AsSpan().Fill((byte)'a');
        input[^1] = 0xff;
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => { polls++; return 0; });
        var budget = new Pcre2ResourceBudget(
            program.Request with { MatchTimeout = TimeSpan.FromSeconds(1) }, CancellationToken.None);
        var scan = new Pcre2FragmentScan();

        var first = Pcre2BacktrackingRunner.MatchInvalidUtf8(program, input, default, default,
            default, Pcre2CaptureMaterialization.None, ref budget, ref scan);
        var firstPolls = polls;
        var restart = new Utf8BytePosition(first.ConsumedEndOffsetInBytes);
        var second = Pcre2BacktrackingRunner.MatchInvalidUtf8(program, input, restart, restart,
            default, Pcre2CaptureMaterialization.None, ref budget, ref scan);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal((1, 2), (second.StartOffsetInBytes, second.EndOffsetInBytes));
        Assert.True(firstPolls - (polls - firstPolls) >= 32);
    }

    [Fact]
    public void AFailedFragmentWithoutAMarkKeepsThePreviousNoMatchMark()
    {
        var program = Compile("a(*MARK:seen)bz");
        var budget = new Pcre2ResourceBudget(program.Request, CancellationToken.None);

        var result = Pcre2BacktrackingRunner.MatchInvalidUtf8(program, Convert.FromHexString("6163627AFF63"),
            default, default, Pcre2CaptureMaterialization.None, ref budget);

        Assert.False(result.Success);
        Assert.Equal("seen", result.Mark);
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
