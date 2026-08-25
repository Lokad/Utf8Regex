using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2LeadingDotStarLiteralIsMatchCompilerTests
{
    [Fact]
    public void LeadingDotStarLiteralUsesAnIsMatchOnlyDirectPlan()
    {
        var regex = new Utf8Pcre2Regex(@".*(ss)");

        Assert.Equal(
            "IsMatch=Pcre2LeadingDotStarLiteralIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
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
    public void LeadingDotStarLiteralMatchesTheMeteredVmAtEveryValidStart(string pattern)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);
        Assert.IsType<Pcre2LeadingDotStarLiteralIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2LeadingDotStarLiteralIsMatchDirectProgram>(vm.DebugCompiledProgram.Operations.IsMatch);

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
    public void LeadingDotStarLiteralFallsBackForMatchOptionsAndLimits()
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
            Assert.Equal(vm.IsMatch(input, 0, options), direct.IsMatch(input, 0, options));
        }

        var tightlyMetered = CreateMetered(Pattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
        Assert.Throws<ArgumentException>(() => direct.IsMatch([0xFF]));
    }

    [Fact]
    public void LeadingDotStarLiteralAnalyzerRejectsDifferentLanguages()
    {
        foreach (var regex in new[]
                 {
                     new Utf8Pcre2Regex(@".+ss"),
                     new Utf8Pcre2Regex(@".*+ss"),
                     new Utf8Pcre2Regex(@".*s+"),
                     new Utf8Pcre2Regex(@".*(s|t)"),
                     new Utf8Pcre2Regex(@".*[s]"),
                     new Utf8Pcre2Regex(@"^.*ss"),
                     new Utf8Pcre2Regex(@".*ss$"),
                     new Utf8Pcre2Regex(@".*(?=ss)ss"),
                     new Utf8Pcre2Regex(@".*ss", Pcre2CompileOptions.Caseless),
                     new Utf8Pcre2Regex(@".*ss", Pcre2CompileOptions.DotAll),
                     new Utf8Pcre2Regex(
                         @".*ss",
                         Pcre2CompileOptions.None,
                         new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr },
                         default,
                         Regex.InfiniteMatchTimeout),
                 })
        {
            Assert.IsNotType<Pcre2LeadingDotStarLiteralIsMatchDirectProgram>(
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
