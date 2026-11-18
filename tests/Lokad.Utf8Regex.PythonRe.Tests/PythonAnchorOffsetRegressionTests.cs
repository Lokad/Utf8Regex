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
        if (match.Success)
        {
            Assert.Equal(global::System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedStart)), match.StartOffsetInBytes);
            Assert.Equal(global::System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedEnd)), match.EndOffsetInBytes);
        }
    }

    [Theory]
    [InlineData("x\\Z", "x", true)]
    [InlineData("x\\Z", "ax", false)]
    [InlineData("x$", "x\n", false)]
    public void AnchorFullMatchAgreesWithCpython(string pattern, string input, bool expectedSuccess)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expectedSuccess, regex.Match(Utf8(input),mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot1 = default;
        using (var captureCursor1 = regex.CreateMatchCursor(Utf8(input), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out snapshot1);
        }
        Assert.Equal(expectedSuccess, snapshot1.Success);
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
        Assert.Equal(expectedSuccess, regex.Match(Utf8(input),startOffsetInBytes,mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot3 = default;
        using (var captureCursor3 = regex.CreateMatchCursor(Utf8(input), startOffsetInBytes, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor3.MoveNext(out snapshot3);
        }
        Assert.Equal(expectedSuccess, snapshot3.Success);
    }

    [Fact]
    public void FullMatchOffsetKeepsLookbehindPrefixContext()
    {
        var regex = new Utf8PythonRegex("(?<=x)foo");
        var value = regex.Match(Utf8("xfoo"),1,mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(value.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(value.GetValueBytes()));
        Utf8PythonRangeMatch detailed = default;
        using (var captureCursor5 = regex.CreateMatchCursor(Utf8("xfoo"), 1, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor5.MoveNext(out detailed);
        }
        Assert.True(detailed.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(detailed.GetValueMemory().Span));
        PythonReMatchAssert.Equal("foo", regex.Match(Utf8("xfoo"),1,mode: Utf8PythonMatchMode.FullMatch));
    }

    [Theory]
    [InlineData("a|aa", "aa", true)]
    [InlineData("(a|b)+", "ab", true)]
    [InlineData("(?P<x>a)(b)\\1", "aba", true)]
    public void FullMatchBacktracksIntoCoveringMatch(string pattern, string input, bool expectedSuccess)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expectedSuccess, regex.Match(Utf8(input),mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot6 = default;
        using (var captureCursor6 = regex.CreateMatchCursor(Utf8(input), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor6.MoveNext(out snapshot6);
        }
        Assert.Equal(expectedSuccess, snapshot6.Success);
    }

    [Theory]
    [InlineData("start(.*)(é😀)")]
    [InlineData("start(.*?)(é😀)")]
    public void DotAllFullMatchKeepsTheAbsoluteSuffixAndCaptureOffsets(string pattern)
    {
        const string body = "é😀\nβ";
        var input = Utf8("aéstart" + body + "é😀!");
        var end = input.Length - 1;
        using var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.DotAll);
        var value = regex.Match(input, 3, mode: Utf8PythonMatchMode.FullMatch, endOffsetInBytes: end);
        Assert.True(value.Success);
        Assert.Equal((3, end), (value.StartOffsetInBytes, value.EndOffsetInBytes));

        using var cursor = regex.CreateMatchCursor(input, 3, mode: Utf8PythonMatchMode.FullMatch, endOffsetInBytes: end);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(2, match.LastIndex);
        Assert.Equal(body, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal("é😀", Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));
    }

    [Theory]
    [InlineData("(?s).+end", "end", false)]
    [InlineData("(?s).+?end", "😀end", true)]
    [InlineData("(?s).*x", "😀", false)]
    [InlineData(".*end", "a\nend", false)]
    [InlineData("(?s)(?i:.*end)", "a\nEND", true)]
    [InlineData("(?s).*end$", "a\nend\n", false)]
    public void FullMatchChecksDotAndSuffixSemantics(string pattern, string text, bool expected)
    {
        using var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expected, regex.Match(Utf8(text), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void DotAllFullMatchMetersThePrefixUnderAFiniteWorkLimit()
    {
        var input = Utf8("start\n" + new string('x', 16_384) + "end");
        using var regex = new Utf8PythonRegex("start.*end", options: PythonReCompileOptions.DotAll,
            policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new() { MaxWorkSteps = 64, MaxDepth = 1, MaxWorkingBytes = 4096, MaxResultBytes = 1 },
            });
        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        using var tooLittleWork = new Utf8PythonRegex("start.*end", options: PythonReCompileOptions.DotAll,
            policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new() { MaxWorkSteps = 1 },
            });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(
            () => tooLittleWork.Match(input, mode: Utf8PythonMatchMode.FullMatch));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
    }

    [Theory]
    [InlineData("(?<=x)foo", "xfoo", 1, true, 1, 4)]
    [InlineData("(?<=x)foo", "afoo", 1, false, 0, 0)]
    [InlineData("\\bx", "axb", 1, false, 0, 0)]
    [InlineData("(?P<x>a)(b)\\1", "aba", 0, true, 0, 3)]
    public void OffsetSearchKeepsFullStringContext(string pattern, string input, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8PythonRegex(pattern);
        var match = regex.Match(Utf8(input),startOffsetInBytes,mode: Utf8PythonMatchMode.Search);
        Assert.Equal(expectedSuccess, match.Success);
        if (match.Success)
        {
            Assert.Equal(global::System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedStart)), match.StartOffsetInBytes);
            Assert.Equal(global::System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedEnd)), match.EndOffsetInBytes);
        }
    }

    [Theory]
    [InlineData("(?<=x)foo", "xfoo", 1, true, 1, 4)]
    [InlineData("(?<=x)foo", "afoo", 1, false, 0, 0)]
    [InlineData("^foo", "xfoo", 1, false, 0, 0)]
    public void OffsetMatchKeepsFullStringContext(string pattern, string input, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var match = new Utf8PythonRegex(pattern).Match(Utf8(input),startOffsetInBytes);
        Assert.Equal(expectedSuccess, match.Success);
        if (match.Success)
        {
            Assert.Equal(global::System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedStart)), match.StartOffsetInBytes);
            Assert.Equal(global::System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedEnd)), match.EndOffsetInBytes);
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
        var match = regex.Match(Encoding.UTF8.GetBytes(input),mode: Utf8PythonMatchMode.Search);
        Assert.Equal(expectedSuccess, match.Success);
        if (expectedSuccess)
        {
            Assert.Equal(input.Substring(expectedStart, expectedEnd - expectedStart), System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        }
    }
}
