using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

/// <summary>R02a slice recognition: ASCII literals, concatenation, ordered
/// alternation, greedy/lazy repetition, and numbered groups lower to owned
/// programs (R03 adds dots with Singleline-scoped newline matching); every
/// other construct or sensitive option declines so existing
/// routes preserve behavior including construction failures.</summary>
public sealed class Utf8SliceParserTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("(a+)(b+)", 2)]
    [InlineData("(ab|cd)+", 1)]
    [InlineData("(a|ab)c", 1)]
    [InlineData("a*?b", 0)]
    [InlineData("a{2,3}b", 0)]
    [InlineData("a{2,}b", 0)]
    [InlineData("(a|)", 1)]
    [InlineData("()", 1)]
    [InlineData(@"a\+b", 0)]
    [InlineData("a{0}b", 0)]
    [InlineData("a.c", 0)]
    [InlineData("a.*b", 0)]
    [InlineData("(a.c)+", 1)]
    [InlineData("^ab", 0)]
    [InlineData("ab$", 0)]
    [InlineData("^a.*b$", 0)]
    [InlineData("^(a.c)+$", 1)]
    [InlineData("a^b", 0)]
    [InlineData("[a-z]", 0)]
    [InlineData("[^a-z]", 0)]
    [InlineData("[a-c-e]", 0)]
    [InlineData("[A-z]", 0)]
    [InlineData("[\\b]", 0)]
    [InlineData("[]a]", 0)]
    [InlineData("[-a]", 0)]
    [InlineData("[[a]", 0)]
    [InlineData("[^]a]", 0)]
    [InlineData("\\b", 0)]
    [InlineData("\\B", 0)]
    [InlineData("\\Aab", 0)]
    [InlineData("ab\\z", 0)]
    [InlineData("ab\\Z", 0)]
    [InlineData("\\bword\\b", 0)]
    [InlineData("x\\by", 0)]
    [InlineData("(a)\\1", 1)]
    [InlineData("(?=a)a", 0)]
    [InlineData("((?=a)a)", 1)]
    [InlineData("(?!a)b", 0)]
    [InlineData("(?<=a)b", 0)]
    [InlineData("(?<!a)b", 0)]
    [InlineData("(?=(a)(b))", 2)]
    [InlineData("(?<word>a)", 1)]
    [InlineData("(?'word'a)", 1)]
    [InlineData("(?<a>x)(b)(?<c>y)", 3)]
    [InlineData("(?<a>(b))", 2)]
    [InlineData("(?<o>a)(?<-o>b)", 1)]
    [InlineData("(?<a>x)(?<b-a>y)", 2)]
    [InlineData("(?<open>a)(?<-open>b)", 1)]
    [InlineData("(a)(b)\\2\\1", 2)]
    [InlineData("(?:a)", 0)]
    [InlineData("(?:[a-z]+)", 0)]
    [InlineData("(?#comment)", 0)]
    [InlineData("(?#)", 0)]
    [InlineData("(a)(?:b)(c)", 2)]
    [InlineData("(?:a)+", 0)]
    [InlineData("(?:a|b)+c", 0)]
    [InlineData("a(?#middle)b", 0)]
    [InlineData("(?>a)", 0)]
    [InlineData("(?>a)(b)", 1)]
    [InlineData("((?>a))", 1)]
    [InlineData("\\d+", 0)]
    [InlineData("\\w+", 0)]
    [InlineData("[\\d]", 0)]
    [InlineData("[\\da]", 0)]
    [InlineData("[^\\da]", 0)]
    [InlineData("[\\d\\s]", 0)]
    [InlineData("[\\w-]", 0)]
    [InlineData("\\p{L}+", 0)]
    [InlineData("[\\p{L}]", 0)]
    [InlineData("(\\p{IsGreek}+)", 1)]
    [InlineData("[\\d-e]", 0)]
    [InlineData("\\bword", 0)]
    public void SupportedShapesLower(string pattern, int expectedGroups)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        Assert.Equal(expectedGroups, program.GroupCount);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.GetGroupNumbers().Length - 1, program.GroupCount);
    }

    [Theory]
    [InlineData(@"\q")]
    [InlineData("é")]
    [InlineData("(a")]
    [InlineData("a)")]
    [InlineData("a**")]
    [InlineData("*a")]
    [InlineData("+a")]
    [InlineData("(?i)+")]
    [InlineData("(?#c)*")]
    [InlineData("a{3,2}")]
    [InlineData("[z-a]")]
    [InlineData("[a-[b]]")]
    [InlineData("[[:alpha:]]")]
    [InlineData("[a-\\d]")]
    [InlineData("[a-\\w]")]
    [InlineData("[a-\\s]")]
    [InlineData("[a-\\p{L}]")]
    [InlineData("[]")]
    [InlineData("[^]")]
    [InlineData("a)[a-z]+")]
    [InlineData("(?(1)[a-z]+|c)")]
    [InlineData("\\1(a)")]
    [InlineData("(a)\\2")]
    [InlineData("\\c ")]
    [InlineData("\\c")]
    [InlineData("\\c?")]
    [InlineData("\\x4")]
    [InlineData("\\u41")]
    [InlineData("\\8")]
    [InlineData("a\\12b")]
    [InlineData("(a)\\10")]
    [InlineData("(?|a)")]
    [InlineData("(?P=name)")]
    [InlineData("(?>a")]
    [InlineData("(?=a")]
    [InlineData("(?<=a")]
    [InlineData("(?<")]
    [InlineData("(?<1>a)")]
    [InlineData("(?<1a>x)")]
    [InlineData("(?<>x)")]
    [InlineData("(?<a>b)(?<a>c)")]
    [InlineData("(?<-open>b)")]
    [InlineData("(?<a-b>x)")]
    [InlineData("(?P<x>a)")]
    [InlineData("(?<a>x")]
    [InlineData("(?<a>x]")]
    [InlineData("(?<-b>a)(?<b>c)")]
    [InlineData("(?<b-a>x)(?<b-c>y)")]
    [InlineData("(?<a-a>x)")]
    [InlineData("(?<1-a>x)")]
    [InlineData("(?<a-1>x)")]
    public void UnsupportedShapesDecline(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("[a-\\w]")]
    [InlineData("[a-\\s]")]
    [InlineData("[a-\\p{L}]")]
    [InlineData("(?i)+")]
    [InlineData("(?#c)*")]
    [InlineData("*a")]
    [InlineData("+a")]
    public void MalformedShapesFailConstructionLikeRuntime(string pattern)
    {
        Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant));
        Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Utf8Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant));
    }

    [Fact]
    public void NonAsciiLiteralBackrefShapesDecline()
    {
        Assert.Null(Utf8SliceParser.TryParse("(" + ((char)0xE9) + ")\\1", RegexOptions.CultureInvariant));
        Assert.Null(Utf8SliceParser.TryParse("(" + char.ConvertFromUtf32(0x1F600) + ")\\1", RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("([0-9]){1,3}?x")]
    [InlineData("([0-9]){1,2}?x")]
    [InlineData("((a){1,3}?b)?c")]
    public void CapturedLazyRangeDeclinesCompiledLane(string pattern)
    {
        // Only ranges whose body writes captures decline: the per-lane
        // abandonment divergence and the enumeration oscillation need
        // capture-writing subtrees (probe-mapped, including literal bodies
        // once the kept progress is observable, plus phantom trailing
        // empties under masking continuations).
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("a??")]
    [InlineData(@"(\p{L}??)x")]
    [InlineData("a{2}?")]
    [InlineData("([0-9]){2}?x")]
    [InlineData("a{2,4}?a")]
    [InlineData("a{1,3}?b")]
    [InlineData("(?:[0-9]){0,2}?x")]
    public void DivergenceFreeLazyRepeatsStayCompiled(string pattern)
    {
        // Single maxima, exact counts, and capture-free ranges agree on both
        // oracle lanes (probe-mapped across nesting, backreferences,
        // lookarounds, and atomics), so the compiled lane keeps the slice
        // program; only capture-writing ranges decline above.
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("a.*?b")]
    [InlineData("a*?b")]
    [InlineData("a+?b")]
    [InlineData("a{2,4}a")]
    public void InfiniteMaximumLazyAndGreedyRepeatsStayCompiled(string pattern)
    {
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData(RegexOptions.ECMAScript)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void SensitiveOptionsDecline(RegexOptions options)
    {
        Assert.Null(Utf8SliceParser.TryParse("(a+)(b+)", options));
    }

    [Theory]
    [InlineData("\\p{L}+")]
    [InlineData("[\\p{L}a]+")]
    [InlineData("(?<=\\p{L})x")]
    public void SensitiveCategoryOptionsDecline(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.ECMAScript));
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.NonBacktracking));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.Multiline)]
    [InlineData(RegexOptions.Singleline)]
    public void InertOptionsAccept(RegexOptions options)
    {
        Assert.NotNull(Utf8SliceParser.TryParse("(a+)(b+)", options));
    }

    [Fact]
    public void DeepNestingDeclines()
    {
        Assert.Null(Utf8SliceParser.TryParse(new string((char)40, 65) + "a" + new string((char)41, 65), RegexOptions.CultureInvariant));
        Assert.NotNull(Utf8SliceParser.TryParse(new string((char)40, 64) + "a" + new string((char)41, 64), RegexOptions.CultureInvariant));
    }

    [Fact]
    public void DeclinedShapesStillFailConstructionThroughFallback()
    {
        Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Utf8Regex("(a", RegexOptions.CultureInvariant));
    }
}
