using System.Globalization;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BudgetedCategoryCountTests
{
    [Fact]
    public void MathCountSharesFiniteAuthorityWithoutScratch()
    {
        const string prefix = "é!";
        var text = string.Concat(Enumerable.Repeat("Aé中∑𝛁😀+9\u0378 ", 512));
        var input = Encoding.UTF8.GetBytes(prefix + text);
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var expected = text.EnumerateRunes().Count(rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.MathSymbol);
        using var token = new CancellationTokenSource();
        {
            var regex = new Utf8Pcre2Regex(@"\p{Sm}", options: default, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new()
                {
                    MaxDepth = 1,
                    MaxWorkingBytes = 1,
                    MaxResultBytes = 1
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
            var allowance = new RejectStorage();
            Assert.Equal(expected, regex.Count(input, floor, default, token.Token, allowance));
            Assert.Equal(0, allowance.Attempts);
        }
    }

    [Theory]
    [InlineData(@"\p{S m}", "A+∑𝛁_😀", 3)]
    [InlineData(@"(?<unit>[\p{Sm}_])", "A+∑𝛁_😀", 4)]
    [InlineData(@"[^\P{L&}]", "Aaǅ中𝓐+", 4)]
    [InlineData(@"\P{S}", "A+∑𝛁_😀", 2)]
    [InlineData(@"\p{Xan}", "é٣中𝕒𝛁_😀", 4)]
    public void CategoryMasksPreserveAliasesUnionsAndComplements(string pattern, string text, int matches)
    {
        const string prefix = "Aé😀";
        var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(text, 512)));
        var floor = Encoding.UTF8.GetByteCount(prefix);
        using var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Ucp, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 4096 }, MatchTimeout = Timeout.InfiniteTimeSpan,
        });
        Assert.Equal(matches * 512, regex.Count(input, floor));
    }

    [Theory]
    [InlineData(Pcre2CompileOptions.Anchored, @"\p{Sm}", "x+∑", 0)]
    [InlineData(Pcre2CompileOptions.EndAnchored, @"\p{Sm}", "+∑x", 0)]
    [InlineData(Pcre2CompileOptions.FirstLine, @"\p{Sm}", "x+\n+∑", 1)]
    [InlineData(Pcre2CompileOptions.Caseless, @"\p{Lu}", "aAǅ+", 3)]
    [InlineData(Pcre2CompileOptions.None, @"\p{Xps}", "é\v\u0085😀", 2)]
    public void CategoryCountPreservesProfileOptions(Pcre2CompileOptions options, string pattern, string text, int matches)
    {
        using var regex = new Utf8Pcre2Regex(pattern, options: options);
        Assert.Equal(matches, regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void CategoryCountKeepsRealWorkDenialAndOriginalCancellation()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("a+∑😀", 1 << 16)));
        var limited = new Utf8Pcre2Regex(@"\p{Sm}", options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxDepth = 4096
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        using var token = new CancellationTokenSource(); token.Cancel();
        var unlimited = new Utf8Pcre2Regex(@"\p{Sm}");
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            unlimited.Count(input, 0, default, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CategoryCountChecksOriginalInterruptionBetweenBlocks(bool cancel)
    {
        var regex = new Utf8Pcre2Regex(@"\p{Sm}", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = Encoding.ASCII.GetBytes(new string('+', 16384));
        var reads = 0;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads < 4) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 20;
        });
        Exception? failure = null;
        try { _ = regex.Count(input, 0, default, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.True(reads >= 4);
    }

    private sealed class RejectStorage : IUtf8MemoryAllowance
    {
        internal int Attempts;
        public bool TryReserve(ulong bytes) { Attempts++; return false; }
        public void Release(ulong bytes) => throw new InvalidOperationException("No loan was admitted.");
    }
}
