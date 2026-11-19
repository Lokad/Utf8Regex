using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Lokad.Utf8Regex.Tests;

/// <summary>Checks scalar route selection and shared capture metadata.</summary>
public sealed class Utf8SliceWiringTests
{
    private static readonly RegexOptions[] Selections =
    [
        RegexOptions.CultureInvariant,
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        RegexOptions.None,
    ];

    [Theory]
    // Basic shapes
    [InlineData("(a+)(b+)")]
    [InlineData("(ab|cd)+")]
    [InlineData("(a|ab)+c")]
    [InlineData("(ab|a)+z")]
    [InlineData("(a{2,3}b+)|(c+d)")]
    [InlineData("a*?b")]
    [InlineData("[a-z]+")]
    [InlineData("([0-9]){1,3}?x")]
    // Atomic groups
    [InlineData("(?>a+)+")]
    [InlineData("(?>a|ab)+c")]
    [InlineData("x(?>y|y)z")]
    [InlineData("(?>(a+))b")]
    [InlineData("(?>\\p{L}+)1")]
    // Backreferences
    [InlineData("(a)\\1")]
    [InlineData("\\1(a)")]
    [InlineData("(\\2a|(b))+")]
    [InlineData("(a|b)+\\1")]
    [InlineData("(a+)\\1")]
    [InlineData("(a)(b)\\2\\1")]
    [InlineData("((a)b)\\1")]
    [InlineData("(a*)\\1")]
    [InlineData("(x)?b\\1")]
    [InlineData("([a-z]+)\\1")]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)\\8")]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)(i)\\9")]
    // Balancing groups
    [InlineData("(?<open>a)+(?<-open>b)+")]
    [InlineData("(?<open>a)(?<-open>b)")]
    [InlineData("(?<a>x)(?<b-a>y)")]
    [InlineData("(?<a>x)(?<b-a>y)(?<c-b>z)")]
    [InlineData("((?<o>a)+(?<-o>b)+)")]
    [InlineData("(?<o>a)(?<-o>b)?b")]
    [InlineData("(?<a>x)(?'b-a'y)")]
    [InlineData("(?<o>\\p{L}+)(?<-o>\\d+)")]
    [InlineData("(?<o>\\p{L}+)(?<-o>\\p{Nd}+)")]
    // Contiguous anchors
    [InlineData("\\Ga+")]
    [InlineData("\\Ga*")]
    [InlineData("\\Gx*|y")]
    [InlineData("\\G(?:a|b)+")]
    // Escapes
    [InlineData("\\x41+")]
    [InlineData("\\u0041+")]
    [InlineData("\\01+")]
    [InlineData("[\\x41-\\x5A]+")]
    [InlineData("[\\101-\\132]+")]
    [InlineData("[\\u0041]+")]
    [InlineData("\\n+")]
    [InlineData("\\t+")]
    [InlineData("\\r+")]
    [InlineData("\\a+")]
    [InlineData("\\f+")]
    [InlineData("\\v+")]
    [InlineData("\\e+")]
    [InlineData("\\cA+")]
    [InlineData("\\cZ+")]
    [InlineData("(?<=\\n)x")]
    // Compound patterns
    [InlineData("[\\da]+")]
    [InlineData("([\\da]+)")]
    [InlineData("[^\\da]+")]
    [InlineData("[\\d\\s]+")]
    [InlineData("[\\w-]+")]
    [InlineData("[\\d-]+")]
    [InlineData("[-\\d]+")]
    [InlineData("[a\\d]+")]
    [InlineData("[\\d-e]+")]
    [InlineData("[\\Da]+")]
    [InlineData("[^\\D\\s]+")]
    // Lookaround
    [InlineData("(?=a)a")]
    [InlineData("(?!a)b")]
    [InlineData("(?<=a)b")]
    [InlineData("(?<!a)b")]
    [InlineData("((?=a)a)")]
    [InlineData("x(?=a*)")]
    [InlineData("x(?=[a-z]*)")]
    [InlineData("x(?=\\p{L}*)")]
    [InlineData("(?=(a)(b))")]
    // Named groups
    [InlineData("(?<word>a)+")]
    [InlineData("(?<a>x)+")]
    [InlineData("(?<a>x)(?<b>y)+")]
    [InlineData("(?<b>a)(c)+")]
    [InlineData("((?<a>x)+)")]
    [InlineData("(?<word>\\d+)+")]
    [InlineData("(?<b>a)(c)\\2")]
    [InlineData("(?<a>ab|cd)+")]
    // Noncapturing groups
    [InlineData("(?:a)+")]
    [InlineData("(?:a|b)+c")]
    [InlineData("(a)(?:b)\\1")]
    [InlineData("(?:a)+(?:b)+")]
    [InlineData("((?:ab)+c)")]
    // Split patterns
    [InlineData("a+")]
    [InlineData("x*|y")]
    [InlineData("\\d+")]
    [InlineData("(?:cat|horse)+")]
    [InlineData("(a)(?:b)(c)+")]
    public void SliceShapesRouteOwned(string pattern)
    {
        foreach (var options in Selections)
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    [Theory]
    [InlineData("(a|ab)c")]
    [InlineData("a{2,3}b")]
    [InlineData("a.c")]
    public void ShapesKeepPublicSemantics(string pattern)
    {
        using var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var text in new[] { "", "a", "1", "a1b", "word", ":", "é", "abc", "aabc", "aaab" })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(bytes));
            Assert.Equal(oracle.Count(text), regex.Count(bytes));
        }
    }

    [Theory]
    [InlineData("(a+)(b+)", "xxaaabbyy", true)]
    [InlineData("(a+)(b+)", "xxaaayy", false)]
    [InlineData("(a+)(b+)", "xxéaaabbé", true)]
    [InlineData("(ab|cd)+", "xxabcdcdyy", true)]
    [InlineData("(a|ab)+c", "xxabcabccyy", true)]
    [InlineData("(a|ab)+c", "xxacyy", true)]
    [InlineData("(a|ab)+c", "xxabyy", false)]
    [InlineData("(ab|a)+z", "ababaz", true)]
    [InlineData("a*?b", "xxaaabyy", true)]
    [InlineData("(a{2,3}b+)|(c+d)", "xxaabbbyy", true)]
    [InlineData("(a{2,3}b+)|(c+d)", "xxabyy", false)]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in Selections)
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
        }
    }

    [Theory]
    [InlineData("(a+)(b+)", "aab aabb aaabbb", 3)]
    [InlineData("(ab|cd)+", "ab cd abcd", 3)]
    [InlineData("a*?b", "ab aab b", 3)]
    [InlineData("(a|ab)+c", "xxabcabccyy", 2)]
    [InlineData("(a+)(b+)", "ääaabbéé", 1)]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        foreach (var options in Selections)
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(expected, regex.Count(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.Count(input, source.Token));
        }
    }

    [Fact]
    public void OwnedOperationsShareCaptureCatalog()
    {
        var regex = new Utf8Regex("(a+)(b+)", options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xxaaabbyy zz");
        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));
        Assert.True(regex.Match(input).Success);
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(detailed.Success);
        Assert.Equal("aaa", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        var enumerated = 0;
        foreach (var item in regex.EnumerateMatches(input))
        {
            enumerated++;
            Assert.True(item.Success);
        }

        Assert.Equal(1, enumerated);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedIsMatch()
    {
        var regex = new Utf8Regex("(a+)(b+)", options: RegexOptions.CultureInvariant);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var exception = Assert.Throws<OperationCanceledException>(
            () => regex.IsMatch(Encoding.UTF8.GetBytes("xxaaabbyy"), canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
    }
}
