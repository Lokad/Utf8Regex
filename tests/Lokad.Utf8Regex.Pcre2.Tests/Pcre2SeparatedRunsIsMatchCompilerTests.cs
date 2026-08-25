using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SeparatedRunsIsMatchCompilerTests
{
    private const string UriPattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";

    [Fact]
    public void SeparatedRunsUseAnIsMatchOnlyDirectPlan()
    {
        var regex = new Utf8Pcre2Regex(UriPattern);

        Assert.Equal(
            "IsMatch=Pcre2SeparatedRunsIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("https://atlas.example.org/reports/export?id=42"u8));
        Assert.False(regex.IsMatch("not a URI"u8));

        var detailed = regex.MatchDetailed("go to https://example.org/a?q=1#top now"u8);
        Assert.True(detailed.Success);
        Assert.Equal("https://example.org/a?q=1#top", detailed.GetValueString());
    }

    [Fact]
    public void SeparatedRunsMatchTheMeteredVmAtEveryValidStart()
    {
        var direct = new Utf8Pcre2Regex(UriPattern);
        var vm = CreateMetered(UriPattern, 1_000_000);
        Assert.IsType<Pcre2SeparatedRunsIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2SeparatedRunsIsMatchDirectProgram>(vm.DebugCompiledProgram.Operations.IsMatch);

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
                     "prefix https://atlas.example.org/reports/export?id=42 suffix",
                     "x://ab?q=hello#fragment",
                     "bad://a then good://ab",
                     "first://ab second://cd",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(vm.IsMatch(input), direct.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                Assert.Equal(vm.IsMatch(input, start), direct.IsMatch(input, start));
            }
        }
    }

    [Fact]
    public void SeparatedRunsGeneralizeBeyondTheUriShape()
    {
        const string Pattern = @"[\d]+--[^x\s]+[^\s]+(?:![a-z]*)?";
        var direct = new Utf8Pcre2Regex(Pattern);
        var vm = CreateMetered(Pattern, 1_000_000);
        Assert.IsType<Pcre2SeparatedRunsIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);

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
            Assert.Equal(vm.IsMatch(input), direct.IsMatch(input));
        }
    }

    [Fact]
    public void SeparatedRunsFallBackForMatchOptionsAndLimits()
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
            Assert.Equal(vm.IsMatch(input, 0, options), direct.IsMatch(input, 0, options));
        }

        var tightlyMetered = CreateMetered(UriPattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
        Assert.Throws<ArgumentException>(() => direct.IsMatch([0xFF]));
    }

    [Fact]
    public void SeparatedRunsAnalyzerRejectsUnprovenLanguages()
    {
        foreach (var regex in new[]
                 {
                     new Utf8Pcre2Regex(@"[\w]{2,}://[^/\s?#]+[^\s?#]+"),
                     new Utf8Pcre2Regex(@"[\w]++://[^/\s?#]+[^\s?#]+"),
                     new Utf8Pcre2Regex(@"[\w]+é[^/\s?#]+[^\s?#]+"),
                     new Utf8Pcre2Regex(@"[\w]+://[^\s?#]+[^/\s?#]+"),
                     new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]+"),
                     new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]++[^\s?#]+"),
                     new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]+[^\s?#]+x"),
                     new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:x){0,2}"),
                     new Utf8Pcre2Regex(UriPattern, Pcre2CompileOptions.Caseless),
                     new Utf8Pcre2Regex(UriPattern, Pcre2CompileOptions.Ucp),
                     new Utf8Pcre2Regex(
                         UriPattern,
                         Pcre2CompileOptions.None,
                         new Utf8Pcre2CompileSettings { Bsr = Pcre2BsrConvention.AnyCrlf },
                         default,
                         Regex.InfiniteMatchTimeout),
                 })
        {
            Assert.IsNotType<Pcre2SeparatedRunsIsMatchDirectProgram>(
                regex.DebugCompiledProgram.Operations.IsMatch);
        }
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern, uint matchLimit) =>
        new(
            pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = matchLimit },
            Regex.InfiniteMatchTimeout);
}
