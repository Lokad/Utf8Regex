using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// R07 equivalent spellings beyond benchmarks: the same language written as a
// folded literal, an explicit-case class union, or a factored alternation
// must agree, so callers never need to hand-rewrite one form into another
// (cf. the vectorscan performance guidance; see the R08 vectorscan
// disposition in PLAN.md).
public sealed class Utf8EquivalentSpellingDiffTests
{
    [Theory]
    [InlineData("[Aa]", "A", RegexOptions.CultureInvariant)]
    [InlineData("[Aa]", "a", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxx", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxABCxx", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabdxx", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("[Aa][Bb][Cc]", "xxabcxx", RegexOptions.CultureInvariant)]
    [InlineData("[Aa][Bb][Cc]", "xxABCxx", RegexOptions.CultureInvariant)]
    [InlineData("[Aa][Bb][Cc]", "xxabdxx", RegexOptions.CultureInvariant)]
    [InlineData("(A|a)(B|b)(C|c)", "xxabcxx", RegexOptions.CultureInvariant)]
    [InlineData("(A|a)(B|b)(C|c)", "xxABCxx", RegexOptions.CultureInvariant)]
    [InlineData("(A|a)(B|b)(C|c)", "xxabdxx", RegexOptions.CultureInvariant)]
    [InlineData("foo(bar|baz)(frotz)?", "foobaz", RegexOptions.CultureInvariant)]
    [InlineData("foo(bar|baz)(frotz)?", "foobarfrotz", RegexOptions.CultureInvariant)]
    [InlineData("foo(bar|baz)(frotz)?", "foobax", RegexOptions.CultureInvariant)]
    [InlineData("foobarfrotz|foobazfrotz|foobar|foobaz", "foobaz", RegexOptions.CultureInvariant)]
    [InlineData("foobarfrotz|foobazfrotz|foobar|foobaz", "foobarfrotz", RegexOptions.CultureInvariant)]
    [InlineData("foobarfrotz|foobazfrotz|foobar|foobaz", "foobax", RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForEquivalentSpellings(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [InlineData("[Aa]", "A", RegexOptions.CultureInvariant)]
    [InlineData("[Aa]", "a", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxx", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxABCxx", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("[Aa][Bb][Cc]", "xxabcxx", RegexOptions.CultureInvariant)]
    [InlineData("[Aa][Bb][Cc]", "xxABCxx", RegexOptions.CultureInvariant)]
    [InlineData("(A|a)(B|b)(C|c)", "xxabcxx", RegexOptions.CultureInvariant)]
    [InlineData("(A|a)(B|b)(C|c)", "xxABCxx", RegexOptions.CultureInvariant)]
    [InlineData("foo(bar|baz)(frotz)?", "foobaz", RegexOptions.CultureInvariant)]
    [InlineData("foo(bar|baz)(frotz)?", "foobarfrotz", RegexOptions.CultureInvariant)]
    [InlineData("foobarfrotz|foobazfrotz|foobar|foobaz", "foobaz", RegexOptions.CultureInvariant)]
    [InlineData("foobarfrotz|foobazfrotz|foobar|foobaz", "foobarfrotz", RegexOptions.CultureInvariant)]
    public void MatchMatchesRuntimeForEquivalentSpellings(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertMatchParity();
    }
}
