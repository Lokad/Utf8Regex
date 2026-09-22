using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// R07 one-pass capture shapes beyond benchmarks: deterministic repetitions
// and disjoint alternations whose captures never need backtracking, plus
// ambiguous controls that do. A future one-pass analysis (in the RE2 sense:
// repetition ends and branch choices obvious per input byte) would claim
// exactly the deterministic rows; the controls must keep the general route.
// See the R08 re2 disposition in PLAN.md.
public sealed class Utf8OnePassCaptureDiffTests
{
    [Theory]
    [InlineData(@"(\d+)-(\d+)", "call 555-1234", RegexOptions.CultureInvariant)]
    [InlineData(@"([^ ]*) (.*)", "hello world", RegexOptions.CultureInvariant)]
    [InlineData(@"(a+)(b+)(c+)", "aaabbbcc", RegexOptions.CultureInvariant)]
    [InlineData(@"x(y|z)", "xy", RegexOptions.CultureInvariant)]
    [InlineData(@"(ab)(cd)", "abcd", RegexOptions.CultureInvariant)]
    [InlineData(@"(a*)(b+)", "aaabb", RegexOptions.CultureInvariant)]
    [InlineData(@"(.*) (.*)", "hello brave world", RegexOptions.CultureInvariant)]
    [InlineData(@"(xy|xz)", "xyz", RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForOnePassCaptures(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [InlineData(@"(\d+)-(\d+)", "call 555-1234", RegexOptions.CultureInvariant)]
    [InlineData(@"([^ ]*) (.*)", "hello world", RegexOptions.CultureInvariant)]
    [InlineData(@"(a+)(b+)(c+)", "aaabbbcc", RegexOptions.CultureInvariant)]
    [InlineData(@"x(y|z)", "xy", RegexOptions.CultureInvariant)]
    [InlineData(@"(ab)(cd)", "abcd", RegexOptions.CultureInvariant)]
    [InlineData(@"(a*)(b+)", "aaabb", RegexOptions.CultureInvariant)]
    [InlineData(@"(.*) (.*)", "hello brave world", RegexOptions.CultureInvariant)]
    [InlineData(@"(xy|xz)", "xyz", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForOnePassCaptures(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }
}
