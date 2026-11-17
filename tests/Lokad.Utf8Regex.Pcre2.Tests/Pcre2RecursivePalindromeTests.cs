using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2RecursivePalindromeTests
{
    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$", true)]
    [InlineData("^(.|(.)(?1)?\\2)$", false)]
    public void RecursivePalindromePreservesEmptyAndCaptureSemantics(string pattern, bool allowsEmpty)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(allowsEmpty, regex.IsMatch([]));
        Assert.True(regex.IsMatch("abccba"u8));
        Assert.False(regex.IsMatch("abca"u8));

        var detailed = regex.MatchDetailed("abcba"u8);
        Assert.True(detailed.Success);
        Assert.Equal("abcba", System.Text.Encoding.UTF8.GetString(detailed.GetGroup(1).GetValueBytes()));
    }

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$")]
    [InlineData("^(.|(.)(?1)?\\2)$")]
    public void RecursivePalindromeMatchesScalarPalindromeSemanticsAtValidStarts(string pattern)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);

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
            var expected = ScalarPalindrome(inputText, pattern.Contains(".?)", StringComparison.Ordinal));
            Assert.Equal(expected, direct.IsMatch(input));
            Assert.Equal(expected, vm.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                Assert.Equal(start == 0 && expected, direct.IsMatch(input, start));
                Assert.Equal(start == 0 && expected, vm.IsMatch(input, start));
            }
        }
    }

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$")]
    [InlineData("^(.|(.)(?1)?\\2)$")]
    public void RecursivePalindromeObservesMatchOptionsAndLimits(string pattern)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);
        var input = "abcba"u8.ToArray();

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
            var expected = options is not (Pcre2MatchOptions.NotBol or Pcre2MatchOptions.NotEol);
            Assert.Equal(expected, direct.IsMatch(input, 0, options));
            Assert.Equal(expected, vm.IsMatch(input, 0, options));
        }

        var tightlyMetered = CreateMetered(pattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch("abcba"u8)).ErrorKind);
    }

    private static bool ScalarPalindrome(string text, bool allowsEmpty)
    {
        // Default $ may precede one terminal LF; dot cannot consume any LF.
        if (text.EndsWith('\n')) text = text[..^1];
        var scalars = text.EnumerateRunes().ToArray();
        return (allowsEmpty || scalars.Length != 0) &&
            !scalars.Any(rune => rune.Value == '\n') &&
            scalars.SequenceEqual(scalars.Reverse());
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern, uint matchLimit) =>
        new(
            pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = matchLimit },
            Regex.InfiniteMatchTimeout);
}
