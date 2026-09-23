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
    [InlineData("(?=a)a")]
    [InlineData("(a)(?(1)b|c)")]
    [InlineData(@"(a)\1")]
    [InlineData("(?:a)")]
    [InlineData("(?<name>a)")]
    [InlineData(@"\d+")]
    [InlineData(@"\w+")]
    [InlineData(@"\q")]
    [InlineData("é")]
    [InlineData("(a")]
    [InlineData("a)")]
    [InlineData("a**")]
    [InlineData("a{3,2}")]
    [InlineData("[\\d]")]
    [InlineData("[z-a]")]
    [InlineData("[a-[b]]")]
    [InlineData("[[:alpha:]]")]
    [InlineData("[\\x41]")]
    [InlineData("[]")]
    [InlineData("[^]")]
    [InlineData("(?<w>[a-z]+)")]
    [InlineData("(?:[a-z]+)")]
    [InlineData("(?=[a-z]+)")]
    [InlineData("a)[a-z]+")]
    [InlineData("(?(1)[a-z]+|c)")]
    public void UnsupportedShapesDecline(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("a{2,4}?a")]
    [InlineData("([0-9]){1,3}?x")]
    [InlineData("a??")]
    public void FiniteMaximumLazyRepeatDeclinesCompiledLane(string pattern)
    {
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant));
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
    [InlineData(RegexOptions.IgnoreCase)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.ECMAScript)]
    [InlineData(RegexOptions.NonBacktracking)]
    [InlineData(RegexOptions.IgnorePatternWhitespace)]
    [InlineData(RegexOptions.ExplicitCapture)]
    public void SensitiveOptionsDecline(RegexOptions options)
    {
        Assert.Null(Utf8SliceParser.TryParse("(a+)(b+)", options));
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
