using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiAnchoredDfaIsMatchCompilerTests
{
    private const string FloatPattern = @"^[-+]?\d*\.?\d*$";

    [Fact]
    public void FloatLanguageUsesDfaOnlyForBooleanExecution()
    {
        var regex = new Utf8Pcre2Regex(FloatPattern);

        Assert.Equal(
            "IsMatch=Pcre2AsciiAnchoredDfaIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("-3.14159"u8));
        Assert.True(regex.IsMatch("+.5"u8));
        Assert.True(regex.IsMatch("."u8));
        Assert.True(regex.IsMatch([]));
        Assert.False(regex.IsMatch("1.2.3"u8));
        Assert.False(regex.IsMatch("١٢"u8));

        var detailed = regex.MatchDetailed("-3.14159"u8);
        Assert.True(detailed.Success);
        Assert.Equal("-3.14159", detailed.Value.GetValueString());
    }

    [Fact]
    public void AnchoredAsciiDfaMatchesTheMeteredVmAtEveryValidStart()
    {
        AssertEquivalentAtEveryValidStart(
            FloatPattern,
            "",
            "+",
            "-",
            ".",
            "+.",
            "3",
            "-3.14159",
            "3.",
            ".5",
            "+.5",
            "12.34\n",
            "12.34\r",
            "1.2.3",
            "١٢",
            "prefix 12.34");

        AssertEquivalentAtEveryValidStart(
            @"^(\d+)(c?)(\d*)z$",
            "12z",
            "12cz",
            "12c34z",
            "12c34z\n",
            "",
            "12c",
            "12ccz",
            "αz");

        AssertEquivalentAtEveryValidStart(
            @"^\s*$",
            "",
            " \t\r\n",
            " \t\r\n\n",
            "a",
            " ");
    }

    [Fact]
    public void AnchoredAsciiDfaMatchesTheVmAcrossAmbiguousRegularLanguages()
    {
        foreach (var pattern in new[]
                 {
                     @"^\d*\d?b$",
                     @"^\d+\d*b+$",
                     @"^\d*\.?\d*a$",
                 })
        {
            var direct = new Utf8Pcre2Regex(pattern);
            var vm = CreateMetered(pattern, 1_000_000);
            Assert.IsType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
                direct.DebugCompiledProgram.Operations.IsMatch);

            foreach (var inputText in EnumerateWords("ab0.\n", 5))
            {
                var input = Encoding.UTF8.GetBytes(inputText);
                Assert.Equal(vm.IsMatch(input), direct.IsMatch(input));
            }
        }
    }

    [Fact]
    public void AnchoredAsciiDfaFallsBackOutsideTheProvenContract()
    {
        var direct = new Utf8Pcre2Regex(FloatPattern);
        var vm = CreateMetered(FloatPattern, 1_000_000);
        var input = "-3.14159"u8.ToArray();

        Assert.IsType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            vm.DebugCompiledProgram.Operations.IsMatch);

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

        Assert.IsNotType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@"[-+]?\d*\.?\d*$").DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@"^[-+]?\d*\.?\d*").DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@"^\d*+\d$").DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@"^(?i:a*)$").DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@"^[^a]*$").DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            new Utf8Pcre2Regex(FloatPattern, Pcre2CompileOptions.Ucp)
                .DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            new Utf8Pcre2Regex(
                FloatPattern,
                Pcre2CompileOptions.None,
                new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr },
                default,
                Regex.InfiniteMatchTimeout)
                .DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@"^(a?){100}a*$").DebugCompiledProgram.Operations.IsMatch);

        var tightlyMetered = CreateMetered(FloatPattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
        Assert.Throws<ArgumentException>(() => direct.IsMatch([0xFF]));
    }

    private static void AssertEquivalentAtEveryValidStart(string pattern, params string[] inputs)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);
        Assert.IsType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(
            direct.DebugCompiledProgram.Operations.IsMatch);

        foreach (var inputText in inputs)
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

    private static IEnumerable<string> EnumerateWords(string alphabet, int maximumLength)
    {
        yield return string.Empty;
        var words = new List<string> { string.Empty };
        for (var length = 1; length <= maximumLength; length++)
        {
            var nextWords = new List<string>(words.Count * alphabet.Length);
            foreach (var prefix in words)
            {
                foreach (var value in alphabet)
                {
                    var word = prefix + value;
                    nextWords.Add(word);
                    yield return word;
                }
            }

            words = nextWords;
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
