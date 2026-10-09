using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// The oracle checks the overlapping grammar's rejection verdict. Product
// admission owns its diagnostics and keeps them stable across constructors.
public sealed class Utf8CompileFailureDiffTests
{
    [Theory]
    [InlineData("(unterminated")]
    [InlineData("[z-a]")]
    [InlineData("*a")]
    [InlineData("a{2,1}")]
    [InlineData("\\")]
    [InlineData("[a")]
    [InlineData("(a")]
    [InlineData("a)")]
    [InlineData("(?n:(?<-x>a))")]
    [InlineData("(?<=(?<1a>b))c")]
    [InlineData("[\\A]+")]
    [InlineData("[\\z]+")]
    [InlineData("[\\B]+")]
    public void InvalidPatternsHaveOwnedFailuresOnEveryRoute(string pattern)
    {
        var options = RegexOptions.CultureInvariant;
        var expected = Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Regex(pattern, options));
        var expectedCompiled = Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Regex(pattern, options | RegexOptions.Compiled));
        var actual = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options));
        var actualCompiled = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options | RegexOptions.Compiled));

        Assert.Equal("pattern", actual.ParamName);
        Assert.Equal("pattern", actualCompiled.ParamName);
        Assert.Contains("UTF-16 pattern offset", actual.Message);
        Assert.Equal(actual.Message, actualCompiled.Message);
    }

    [Theory]
    [InlineData("(?n:(?<-x>a))", RegexParseError.UndefinedNamedReference, 9)]
    [InlineData("(?<=(?<1a>b))c", RegexParseError.CaptureGroupNameInvalid, 8)]
    public void InvalidNamesAndPopsHaveOwnedFailures(string pattern, RegexParseError error, int offset)
    {
        // Preserve rejection of these malformed names/pops on every constructor.
        // Oracle error attributes remain historical grammar evidence.
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options));
            var actualTimeout = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout));
            var actualDefault = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern));

            Assert.Equal(error, expected.Error);
            Assert.Equal(offset, expected.Offset);
            Assert.Equal("pattern", actual.ParamName);
            Assert.Equal("pattern", actualTimeout.ParamName);
            Assert.Equal("pattern", actualDefault.ParamName);
            Assert.Contains("UTF-16 pattern offset", actual.Message);
            Assert.Equal(actual.Message, actualTimeout.Message);
            Assert.Equal(actual.Message, actualDefault.Message);
        }

    }

    [Theory]
    [InlineData("(?<x>a)")]
    [InlineData("(?<1>a)")]
    [InlineData("(?<a>x)(?<-a>y)")]
    [InlineData("(?n:(?<a>x)(?<-a>y))")]
    public void FlipWideningControlsStillConstruct(string pattern)
    {
        // Valid named/numeric groups and balancing pops keep constructing on every
        // route; the flip rejects only the undefined pop and the invalid name.
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options);
            var product = new Utf8Regex(pattern, options);
            var productTimeout = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout);

            Assert.Equal(oracle.GetGroupNames(), product.GetGroupNames());
            Assert.Equal(oracle.GetGroupNumbers(), product.GetGroupNumbers());
            Assert.Equal(oracle.GetGroupNames(), productTimeout.GetGroupNames());
        }
    }
}
