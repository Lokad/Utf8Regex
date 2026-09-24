using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReSliceConditionalTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static void AssertOwnedBackends(Utf8PythonRegex regex)
    {
        Assert.True(regex.DebugUsesOwnedSlice, "pattern=" + regex.Pattern);
        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugSearchBackend);
        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugMatchBackend);
        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugCountBackend);
    }

    private static Regex Bcl(Utf8PythonRegex regex)
    {
        return new Regex(regex.DebugTranslatedPattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
    }

    // R03 conditionals for the Python profile: the translator maps group
    // conditionals (with number permutation) onto the owned core slice.
    // Ground truths are CPython 3.13.2.
    [Theory]
    [InlineData("(a)?(?(1)b|c)")]
    [InlineData("(a)(?(1)b|c)(d)")]
    [InlineData("(?P<n>a)?(?(n)b|c)")]
    public void ConditionalShapesRouteOwned(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a)?(?(1)b|c)", "ab", 0, true, 0, 2)]
    [InlineData("(a)?(?(1)b|c)", "c", 0, true, 0, 1)]
    [InlineData("(a)(?(1)b|c)(d)", "abd", 0, true, 0, 3)]
    [InlineData("(?P<n>a)?(?(n)b|c)", "ab", 0, true, 0, 2)]
    [InlineData("(?P<n>a)?(?(n)b|c)", "c", 0, true, 0, 1)]
    public void ConditionalSearchMatchesTranslatedOracle(
        string pattern, string inputText, int startByte, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var input = Utf8(inputText);
        var expected = Bcl(regex).Match(inputText, 0);
        var found = regex.Search(input, startByte);
        Assert.Equal(expectedSuccess, expected.Success);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expected.Index, found.StartOffsetInUtf16);
            Assert.Equal(expected.Index + expected.Length, found.EndOffsetInUtf16);
            Assert.Equal(expectedStart, found.StartOffsetInUtf16);
            Assert.Equal(expectedEnd, found.EndOffsetInUtf16);
        }

        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a)?(?(1)b|c)", "ab", 1)]
    [InlineData("(a)?(?(1)b|c)", "c", 1)]
    [InlineData("(a)(?(1)b|c)(d)", "abd", 1)]
    [InlineData("(?P<n>a)?(?(n)b|c)", "ab", 1)]
    [InlineData("(?P<n>a)?(?(n)b|c)", "c", 1)]
    public void ConditionalCpythonCountMatchesManaged(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText)));
        AssertOwnedBackends(regex);
    }

    [Theory]
    [InlineData("(a)?(?(1)b|c)", "ab", 0, 2)]
    [InlineData("(a)?(?(1)b|c)", "c", 0, 1)]
    [InlineData("(?P<n>a)?(?(n)b|c)", "ab", 0, 2)]
    public void ConditionalCpythonMatchMatchesManaged(string pattern, string inputText, int expectedStart, int expectedLength)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var found = regex.Match(Utf8(inputText));
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.StartOffsetInUtf16);
        Assert.Equal(expectedLength, found.EndOffsetInUtf16 - found.StartOffsetInUtf16);
        AssertOwnedBackends(regex);
    }

    [Theory]
    [InlineData("(?P<n>a)?(?(n)b|c)", "ab", true)]
    [InlineData("(?P<n>a)?(?(n)b|c)", "c", true)]
    [InlineData("(?P<n>a)?(?(n)b|c)", "ac", false)]
    public void ConditionalCpythonFullMatchMatchesManaged(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        AssertOwnedBackends(regex);
    }
}