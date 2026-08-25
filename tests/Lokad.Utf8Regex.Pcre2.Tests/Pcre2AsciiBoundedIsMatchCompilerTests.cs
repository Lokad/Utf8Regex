using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiBoundedIsMatchCompilerTests
{
    private const string CreditCardPattern = @"(\d{4}[- ]){3}\d{3,4}";

    [Fact]
    public void BoundedAsciiLanguageUsesAnIsMatchOnlyDirectPlan()
    {
        var regex = new Utf8Pcre2Regex(CreditCardPattern);

        Assert.Equal(
            "IsMatch=Pcre2AsciiBoundedIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("4111-1111-1111-111"u8));
        Assert.True(regex.IsMatch("4111 1111 1111 1111"u8));
        Assert.False(regex.IsMatch("4111-1111-1111-11"u8));
        Assert.False(regex.IsMatch("٤١١١-1111-1111-1111"u8));

        var detailed = regex.MatchDetailed("card 4111-1111-1111-1111 done"u8);
        Assert.True(detailed.Success);
        Assert.Equal("4111-1111-1111-1111", detailed.GetValueString());
        Assert.Equal("1111-", detailed.GetGroup(1).GetValueString());
    }

    [Fact]
    public void BoundedAsciiLanguageMatchesTheMeteredVmAtEveryValidStart()
    {
        var direct = new Utf8Pcre2Regex(CreditCardPattern);
        var vm = CreateMetered(CreditCardPattern, 1_000_000);
        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(vm.DebugCompiledProgram.Operations.IsMatch);

        foreach (var inputText in new[]
                 {
                     "",
                     "4111-1111-1111-111",
                     "4111-1111-1111-1111",
                     "x4111 1111-1111 1111y",
                     "4111-1111-1111-11",
                     "٤١١١-1111-1111-1111",
                     "é4111-1111-1111-1111🙂",
                     "4111-1111-1111-11 then 5555-5555-5555-5555",
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
    [InlineData(@"\d{4}-\d{2}-\d{2}", "on 2026-08-25", true)]
    [InlineData(@"\d{4}-\d{2}-\d{2}", "on ٢٠٢٦-08-25", false)]
    [InlineData(@"^\d{1,2}/\d{1,2}/\d{4}$", "25/8/2026", true)]
    [InlineData(@"^\d{1,2}/\d{1,2}/\d{4}$", "on 25/8/2026", false)]
    [InlineData(@"^\d{1,2}/\d{1,2}/\d{4}$", "25/8/2026\n", true)]
    [InlineData(@"^\d{1,2}/\d{1,2}/\d{4}$", "٢٥/8/2026", false)]
    [InlineData(@"(?:\d{2}|[A-Z]{2})-[a-z]{2}", "code AB-xy", true)]
    [InlineData(@"(?:\d{2}|[A-Z]{2})-[a-z]{2}", "code A1-xy", false)]
    public void BoundedAsciiLanguageGeneralizesAcrossFiniteShapes(
        string pattern,
        string inputText,
        bool expected)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);
        var input = Encoding.UTF8.GetBytes(inputText);

        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(expected, direct.IsMatch(input));
        Assert.Equal(vm.IsMatch(input), direct.IsMatch(input));
    }

    [Fact]
    public void BoundedAsciiLanguageFallsBackForMatchOptionsAndLimits()
    {
        var direct = new Utf8Pcre2Regex(CreditCardPattern);
        var vm = CreateMetered(CreditCardPattern, 1_000_000);
        var input = "card 4111-1111-1111-1111"u8.ToArray();

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

        var tightlyMetered = CreateMetered(CreditCardPattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
        Assert.Throws<ArgumentException>(() => direct.IsMatch([0xFF]));
    }

    [Fact]
    public void BoundedAsciiLanguageAnalyzerRejectsUnprovenLanguages()
    {
        foreach (var regex in new[]
                 {
                     new Utf8Pcre2Regex(@"\d+"),
                     new Utf8Pcre2Regex(@"[^0]{2}"),
                     new Utf8Pcre2Regex(@".{2}"),
                     new Utf8Pcre2Regex(@"é{2}"),
                     new Utf8Pcre2Regex(@"(?i:a){2}"),
                     new Utf8Pcre2Regex(@"(?:a|b|c|d|e){4}[0-9]"),
                     new Utf8Pcre2Regex(@"[a]{129}[b]"),
                     new Utf8Pcre2Regex(@"(a)\1"),
                     new Utf8Pcre2Regex(@"\A\d{4}-\d{2}-\d{2}\z"),
                     new Utf8Pcre2Regex(CreditCardPattern, Pcre2CompileOptions.Ucp),
                     new Utf8Pcre2Regex(CreditCardPattern, Pcre2CompileOptions.Caseless),
                     new Utf8Pcre2Regex(
                         CreditCardPattern,
                         Pcre2CompileOptions.None,
                         new Utf8Pcre2CompileSettings { Bsr = Pcre2BsrConvention.AnyCrlf },
                         default,
                         Regex.InfiniteMatchTimeout),
                 })
        {
            Assert.IsNotType<Pcre2AsciiBoundedIsMatchDirectProgram>(
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
