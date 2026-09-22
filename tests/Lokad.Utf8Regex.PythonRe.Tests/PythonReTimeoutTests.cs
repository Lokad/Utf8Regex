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
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Search(CreateCatastrophicInput()));

        var input = Encoding.UTF8.GetBytes("aaabaaa");
        Assert.True(regex.Search(input).Success);
        Assert.Equal(1, regex.Count(input));
        Assert.Equal("aaab-", regex.SubnToString(input, "-").ResultText);
    }
}
