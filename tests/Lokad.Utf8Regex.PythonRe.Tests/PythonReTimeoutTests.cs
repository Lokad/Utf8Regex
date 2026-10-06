using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Timeout enforcement across result-producing operations: catastrophic input
// aborts Search, substitution, and split with RegexMatchTimeoutException, and
// the timed-out instance stays usable afterwards.
[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReTimeoutTests
{
    private static byte[] CreateCatastrophicInput()
    {
        return Encoding.UTF8.GetBytes(new string((char)97, 100000) + "!");
    }

    [Fact]
    public void CatastrophicSearchHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Search(CreateCatastrophicInput()));
    }

    [Fact]
    public void CatastrophicSubstitutionHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.SubnToString(CreateCatastrophicInput(), "-"));
    }

    [Fact]
    public void CatastrophicSplitHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.SplitToStrings(CreateCatastrophicInput()));
    }

    [Fact]
    public void TimedOutInstanceRemainsUsable()
    {
        // Recovery includes the first substitution call. Give its cold path
        // scheduling headroom while still forcing a real catastrophic timeout.
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(100));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Search(CreateCatastrophicInput()));

        var input = Encoding.UTF8.GetBytes("aaabaaa");
        Assert.True(regex.Search(input).Success);
        Assert.Equal(1, regex.Count(input));
        Assert.Equal("aaab-", regex.SubnToString(input, "-").ResultText);
    }
    [Fact]
    public void FiniteTimeoutSupportsFastPaths()
    {
        // This is a finite-policy success smoke, including cold core discovery
        // and projection. Their deadline now includes startup; a 1 ms ceiling
        // can correctly expire there. Explicit expiry tests keep tight budgets.
        var regex = new Utf8PythonRegex("abc", PythonReCompileOptions.None, TimeSpan.FromSeconds(1));
        var input = Encoding.UTF8.GetBytes("xxabcxx");

        Assert.True(regex.Search(input).Success);
        Assert.Equal(1, regex.Count(input));
        Assert.Single(regex.FindAll(input));
        Assert.Equal("xxZxx", regex.SubnToString(input, "Z").ResultText);
        Assert.Equal(2, regex.SplitToStrings(input).Length);
    }
    [Fact]
    public void CatastrophicFindIterDetailedHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.FindIterDetailed(CreateCatastrophicInput()));
    }

    [Fact]
    public void CatastrophicSubnHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Subn(CreateCatastrophicInput(), "-"));
    }
}
