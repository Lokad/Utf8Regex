using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// PLAN.md C05: PythonRe converted IgnoreCase to invariant folding, missing
// CPython-only fold partners (`i` vs U+0131/U+0130, `[a-z]` vs U+0130) and
// over-matching ASCII-mode literals against non-ASCII partners (`k` vs U+212A
// under Ascii|IgnoreCase). Expectations below are CPython 3.13.2 ground truth
// (see ground-c05.py). The model: Unicode literals match
// `{TOLOWER(m)} + fixes[TOLOWER(m)]` (CPython `Lib/re/_casefix.py`, 50 entries)
// tested against lowered input; BMP ranges expand member-wise the same way;
// backrefs compare per-char TOLOWER without the fixes table.
public sealed class PythonIgnoreCaseRegressionTests
{
    [Theory]
    [InlineData("i", "i", true, 0, 1)]
    [InlineData("i", "I", true, 0, 1)]
    [InlineData("i", "\u0130", true, 0, 1)]
    [InlineData("i", "\u0131", true, 0, 1)]
    [InlineData("i", "j", false, 0, 0)]
    [InlineData("I", "I", true, 0, 1)]
    [InlineData("I", "i", true, 0, 1)]
    [InlineData("k", "k", true, 0, 1)]
    [InlineData("k", "K", true, 0, 1)]
    [InlineData("k", "\u212A", true, 0, 1)]
    [InlineData("s", "s", true, 0, 1)]
    [InlineData("s", "S", true, 0, 1)]
    [InlineData("s", "\u017F", true, 0, 1)]
    [InlineData("\u212A", "k", true, 0, 1)]
    [InlineData("\u212A", "K", true, 0, 1)]
    [InlineData("\u212A", "\u212A", true, 0, 1)]
    [InlineData("\u03C3", "\u03C3", true, 0, 1)]
    [InlineData("\u03C3", "\u03A3", true, 0, 1)]
    [InlineData("\u03C3", "\u03C2", true, 0, 1)]
    [InlineData("\u03C2", "\u03C2", true, 0, 1)]
    [InlineData("\u03C2", "\u03C3", true, 0, 1)]
    [InlineData("\u03C2", "\u03A3", true, 0, 1)]
    [InlineData("\u00B5", "\u00B5", true, 0, 1)]
    [InlineData("\u00B5", "\u03BC", true, 0, 1)]
    [InlineData("\u00B5", "\u039C", true, 0, 1)]
    [InlineData("a", "a", true, 0, 1)]
    [InlineData("a", "A", true, 0, 1)]
    [InlineData("a", "\u00E1", false, 0, 0)]
    public void UnicodeLiteralsFollowCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern, PythonReCompileOptions.IgnoreCase), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("[a-z]", "m", true, 0, 1)]
    [InlineData("[a-z]", "\u0130", true, 0, 1)]
    [InlineData("[a-z]", "\u0131", true, 0, 1)]
    [InlineData("[a-z]", "\u017F", true, 0, 1)]
    [InlineData("[a-z]", "\u212A", true, 0, 1)]
    [InlineData("[a-z]", "!", false, 0, 0)]
    [InlineData("[a-z]", "A", true, 0, 1)]
    [InlineData("[A-Z]", "A", true, 0, 1)]
    [InlineData("[A-Z]", "i", true, 0, 1)]
    [InlineData("[A-Z]", "\u0131", true, 0, 1)]
    [InlineData("[A-Z]", "\u0130", true, 0, 1)]
    [InlineData("[^a-z]", "\u0131", false, 0, 0)]
    [InlineData("[^a-z]", "!", true, 0, 1)]
    [InlineData("[^a-z]", "\u0130", false, 0, 0)]
    [InlineData("[k]", "\u212A", true, 0, 1)]
    [InlineData("[^k]", "\u212A", false, 0, 0)]
    [InlineData("[\u03B1-\u03C9]", "\u03B1", true, 0, 1)]
    [InlineData("[\u03B1-\u03C9]", "\u03A9", true, 0, 1)]
    [InlineData("[\u03B1-\u03C9]", "\u03C2", true, 0, 1)]
    [InlineData("[\u03B1-\u03C9]", "\u03A3", true, 0, 1)]
    public void UnicodeRangesFollowCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern, PythonReCompileOptions.IgnoreCase), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("(?i:i)", "\u0130", true, 0, 1)]
    [InlineData("(?i:k)", "\u212A", true, 0, 1)]
    [InlineData("(?-i:I)", "i", false, 0, 0)]
    [InlineData("(?-i:I)", "I", true, 0, 1)]
    [InlineData("(?i:[\\w])", "\u0130", true, 0, 1)]
    public void ScopedOptionsFollowCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern, PythonReCompileOptions.IgnoreCase), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("k", "k", true, 0, 1)]
    [InlineData("k", "K", true, 0, 1)]
    [InlineData("k", "\u212A", false, 0, 0)]
    [InlineData("i", "i", true, 0, 1)]
    [InlineData("i", "I", true, 0, 1)]
    [InlineData("i", "\u0130", false, 0, 0)]
    [InlineData("i", "\u0131", false, 0, 0)]
    [InlineData("[a-z]", "m", true, 0, 1)]
    [InlineData("[a-z]", "\u212A", false, 0, 0)]
    [InlineData("[a-z]", "\u0130", false, 0, 0)]
    public void AsciiLiteralsAndRangesExcludeNonAsciiPartners(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern, PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("(i)\\1", "ii", true, 0, 2)]
    [InlineData("(i)\\1", "iI", true, 0, 2)]
    [InlineData("(i)\\1", "i\u0131", false, 0, 0)]
    [InlineData("([a-z])\\1", "aA", true, 0, 2)]
    [InlineData("(k)\\1", "kK", true, 0, 2)]
    [InlineData("(k)\\1", "k\u212A", true, 0, 2)]
    [InlineData("([kj])\\1", "kK", true, 0, 2)]
    [InlineData("([ab])\\1", "aA", true, 0, 2)]
    public void BackreferencesFollowCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern, PythonReCompileOptions.IgnoreCase), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("(k)\\1", "kK", true, 0, 2)]
    public void AsciiBackreferencesFollowCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern, PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("i", "\u0130", true)]
    [InlineData("[a-z]", "\u0130", true)]
    [InlineData("k", "\u212A", true)]
    public void UnicodeFullMatchFollowsCpython(string pattern, string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, new Utf8PythonRegex(pattern, PythonReCompileOptions.IgnoreCase).FullMatch(Encoding.UTF8.GetBytes(input)).Success);
    }

    // Previously managed-folding gaps: owned scalar classes and lexical
    // reference lowercase policies must preserve these CPython expectations.
    [Fact]
    public void TurkicBackreferenceStaysPinnedToCpython()
    {
        Assert.True(new Utf8PythonRegex("(i)\\1", PythonReCompileOptions.IgnoreCase).Search(Encoding.UTF8.GetBytes("i\u0130")).Success);
        Assert.True(new Utf8PythonRegex("(\u0130)\\1", PythonReCompileOptions.IgnoreCase).Search(Encoding.UTF8.GetBytes("\u0130i")).Success);
    }

    [Fact]
    public void ScopedAsciiKelvinStaysPinnedToCpython()
    {
        Assert.False(new Utf8PythonRegex("(?a:k)", PythonReCompileOptions.IgnoreCase).Search(Encoding.UTF8.GetBytes("\u212A")).Success);
    }

    [Fact]
    public void AsciiTopUnicodeScopedBackreferenceStaysPinnedToCpython()
    {
        Assert.True(new Utf8PythonRegex("(?u:(i)\\1)", PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase).Search(Encoding.UTF8.GetBytes("iI")).Success);
    }

    [Fact]
    public void AsciiComplexBackreferenceStaysPinnedToCpython()
    {
        Assert.True(new Utf8PythonRegex("([kj])\\1", PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase).Search(Encoding.UTF8.GetBytes("kK")).Success);
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
