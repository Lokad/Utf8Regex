using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CapturedSimpleBooleanTests
{
    private const string Pattern = @"(\d{4}[- ]){3}\d{3,4}";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    private static bool Scalar(Utf8Regex regex, byte[] input)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
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
    public void AsciiExistenceNeedsNoCaptureWorkspace(string pattern, string text, bool expected)
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan,
                Funded with { MaxWorkingBytes = 1, MaxResultBytes = 1, MaxDepth = 1 });
            var storage = Encoding.UTF8.GetBytes("pad" + text + "tail");
            var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(expected, regex.IsMatch(input));
                Assert.Equal(Utf8ExecutionRoute.NativeAsciiSimplePattern, session.ExecutionRoute);
                Assert.Equal(expected, regex.IsMatch(input, token.Token));
            }
            finally { session.Complete(); }
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
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, Funded);
            using var token = new CancellationTokenSource();
            foreach (var text in texts)
            {
                var input = Encoding.UTF8.GetBytes(text);
                var expected = Scalar(regex, input);
                Assert.Equal((pattern, mode, text, expected), (pattern, mode, text, regex.IsMatch(input)));
                Assert.Equal((pattern, mode, text, expected), (pattern, mode, text, regex.IsMatch(input, token.Token)));
            }
        }
        var linear = new Utf8Regex(Pattern, RegexOptions.NonBacktracking);
        Assert.True(linear.IsMatch("1234-5678-1234-456"u8));
        Assert.True(linear.IsMatch(Encoding.UTF8.GetBytes("١٢٣٤-٥٦٧٨-١٢٣٤-٤٥٦")));
    }

    [Fact]
    public void CapturesOffsetsAndRetainedResultsKeepTheirExecutors()
    {
        var regex = new Utf8Regex(Pattern);
        var input = "1234-5678-1234-4567"u8.ToArray();
        var retained = regex.MatchDetailed(input);
        Assert.Equal(3, retained.GetGroup(1).CaptureCount);
        Assert.Equal("1234-", retained.GetGroup(1).GetCapture(0).GetValueString());
        Assert.Equal("5678-", retained.GetGroup(1).GetCapture(1).GetValueString());
        Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
        Assert.True(regex.IsMatchFromUtf16Offset(input, 0));
        Assert.False(regex.IsMatchFromUtf16Offset(input, 1));
        Assert.Equal("1234-", retained.GetGroup(1).GetCapture(0).GetValueString());
        foreach (var prefix in new[] { "1234-5678-1234-456", "no match" })
        {
            byte[] malformed = [.. Encoding.UTF8.GetBytes(prefix), 0xff];
            Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(malformed, canceled.Token)).CancellationToken);
        }
    }

    [Fact]
    public void WholeKernelAdmissionIncludesValidationAndPublication()
    {
        var input = "1234-5678-1234-456"u8.ToArray();
        var invocation = new Utf8SliceInvocation(new(1000000, 1, 1, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(Utf8Validation.Validate(input, ref invocation).IsAscii);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        for (ulong work = 1; work <= invocation.Budget.Steps; work++)
        {
            var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
                Funded with { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            if (work == invocation.Budget.Steps) Assert.True(regex.IsMatch(input));
            else Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input)).ResourceKind);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SearchUsesTheOriginalCancellationAndDeadlineAfterValidation(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(new string('x', 320000) + "1234-5678-1234-456");
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.Validate(input, ref validation);
        var entered = false;
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var regex = new Utf8Regex(Pattern, RegexOptions.None, timeout, Funded);
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validation.Budget.Steps) return;
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
        var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromSeconds(1), Funded);
        Assert.True(regex.IsMatch("1234-5678-1234-456"u8, token.Token));
        cancelAt = reads; reads = 0;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch("1234-5678-1234-456"u8, token.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
