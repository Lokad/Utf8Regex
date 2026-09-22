using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Sibling audit of the core case-pair folding fix: explicit-case classes
// must match both variants on every adapter route too.
[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonCasePairClassRegressionTests
{
    [Theory]
    [InlineData("[Aa]", "A", true)]
    [InlineData("[Aa]", "a", true)]
    [InlineData("[Aa][Bb][Cc]", "ABC", true)]
    [InlineData("[Aa][Bb][Cc]", "abc", true)]
    [InlineData("[Aa][Bb][Cc]", "abx", false)]
    public void CasePairClassesMatchBothVariants(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Search(input).Success);
        Assert.Equal(expected ? 1 : 0, regex.Count(input));
    }
}
