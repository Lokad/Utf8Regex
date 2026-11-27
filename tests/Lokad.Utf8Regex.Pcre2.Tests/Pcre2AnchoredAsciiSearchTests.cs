using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AnchoredAsciiSearchTests
{
    private const string FloatPattern = @"^[-+]?\d*\.?\d*$";

    [Fact]
    public void FloatLanguagePreservesBooleanAndCaptureResults()
    {
        var regex = new Utf8Pcre2Regex(FloatPattern);
        Assert.True(regex.IsMatch("-3.14159"u8));
        Assert.True(regex.IsMatch("+.5"u8));
        Assert.True(regex.IsMatch("."u8));
        Assert.True(regex.IsMatch([]));
        Assert.False(regex.IsMatch("1.2.3"u8));
        Assert.False(regex.IsMatch("١٢"u8));

        var detailed = regex.Match("-3.14159"u8);
        Assert.True(detailed.Success);
        Assert.Equal("-3.14159", System.Text.Encoding.UTF8.GetString(detailed.GetValueBytes()));
    }

    [Fact]
    public void AnchoredAsciiResultsAgreeAtEveryValidStart()
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
    public void AmbiguousRegularLanguagesMatchAnIndependentOracle()
    {
        foreach (var pattern in new[]
                 {
                     @"^\d*\d?b$",
                     @"^\d+\d*b+$",
                     @"^\d*\.?\d*a$",
                 })
        {
            var direct = new Utf8Pcre2Regex(pattern);
            var vm = CreateMetered(pattern);
            var oracle = new Regex(pattern.Replace(@"\d", "[0-9]", StringComparison.Ordinal)
                .Replace(@"\s", @"[\t\n\r\f\v ]", StringComparison.Ordinal), RegexOptions.CultureInvariant);

            foreach (var inputText in EnumerateWords("ab0.\n", 5))
            {
                var input = Encoding.UTF8.GetBytes(inputText);
                var expected = oracle.IsMatch(inputText);
                Assert.Equal(expected, direct.IsMatch(input));
                Assert.Equal(expected, vm.IsMatch(input));
            }
        }
    }

    private static void AssertEquivalentAtEveryValidStart(string pattern, params string[] inputs)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern);
        var oracle = new Regex(pattern.Replace(@"\d", "[0-9]", StringComparison.Ordinal)
            .Replace(@"\s", @"[\t\n\r\f\v ]", StringComparison.Ordinal), RegexOptions.CultureInvariant);

        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            var expected = oracle.IsMatch(inputText);
            Assert.Equal(expected, direct.IsMatch(input));
            Assert.Equal(expected, vm.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                var expectedAtStart = oracle.IsMatch(inputText, Encoding.UTF8.GetCharCount(input.AsSpan(0, start)));
                Assert.Equal(expectedAtStart, direct.IsMatch(input, start));
                Assert.Equal(expectedAtStart, vm.IsMatch(input, start));
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

    private static Utf8Pcre2Regex CreateMetered(string pattern) =>
        new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 4096
            },
            MatchTimeout = Regex.InfiniteMatchTimeout
        });
}
