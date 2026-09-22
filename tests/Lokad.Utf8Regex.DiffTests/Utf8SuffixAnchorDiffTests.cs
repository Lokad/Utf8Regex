using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// R07 suffix-anchored candidate discovery beyond benchmarks: end anchors,
// absolute end, end-or-final-newline, and anchored suffix loops. A future
// reverse-scan (suffix-first) candidate optimization must agree with forward
// verification on all of these (see the R08 regex-rust disposition in
// PLAN.md: reverse inner/suffix search with quadratic backstops).
public sealed class Utf8SuffixAnchorDiffTests
{
    [Theory]
    [InlineData(@"abc$", "xxabc", RegexOptions.CultureInvariant)]
    [InlineData(@"abc$", "abcxx", RegexOptions.CultureInvariant)]
    [InlineData(@"abc$", "xxabc\n", RegexOptions.CultureInvariant)]
    [InlineData(@"\w+@\w+$", "mail a@b", RegexOptions.CultureInvariant)]
    [InlineData(@"\d+-\d+$", "call 555-1234", RegexOptions.CultureInvariant)]
    [InlineData(@"\d+-\d+$", "call 555-1234!", RegexOptions.CultureInvariant)]
    [InlineData(@"cat$", "cat\ncat", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData(@"[a-z]+$", "ABC", RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForSuffixAnchors(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [InlineData(@"abc$", "xxabc", RegexOptions.CultureInvariant)]
    [InlineData(@"abc$", "abcxx", RegexOptions.CultureInvariant)]
    [InlineData(@"abc$", "xxabc\n", RegexOptions.CultureInvariant)]
    [InlineData(@"\w+@\w+$", "mail a@b", RegexOptions.CultureInvariant)]
    [InlineData(@"\d+-\d+$", "call 555-1234", RegexOptions.CultureInvariant)]
    [InlineData(@"\d+-\d+$", "call 555-1234!", RegexOptions.CultureInvariant)]
    [InlineData(@"cat$", "cat\ncat", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData(@"[a-z]+$", "ABC", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForSuffixAnchors(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }
}
