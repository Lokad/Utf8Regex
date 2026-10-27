using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// backreference widths were always (0, null), so every lookbehind over
// a backreference was rejected; verbose mode skipped char.IsWhiteSpace instead of
// Python ASCII whitespace; and bare `\z` passed through although CPython rejects it.
// Expectations are CPython 3.13.2 ground truth (see ground-c10*.py). Non-ASCII
// expectations below use explicit escapes: raw non-ASCII flattens unpredictably.
public sealed class PythonSyntaxParityRegressionTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Theory]
    [InlineData("(a)(?<=\\1)b", "ab", true, 0, 2)]
    [InlineData("(a)(?<=\\1)b", "abb", true, 0, 2)]
    [InlineData("(a|b)(?<=\\1)b", "ab", true, 0, 2)]
    [InlineData("(a|b)(?<=\\1)b", "aab", true, 1, 3)]
    [InlineData("(a|b)(?<=\\1)b", "abb", true, 0, 2)]
    [InlineData("(a)?(?<=\\1)b", "ab", true, 0, 2)]
    [InlineData("(a)?(?<=\\1)b", "aab", true, 1, 3)]
    [InlineData("(a)?(?<=\\1)b", "abb", true, 0, 2)]
    [InlineData("(a){2,3}(?<=\\1)b", "aab", true, 0, 3)]
    [InlineData("(a){2,3}(?<=\\1)b", "ab", false, 0, 0)]
    [InlineData("((a)b)(?<=\\1)b", "abb", true, 0, 3)]
    [InlineData("((a)b)(?<=\\1)b", "ab", false, 0, 0)]
    [InlineData("(?P<x>ab)(?<=\\1)b", "abb", true, 0, 3)]
    [InlineData("(a)(?<=\\1\\1)b", "aab", true, 1, 3)]
    [InlineData("(a)(?<=\\1\\1)b", "ab", false, 0, 0)]
    [InlineData("(a)(?<=\\1\\1)b", "abb", false, 0, 0)]
    public void LookbehindBackreferenceWidthsFollowCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("(a+)(?<=\\1)b")]
    [InlineData("(a|bb)(?<=\\1)b")]
    public void VariableWidthLookbehindBackreferencesStayRejected(string pattern)
    {
        var exception = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Contains("look-behind requires fixed-width pattern", exception.Message);
    }

    [Theory]
    [InlineData("(?<=\\1)(a)b")]
    [InlineData("(?:ab)(?<=\\1)b")]
    public void ForwardBackreferencesStayRejected(string pattern)
    {
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
    }

    [Fact]
    public void BoundedLargeRepeatLookbehindWidthsDoNotWrap()
    {
        var regex = new Utf8PythonRegex("((a{46350}){46350})(?<=\\1)b");
        Assert.True(regex.Search(Utf8("b")).Success == false);
    }

    [Theory]
    [InlineData("(?x)a\u00A0b", "a\u00A0b", true, 0, 3)]
    [InlineData("(?x)a\u00A0b", "ab", false, 0, 0)]
    [InlineData("(?x)a\u1680b", "a\u1680b", true, 0, 3)]
    [InlineData("(?x)a\tb", "ab", true, 0, 2)]
    [InlineData("(?x)a b", "ab", true, 0, 2)]
    [InlineData("(?x)a#b\nb", "ab", true, 0, 2)]
    [InlineData("(?x)a\\ b", "a b", true, 0, 3)]
    [InlineData("(?x)[ ]a", " a", true, 0, 2)]
    public void VerboseWhitespaceFollowsCpythonGrammar(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("(a)?b\\1", "b", false, 0, 0)]
    [InlineData("(a)?\\1", "", false, 0, 0)]
    [InlineData("((a)b)?\\2", "", false, 0, 0)]
    [InlineData("(a)?b\\1", "aba", true, 0, 3)]
    public void UnsetBackreferencesFailLikeCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("\\e", "bad escape \\e")]
    [InlineData("\\e+", "bad escape \\e")]
    [InlineData("\\cA", "bad escape \\c")]
    [InlineData("\\cA+", "bad escape \\c")]
    public void UnsupportedControlEscapesStayRejected(string pattern, string expectedMessage)
    {
        var exception = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("\\z")]
    [InlineData("[\\z]")]
    public void LoneLowercaseZStaysRejected(string pattern)
    {
        var exception = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Contains("bad escape \\z", exception.Message);
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
