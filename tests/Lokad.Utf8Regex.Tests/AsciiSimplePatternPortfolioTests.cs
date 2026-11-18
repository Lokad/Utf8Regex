using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class AsciiSimplePatternPortfolioTests
{

    [Theory]
    [InlineData(RegexOptions.IgnoreCase)]
    [InlineData(RegexOptions.ECMAScript)]
    public void BoundedSuffixOptionsKeepWhitespaceAndCaseSemantics(RegexOptions options)
    {
        const string pattern = @"\s[a-zA-Z]{0,12}ing\s";
        var regex = new Utf8Regex(pattern, options: options);
        foreach (var text in new[] { "\u2003sing\u00a0", " SING ", " singing ", " xing\n", "é sing Ω" })
        {
            var input = System.Text.Encoding.UTF8.GetBytes(text);
            Assert.Equal(Regex.IsMatch(text, pattern, options), regex.IsMatch(input));
            Assert.Equal(Regex.Count(text, pattern, options), regex.Count(input));
        }
    }
}
