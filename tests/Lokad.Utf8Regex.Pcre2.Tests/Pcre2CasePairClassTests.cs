using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Sibling audit of the core case-pair folding fix: explicit-case classes
// must match both variants on every adapter route too.
public sealed class Pcre2CasePairClassTests
{
    [Theory]
    [InlineData("[Aa]", "A")]
    [InlineData("[Aa]", "a")]
    [InlineData("[Aa][Bb][Cc]", "ABC")]
    [InlineData("[Aa][Bb][Cc]", "abc")]
    [InlineData("[Aa][Bb][Cc]", "abx")]
    public void CasePairClassesMatchBothVariants(string pattern, string inputText)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        var input = Encoding.UTF8.GetBytes(inputText);
        var expected = inputText != "abx";
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected, regex.Match(input).Success);
        Assert.Equal(expected ? 1 : 0, regex.Count(input));
    }
}
