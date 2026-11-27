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
        MaxDepth = 1,
        MaxWorkingBytes = 1,
        MaxResultBytes = 1
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
            var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = controlled ? new()
                {
                    MaxDepth = 10000,
                    MaxWorkingBytes = 1 << 20,
                    MaxResultBytes = 1 << 20
                }

    : default
            });
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
                        var match = regex.Match(storage.AsSpan(1, bytes.Length), start, cancellationToken: controlled ? live.Token : default, mode: Utf8PythonMatchMode.FullMatch);
                        Assert.Equal(expected, match.Success);
                        if (!expected) continue;
                        Assert.Equal((start, bytes.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
                        Assert.Equal(body, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
                    }
        }
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Шерлок ")]
    [InlineData("😀A")]
    public void ValueMatchingUsesOwnedLiteralInstructionsWithoutColdPreparationOrMajorScratch(string period)
    {
        var regex = new Utf8PythonRegex("(?:" + period + ")+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Small
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(period, 65)));
        using var denied = new Utf8ConstructionScope(0, 0, 0);
        using var live = new CancellationTokenSource();
        var match = regex.Match(input, 0, cancellationToken: live.Token, mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(match.Success);
        Assert.Equal(0UL, denied.Budget.ReservedBytes);
        Assert.Equal(Encoding.UTF8.GetString(input), System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Theory]
    [InlineData("(?:ab)+?", "abab")]
    [InlineData("(ab)+", "abab")]
    [InlineData("(?:ab|cd)+", "abcd")]
    [InlineData("(?i:(?:ab)+)", "ABab")]
    [InlineData("(?:ab)+(?!x)", "abab")]
    [InlineData("(?:ab)*", "")]
    public void UnsupportedRootsKeepFullVerificationAndCaptures(string pattern, string text)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch detailed = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out detailed);
        }
        Assert.True(detailed.Success);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString((detailed.Groups[0].Success ? detailed.GetGroupMemory(detailed.Groups[0].Number).Span : ReadOnlySpan<byte>.Empty)));
        if (pattern == "(ab)+") Assert.Equal("ab", System.Text.Encoding.UTF8.GetString((detailed.Groups[1].Success ? detailed.GetGroupMemory(detailed.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("ab", "c", "+")]
    [InlineData("ab", "ab", "{1,3}")]
    [InlineData("é😀", "中Ω", "{2,4}")]
    [InlineData("\0é", "\0", "{1,1000000}")]
    [InlineData("😀", "é😀", "+")]
    public void FixedLiteralTailsPreserveFullSuffixAndScalarCoordinates(string period, string tail, string quantifier)
    {
        var pattern = "(?:" + Regex.Escape(period) + ")" + quantifier + Regex.Escape(tail);
        var oracle = new Regex(@"\G(?:" + pattern + @")\z");
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Small
        });
        using var live = new CancellationTokenSource();
        foreach (var count in new[] { 0, 1, 2, 3, 4, 15, 16, 31, 32, 63, 64, 65 })
            foreach (var prefix in new[] { "", "π😀" })
                foreach (var candidate in new[] {
            string.Concat(Enumerable.Repeat(period, count)) + tail,
            string.Concat(Enumerable.Repeat(period, count)) + tail + "!",
            string.Concat(Enumerable.Repeat(period, count)),
            string.Concat(Enumerable.Repeat(period, count)) + "x" + tail })
                {
                    var text = prefix + candidate;
                    var bytes = Encoding.UTF8.GetBytes(text);
                    byte[] storage = [0xff, .. bytes, 0xff];
                    var start = Encoding.UTF8.GetByteCount(prefix);
                    var expected = oracle.Match(text, prefix.Length).Success;
                    var match = regex.Match(storage.AsSpan(1, bytes.Length), start, cancellationToken: live.Token, mode: Utf8PythonMatchMode.FullMatch);
                    Assert.Equal(expected, match.Success);
                    if (!expected) continue;
                    Assert.Equal((start, bytes.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
                    Assert.Equal(candidate, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
                }
    }

    [Theory]
    [InlineData(59)]
    [InlineData(60)]
    public void FixedStackProofBoundaryRetainsUnicodeAndFallbackCorrectness(int periodScalars)
    {
        var period = string.Concat(Enumerable.Range(0, periodScalars).Select(i => new Rune(0x1f600 + i).ToString()));
        var regex = new Utf8PythonRegex("(?:" + period + ")+🚀", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = periodScalars == 59 ? Small : default
        });
        var text = period + period + "🚀";
        var input = Encoding.UTF8.GetBytes(text);
        var match = regex.Match(input, mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(match.Success);
        Assert.Equal((0, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryScalarBoundaryAndOriginalCancellationRetainTheirAuthority(bool hasTail)
    {
        var regex = new Utf8PythonRegex("(?:é😀)+" + (hasTail ? "Ω" : ""));
        var bytes = Encoding.UTF8.GetBytes("πé😀é😀" + (hasTail ? "Ω" : ""));
        for (var start = 0; start <= bytes.Length; start++)
        {
            if (start < bytes.Length && (bytes[start] & 0xc0) == 0x80)
                Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(bytes, start, mode: Utf8PythonMatchMode.FullMatch));
            else
                Assert.Equal(start is 2 or 8, regex.Match(bytes, start, mode: Utf8PythonMatchMode.FullMatch).Success);
        }

        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Match(bytes, 0, cancellationToken: canceled.Token, mode: Utf8PythonMatchMode.FullMatch)).CancellationToken);
    }

    [Theory]
    [InlineData("(?:ab)+c", "axabc")]
    [InlineData("(?:é😀)+Ω", "é😁é😀Ω")]
    public void LiteralTailCannotHideAWrongPeriodOfTheSameByteLength(string pattern, string text)
    {
        var regex = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Small
        });
        Assert.False(regex.Match(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ComparisonBlocksObserveOriginalCancellationAndDeadline(bool expire, bool hasTail)
    {
        var pattern = "(?:Шерлок )+" + (hasTail ? "Ω" : "");
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Шерлок ", 4096)) + (hasTail ? "Ω" : ""));
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Small
        });
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
                var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.FullMatch));
                Assert.Equal(pattern, error.Pattern); Assert.Empty(error.Input);
            }
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.FullMatch)).CancellationToken);
        }
        finally { diagnostics.Complete(); }
        Assert.True(called); ticks = 0;
        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FinalPublicationPollsTokenAfterItsLastDeadlineRead(bool success, bool hasTail)
    {
        var regex = new Utf8PythonRegex("(?:ab)+" + (hasTail ? "c" : ""), options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromSeconds(30),
            ResourcePolicy = Small
        });
        var input = Encoding.UTF8.GetBytes((success ? "abab" : "abax") + (hasTail ? "c" : ""));
        var totalReads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { totalReads++; return 0; }))
            Assert.Equal(success, regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        using var token = new CancellationTokenSource();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == totalReads) token.Cancel(); return 0; });
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.FullMatch)).CancellationToken);
    }

    [Theory]
    [InlineData("(?:Шерлок )+", "Шерлок Шерлок ")]
    [InlineData("(?:ab)+c", "ababc")]
    [InlineData("(?:é😀){1,4}Ω", "é😀é😀Ω")]
    public void FundedClosureKeepsParentLoanAndBorrowedValueValid(string pattern, string text)
    {
        var account = new Allowance(16 << 20); Assert.True(account.TryReserve(13));
        using var regex = Utf8PythonRegex.Compile(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Small,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = account
        }, cancellationToken: default);
        var held = account.Used; account.Maximum = held;
        var input = Encoding.UTF8.GetBytes(text);
        var match = regex.Match(input, mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(match.Success); Assert.Equal(held, account.Used);
        regex.Dispose(); Assert.Equal(13UL, account.Used);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Throws<ObjectDisposedException>(() => regex.Match(input, mode: Utf8PythonMatchMode.FullMatch));
        account.Release(13);
    }
}
