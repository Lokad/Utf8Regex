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

    // R03 anchors: dot-free anchor shapes (`^a|b$`) pass the translator
    // verbatim and run the owned core slice; dotted anchor shapes decline
    // (scalar-exact class rewrite, same as dots). Simple literal anchors
    // (`a$`, `^a`) stay on the AsciiSimple core route. Multiline `^` search
    // past a line feed misses on the AsciiSimple route (pre-existing core
    // gap, also observable without PythonRe); multiline rows below only
    // cover slice-owned shapes. Expectations are CPython 3.13.2 ground
    // truth with UTF-16 offsets.
    [Theory]
    [InlineData("^a|b$")]
    public void AnchorShapesRouteOwned(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.True(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugSearchBackend);
        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugCountBackend);
    }

    [Theory]
    [InlineData("^a.*b$")]
    [InlineData("^(a.c)+$")]
    [InlineData("a$")]
    [InlineData("^a")]
    public void DottedAndSimpleAnchorsDeclineOwnedSlice(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> AnchorSearchCases()
    {
        yield return new object?[] { "^a|b$", "aXb", 0, true, 0, 1 };
        yield return new object?[] { "^a|b$", "xax", 0, false, 0, 0 };
        yield return new object?[] { "^a.*b$", "a1b", 0, true, 0, 3 };
        yield return new object?[] { "^a.*b$", "xa1b", 0, false, 0, 0 };
        yield return new object?[] { "^(a.c)+$", "abcayc", 0, true, 0, 6 };
        yield return new object?[] { "a$", "xxa", 0, true, 2, 3 };
        yield return new object?[] { "^a", "axx", 0, true, 0, 1 };
    }

    [Theory]
    [MemberData(nameof(AnchorSearchCases))]
    public void AnchorSearchMatchesTranslatedOracle(
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

    [Fact]
    public void MultilineAnchorSearchMatchesTranslatedOracle()
    {
        var regex = new Utf8PythonRegex("^a|b$", PythonReCompileOptions.Multiline);
        Assert.True(regex.DebugUsesOwnedSlice);
        var expected = new Regex(
            regex.DebugTranslatedPattern,
            RegexOptions.CultureInvariant | RegexOptions.Multiline,
            Regex.InfiniteMatchTimeout).Match("x\na");
        var found = regex.Search(Utf8("x\na"), 0);
        Assert.True(expected.Success);
        Assert.True(found.Success);
        Assert.Equal(expected.Index, found.StartOffsetInUtf16);
        Assert.Equal(2, found.StartOffsetInUtf16);
        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }

    [Theory]
    [InlineData("^a|b$", "aXb", 2)]
    [InlineData("^a.*b$", "a1b", 1)]
    [InlineData("^(a.c)+$", "abcayc", 1)]
    public void AnchorCountMatchesCpython(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^a|b$", "aXb", true)]
    [InlineData("^a|b$", "xax", false)]
    [InlineData("^a.*b$", "a1b", true)]
    public void AnchorMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^a|b$", "a", true)]
    [InlineData("^a|b$", "b", true)]
    [InlineData("^a|b$", "aXb", false)]
    [InlineData("^a.*b$", "a1b", true)]
    public void AnchorFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    // R03 classes: positive ASCII classes pass the translator through
    // and run the owned core slice; negated classes are rewritten
    // scalar-exact (like dots) and decline the slice while keeping CPython
    // parity through the translated pattern. Expectations are CPython
    // 3.13.2 ground truth with UTF-16 offsets. `[\\n-\\r]`-style escape
    // ranges are excluded: the translator currently drops those backslashes
    // (pre-existing translator gap, out of slice scope).
    // The translator wraps every class in a non-capturing group the
    // core slice declines, and rewrites complements scalar-exact, so all
    // class shapes decline the owned slice (like dots) while keeping
    // parity through the translated pattern.
    [Theory]
    [InlineData("[a-z]+")]
    [InlineData("[abc]+")]
    [InlineData("[a-c-e]+")]
    [InlineData("[A-z]+")]
    [InlineData("[^a-z]+")]
    [InlineData("[\\d]+")]
    public void ClassShapesDeclineOwnedSlice(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ClassSearchCases()
    {
        yield return new object?[] { "[a-z]+", "xa\U0001F600by", 0, true, 0, 2 };
        yield return new object?[] { "[^a-z]+", "x\U0001F600y", 0, true, 1, 3 };
        yield return new object?[] { "[abc]+", "xxcabbayy", 0, true, 2, 7 };
        yield return new object?[] { "[a-c-e]+", "xxdyy", 0, false, 0, 0 };
        yield return new object?[] { "[A-z]+", "xx[yy", 0, true, 0, 5 };
    }

    [Theory]
    [MemberData(nameof(ClassSearchCases))]
    public void ClassSearchMatchesTranslatedOracle(
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
    [InlineData("[a-z]+", "xa\U0001F600by", 2)]
    [InlineData("[^a-z]+", "x\U0001F600y", 1)]
    [InlineData("[a-c-e]+", "abcdef-", 3)]
    public void ClassCountMatchesCpython(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[a-z]+", "abc", true)]
    [InlineData("[^a-z]+", "x\U0001F600y", false)]
    [InlineData("[abc]+", "xca", false)]
    public void ClassMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[a-z]+", "abc", true)]
    [InlineData("[^a-z]+", "\U0001F600", true)]
    [InlineData("[a-z]+", "abC", false)]
    public void ClassFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    // R03 boundaries: the translator rewrites `\b`/`\B` into
    // lookaround plus `[\p{L}\p{N}_]` guards (and anchors into forms) the
    // core slice declines, so all boundary shapes decline the owned slice
    // while keeping parity through the translated pattern. Expectations mix
    // translated-oracle agreement with CPython 3.13.2 ground truths (UTF-16
    // offsets); the Mn/ZWJ-adjacent translator model stays R04 verification
    // debt and is not pinned beyond the ZWJ row below.
    [Theory]
    [InlineData("\\bword\\b")]
    [InlineData("\\b")]
    [InlineData("\\B")]
    [InlineData("\\bx\\b")]
    [InlineData("\\Aab")]
    [InlineData("ab\\Z")]
    public void BoundaryShapesDeclineOwnedSlice(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> BoundarySearchCases()
    {
        yield return new object?[] { "\\bword\\b", "a word here", 0, true, 2, 6 };
        yield return new object?[] { "\\bx\\b", "axb", 0, false, 0, 0 };
        yield return new object?[] { "\\b", "a\u00E9", 0, true, 0, 0 };
        yield return new object?[] { "\\b", "a\u200Db", 0, true, 0, 0 };
    }

    [Theory]
    [MemberData(nameof(BoundarySearchCases))]
    public void BoundarySearchMatchesTranslatedOracle(
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
    [InlineData("\\bword\\b", "a word here", 1)]
    [InlineData("\\B", "ab", 1)]
    public void BoundaryCountMatchesCpython(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Fact]
    public void EmptyCapableBoundaryCountUsesManagedBackend()
    {
        // Like `.*`: empty-capable declined shapes count through the managed
        // engine with identical results.
        var regex = new Utf8PythonRegex("\\b", PythonReCompileOptions.None);
        Assert.Equal(6, regex.Count(Utf8("a word here"), 0));
    }

    [Theory]
    [InlineData("\\bword\\b", "word here", true)]
    [InlineData("\\bword\\b", "a word here", false)]
    public void BoundaryMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\bword\\b", "word", true)]
    [InlineData("\\bword\\b", "words", false)]
    public void BoundaryFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    // R03 shorthands: bare and grouped digits pass the translator
    // through and run the owned core slice (BCL-exact on every input the
    // translator leaves alone); all other shorthands are rewritten scalar-
    // exact or fragmented and decline. Astral Nd inputs stay out: the
    // translator has no scalar rewrite for digit shapes, so the owned route
    // inherits BCL unit semantics there while CPython matches whole scalars
    // (R04 work).
    [Theory]
    [InlineData("(\\d+)")]
    [InlineData("\\d+")]
    public void DigitShapesRouteOwned(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.True(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugSearchBackend);
        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugCountBackend);
    }

    [Theory]
    [InlineData("(\\w+)")]
    [InlineData("(\\s+)")]
    [InlineData("\\D+")]
    [InlineData("\\w+")]
    [InlineData("\\W+")]
    [InlineData("\\s+")]
    [InlineData("\\S+")]
    [InlineData("[\\d]")]
    public void BareShorthandShapesDeclineOwnedSlice(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ShorthandSearchCases()
    {
        yield return new object?[] { "(\\d+)", "a12b", 0, true, 1, 3 };
        yield return new object?[] { "(\\w+)", "a_bc1", 0, true, 0, 5 };
        yield return new object?[] { "(\\s+)", "a b", 0, true, 1, 2 };
        yield return new object?[] { "\\d+", "a1b", 0, true, 1, 2 };
        yield return new object?[] { "\\w+", "aé", 0, true, 0, 2 };
    }

    [Theory]
    [MemberData(nameof(ShorthandSearchCases))]
    public void ShorthandSearchMatchesTranslatedOracle(
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
    [InlineData("(\\d+)", "a12b34", 2)]
    [InlineData("(\\w+)", "a b_c1", 2)]
    [InlineData("\\d+", "a1b2", 2)]
    public void ShorthandCountMatchesCpython(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(\\d+)", "12ab", true)]
    [InlineData("(\\w+)", "a_bc1", true)]
    public void ShorthandMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(\\d+)", "12", true)]
    [InlineData("(\\s+)", " ", true)]
    public void ShorthandFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
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
    // R03 backreferences: numeric backward references pass the translator
    // through verbatim (managed numbers equal Python numbers when no named
    // group shifts them) and run the owned core slice; named references and
    // rewritten group bodies decline. Expectations mix translated-oracle
    // agreement with CPython 3.13.2 ground truths.
    [Theory]
    [InlineData("(a)\\1")]
    [InlineData("(a)(b)\\2\\1")]
    [InlineData("(a|b)+\\1")]
    [InlineData("(\\d+)\\1")]
    [InlineData("(x)?b\\1")]
    public void BackrefShapesRouteOwned(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?P<x>a)(?P=x)")]
    [InlineData("(\\w+)\\1")]
    public void BackrefShapesDeclineOwnedSlice(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> BackrefSearchCases()
    {
        yield return new object?[] { "(a)\\1", "xaa", 0, true, 1, 3 };
        yield return new object?[] { "(a)(b)\\2\\1", "xxabbayy", 0, true, 2, 6 };
        yield return new object?[] { "(a|b)+\\1", "abb", 0, true, 0, 3 };
        yield return new object?[] { "(x)?b\\1", "b", 0, false, 0, 0 };
        yield return new object?[] { "(x)?b\\1", "bxb", 0, false, 0, 0 };
        yield return new object?[] { "(\\d+)\\1", "a1212b", 0, true, 1, 5 };
        yield return new object?[] { "(?P<x>a)(?P=x)", "xxaayy", 0, true, 2, 4 };
    }

    [Theory]
    [MemberData(nameof(BackrefSearchCases))]
    public void BackrefSearchMatchesTranslatedOracle(
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
    [InlineData("(a)\\1", "aa aa", 2)]
    [InlineData("(a|b)+\\1", "abb abb", 2)]
    [InlineData("(x)?b\\1", "bbb", 0)]
    [InlineData("(\\d+)\\1", "12ab1212", 1)]
    [InlineData("(?P<x>a)(?P=x)", "aa aa", 2)]
    public void BackrefCountMatchesCpython(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a)\\1", "aa", true)]
    [InlineData("(a)\\1", "ab", false)]
    [InlineData("(a|b)+\\1", "abb", true)]
    [InlineData("(\\d+)\\1", "1212", true)]
    public void BackrefMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a)\\1", "aa", true)]
    [InlineData("(a)\\1", "ab", false)]
    [InlineData("(a|b)+\\1", "abb", true)]
    [InlineData("(\\d+)\\1", "1212", true)]
    public void BackrefFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
    // R03 lookarounds: all four assertions pass the translator through
    // verbatim over ASCII bodies and run the owned core slice; rewritten
    // bodies decline; variable-width lookbehind fails translation exactly
    // like CPython (fixed-width required). Expectations mix
    // translated-oracle agreement with CPython 3.13.2 ground truths.
    [Theory]
    [InlineData("(?=a)a")]
    [InlineData("(?!b)a")]
    [InlineData("(?<=a)b")]
    [InlineData("(?<!a)b")]
    [InlineData("a(?<=a)b")]
    public void LookaroundShapesRouteOwned(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        AssertOwnedBackends(regex);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?=\\w+)a")]
    public void LookaroundShapesDeclineOwnedSlice(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<=a+)b")]
    [InlineData("(?<=(a|ab))")]
    public void VariableLookbehindFailsTranslation(string pattern)
    {
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern, PythonReCompileOptions.None));
    }

    public static IEnumerable<object?[]> LookaroundSearchCases()
    {
        yield return new object?[] { "(?=a)a", "xaa", 0, true, 1, 2 };
        yield return new object?[] { "(?!b)a", "xab", 0, true, 1, 2 };
        yield return new object?[] { "(?<=a)b", "xab", 0, true, 2, 3 };
        yield return new object?[] { "(?<!a)b", "xcb", 0, true, 2, 3 };
        yield return new object?[] { "a(?<=a)b", "xab", 0, true, 1, 3 };
    }

    [Theory]
    [MemberData(nameof(LookaroundSearchCases))]
    public void LookaroundSearchMatchesTranslatedOracle(
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
    [InlineData("(?=a)a", "aaa", 3)]
    [InlineData("(?<=a)b", "aab ab", 2)]
    [InlineData("(?!b)a", "ba ba", 2)]
    public void LookaroundCountMatchesCpython(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?=a)a", "aa", true)]
    [InlineData("(?!b)a", "ab", true)]
    [InlineData("a(?<=a)b", "ab", true)]
    [InlineData("(?<=a)b", "ab", false)]
    public void LookaroundMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?!b)a", "a", true)]
    [InlineData("a(?<=a)b", "ab", true)]
    [InlineData("(?=a)a", "aa", false)]
    [InlineData("(?<=a)b", "ab", false)]
    public void LookaroundFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
    // R03 mixed-class shorthands: member shorthands are rewritten
    // scalar-exact by the translator (like bare shorthands), so mixed
    // classes decline the owned slice with translated-oracle parity.
    // Expectations mix translated-oracle agreement with CPython 3.13.2
    // ground truths (CPython classes are Unicode, matching the rewrite).
    [Theory]
    [InlineData("[\\da]")]
    [InlineData("[^\\da]")]
    [InlineData("[\\w-]")]
    [InlineData("[\\da]+")]
    public void HybridShapesDeclineOwnedSlice(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.False(regex.DebugUsesOwnedSlice, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> HybridSearchCases()
    {
        var arabicDigit = char.ConvertFromUtf32(0x660);
        yield return new object?[] { "[\\da]", "x1a", 0, true, 1, 2 };
        yield return new object?[] { "[\\da]", "x" + arabicDigit + "y", 0, true, 1, 2 };
        yield return new object?[] { "[^\\da]", "xay", 0, true, 0, 1 };
        yield return new object?[] { "[\\w-]", "a-b", 0, true, 0, 1 };
    }

    [Theory]
    [MemberData(nameof(HybridSearchCases))]
    public void HybridSearchMatchesTranslatedOracle(
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

    public static IEnumerable<object?[]> HybridCountCases()
    {
        var arabicDigit = char.ConvertFromUtf32(0x660);
        yield return new object?[] { "[\\da]", "a1b2", 3 };
        yield return new object?[] { "[\\da]", "a" + arabicDigit + "b", 2 };
        yield return new object?[] { "[^\\da]", "a-b", 2 };
    }

    [Theory]
    [MemberData(nameof(HybridCountCases))]
    public void HybridCountMatchesCpython(string pattern, string inputText, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[\\da]+", "a1b", true)]
    [InlineData("[\\da]+", "xb", false)]
    public void HybridMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.Match(Utf8(inputText), 0).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[\\da]+", "a1", true)]
    [InlineData("[\\da]+", "a1b", false)]
    public void HybridFullMatchMatchesCpython(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None);
        Assert.Equal(expected, regex.FullMatch(Utf8(inputText)).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}