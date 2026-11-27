using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CapturedSimpleBooleanTests
{
    private const string Pattern = @"(\d{4}[- ]){3}\d{3,4}";
    private static Utf8ResourcePolicy Funded => new() { MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    private static bool Scalar(Utf8Regex regex, byte[] input)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        invocation.AnchorOriginByte = program.RightToLeft ? input.Length : 0;
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var match = Utf8SliceRunner.TryFindFirst(program, input, program.RightToLeft ? input.Length : 0,
                slots.Starts, slots.Ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach); return match.Found;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Theory]
    [InlineData(Pattern, "1234-5678-1234-456", true)]
    [InlineData(Pattern, "x1234-5678-1234-4567x", true)]
    [InlineData(Pattern, "1234-5678-1234-x56", false)]
    [InlineData(@"(\d{2})X", " x12X ", true)]
    [InlineData(@"(\d{1,2})X", " x1X ", true)]
    [InlineData(@"(\w{1,2})X", " abX ", true)]
    [InlineData(@"([^\d]{1,2})X", " \tX ", true)]
    public void AsciiExistenceUsesFundedWorkingStorage(string pattern, string text, bool expected)
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded with
                {
                    MaxResultBytes = 1
                }
            });
            var storage = Encoding.UTF8.GetBytes("pad" + text + "tail");
            var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected, regex.IsMatch(input, token.Token));
        }
    }

    [Fact]
    public void OrderedBranchesUnicodeAnchorsAndExcludedEffectsAgreeWithTheScalarVm()
    {
        string[] patterns = [Pattern, @"((?:\d|\d\d))X", @"(\d{1,2})X", @"(\w{1,2})X", @"(\s{1,2})X",
            @"(?m:^(\d{1,2})X$)", @"\A(\d{1,2})X\z", @"(\d{1,2})X$", @"(\d{1,2}?)X",
            @"((?>\d|\d\d))X", @"\b(\d{1,2})X\b", @"(\d{1,2})(?=X)", @"(\d)\1",
            @"(?<g>\d)(?<g>\d)X", @"(?<1>\d)X", @"\G(\d)X", @"(\d)?(?(1)X|Y)" ];
        string[] texts = ["", "12X", "123X", "\n1X\n", "12X\nx", " 1234-5678-1234-456 ",
            "١٢X", "𝟙𝟚X", "é12X", "12X😀", "e\u0301X", "XX", "Y", "11"];
        foreach (var pattern in patterns)
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.IgnoreCase })
        {
            var regex = new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            using var token = new CancellationTokenSource();
            foreach (var text in texts)
            {
                var input = Encoding.UTF8.GetBytes(text);
                var expected = Scalar(regex, input);
                Assert.Equal((pattern, mode, text, expected), (pattern, mode, text, regex.IsMatch(input)));
                Assert.Equal((pattern, mode, text, expected), (pattern, mode, text, regex.IsMatch(input, token.Token)));
            }
        }
        var linear = new Utf8Regex(Pattern, options: RegexOptions.NonBacktracking);
        Assert.True(linear.IsMatch("1234-5678-1234-456"u8));
        Assert.True(linear.IsMatch(Encoding.UTF8.GetBytes("١٢٣٤-٥٦٧٨-١٢٣٤-٤٥٦")));
    }

    [Fact]
    public void CapturesOffsetsAndRetainedResultsKeepTheirExecutors()
    {
        var regex = new Utf8Regex(Pattern);
        var input = "1234-5678-1234-4567"u8.ToArray();
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(3, retained.GetCaptures(1).Length);
        Assert.Equal("1234-", System.Text.Encoding.UTF8.GetString(input.AsSpan().Slice(retained.GetCaptures(1)[0].StartOffsetInBytes, retained.GetCaptures(1)[0].EndOffsetInBytes - retained.GetCaptures(1)[0].StartOffsetInBytes)));
        Assert.Equal("5678-", System.Text.Encoding.UTF8.GetString(input.AsSpan().Slice(retained.GetCaptures(1)[1].StartOffsetInBytes, retained.GetCaptures(1)[1].EndOffsetInBytes - retained.GetCaptures(1)[1].StartOffsetInBytes)));
        Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
        Assert.True(regex.Match(input, 0, []).Success);
        Assert.False(regex.Match(input, 1, []).Success);
        Assert.Equal("1234-", System.Text.Encoding.UTF8.GetString(input.AsSpan().Slice(retained.GetCaptures(1)[0].StartOffsetInBytes, retained.GetCaptures(1)[0].EndOffsetInBytes - retained.GetCaptures(1)[0].StartOffsetInBytes)));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        foreach (var text in new[] { "1234-5678-1234-456", "no match" })
        {
            var subject = Encoding.UTF8.GetBytes(text);
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(subject, canceled.Token)).CancellationToken);
        }
    }

    [Fact]
    public void AdjacentCaptureMarkersStillConsumeTheirProofVisits()
    {
        const string pattern = "^((((([0-9]+)))))x$";
        var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = "12x"u8;
        var firstCapture = Array.FindIndex(program.Instructions, instruction => instruction.Kind == Utf8SliceInstructionKind.CaptureStart);

        Assert.True(Utf8SliceRunner.MayMatchBranchedAsciiPrefix(program, input, 0, firstCapture,
            out var end, proveMatch: true, instructionLimit: 3));
        Assert.Equal(-1, end);
        Assert.True(Utf8SliceRunner.MayMatchBranchedAsciiPrefix(program, input, 0, firstCapture,
            out end, proveMatch: true));
        Assert.Equal(input.Length, end);

        var captures = new Utf8CaptureRange[6];
        Assert.True(regex.Match(input, 0, captures).Success);
        foreach (var capture in captures.AsSpan(1))
            Assert.Equal((true, 0, 2), (capture.Success, capture.StartOffsetInBytes, capture.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SearchUsesTheOriginalCancellationAndDeadline(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(new string('x', 320000) + "1234-5678-1234-456");
        var entered = false;
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout,
            ResourcePolicy = Funded
        });
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (entered) return;
            entered = true;
            if (expire) ticks += 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(timeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.True(regex.IsMatch("1234-5678-1234-456"u8));
    }

    [Fact]
    public void WholeKernelPublicationObservesCancellationFromTheLastDeadlinePoll()
    {
        var reads = 0; var cancelAt = int.MaxValue;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => { if (++reads == cancelAt) token.Cancel(); return 0; });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1),
            ResourcePolicy = Funded
        });
        Assert.True(regex.IsMatch("1234-5678-1234-456"u8, token.Token));
        cancelAt = reads; reads = 0;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch("1234-5678-1234-456"u8, token.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
