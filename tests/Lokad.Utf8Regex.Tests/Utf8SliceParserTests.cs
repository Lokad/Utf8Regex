using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

/// <summary>Scalar program admission for literals, classes, alternatives,
/// repetition, captures, references, lookarounds and scoped options. Invalid
/// grammar fails through owned pattern diagnostics; NonBacktracking admits
/// regular graphs and rejects unsupported constructs deliberately.</summary>
public sealed class Utf8SliceParserTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("é", 0)]
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
    [InlineData("[a-[b]]", 0)]
    [InlineData("[^a-z]", 0)]
    [InlineData("[a-c-e]", 0)]
    [InlineData("[[:alpha:]]", 0)]
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
    [InlineData("\\1(a)", 1)]
    [InlineData("(?<a>b)(?<a>c)", 1)]
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
    [InlineData("(?<1>a)", 1)]
    [InlineData("(?<-b>a)(?<b>c)", 1)]
    [InlineData("(?<a-a>x)", 1)]
    [InlineData("(?<a-1>x)", 1)]
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
    [InlineData("(a")]
    [InlineData("a)")]
    [InlineData("a**")]
    [InlineData("*a")]
    [InlineData("+a")]
    [InlineData("(?i)+")]
    [InlineData("(?#c)*")]
    [InlineData("a{3,2}")]
    [InlineData("[z-a]")]
    [InlineData("[a-\\d]")]
    [InlineData("[a-\\w]")]
    [InlineData("[a-\\s]")]
    [InlineData("[a-\\p{L}]")]
    [InlineData("[]")]
    [InlineData("[^]")]
    [InlineData("a)[a-z]+")]
    [InlineData("(?(1)[a-z]+|c)")]
    [InlineData("(a)\\2")]
    [InlineData("\\c ")]
    [InlineData("\\c")]
    [InlineData("\\c?")]
    [InlineData("\\x4")]
    [InlineData("\\u41")]
    [InlineData("\\8")]
    [InlineData("(?|a)")]
    [InlineData("(?P=name)")]
    [InlineData("(?>a")]
    [InlineData("(?=a")]
    [InlineData("(?<=a")]
    [InlineData("(?<")]
    [InlineData("(?<1a>x)")]
    [InlineData("(?<>x)")]
    [InlineData("(?<-open>b)")]
    [InlineData("(?<a-b>x)")]
    [InlineData("(?P<x>a)")]
    [InlineData("(?<a>x")]
    [InlineData("(?<a>x]")]
    [InlineData("(?<b-a>x)(?<b-c>y)")]
    [InlineData("(?<1-a>x)")]
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
    public void MalformedShapesHaveOwnedConstructionFailures(string pattern)
    {
        Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant));
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant));
    }

    [Fact]
    public void NonAsciiLiteralBackrefShapesLower()
    {
        Assert.NotNull(Utf8SliceParser.TryParse("(" + ((char)0xE9) + ")\\1", RegexOptions.CultureInvariant));
        Assert.NotNull(Utf8SliceParser.TryParse("(" + char.ConvertFromUtf32(0x1F600) + ")\\1", RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("([0-9]){1,3}?x")]
    [InlineData("([0-9]){1,2}?x")]
    [InlineData("((a){1,3}?b)?c")]
    public void CapturedLazyRangeUsesInterpreterUnwindInBothModes(string pattern)
    {
        // Compiled .NET 10 can leak abandoned progress or repeat global positions.
        // Both owned modes retain the interpreter's complete capture rollback.
        var ordinary = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(ordinary);
        Assert.False(ordinary.UsesInterpretedLazyLoopSemantics);
        var compiled = Utf8SliceParser.TryParse(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        Assert.NotNull(compiled);
        Assert.True(compiled.UsesInterpretedLazyLoopSemantics);
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
        // program; the affected capture-writing ranges above also stay owned.
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
    [InlineData(RegexOptions.NonBacktracking)]
    public void NonBacktrackingRegularGraphsKeepOwnedLinearPrograms(RegexOptions options)
    {
        Assert.True(Utf8SliceParser.TryParse("(a+)(b+)", options)?.IsNonBacktracking);
    }

    [Theory]
    [InlineData("\\p{L}+")]
    [InlineData("[\\p{L}a]+")]
    [InlineData("(?<=\\p{L})x")]
    public void CategoryOptionsRespectOwnedEngineCapabilities(string pattern)
    {
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.ECMAScript));
        var nonBacktracking = Utf8SliceParser.TryParse(pattern, RegexOptions.NonBacktracking);
        if (pattern.StartsWith("(?<=", StringComparison.Ordinal)) Assert.Null(nonBacktracking);
        else Assert.True(nonBacktracking?.IsNonBacktracking);
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
    public void DeepNestingKeepsOwnedProgram()
    {
        foreach (var depth in new[] { 64, 65, 257 })
        {
            var program = Utf8SliceParser.TryParse(new string('(', depth) + "a" + new string(')', depth), RegexOptions.CultureInvariant);
            Assert.NotNull(program);
            Assert.Equal(depth, program.GroupCount);
            Assert.False(program.CanMatchEmpty);
        }
    }

    [Fact]
    public void UnclosedGroupFailsOwnedConstruction()
    {
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex("(a", RegexOptions.CultureInvariant));
    }
}
