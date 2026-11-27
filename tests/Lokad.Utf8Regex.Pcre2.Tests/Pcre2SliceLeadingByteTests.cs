using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SliceLeadingByteTests
{
    private const string Pattern = @"(?=(?<token>[a-z]{3}[0-9]{3}))abc123";

    [Theory]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    [InlineData(9, false)]
    [InlineData(14, false)]
    public void PublicByteOffsetsRemainScalarAligned(int start, bool expected)
    {
        var regex = new Utf8Pcre2Regex(Pattern);
        var input = "abc123éabc123"u8.ToArray();
        Assert.Equal(expected, regex.IsMatch(input, start));
        ReadOnlySpan<byte> bytes = input;
        var match = regex.Match(bytes, start);
        Assert.Equal(expected, match.Success);
        if (match.Success)
        {
            Assert.Equal(start == 0 ? 0 : 8, match.StartOffsetInBytes);
            Assert.Equal(start == 0 ? 6 : 14, match.EndOffsetInBytes);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, 7));
    }

    [Fact]
    public void LastScanCannotPublishNoMatchAfterOriginalCancellation()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = new Utf8Pcre2Regex(Pattern);
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var input = Encoding.UTF8.GetBytes(new string('x', 2048));
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        for (var i = 0; i < 63; i++) _ = invocation.CheckProgress();
        invocation.CheckpointHook = _ => live.Cancel();
        OperationCanceledException? failure = null;
        try { _ = Pcre2SliceDriver.FindFirst(direct.SliceProgram, input, 0, direct.Fallback.MinimumByteLength, ref invocation, direct.Fallback.LeadingAsciiByte); }
        catch (OperationCanceledException error) { failure = error; }
        Assert.Equal(live.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4096)]
    public void MissingLeadingByteAdmitsBoundedScansWithLiveTokenOrWorkLimit(int candidateCount)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = new Utf8Pcre2Regex(Pattern);
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var minimum = direct.Fallback.MinimumByteLength;
        var input = Encoding.UTF8.GetBytes(new string('x', candidateCount + minimum - 1));
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.False(Pcre2SliceDriver.FindFirst(direct.SliceProgram, input, 0, minimum, ref invocation, direct.Fallback.LeadingAsciiByte).Success);
        Assert.Equal((ulong)(candidateCount + 2047) / 2048, invocation.Budget.DiagnosticCheckpoints);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        if (candidateCount <= 2048) return;
        var limited = new Utf8SliceInvocation(new(0, 0), Timeout.InfiniteTimeSpan, default);
        Assert.True(limited.Ledger.TryReserve(13));
        Pcre2MatchException? error = null;
        try { _ = Pcre2SliceDriver.FindFirst(direct.SliceProgram, input, 0, minimum, ref limited, direct.Fallback.LeadingAsciiByte); }
        catch (Pcre2MatchException failure) { error = failure; }
        Assert.Equal(13UL, limited.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(Pattern, "é😀xxxx abc123", true)]
    [InlineData(Pattern, "abx abc999 abc123", true)]
    [InlineData(Pattern, "abc999", false)]
    [InlineData(@"(?=(?<t>a))a\k<t>", "é😀xaa", true)]
    [InlineData(@"(?!(?<t>b))abc", "xxxabc", true)]
    [InlineData(@"(?<t>a)(?=b)b", "xaaab", true)]
    [InlineData(@"(?<t>a?)", "xxx", true)]
    [InlineData(@"\G(?<t>abc)", "xabc", false)]
    [InlineData(@"^(?=(?<t>abc))abc", "xabc", false)]
    [InlineData(@"(?=(?<t>\x00a))\x00a", "é\0a", true)]
    [InlineData(@"(?:(?=(?<t>abc))abc|(?<x>axy))", "zzzaxy", true)]
    [InlineData(@"(?=(?<t>é))é", "xxxé", true)]
    public void PublicBooleanSearchPreservesAssertionsCapturesAndOrigins(string pattern, string text, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        using var live = new CancellationTokenSource();
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected, regex.IsMatch(input, 0));
        Assert.Equal(expected, regex.IsMatch(input, 0, Pcre2MatchOptions.None, live.Token));
        Assert.Equal(expected, regex.Match(input).Success);
        if (expected && pattern == Pattern)
        {
            ReadOnlySpan<byte> result2Input = input;
            var result2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var result2Result = regex.Match(result2Input, captures: result2Captures);
            Assert.Equal("abc123", System.Text.Encoding.UTF8.GetString((result2Captures[1].Success ? result2Input.Slice(result2Captures[1].StartOffsetInBytes, result2Captures[1].EndOffsetInBytes - result2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnchoredAndCaselessRequestsDoNotUseAnInvalidLiteralProof(bool anchored)
    {
        var regex = new Utf8Pcre2Regex(Pattern, options: anchored ? Pcre2CompileOptions.Anchored : Pcre2CompileOptions.Caseless);
        var input = anchored ? "xxxxabc123"u8.ToArray() : "xxxxABC123"u8.ToArray();
        Assert.Equal(!anchored, regex.IsMatch(input));
        Assert.Equal(!anchored, regex.IsMatch(input, 0));
    }

    [Fact]
    public void BranchedCountUsesTheSharedBoundedScanFromAByteOffset()
    {
        const string octet = @"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string excludedPrefix = "22.23.24.25 é ";
        var input = Encoding.UTF8.GetBytes(excludedPrefix + new string('x', 32763) + "😀 123.17.24.25");
        var from = Encoding.UTF8.GetByteCount(excludedPrefix);
        using var regex = new Utf8Pcre2Regex(@"(?:" + octet + @"\.){3}" + octet, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1 << 20 },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        using var live = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, from, Pcre2MatchOptions.None, live.Token));
    }

    [Fact]
    public void SliceCountUsesExactBranchPrefixesBeforeVerification()
    {
        const string prefix = "Holmes x Watson 😀 ";
        var text = prefix + new string('H', 32_768) + "Holmes\n Watson x Holmes\n Holmes y Watson";
        var input = Encoding.UTF8.GetBytes(text);
        var floor = Encoding.UTF8.GetByteCount(prefix);
        using var regex = new Utf8Pcre2Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1 << 20 },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        using var live = new CancellationTokenSource();
        Assert.Equal(2, regex.Count(input, floor, Pcre2MatchOptions.None, live.Token));
        Assert.Equal(0, regex.Count(input, floor, Pcre2MatchOptions.Anchored, live.Token));
    }

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4096)]
    public void BoundedPublicSearchKeepsOriginalTokenAndWorkLimit(int prefix)
    {
        var regex = new Utf8Pcre2Regex(Pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 64,
                MaxWorkingBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal((byte)'a', direct.Fallback.LeadingAsciiByte);
        var input = Encoding.UTF8.GetBytes(new string('x', prefix) + "abc123");
        Assert.True(regex.IsMatch(input, 0));
        using var live = new CancellationTokenSource(); live.Cancel();
        Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(input, 0, Pcre2MatchOptions.None, live.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScanQuantaPreserveOriginalInterruptionAndBorrowedLoans(bool expire)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = new Utf8Pcre2Regex(Pattern);
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var input = Encoding.UTF8.GetBytes(new string('x', 4096) + "abc123");
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(256, 1 << 20), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        for (var i = 0; i < 63; i++) _ = invocation.CheckProgress();
        invocation.CheckpointHook = _ => { if (expire) ticks = 100; else live.Cancel(); };
        Exception? failure = null;
        try { _ = Pcre2SliceDriver.FindFirst(direct.SliceProgram, input, 0, direct.Fallback.MinimumByteLength, ref invocation, direct.Fallback.LeadingAsciiByte); }
        catch (Exception error) { failure = error; }
        if (expire) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        else Assert.Equal(live.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
    }
}
