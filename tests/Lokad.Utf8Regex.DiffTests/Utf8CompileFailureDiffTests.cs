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
}
