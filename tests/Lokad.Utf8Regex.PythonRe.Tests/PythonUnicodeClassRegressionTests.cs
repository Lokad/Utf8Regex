using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// PLAN.md C04: non-ASCII `\w`, `\s` (and related escapes) inherited .NET
// character definitions instead of CPython semantics. Expectations below are
// CPython 3.13.2 ground truth (see ground-c04.py). A full-scalar census
// (census-c04.py) additionally establishes that `[\p{L}\p{N}_]` agrees with
// Python `\w`, and `[\s\x1c-\x1f]` agrees with Python `\s`, on every
// non-surrogate scalar, while .NET `\w` diverges on 3,610 scalars (marks,
// letter/other numbers, connector punctuation beyond `_`) and .NET `\s`
// diverges exactly on U+001C-U+001F. Python `\d` agrees with .NET `\d`
// (Unicode decimal digits) on every scalar.
public sealed class PythonUnicodeClassRegressionTests
{
    [Theory]
    [InlineData("é", true, 0, 1)]
    [InlineData("́", false, 0, 0)]
    [InlineData("²", true, 0, 1)]
    [InlineData("‿", false, 0, 0)]
    [InlineData("a", true, 0, 1)]
    [InlineData("_", true, 0, 1)]
    [InlineData("5", true, 0, 1)]
    [InlineData(" ", false, 0, 0)]
    public void UnicodeWordClassFollowsCpython(string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(@"\w"), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("é", false, 0, 0)]
    [InlineData("́", true, 0, 1)]
    [InlineData("²", false, 0, 0)]
    [InlineData(" ", true, 0, 1)]
    public void UnicodeNonWordClassFollowsCpython(string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(@"\W"), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData(" ", true, 0, 1)]
    [InlineData("\u00A0", true, 0, 1)]
    [InlineData("\u2028", true, 0, 1)]
    [InlineData("\u202F", true, 0, 1)]
    [InlineData("\u001C", true, 0, 1)]
    [InlineData("\u001F", true, 0, 1)]
    [InlineData("a", false, 0, 0)]
    public void UnicodeSpaceClassFollowsCpython(string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(@"\s"), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("\u001C", false, 0, 0)]
    [InlineData("a", true, 0, 1)]
    public void UnicodeNonSpaceClassFollowsCpython(string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(@"\S"), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("5", true, 0, 1)]
    [InlineData("٣", true, 0, 1)]
    [InlineData("²", false, 0, 0)]
    [InlineData("a", false, 0, 0)]
    public void UnicodeDigitClassFollowsCpython(string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(@"\d"), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("²", true, 0, 1)]
    [InlineData("5", false, 0, 0)]
    public void UnicodeNonDigitClassFollowsCpython(string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(@"\D"), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData(@"[\w]", "́", false, 0, 0)]
    [InlineData(@"[^\w]", "́", true, 0, 1)]
    [InlineData(@"[^\w]", "a", false, 0, 0)]
    [InlineData(@"[\W]", "́", true, 0, 1)]
    [InlineData(@"[\s]", "\u001C", true, 0, 1)]
    [InlineData(@"[^\s]", "\u001C", false, 0, 0)]
    [InlineData(@"[\w\d]", "²", true, 0, 1)]
    [InlineData(@"[^\w\d]", "²", false, 0, 0)]
    [InlineData(@"[a\w]", "é", true, 0, 1)]
    [InlineData(@"[^a\w]", "é", false, 0, 0)]
    public void BracketedPredefinedClassesFollowCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData(@"\bé\b", "a é b", true, 2, 3)]
    [InlineData(@"é\b", "é ", true, 0, 1)]
    [InlineData(@"\b", "áb", true, 0, 0)]
    public void UnicodeWordBoundariesFollowCpython(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Fact]
    public void UnicodeNonWordBoundaryRejectsMarkBoundaryLikeCpython()
    {
        Assert.False(new Utf8PythonRegex(@"\B").Search(Encoding.UTF8.GetBytes("áb")).Success);
    }

    [Theory]
    [InlineData(@"(?a:\w)", "é", false)]
    [InlineData(@"(?a:\s)", "\u001C", false)]
    [InlineData(@"(?a:\w)", "a", true)]
    public void ScopedAsciiClassesKeepAsciiSemantics(string pattern, string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, new Utf8PythonRegex(pattern).Search(Encoding.UTF8.GetBytes(input)).Success);
    }

    [Theory]
    [InlineData(@"\w", "é", true)]
    [InlineData(@"\w", "́", false)]
    [InlineData(@"\s", "\u001C", true)]
    [InlineData(@"\d", "٣", true)]
    [InlineData(@"\D", "²", true)]
    public void UnicodeFullMatchFollowsCpython(string pattern, string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, new Utf8PythonRegex(pattern).FullMatch(Encoding.UTF8.GetBytes(input)).Success);
    }

    // Supplementary-plane disposition (see SPEC-PYTHONRE "Deliberate boundaries"):
    // the UTF-16 execution engines cannot match supplementary categories, and
    // word boundaries around supplementary scalars follow UTF-16 units. These
    // witnesses pin the CPython 3.13.2 expectations until a scalar execution
    // layer exists. They stay skipped rather than failing the suite.
    [Fact(Skip = "Supplementary category matching needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralWordClassStaysPinnedToCpython()
    {
        var input = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(0x10400));
        var match = new Utf8PythonRegex(@"\w").Search(input);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInUtf16);
        Assert.Equal(2, match.EndOffsetInUtf16);
    }

    [Fact(Skip = "Supplementary category matching needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralBracketedWordClassStaysPinnedToCpython()
    {
        var input = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(0x10400));
        var match = new Utf8PythonRegex(@"[\w]").Search(input);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInUtf16);
        Assert.Equal(2, match.EndOffsetInUtf16);
    }

    [Fact(Skip = "Supplementary category matching needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralWordFullMatchStaysPinnedToCpython()
    {
        Assert.True(new Utf8PythonRegex(@"\w").FullMatch(Encoding.UTF8.GetBytes(char.ConvertFromUtf32(0x10400))).Success);
    }

    [Fact(Skip = "Supplementary category matching needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralDigitClassStaysPinnedToCpython()
    {
        var input = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(0x1D7CE));
        var match = new Utf8PythonRegex(@"\d").Search(input);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInUtf16);
        Assert.Equal(2, match.EndOffsetInUtf16);
    }

    [Fact(Skip = "Supplementary word boundaries follow UTF-16 units; see SPEC-PYTHONRE.")]
    public void AstralTrailingBoundaryStaysPinnedToCpython()
    {
        Assert.False(new Utf8PythonRegex(@"x\b").Search(Encoding.UTF8.GetBytes("x" + char.ConvertFromUtf32(0x10400))).Success);
    }

    [Fact(Skip = "Supplementary word boundaries follow UTF-16 units; see SPEC-PYTHONRE.")]
    public void AstralLeadingBoundaryStaysPinnedToCpython()
    {
        Assert.False(new Utf8PythonRegex(@"\bx").Search(Encoding.UTF8.GetBytes(char.ConvertFromUtf32(0x10400) + "x")).Success);
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
