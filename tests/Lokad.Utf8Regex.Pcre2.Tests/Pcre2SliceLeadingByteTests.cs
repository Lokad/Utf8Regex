using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

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
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, 7));
    }

    [Fact]
    public void LastScanCannotPublishNoMatchAfterOriginalCancellation()
    {
        var regex = new Utf8Pcre2Regex(Pattern);
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var input = Utf8ValidatedInput.Create(Encoding.UTF8.GetBytes(new string('x', 2048)));
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, live.Token);
        invocation.ShareStorageForCursor(); Assert.True(invocation.Ledger.TryReserve(13));
        for (var i = 0; i < 63; i++) _ = invocation.TryChargeStep();
        invocation.CheckpointHook = _ => live.Cancel();
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, invocation, input,
            new(0), regex.DebugCompiledProgram.Request, direct.Fallback.LeadingAsciiByte);
        OperationCanceledException? failure = null;
        try { _ = cursor.MoveNext(); } catch (OperationCanceledException error) { failure = error; }
        Assert.Equal(live.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.False(cursor.Current.Success);
    }

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4096)]
    public void MissingLeadingByteAdmitsBoundedScansEvenWithoutPublicLimits(int length)
    {
        var regex = new Utf8Pcre2Regex(Pattern);
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var input = Utf8ValidatedInput.Create(Encoding.UTF8.GetBytes(new string('x', length)));
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        invocation.ShareStorageForCursor(); Assert.True(invocation.Ledger.TryReserve(13));
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, invocation, input,
            new(0), regex.DebugCompiledProgram.Request, direct.Fallback.LeadingAsciiByte);
        Assert.False(cursor.MoveNext());
        Assert.Equal((ulong)(length + 2047) / 2048, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        if (length <= 2048) return;
        var limited = new Utf8SliceInvocation(new(1, 0, 0), Timeout.InfiniteTimeSpan, default);
        limited.ShareStorageForCursor(); Assert.True(limited.Ledger.TryReserve(13));
        var denied = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, limited, input,
            new(0), regex.DebugCompiledProgram.Request, direct.Fallback.LeadingAsciiByte);
        Pcre2MatchException? error = null;
        try { _ = denied.MoveNext(); } catch (Pcre2MatchException failure) { error = failure; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(error).ErrorKind);
        Assert.Equal(2UL, limited.Budget.Steps);
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

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4096)]
    public void BoundedPublicSearchKeepsStrictValidationAndUsefulLimits(int prefix)
    {
        var regex = new Utf8Pcre2Regex(Pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 512,
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
        var regex = new Utf8Pcre2Regex(Pattern);
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var input = Utf8ValidatedInput.Create(Encoding.UTF8.GetBytes(new string('x', 4096) + "abc123"));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(100000, 256, 1 << 20), TimeSpan.FromMilliseconds(50), live.Token);
        invocation.ShareStorageForCursor();
        Assert.True(invocation.Ledger.TryReserve(13));
        for (var i = 0; i < 63; i++) _ = invocation.TryChargeStep();
        invocation.CheckpointHook = _ => { if (expire) ticks = 100; else live.Cancel(); };
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, invocation, input,
            new(0), regex.DebugCompiledProgram.Request, direct.Fallback.LeadingAsciiByte);
        Exception? failure = null;
        try { _ = cursor.MoveNext(); }
        catch (Exception error) { failure = error; }
        if (expire) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        else Assert.Equal(live.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL + 2 * Utf8ConstructionBudget.ArrayBytes<int>(direct.SliceProgram.GroupCount),
            invocation.Budget.PeakWorkingBytes);
        Assert.False(cursor.Current.Success);
    }
}
