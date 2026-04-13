using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexDeferredOptionsDiffTests
{
    [Theory]
    [InlineData("abc", "xxabcxxabc", RegexOptions.RightToLeft | RegexOptions.CultureInvariant)]
    [InlineData("ab+", "ab abb abbb", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    public void IsMatchFallsBackAndStillMatchesRuntimeForDeferredOptionFamilies(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", RegexOptions.RightToLeft | RegexOptions.CultureInvariant)]
    [InlineData("ab+", "ab abb abbb", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    public void MatchFallsBackAndStillMatchesRuntimeForDeferredOptionFamilies(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertMatchParity();
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", "ZZ", RegexOptions.RightToLeft | RegexOptions.CultureInvariant)]
    [InlineData("ab+", "ab abb abbb", "Q", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    public void ReplaceFallsBackAndStillMatchesRuntimeForDeferredOptionFamilies(string pattern, string input, string replacement, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertReplaceParity(replacement);
    }
}
