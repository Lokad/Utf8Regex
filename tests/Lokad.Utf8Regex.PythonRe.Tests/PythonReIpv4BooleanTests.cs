using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReIpv4BooleanTests
{
    private const string Pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
    private static readonly Utf8ResourcePolicy Small = new()
        { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };

    [Theory]
    [InlineData("012.200.033.199", 0, true)]
    [InlineData("012.200.033.19A", 0, true)]
    [InlineData("666.666.666.666", 0, false)]
    [InlineData("1.2.3.4", 0, false)]
    [InlineData("99.99.99.99", 0, true)]
    [InlineData("255.255.255.255", 0, true)]
    [InlineData("256.255.255.255", 0, true)] // Unanchored suffix starts at 56.
    [InlineData("é😀012.200.033.199", 2, true)]
    [InlineData("é😀012.200.033.199", 6, true)]
    [InlineData("é😀012.200.033.199", 21, false)]
    [InlineData("０１２.２００.０３３.１９９", 0, false)]
    [InlineData("", 0, false)]
    public void CompleteAsciiGrammarNeedsNoVmOrResultStorage(string text, int offset, bool expected)
    {
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { PythonReCompileOptions.None, PythonReCompileOptions.Ascii })
        {
            using var regex = new Utf8PythonRegex(Pattern, options, Timeout.InfiniteTimeSpan, Small);
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(expected, regex.IsMatch(input, offset));
            Assert.Equal(expected, regex.IsMatch(input, offset, token.Token));
        }
    }

    [Theory]
    [InlineData(2029)] [InlineData(2033)] [InlineData(2034)] [InlineData(2047)] [InlineData(4095)]
    public void CrossingWindowsKeepScalarStartAndPartialHit(int padding)
    {
        using var regex = new Utf8PythonRegex(Pattern, default, Timeout.InfiniteTimeSpan, Small);
        var input = Encoding.UTF8.GetBytes("😀" + new string('x', padding) + "012.200.033.19A😀");
        Assert.True(regex.IsMatch(input, 4));
        Assert.True(regex.IsMatch(input, 4 + padding + 1)); // Legal two-digit first octet.
        Assert.False(regex.IsMatch(input, 4 + padding + 2));
    }

    [Fact]
    public void WholeSubjectValidationAndEntryFailuresArePreserved()
    {
        using var regex = new Utf8PythonRegex(Pattern, default, Timeout.InfiniteTimeSpan, Small);
        foreach (var input in new[] { "012.200.033.199"u8.ToArray().Concat(new byte[] { 0xff }).ToArray(),
            new byte[] { 0xff }.Concat("012.200.033.199"u8.ToArray()).ToArray() })

        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch("😀012.200.033.199"u8, 1));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch("😀"u8, 1, canceled.Token)).CancellationToken);
        using var denied = new Utf8PythonRegex(Pattern, default, Timeout.InfiniteTimeSpan,
            Small with { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.Throws<Utf8ResourceLimitExceededException>(
            () => denied.IsMatch("012.200.033.199"u8)).ResourceKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalAuthorityInterruptsDiscovery(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(Pattern, default, TimeSpan.FromMilliseconds(20), Small);
        var input = Encoding.UTF8.GetBytes(new string('x', 262144) + "012.200.033.199");
        var interrupted = false;
        var diagnostic = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            interrupted = true;
            if (expire) ticks = 30; else token.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.IsMatch(input, 0, token.Token));
            Assert.True(interrupted);
            if (expire) Assert.Equal(TimeSpan.FromMilliseconds(20), Assert.IsType<RegexMatchTimeoutException>(failure).MatchTimeout);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { diagnostic.Complete(); }
    }

    [Theory]
    [InlineData("^", "x012.200.033.199", false)]
    [InlineData("", "012.200.033.199", true)]
    public void OtherRootsKeepAssertionsAndRetainedCaptureResults(string prefix, string text, bool expected)
    {
        using var regex = new Utf8PythonRegex(prefix + "(?P<address>" + Pattern + ")");
        var input = Encoding.UTF8.GetBytes(text);
        var held = regex.SearchDetailedData(input);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected, held.Success);
        regex.Dispose();
        if (expected) Assert.Equal(text, held.Groups[1].ValueText);
    }

    [Fact]
    public void RetainedCoreSharesAdmittedProgramLifetimeAndRefundsTemporaryWork()
    {
        var account = new Allowance(16 << 20);
        Assert.True(account.TryReserve(13));
        using var regex = Utf8PythonRegex.Compile(Pattern, default, Timeout.InfiniteTimeSpan,
            Small, default, Timeout.InfiniteTimeSpan, default, account);
        var held = account.Used;
        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.IsMatch("012.200.033.199"u8));
        Assert.False(regex.IsMatch("1.2.3.4"u8));
        Assert.Equal(held, account.Used);
        regex.Dispose(); Assert.Equal(13UL, account.Used);
        Assert.Throws<ObjectDisposedException>(() => regex.IsMatch("012.200.033.199"u8));
        account.Release(13); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void CursorAliasAndSnapshotSurviveFundedProgramClosure()
    {
        var account = new Allowance(16 << 20);
        using var regex = Utf8PythonRegex.Compile(Pattern, default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, account);
        using var cursor = regex.CreateMatchCursor("😀012.200.033.199 x 99.99.99.99"u8.ToArray());
        var alias = cursor;
        regex.Dispose(); Assert.True(account.Used > 0);
        Assert.True(alias.MoveNext(out var retained));
        Assert.Equal("012.200.033.199", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.True(cursor.MoveNext(out _));
        Assert.False(alias.MoveNext(out _));
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal("012.200.033.199", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }
}
