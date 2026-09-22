using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReResourcePolicyTests
{
    [Fact]
    public void UnlimitedPolicyConstructsBothFlavorsAndRoundTrips()
    {
        var fromText = new Utf8PythonRegex("cat", PythonReCompileOptions.None, Utf8PythonRegex.DefaultMatchTimeout, Utf8ResourcePolicy.Unlimited);
        var fromBytes = new Utf8PythonRegex("cat"u8, PythonReCompileOptions.None, Utf8PythonRegex.DefaultMatchTimeout, Utf8ResourcePolicy.Unlimited);
        Assert.Equal(Utf8ResourcePolicy.Unlimited, fromText.ResourcePolicy);
        Assert.Equal(Utf8ResourcePolicy.Unlimited, fromBytes.ResourcePolicy);
        Assert.Equal(1, fromText.Count(Encoding.UTF8.GetBytes("cat")));
        Assert.Equal(1, fromBytes.Count(Encoding.UTF8.GetBytes("cat")));
    }

    [Fact]
    public void FiniteBudgetsAreNotSupportedByPythonRe()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100ul };
        Assert.Throws<System.NotSupportedException>(() => new Utf8PythonRegex("cat", PythonReCompileOptions.None, Utf8PythonRegex.DefaultMatchTimeout, policy));
        Assert.Throws<System.NotSupportedException>(() => new Utf8PythonRegex("cat"u8, PythonReCompileOptions.None, Utf8PythonRegex.DefaultMatchTimeout, policy));
    }

    [Fact]
    public void ZeroBudgetIsRejectedBeforeSupportCheck()
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = 0u };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new Utf8PythonRegex("cat", PythonReCompileOptions.None, Utf8PythonRegex.DefaultMatchTimeout, policy));
    }
    [Theory]
    [InlineData("cat", "xxcatxx")]
    [InlineData(@"(\w+)@(\w+)", "mail me a@b.cd ok")]
    public void DefaultMatchesExplicitUnlimited(string pattern, string inputText)
    {
        // R06 compatibility evidence: default construction behaves exactly like
        // an explicit Unlimited policy across value and global operations.
        var input = Encoding.UTF8.GetBytes(inputText);
        var baseline = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var explicitUnlimited = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Utf8PythonRegex.DefaultMatchTimeout, Utf8ResourcePolicy.Unlimited);

        Assert.Equal(baseline.Count(input), explicitUnlimited.Count(input));
        var expectedSearch = baseline.Search(input);
        var actualSearch = explicitUnlimited.Search(input);
        Assert.Equal(expectedSearch.Success, actualSearch.Success);
        Assert.Equal(expectedSearch.StartOffsetInUtf16, actualSearch.StartOffsetInUtf16);
        Assert.Equal(expectedSearch.EndOffsetInUtf16, actualSearch.EndOffsetInUtf16);
        Assert.Equal(baseline.FindAll(input).Length, explicitUnlimited.FindAll(input).Length);
        Assert.Equal(baseline.SplitToStrings(input).Length, explicitUnlimited.SplitToStrings(input).Length);
    }
}
