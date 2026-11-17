using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SeparatedRunSearchTests
{
    private const string UriPattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";

    [Fact]
    public void SeparatedRunsPreserveUriValues()
    {
        var regex = new Utf8Pcre2Regex(UriPattern);

        Assert.True(regex.IsMatch("https://atlas.example.org/reports/export?id=42"u8));
        Assert.False(regex.IsMatch("not a URI"u8));

        var detailed = regex.MatchDetailed("go to https://example.org/a?q=1#top now"u8);
        Assert.True(detailed.Success);
        Assert.Equal("https://example.org/a?q=1#top", detailed.GetValueString());
    }

    [Fact]
    public void SeparatedRunsMatchScalarClassOracleAtValidStarts()
    {
        var direct = new Utf8Pcre2Regex(UriPattern);
        var vm = CreateMetered(UriPattern, 1_000_000);

        foreach (var inputText in new[]
                 {
                     "",
                     "://ab",
                     "x://a",
                     "x://ab",
                     "x://a/",
                     "x:///a",
                     "-x://ab",
                     "é://ab",
                     "éx://ab",
                     "x://a b",
                     "x://é🙂",
                     "x://😀",
                     "x://a😀",
                     "prefix https://atlas.example.org/reports/export?id=42 suffix",
                     "x://ab?q=hello#fragment",
                     "bad://a then good://ab",
                     "first://ab second://cd",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(ScalarClassOracle(inputText, direct.Pattern, 0), direct.IsMatch(input));
            Assert.Equal(ScalarClassOracle(inputText, direct.Pattern, 0), vm.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                var expected = ScalarClassOracle(inputText, direct.Pattern, Encoding.UTF8.GetString(input.AsSpan(0, start)).EnumerateRunes().Count());
                Assert.Equal(expected, direct.IsMatch(input, start));
                Assert.Equal(expected, vm.IsMatch(input, start));
            }
        }
    }

    [Fact]
    public void SeparatedRunsGeneralizeBeyondTheUriShape()
    {
        const string Pattern = @"[\d]+--[^x\s]+[^\s]+(?:![a-z]*)?";
        var direct = new Utf8Pcre2Regex(Pattern);
        var vm = CreateMetered(Pattern, 1_000_000);

        foreach (var inputText in new[]
                 {
                     "1--ab",
                     "42--a/",
                     "x1--é🙂!done",
                     "1--xabc",
                     "1--a",
                     "é--ab",
                     "prefix 7--ab suffix",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(ScalarClassOracle(inputText, direct.Pattern, 0), direct.IsMatch(input));
            Assert.Equal(ScalarClassOracle(inputText, direct.Pattern, 0), vm.IsMatch(input));
        }
    }

    [Fact]
    public void SeparatedRunsObserveMatchOptionsAndLimits()
    {
        var direct = new Utf8Pcre2Regex(UriPattern);
        var vm = CreateMetered(UriPattern, 1_000_000);
        var input = "prefix https://example.org/a?q=1#top"u8.ToArray();

        foreach (var options in new[]
                 {
                     Pcre2MatchOptions.Anchored,
                     Pcre2MatchOptions.EndAnchored,
                     Pcre2MatchOptions.NotBol,
                     Pcre2MatchOptions.NotEol,
                     Pcre2MatchOptions.NotEmpty,
                     Pcre2MatchOptions.NotEmptyAtStart,
                 })
        {
            var expected = options != Pcre2MatchOptions.Anchored;
            Assert.Equal(expected, direct.IsMatch(input, 0, options));
            Assert.Equal(expected, vm.IsMatch(input, 0, options));
        }

        var tightlyMetered = CreateMetered(UriPattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
    }

    private static bool ScalarClassOracle(string text, string pattern, int scalarStart)
    {
        // These two grammars use ASCII positive classes and negative ASCII
        // delimiter classes only. Map each non-ASCII scalar to one BMP symbol:
        // this preserves every predicate while preventing BCL surrogate splitting.
        // This projection is not a general PCRE2 or Unicode-class comparator.
        var projected = string.Concat(text.EnumerateRunes().Select(r => r.IsAscii ? (char)r.Value : 'é'));
        var ascii = pattern.Replace(@"[\w]", "[A-Za-z0-9_]", StringComparison.Ordinal)
            .Replace(@"[\d]", "[0-9]", StringComparison.Ordinal)
            .Replace(@"\s", @"\t\n\r\f\v ", StringComparison.Ordinal);
        return new Regex(ascii, RegexOptions.CultureInvariant).IsMatch(projected, scalarStart);
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern, uint matchLimit) =>
        new(
            pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = matchLimit },
            Regex.InfiniteMatchTimeout);
}
