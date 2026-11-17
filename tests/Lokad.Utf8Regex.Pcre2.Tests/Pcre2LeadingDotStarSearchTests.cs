using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2LeadingDotStarSearchTests
{
    [Fact]
    public void LeadingDotStarPreservesCapturedGreedySearch()
    {
        var regex = new Utf8Pcre2Regex(@".*(ss)");

        Assert.True(regex.IsMatch("Essential services"u8));
        Assert.False(regex.IsMatch("A regular service"u8));

        var detailed = regex.MatchDetailed("glass"u8);
        Assert.True(detailed.Success);
        Assert.Equal("glass", detailed.GetValueString());
        Assert.Equal("ss", detailed.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(@".*(ss)")]
    [InlineData(@".*ss")]
    [InlineData(@".*(?:ss)")]
    [InlineData(@".*((ab))")]
    [InlineData(@".*?tail")]
    [InlineData(".*é🙂")]
    [InlineData(".*\\n")]
    public void LeadingDotStarMatchesOverlappingBclGrammarAtValidStarts(string pattern)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);
        // An unconstrained dot repeat plus exact suffix has the same Boolean
        // existence over these scalar-aligned subjects in the BCL grammar.
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);

        foreach (var inputText in new[]
                 {
                     "",
                     "s",
                     "ss",
                     "glass",
                     "Essential services are provided",
                     "x\nss",
                     "s\ns",
                     "é🙂",
                     "xé🙂y",
                     "ab x tail",
                     "\n",
                     "x\ny",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), direct.IsMatch(input));
            Assert.Equal(oracle.IsMatch(inputText), vm.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                var characterStart = Encoding.UTF8.GetCharCount(input.AsSpan(0, start));
                Assert.Equal(oracle.IsMatch(inputText, characterStart), direct.IsMatch(input, start));
                Assert.Equal(oracle.IsMatch(inputText, characterStart), vm.IsMatch(input, start));
            }
        }
    }

    [Fact]
    public void LeadingDotStarObservesMatchOptionsAndLimits()
    {
        const string Pattern = @".*(ss)";
        var direct = new Utf8Pcre2Regex(Pattern);
        var vm = CreateMetered(Pattern, 1_000_000);
        var input = "x\nEssential services"u8.ToArray();

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
            var expected = options is not (Pcre2MatchOptions.Anchored or Pcre2MatchOptions.EndAnchored);
            Assert.Equal(expected, direct.IsMatch(input, 0, options));
            Assert.Equal(expected, vm.IsMatch(input, 0, options));
        }

        var tightlyMetered = CreateMetered(Pattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern, uint matchLimit) =>
        new(
            pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = matchLimit },
            Regex.InfiniteMatchTimeout);
}
