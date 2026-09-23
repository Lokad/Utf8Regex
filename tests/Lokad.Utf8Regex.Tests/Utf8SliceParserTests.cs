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
    public void SupportedShapesLower(string pattern, int expectedGroups)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        Assert.Equal(expectedGroups, program.GroupCount);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.GetGroupNumbers().Length - 1, program.GroupCount);
    }

    [Theory]
    [InlineData("[a-z]+")]
    [InlineData("^ab")]
    [InlineData("ab$")]
    [InlineData(@"\bword")]
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
    public void UnsupportedShapesDecline(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
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
