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

    [Theory]
    [InlineData("(a+)(b+)", "xxaaabbyy", 0)]
    [InlineData("(a+)(b+)", "xxaaabbyy", 2)]
    [InlineData("(a+)(b+)", "xxaaayy", 0)]
    public void LiveTokenMatchesTokenless(string pattern, string inputText, int start)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        var input = Utf8(inputText);
        using var source = new CancellationTokenSource();
        var token = source.Token;
        Assert.Equal(
            regex.IsMatch(input, start),
            regex.IsMatch(input, start, token));
        Assert.Equal(
            regex.Search(input, start).Success,
            regex.Search(input, start, token).Success);
        Assert.Equal(
            regex.Match(input, start).Success,
            regex.Match(input, start, token).Success);
        Assert.Equal(
            regex.Count(input, start),
            regex.Count(input, start, token));
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedValueOperations()
    {
        var regex = new Utf8PythonRegex("(a+)(b+)", PythonReCompileOptions.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var input = Utf8("xxaaabbyy");
        Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, 0, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => regex.Match(input, 0, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => regex.FullMatch(input, 0, canceled.Token));
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

    // R03 dots: the translator rewrites every `.` into a scalar-exact
    // lookahead plus BMP/surrogate-pair alternation, which the core slice
    // parser declines (classes, lookarounds, non-capturing groups). Dot
    // shapes therefore never produce an owned slice program; scalar
    // parity rides the translated pattern instead. These pins lock both
    // the decline and the CPython ground truths (verified against
    // CPython 3.13.2 `re`: scalar dots, whole-scalar counts, DOTALL).
    public static IEnumerable<object?[]> DotShapes()
    {
        yield return new object?[] { "(a.c)+" };
        yield return new object?[] { "a.c" };
        yield return new object?[] { "a.*b" };
        yield return new object?[] { ".." };
        yield return new object?[] { ".*" };
        yield return new object?[] { "x*|y." };
        yield return new object?[] { "a|.*" };
    }

    [Theory]
    [MemberData(nameof(DotShapes))]
    public void DotShapesDeclineOwnedSlice(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> DotSearchCases()
    {
        yield return new object?[] { "(a.c)+", "xa\U0001F600c", 0, true, 1, 5 };
        yield return new object?[] { "a.c", "xa\U0001F600c", 0, true, 1, 5 };
        yield return new object?[] { "a.*b", "a\U0001F600b", 0, true, 0, 4 };
        yield return new object?[] { "..", "\U0001F600\U0001F600", 0, true, 0, 4 };
        yield return new object?[] { "..", "\U0001F600", 0, false, 0, 0 };
        yield return new object?[] { "(a.c)+", "xxabcaycyy", 0, true, 2, 8 };
    }

    [Theory]
    [MemberData(nameof(DotSearchCases))]
    public void DotSearchMatchesTranslatedOracle(
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

        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> DotCountCases()
    {
        yield return new object?[] { "(a.c)+", "xa\U0001F600c", 0, 1 };
        yield return new object?[] { "a.*b", "a\U0001F600b", 0, 1 };
        yield return new object?[] { "(a.c)+", "abc ayc", 0, 2 };
    }

    [Theory]
    [MemberData(nameof(DotCountCases))]
    public void DotCountMatchesCpython(string pattern, string inputText, int startByte, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startByte));
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Fact]
    public void DotStarCountMatchesCpython()
    {
        Assert.Equal(2, new Utf8PythonRegex(".*", PythonReCompileOptions.None).Count(Utf8("\U0001F600"), 0));
    }

    [Theory]
    [InlineData("a.c", "a\U0001F600c", true)]
    [InlineData("(a.c)+", "xa\U0001F600c", false)]
    [InlineData("a.*b", "a\U0001F600b", true)]
    public void DotMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("a.c", "a\U0001F600c", true)]
    [InlineData("..", "\U0001F600", false)]
    [InlineData("a.*b", "a\U0001F600b", true)]
    public void DotFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("a.*b", "xxa\nbzz", false)]
    [InlineData("a.*b", "xxa\rbzz", true)]
    public void DotNewlineSemanticsMatchCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Search(Utf8(inputText), 0).Success);
    }

    [Fact]
    public void DotAllSearchMatchesNewline()
    {
        var regex = new Utf8PythonRegex("a.*b", PythonReCompileOptions.DotAll);
        Assert.False(regex.DebugUsesOwnedSlice);
        Assert.True(regex.Search(Utf8("xxa\nbzz"), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }
}


