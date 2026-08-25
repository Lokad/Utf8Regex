using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PalindromeIsMatchCompilerTests
{
    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$", true)]
    [InlineData("^(.|(.)(?1)?\\2)$", false)]
    public void RecursivePalindromeUsesAnIsMatchOnlyDirectPlan(string pattern, bool allowsEmpty)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(
            "IsMatch=Pcre2PalindromeIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.Equal(allowsEmpty, regex.IsMatch([]));
        Assert.True(regex.IsMatch("abccba"u8));
        Assert.False(regex.IsMatch("abca"u8));

        var detailed = regex.MatchDetailed("abcba"u8);
        Assert.True(detailed.Success);
        Assert.Equal("abcba", detailed.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$")]
    [InlineData("^(.|(.)(?1)?\\2)$")]
    public void RecursivePalindromeMatchesTheMeteredVmAcrossUtf8AndLineEndings(string pattern)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);
        Assert.IsType<Pcre2PalindromeIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2PalindromeIsMatchDirectProgram>(vm.DebugCompiledProgram.Operations.IsMatch);

        foreach (var inputText in new[]
                 {
                     "",
                     "a",
                     "aa",
                     "aba",
                     "abba",
                     "abcba",
                     "abccba",
                     "abca",
                     "é🙂é",
                     "é🙂🙂é",
                     "é🙂a",
                     "\n",
                     "aba\n",
                     "\r\n",
                     "a\nb",
                     "\n\n",
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

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$")]
    [InlineData("^(.|(.)(?1)?\\2)$")]
    public void RecursivePalindromeFallsBackForMatchOptionsAndLimits(string pattern)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);
        var input = "abcba\n"u8.ToArray();

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

        var tightlyMetered = CreateMetered(pattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch("abcba"u8)).ErrorKind);

        Assert.Throws<ArgumentException>(() => direct.IsMatch([0xFF]));
    }

    [Fact]
    public void RecursivePalindromeAnalyzerRejectsDifferentSemantics()
    {
        foreach (var regex in new[]
                 {
                     new Utf8Pcre2Regex("((.)(?1)\\2|.?)$"),
                     new Utf8Pcre2Regex("^((.)(?1)\\2|.?)"),
                     new Utf8Pcre2Regex("^((.)(?1)\\1|.?)$"),
                     new Utf8Pcre2Regex("^((.)(?1)\\2|.)$"),
                     new Utf8Pcre2Regex("^((.)(?1)?\\2|.?)$"),
                     new Utf8Pcre2Regex("^(.|(.)(?1)\\2)$"),
                     new Utf8Pcre2Regex("^(.|(.)(?1)?\\1)$"),
                     new Utf8Pcre2Regex("^((.)(?1)\\2|.?)$", Pcre2CompileOptions.DotAll),
                     new Utf8Pcre2Regex("^((.)(?1)\\2|.?)$", Pcre2CompileOptions.Multiline),
                     new Utf8Pcre2Regex(
                         "^((.)(?1)\\2|.?)$",
                         Pcre2CompileOptions.None,
                         new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr },
                         default,
                         Regex.InfiniteMatchTimeout),
                 })
        {
            Assert.IsNotType<Pcre2PalindromeIsMatchDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
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
