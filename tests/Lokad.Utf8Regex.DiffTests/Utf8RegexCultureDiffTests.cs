using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexCultureDiffTests
{
    [Theory]
    [InlineData("^aa$", "aA", "da-DK", RegexOptions.None)]
    [InlineData("^aa$", "aA", "da-DK", RegexOptions.IgnoreCase)]
    [InlineData("^aA$", "aA", "da-DK", RegexOptions.IgnoreCase)]
    public void IsMatchMatchesRuntimeForMirroredCultureCases(string pattern, string input, string cultureName, RegexOptions options)
    {
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/RegexCultureTests.cs
        using var _ = new ThreadCultureScope(cultureName);

        var utf8Regex = new Utf8Regex(pattern, options);
        var utf8Compiled = new Utf8Regex(pattern, options | RegexOptions.Compiled);
        var dotnet = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var dotnetCompiled = new Regex(pattern, options | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
        var bytes = Encoding.UTF8.GetBytes(input);

        var expected = dotnet.IsMatch(input);
        Assert.Equal(expected, dotnetCompiled.IsMatch(input));
        Assert.Equal(expected, utf8Regex.IsMatch(bytes));
        Assert.Equal(expected, utf8Compiled.IsMatch(bytes));
    }

    [Theory]
    [InlineData("\u212A", "K", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("\u212B", "\u00C5", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("\u03F4", "\u0398", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("\u212A|x", "K", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForMirroredIgnoreCaseCases(string pattern, string input, RegexOptions options)
    {
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/RegexIgnoreCaseTests.cs
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    private sealed class ThreadCultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;

        public ThreadCultureScope(string cultureName)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUiCulture = CultureInfo.CurrentUICulture;
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}
