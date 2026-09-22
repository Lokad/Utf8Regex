using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// R07 adversarial alternation shapes beyond benchmarks, targeting the
// multi-literal family routes: branch-order sensitivity (leftmost-first, not
// longest), shared prefixes/suffixes, many-branch families, and empty
// branches. Any future combined-automaton port (see the R08 aho-corasick
// disposition in PLAN.md) must preserve these orderings.
public sealed class Utf8AdversarialAlternationDiffTests
{
    [Theory]
    [InlineData("abc|ab|a", "abc", RegexOptions.CultureInvariant)]
    [InlineData("a|ab|abc", "abc", RegexOptions.CultureInvariant)]
    [InlineData("abcd|bc", "abcd", RegexOptions.CultureInvariant)]
    [InlineData("bc|abcd", "abcd", RegexOptions.CultureInvariant)]
    [InlineData("car|card|care|careful|cat", "careful", RegexOptions.CultureInvariant)]
    [InlineData("horse|cat|horsefly", "horsefly", RegexOptions.CultureInvariant)]
    [InlineData("aa|bb|cc|dd|ee|ff|gg|hh|ii|jj|kk|ll", "xkkx", RegexOptions.CultureInvariant)]
    [InlineData("cat|", "dog", RegexOptions.CultureInvariant)]
    [InlineData("CAT|horse", "CAT horse", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "horsecat", RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForAdversarialAlternation(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [InlineData("abc|ab|a", "abc", RegexOptions.CultureInvariant)]
    [InlineData("a|ab|abc", "abc", RegexOptions.CultureInvariant)]
    [InlineData("abcd|bc", "abcd", RegexOptions.CultureInvariant)]
    [InlineData("bc|abcd", "abcd", RegexOptions.CultureInvariant)]
    [InlineData("car|card|care|careful|cat", "careful", RegexOptions.CultureInvariant)]
    [InlineData("horse|cat|horsefly", "horsefly", RegexOptions.CultureInvariant)]
    [InlineData("aa|bb|cc|dd|ee|ff|gg|hh|ii|jj|kk|ll", "xkkx", RegexOptions.CultureInvariant)]
    [InlineData("cat|", "dog", RegexOptions.CultureInvariant)]
    [InlineData("CAT|horse", "CAT horse", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "horsecat", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForAdversarialAlternation(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }
}
