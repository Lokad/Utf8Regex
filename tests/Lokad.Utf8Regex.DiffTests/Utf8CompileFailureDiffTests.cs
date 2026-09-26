using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// R07 compile failures against the oracle: patterns the BCL rejects are
// rejected here too, with byte-identical BCL RegexParseException identity and
// messages on every construction route (the fallback verifier constructs the
// BCL engine, so the oracle reports itself).
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
    public void InvalidPatternsThrowBclExceptionsOnEveryRoute(string pattern)
    {
        var options = RegexOptions.CultureInvariant;
        var expected = Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Regex(pattern, options));
        var expectedCompiled = Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Regex(pattern, options | RegexOptions.Compiled));
        var actual = Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Utf8Regex(pattern, options));
        var actualCompiled = Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Utf8Regex(pattern, options | RegexOptions.Compiled));

        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expectedCompiled.Message, actualCompiled.Message);
    }

    [Theory]
    [InlineData("(?n:(?<-x>a))", RegexParseError.UndefinedNamedReference, 9)]
    [InlineData("(?<=(?<1a>b))c", RegexParseError.CaptureGroupNameInvalid, 8)]
    public void FlipWideningPatternsThrowBclExactErrors(string pattern, RegexParseError error, int offset)
    {
        // Parser-flip conformance fix (2026-09-26): the removed fork accepted these
        // invalid shapes, so construction must now fail exactly like the .NET 10 oracle
        // on every string constructor route, ordinary and compiled.
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options));
            var actualTimeout = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout));
            _ = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern));

            Assert.Equal(error, expected.Error);
            Assert.Equal(offset, expected.Offset);
            Assert.Equal(expected.Error, actual.Error);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Message, actual.Message);
            Assert.Equal(expected.Error, actualTimeout.Error);
            Assert.Equal(expected.Offset, actualTimeout.Offset);
            Assert.Equal(expected.Message, actualTimeout.Message);
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
