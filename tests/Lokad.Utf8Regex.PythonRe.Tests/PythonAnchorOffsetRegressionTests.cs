using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// the facade mixed whole-subject matching with sliced suffixes, so
// `^` matched slice starts, lookbehind lost its prefix (and value/detailed
// disagreed), `\Z` admitted a trailing newline, and `\B` matched the empty
// subject. Offsets now follow Python `pos` semantics with intact context on every
// entry point. Expectations are CPython 3.13.2 ground truth (see ground-c08*.py);
// byte offsets equal UTF-16 offsets on these ASCII subjects.
public sealed class PythonAnchorOffsetRegressionTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Theory]
    [InlineData("x\\Z", "x\n", false, 0, 0)]
    [InlineData("x\\Z", "x", true, 0, 1)]
    [InlineData("x$", "x\n", true, 0, 1)]
    [InlineData("\\B", "", false, 0, 0)]
    [InlineData("\\B", "a", false, 0, 0)]
    [InlineData("\\B", " ", true, 0, 0)]
    [InlineData("\\b", "", false, 0, 0)]
    [InlineData("x\\b", "axb", false, 0, 0)]
    [InlineData("x\\b", "ax", true, 1, 2)]
    public void AnchorSearchAgreesWithCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8PythonRegex(pattern);
        AssertSearch(regex, input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("x\\Z", "x", true, 0, 1)]
    [InlineData("x\\Z", "ax", false, 0, 0)]
    [InlineData("x\\b", "ax", false, 0, 0)]
    [InlineData("\\B", " ", true, 0, 0)]
    [InlineData("\\B", "", false, 0, 0)]
    public void AnchorMatchAgreesWithCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var match = new Utf8PythonRegex(pattern).Match(Utf8(input));
        Assert.Equal(expectedSuccess, match.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, match.StartOffsetInUtf16);
            Assert.Equal(expectedEnd, match.EndOffsetInUtf16);
        }
    }

    [Theory]
    [InlineData("x\\Z", "x", true)]
    [InlineData("x\\Z", "ax", false)]
    [InlineData("x$", "x\n", false)]
    public void AnchorFullMatchAgreesWithCpython(string pattern, string input, bool expectedSuccess)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expectedSuccess, regex.FullMatch(Utf8(input)).Success);
        Assert.Equal(expectedSuccess, regex.FullMatchDetailed(Utf8(input)).Success);
        Assert.Equal(expectedSuccess, regex.FullMatchDetailedData(Utf8(input)).Success);
    }

    [Theory]
    [InlineData("^foo", "xfoo", 1, false)]
    [InlineData("^foo", "foo", 0, true)]
    [InlineData("\\Afoo", "xfoo", 1, false)]
    [InlineData("(?<=x)foo", "xfoo", 1, true)]
    [InlineData("(?<=x)foo", "afoo", 1, false)]
    public void FullMatchOffsetFollowsPosContract(string pattern, string input, int startOffsetInBytes, bool expectedSuccess)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expectedSuccess, regex.FullMatch(Utf8(input), startOffsetInBytes).Success);
        Assert.Equal(expectedSuccess, regex.FullMatchDetailed(Utf8(input), startOffsetInBytes).Success);
        Assert.Equal(expectedSuccess, regex.FullMatchDetailedData(Utf8(input), startOffsetInBytes).Success);
    }

    [Fact]
    public void FullMatchOffsetKeepsLookbehindPrefixContext()
    {
        var regex = new Utf8PythonRegex("(?<=x)foo");
        var value = regex.FullMatch(Utf8("xfoo"), 1);
        Assert.True(value.Success);
        Assert.Equal(1, value.StartOffsetInUtf16);
        Assert.Equal(4, value.EndOffsetInUtf16);
        Assert.Equal("foo", value.GetValueString());
        var detailed = regex.FullMatchDetailedData(Utf8("xfoo"), 1);
        Assert.True(detailed.Success);
        Assert.Equal("foo", detailed.Value.ValueText);
        Assert.Equal("foo", regex.FullMatchToString(Utf8("xfoo"), 1));
    }

    [Theory]
    [InlineData("a|aa", "aa", true)]
    [InlineData("(a|b)+", "ab", true)]
    [InlineData("(?P<x>a)(b)\\1", "aba", true)]
    public void FullMatchBacktracksIntoCoveringMatch(string pattern, string input, bool expectedSuccess)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expectedSuccess, regex.FullMatch(Utf8(input)).Success);
        Assert.Equal(expectedSuccess, regex.FullMatchDetailed(Utf8(input)).Success);
    }

    [Theory]
    [InlineData("(?<=x)foo", "xfoo", 1, true, 1, 4)]
    [InlineData("(?<=x)foo", "afoo", 1, false, 0, 0)]
    [InlineData("\\bx", "axb", 1, false, 0, 0)]
    [InlineData("(?P<x>a)(b)\\1", "aba", 0, true, 0, 3)]
    public void OffsetSearchKeepsFullStringContext(string pattern, string input, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8PythonRegex(pattern);
        var match = regex.Search(Utf8(input), startOffsetInBytes);
        Assert.Equal(expectedSuccess, match.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, match.StartOffsetInUtf16);
            Assert.Equal(expectedEnd, match.EndOffsetInUtf16);
        }
    }

    [Theory]
    [InlineData("(?<=x)foo", "xfoo", 1, true, 1, 4)]
    [InlineData("(?<=x)foo", "afoo", 1, false, 0, 0)]
    [InlineData("^foo", "xfoo", 1, false, 0, 0)]
    public void OffsetMatchKeepsFullStringContext(string pattern, string input, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var match = new Utf8PythonRegex(pattern).Match(Utf8(input), startOffsetInBytes);
        Assert.Equal(expectedSuccess, match.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, match.StartOffsetInUtf16);
            Assert.Equal(expectedEnd, match.EndOffsetInUtf16);
        }
    }


    [Theory]
    [InlineData("^a", "ba", 1, 0)]
    public void AnchoredCountRespectsPosContract(string pattern, string input, int startOffsetInBytes, int expectedCount)
    {
        Assert.Equal(expectedCount, new Utf8PythonRegex(pattern).Count(Utf8(input), startOffsetInBytes));
    }

    private static void AssertSearch(Utf8PythonRegex regex, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var match = regex.Search(Encoding.UTF8.GetBytes(input));
        Assert.Equal(expectedSuccess, match.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, match.StartOffsetInUtf16);
            Assert.Equal(expectedEnd, match.EndOffsetInUtf16);
            Assert.Equal(input.Substring(expectedStart, expectedEnd - expectedStart), match.GetValueString());
        }
    }
}
