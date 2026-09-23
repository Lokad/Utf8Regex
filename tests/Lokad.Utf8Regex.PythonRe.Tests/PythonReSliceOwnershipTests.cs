using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReSliceOwnershipTests
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

    [Theory]
    [InlineData("(a+)(b+)")]
    [InlineData("(ab|cd)+")]
    [InlineData("(a|ab)+c")]
    public void SliceShapesRouteOwned(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }

    [Theory]
    [InlineData("(a+)(b+)", "xxaaabbyy", 0)]
    [InlineData("(a+)(b+)", "xxaaabbyy", 2)]
    [InlineData("(a+)(b+)", "xxaaayy", 0)]
    [InlineData("(a|ab)+c", "xxabcabccyy", 0)]
    [InlineData("(a+)(b+)", "éaaabbé", 0)]
    public void OwnedSearchMatchesTranslatedBcl(string pattern, string inputText, int start)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var expected = Bcl(regex).Match(inputText, start);
        var found = regex.Search(Utf8(inputText), start);
        Assert.Equal(expected.Success, found.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, found.StartOffsetInUtf16);
            Assert.Equal(expected.Index + expected.Length, found.EndOffsetInUtf16);
        }

        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a+)(b+)", "xxaaabbyy", 0)]
    [InlineData("(a+)(b+)", "xxaaabbyy", 2)]
    [InlineData("(a+)(b+)", "xxaaabbyy", 3)]
    public void OwnedMatchMatchesTranslatedBcl(string pattern, string inputText, int start)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var input = Utf8(inputText);
        var expected = Bcl(regex).Match(inputText, start);
        var expectedAtStart = expected.Success && expected.Index == start;
        Assert.Equal(expectedAtStart, regex.Match(input, start).Success);
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a+)(b+)", "aab aabb", 0)]
    [InlineData("(a+)(b+)", "aab aabb", 4)]
    [InlineData("(a+)(b+)", "xyz", 0)]
    public void OwnedCountMatchesTranslatedBcl(string pattern, string inputText, int start)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var expected = Bcl(regex).Matches(inputText, start).Count;
        var input = Utf8(inputText);
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedProjectsGroups()
    {
        var regex = new Utf8PythonRegex("(a+)(b+)", PythonReCompileOptions.None);
        var expected = Bcl(regex).Match("xxaaabbyy");
        var detailed = regex.SearchDetailed(Utf8("xxaaabbyy"), 0);
        Assert.True(detailed.Success);
        Assert.Equal(expected.Groups[0].Value, detailed.GetGroup(0).GetValueString());
        Assert.Equal(expected.Groups[1].Value, detailed.GetGroup(1).GetValueString());
        Assert.Equal(expected.Groups[2].Value, detailed.GetGroup(2).GetValueString());
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }

    [Fact]
    public void FullMatchStaysManagedForSliceShapes()
    {
        var regex = new Utf8PythonRegex("(a+)(b+)", PythonReCompileOptions.None);
        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugFullMatchBackend);
        Assert.True(regex.FullMatch(Utf8("aaabb")).Success);
        Assert.False(regex.FullMatch(Utf8("xxaaabbyy")).Success);
    }

    [Theory]
    [InlineData("a*", "ba", 0, 3)]
    [InlineData("x*|y", "y", 0, 3)]
    public void OwnedEmptyCapableCountMatchesCpython(string pattern, string inputText, int start, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var input = Utf8(inputText);
        Assert.Equal(expected, regex.Count(input, start));
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
    [Fact]
    public void OwnedSliceCountTimeoutThrowsMatchTimeout()
    {
        // Quadratic miss census: every start unit replays
        // the (a+)(b+) miss over the remaining suffix, burning
        // ~0.6 s in Debug, so the 1 ms budget expires inside
        // the owned slice and surfaces with timeout identity
        // instead of the raw deadline exception.
        var input = Utf8(new string((char)97, 2000));
        var regex = new Utf8PythonRegex(
            "(a+)(b+)",
            PythonReCompileOptions.None,
            TimeSpan.FromMilliseconds(1));
        Assert.True(regex.DebugUsesOwnedSlice);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedSearch()
    {
        var regex = new Utf8PythonRegex("(a+)(b+)", PythonReCompileOptions.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => regex.Search(Utf8("xxaaabbyy"), 0, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => regex.Count(Utf8("xxaaabbyy"), 0, canceled.Token));
    }
}


