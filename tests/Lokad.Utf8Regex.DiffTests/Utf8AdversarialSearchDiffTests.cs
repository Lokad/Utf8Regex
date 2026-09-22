using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// R07 adversarial search shapes beyond benchmarks, targeting the prepared
// substring-search tiers (first/last-byte anchors, dotnet-like anchors,
// Horspool, packed-pair, vector-anchored, SpanIndexOf delegation): periodic
// needles that defeat naive anchor loops, short-input edges, and long
// periodic literals. See the R08 memchr disposition in PLAN.md for the
// Two-Way port candidate these rows will guard.
public sealed class Utf8AdversarialSearchDiffTests
{
    public static IEnumerable<object[]> SearchRows()
    {
        const RegexOptions ci = RegexOptions.CultureInvariant;
        const RegexOptions ciIgnore = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        yield return new object[] { "aaaaab", new string((char)97, 200) + "b", ci };
        yield return new object[] { "aaaaab", new string((char)97, 200), ci };
        yield return new object[] { "ababababab", "abababababc", ci };
        yield return new object[] { "ababababab", new string((char)97, 200), ci };
        var longPeriodic = string.Concat(Enumerable.Repeat("ab", 20));
        yield return new object[] { longPeriodic, string.Concat(Enumerable.Repeat("ab", 200)), ci };
        yield return new object[] { longPeriodic, string.Concat(Enumerable.Repeat("ab", 199)) + "ac", ci };
        yield return new object[] { "abcdef", "abc", ci };
        yield return new object[] { "a", string.Empty, ci };
        yield return new object[] { "ab", "a", ci };
        yield return new object[] { "abcdefghij", "xxabcdefghijxx", ci };
        yield return new object[] { "AbCdEfGh", "xxabcdefghxx", ciIgnore };
        yield return new object[] { "QZXJKVBWFPQZ", "xxqzxjkvbwfPQZxx".ToLowerInvariant(), ciIgnore };
        yield return new object[] { "ABCDEFGHIJKLMNOP", "xxabcdefghijklmnoPxx", ciIgnore };
    }

    [Theory]
    [MemberData(nameof(SearchRows))]
    public void IsMatchMatchesRuntimeForAdversarialSearch(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [MemberData(nameof(SearchRows))]
    public void MatchMatchesRuntimeForAdversarialSearch(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertMatchParity();
    }

    [Theory]
    [MemberData(nameof(SearchRows))]
    public void CountMatchesRuntimeForAdversarialSearch(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertCountParity();
    }
}
