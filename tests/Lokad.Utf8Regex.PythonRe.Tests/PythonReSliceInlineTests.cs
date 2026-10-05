using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReSliceInlineTests
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

    // R03 inline options for the Python profile: the translator passes
    // scoped i/m/s/x flags through verbatim onto the owned core slice
    // (dots are rewritten scalar-exact first and decline, like bare dots;
    // a leading toggle is hoisted into the translation options instead).
    // Ground truths are CPython 3.13.2 with UTF-16 offsets.
    [Theory]
    [InlineData("(?m:^a)+")]
    [InlineData("(?i:ab)+")]
    [InlineData("(a)(?i:b)+")]
    [InlineData("(?im:ab)+")]
    [InlineData("(?-i:ab)+")]
    public void InlineShapesRouteOwned(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(pattern, regex.DebugTranslatedPattern);
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?s:a.c)+")]
    [InlineData("(?s:(?i:a.c))+")]
    [InlineData("(?x:a b)+")]
    [InlineData("(?i)a+")]
    public void RewrittenAndHoistedShapesKeepTheirQualifiedRoutes(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(pattern.Contains('.') || pattern.Contains("(?x:", StringComparison.Ordinal), regex.DebugUsesOwnedSlice);
    }

    [Fact]
    public void LeadingToggleHoistsIntoTranslationOptions()
    {
        var regex = new Utf8PythonRegex("(?i)a+", PythonReCompileOptions.None);
        Assert.Equal("a+", regex.DebugTranslatedPattern);
        Assert.False(regex.DebugUsesOwnedSlice);
    }

    [Fact]
    public void ScopedVerbosePrestripsBeforeTranslation()
    {
        var regex = new Utf8PythonRegex("(?x:a b)+", PythonReCompileOptions.None);
        Assert.Equal("(?x:ab)+", regex.DebugTranslatedPattern);
        AssertOwnedBackends(regex);
        var input = Utf8("xababx");
        Assert.Equal("abab", regex.Search(input).GetValueString());
        Assert.Equal(1, regex.Count(input));
        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }

    public static IEnumerable<object?[]> SearchCases()
    {
        yield return new object?[] { "(?m:^a)+", "x\na", 0, true, 2, 3 };
        yield return new object?[] { "(?m:^a)+", "xa", 0, false, 0, 0 };
        yield return new object?[] { "(?i:ab)+", "xxABxx", 0, true, 2, 4 };
        yield return new object?[] { "(?i:ab)+", "xxabxx", 0, true, 2, 4 };
        yield return new object?[] { "(a)(?i:b)+", "xaBB", 0, true, 1, 4 };
        yield return new object?[] { "(a)(?i:b)+", "xabb", 0, true, 1, 4 };
        yield return new object?[] { "(?im:ab)+", "AB", 0, true, 0, 2 };
        yield return new object?[] { "(?-i:ab)+", "ab", 0, true, 0, 2 };
        yield return new object?[] { "(?-i:ab)+", "AB", 0, false, 0, 0 };
        yield return new object?[] { "(?s:a.c)+", "xa\ncy", 0, true, 1, 4 };
        yield return new object?[] { "(?s:a.c)+", "xabcy", 0, true, 1, 4 };
        yield return new object?[] { "(?x:a b)+", "xabx", 0, true, 1, 3 };
        yield return new object?[] { "(?x:a b)+", "xaxb", 0, false, 0, 0 };
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public void InlineSearchMatchesTranslatedOracle(
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

        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?i)a+", "AaA", 0, true, 0, 3)]
    [InlineData("(?i)a+", "bbb", 0, false, 0, 0)]
    public void LeadingToggleSearchMatchesCpython(
        string pattern, string inputText, int startByte, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        // The toggle is hoisted into the translation options, so the
        // translated-pattern oracle needs the forwarded IgnoreCase flag.
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var expected = new Regex(
            regex.DebugTranslatedPattern,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            Regex.InfiniteMatchTimeout).Match(inputText, 0);
        var found = regex.Search(Utf8(inputText), startByte);
        Assert.Equal(expectedSuccess, expected.Success);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expected.Index, found.StartOffsetInUtf16);
            Assert.Equal(expectedStart, found.StartOffsetInUtf16);
            Assert.Equal(expectedEnd, found.EndOffsetInUtf16);
        }

        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?m:^a)+", "x\na", 1)]
    [InlineData("(?i:ab)+", "xxABxx", 1)]
    [InlineData("(a)(?i:b)+", "xaBB", 1)]
    [InlineData("(?im:ab)+", "AB", 1)]
    [InlineData("(?-i:ab)+", "ab", 1)]
    [InlineData("(?-i:ab)+", "AB", 0)]
    [InlineData("(?s:a.c)+", "xa\ncy", 1)]
    [InlineData("(?i)a+", "AaA", 1)]
    [InlineData("(?x:a b)+", "xabx", 1)]
    public void InlineCountMatchesCpython(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?m:^a)+", "x\na", false)]
    [InlineData("(?i:ab)+", "xxABxx", false)]
    [InlineData("(a)(?i:b)+", "xaBB", false)]
    [InlineData("(?s:a.c)+", "xa\ncy", false)]
    [InlineData("(?x:a b)+", "xabx", false)]
    [InlineData("(?i)a+", "bbb", false)]
    [InlineData("(?im:ab)+", "AB", true)]
    [InlineData("(?-i:ab)+", "ab", true)]
    [InlineData("(?i)a+", "AaA", true)]
    public void InlineMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?m:^a)+", "x\na", false)]
    [InlineData("(?i:ab)+", "AB", true)]
    [InlineData("(a)(?i:b)+", "aBB", true)]
    [InlineData("(?im:ab)+", "ab", true)]
    [InlineData("(?-i:ab)+", "ab", true)]
    [InlineData("(?-i:ab)+", "AB", false)]
    [InlineData("(?s:a.c)+", "a\nc", true)]
    [InlineData("(?i)a+", "AaA", true)]
    [InlineData("(?i)a+", "bbb", false)]
    [InlineData("(?x:a b)+", "ab", true)]
    public void InlineFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Fact]
    public void InlineScopedGroupProjectsThroughSearchDetailed()
    {
        const string pattern = "(a)(?i:b)+";
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var expected = Bcl(regex).Match("xaBB", 0);
        Assert.True(expected.Success);
        var detailed = regex.SearchDetailed(Utf8("xaBB"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("aBB", detailed.GetGroup(0).GetValueString());
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}
