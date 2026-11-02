using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLiteralRepeatFullMatchTests
{
    private static Utf8ResourcePolicy Small => new()
    {
        MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1,
    };

    [Theory]
    [InlineData("ab", "+")]
    [InlineData("Шерлок ", "+")]
    [InlineData("é😀A", "+")]
    [InlineData("\0é", "{1,3}")]
    [InlineData("abc", "{2,4}")]
    [InlineData("é", "{1,1000000}")]
    public void WholeSuffixResultsPreserveBoundsScalarCoordinatesAndUnalignedStorage(string period, string quantifier)
    {
        var pattern = "(?:" + Regex.Escape(period) + ")" + quantifier;
        var oracle = new Regex(@"\G(?:" + pattern + @")\z");
        foreach (var controlled in new[] { false, true })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                controlled ? new() { MaxWorkSteps = 1000000, MaxDepth = 10000,
                    MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 } : default);
            using var live = new CancellationTokenSource();
            foreach (var count in new[] { 0, 1, 2, 3, 4, 15, 16, 31, 32, 63, 64, 65 })
            foreach (var prefix in new[] { "", "π😀" })
            foreach (var suffix in new[] { "", "!" })
            {
                var body = string.Concat(Enumerable.Repeat(period, count)) + suffix;
                var text = prefix + body;
                var bytes = Encoding.UTF8.GetBytes(text);
                byte[] storage = [0xff, .. bytes, 0xff];
                var start = Encoding.UTF8.GetByteCount(prefix);
                var expected = oracle.Match(text, prefix.Length).Success;
                var match = regex.FullMatch(storage.AsSpan(1, bytes.Length), start,
                    controlled ? live.Token : default);
                Assert.Equal(expected, match.Success);
                if (!expected) continue;
                Assert.Equal((start, bytes.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
                Assert.Equal((prefix.Length, text.Length), (match.StartOffsetInUtf16, match.EndOffsetInUtf16));
                Assert.Equal(body, match.GetValueString());
            }
        }
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Шерлок ")]
    [InlineData("😀A")]
    public void ValueMatchingBorrowsExistingLiteralWithoutColdPreparationOrMajorScratch(string period)
    {
        var regex = new Utf8PythonRegex("(?:" + period + ")+", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Small);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(period, 65)));
        using var denied = new Utf8ConstructionScope(0, 0, 0, 0);
        using var live = new CancellationTokenSource();
        var match = regex.FullMatch(input, 0, live.Token);
        Assert.True(match.Success);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Assert.Equal(0UL, denied.Budget.ReservedBytes);
        Assert.Equal(0UL, denied.Budget.WorkSteps);
        Assert.Equal(Encoding.UTF8.GetString(input), match.GetValueString());
    }

    [Theory]
    [InlineData("(?:ab)+?", "abab")]
    [InlineData("(ab)+", "abab")]
    [InlineData("(?:ab|cd)+", "abcd")]
    [InlineData("(?i:(?:ab)+)", "ABab")]
    [InlineData("(?:ab)+c", "ababc")]
    [InlineData("(?:ab)*", "")]
    public void UnsupportedRootsKeepFullVerificationAndCaptures(string pattern, string text)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.FullMatch(input).Success);
        var detailed = regex.FullMatchDetailedData(input);
        Assert.True(detailed.Success);
        Assert.Equal(text, detailed.Groups[0].ValueText);
        if (pattern == "(ab)+") Assert.Equal("ab", detailed.Groups[1].ValueText);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
    }

    [Fact]
    public void EveryValidBoundaryAndMalformedPrefixOrTailRetainsItsAuthority()
    {
        var regex = new Utf8PythonRegex("(?:é😀)+");
        var bytes = Encoding.UTF8.GetBytes("πé😀é😀");
        for (var start = 0; start <= bytes.Length; start++)
        {
            if (start < bytes.Length && (bytes[start] & 0xc0) == 0x80)
                Assert.Throws<ArgumentOutOfRangeException>(() => regex.FullMatch(bytes, start));
            else
                Assert.Equal(start is 2 or 8, regex.FullMatch(bytes, start).Success);
        }
        foreach (var input in new byte[][] { [0xff, .. "é😀"u8], [.. "é😀"u8, 0xff], [0xff, (byte)'x'] })
            Assert.Throws<ArgumentException>(() => regex.FullMatch(input, input[0] == 0xff ? 1 : 0));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.FullMatch([0xff], 0, canceled.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(1UL)] [InlineData(4UL)] [InlineData(16UL)]
    public void WorkDenialIsNotNoMatchAndFreshInvocationsRecover(ulong work)
    {
        var regex = new Utf8PythonRegex("(?:Шерлок )+", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Small with { MaxWorkSteps = work });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Шерлок ", 1024)));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Assert.True(new Utf8PythonRegex("(?:Шерлок )+", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Small).FullMatch(input).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ComparisonBlocksObserveOriginalCancellationAndDeadline(bool expire)
    {
        const string pattern = "(?:Шерлок )+";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Шерлок ", 4096)));
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20), Small);
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var called = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            called = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire)
            {
                var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.FullMatch(input, 0, token.Token));
                Assert.Equal(pattern, error.Pattern); Assert.Empty(error.Input);
            }
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.FullMatch(input, 0, token.Token)).CancellationToken);
        }
        finally { diagnostics.Complete(); }
        Assert.True(called); ticks = 0;
        Assert.True(regex.FullMatch(input).Success);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalPublicationPollsTokenAfterItsLastDeadlineRead(bool success)
    {
        var regex = new Utf8PythonRegex("(?:ab)+", PythonReCompileOptions.None, TimeSpan.FromSeconds(30), Small);
        var input = Encoding.UTF8.GetBytes(success ? "abab" : "abax");
        var totalReads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { totalReads++; return 0; }))
            Assert.Equal(success, regex.FullMatch(input).Success);
        using var token = new CancellationTokenSource();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == totalReads) token.Cancel(); return 0; });
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.FullMatch(input, 0, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeclinedRepeatProofKeepsColdPreparationAuthority(bool expire)
    {
        const string pattern = "(?:ab)+c";
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20));
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        Action<ulong> hook = _ =>
        {
            if (++checkpoints != 3) return;
            if (expire) ticks = 50; else token.Cancel();
        };
        construction.Budget.CheckpointHook = hook;
        if (expire)
        {
            var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.FullMatch("ababc"u8, 0, token.Token));
            Assert.Equal(pattern, error.Pattern); Assert.Equal("ababc", error.Input);
        }
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.FullMatch("ababc"u8, 0, token.Token)).CancellationToken);
        Assert.Equal(3, checkpoints);
        Assert.Same(hook, construction.Budget.CheckpointHook);
        Assert.Equal(0UL, construction.Budget.ReservedBytes);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        ticks = 0;
        Assert.True(regex.FullMatch("ababc"u8).Success);
    }

    [Fact]
    public void FundedClosureKeepsParentLoanAndBorrowedValueValid()
    {
        var account = new Allowance(16 << 20); Assert.True(account.TryReserve(13));
        using var regex = Utf8PythonRegex.Compile("(?:Шерлок )+", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Small, default, Timeout.InfiniteTimeSpan, default, account);
        var held = account.Used; account.Maximum = held;
        var input = "Шерлок Шерлок "u8.ToArray();
        var match = regex.FullMatch(input);
        Assert.True(match.Success); Assert.Equal(held, account.Used);
        regex.Dispose(); Assert.Equal(13UL, account.Used);
        Assert.Equal("Шерлок Шерлок ", match.GetValueString());
        Assert.Throws<ObjectDisposedException>(() => regex.FullMatch(input));
        account.Release(13);
    }
}
