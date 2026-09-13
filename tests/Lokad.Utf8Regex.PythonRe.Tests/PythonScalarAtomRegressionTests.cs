using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// PLAN.md C06: pattern atoms operated on UTF-16 halves instead of scalars. A
// bare `.` matched one half, astral literals quantified only their trailing
// half, complements reported half ranges, and the value extractor reinterpreted
// split offsets as rune indices while offsets stayed split. Expectations below
// are CPython 3.13.2 ground truth (see ground-c06*.py); astral expectations use
// UTF-16 offsets, so one supplementary scalar spans two units.
public sealed class PythonScalarAtomRegressionTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Theory]
    [InlineData(".", "\U0001F600", true, 0, 2)]
    [InlineData(".", "a", true, 0, 1)]
    [InlineData("x.", "x\U0001F600", true, 0, 3)]
    [InlineData("(?a:.)", "\U0001F600", true, 0, 2)]
    public void DotSearchMatchesWholeScalars(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData(".", "\n", false, 0, 0)]
    [InlineData("(?s:.)", "\n", true, 0, 1)]
    [InlineData(".", "a\nb", true, 0, 1)]
    [InlineData("(?s:.)", "a\nb", true, 0, 1)]
    public void DotHonorsDotAllScope(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("..", "\U0001F600", false, 0, 0)]
    [InlineData(".{2}", "\U0001F600\U0001F600", true, 0, 4)]
    [InlineData("a.*b", "a\U0001F600b", true, 0, 4)]
    public void DotRepetitionCountsScalars(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData(".", "\U0001F600", true)]
    [InlineData("a.*b", "a\U0001F600b", true)]
    [InlineData("..", "\U0001F600", false)]
    public void DotFullMatchCoversWholeScalars(string pattern, string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, new Utf8PythonRegex(pattern).FullMatch(Utf8(input)).Success);
    }

    [Fact]
    public void DotCaptureAndBackreferenceSpanWholeScalars()
    {
        var emoji = char.ConvertFromUtf32(0x1F600);
        Assert.Equal([emoji], new Utf8PythonRegex("(.)").FindAllToStrings(Utf8(emoji)).ScalarValues);
        AssertSearch(new Utf8PythonRegex("(.)\\1"), emoji + emoji, true, 0, 4);
    }

    [Theory]
    [InlineData("(?<=.)x", "ax", true, 1, 2)]
    [InlineData("(?<=.)x", "\U0001F600x", true, 2, 3)]
    public void DotLookbehindAdmitsScalarWidth(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Fact]
    public void DotFindAllProgressesByScalars()
    {
        var values = new Utf8PythonRegex(".").FindAllToStrings(Utf8("a\U0001F600b")).ScalarValues;
        Assert.Equal(["a", char.ConvertFromUtf32(0x1F600), "b"], values);
    }

    [Theory]
    [InlineData("\U0001F600", "\U0001F600", true, 0, 2)]
    [InlineData("\U0001F600+", "\U0001F600", true, 0, 2)]
    [InlineData("\U0001F600+", "\U0001F600\U0001F600", true, 0, 4)]
    [InlineData("[^a]", "\U00010400", true, 0, 2)]
    [InlineData("[^a]", "\U0001F600", true, 0, 2)]
    public void AstralLiteralsAndNegationsSpanWholeScalars(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("\U0001F600", "\U0001F600", true)]
    [InlineData("\U0001F600+", "\U0001F600\U0001F600", true)]
    public void AstralLiteralFullMatchCoversWholeScalars(string pattern, string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, new Utf8PythonRegex(pattern).FullMatch(Utf8(input)).Success);
    }

    [Theory]
    [InlineData("\U00010428", "\U00010400", true, 0, 2)]
    [InlineData("\U00010400", "\U00010428", true, 0, 2)]
    public void AstralIgnoreCaseFoldsBothDirections(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern, PythonReCompileOptions.IgnoreCase), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("(?a:\U00010428)", "\U00010400", false, 0, 0)]
    [InlineData("(?a:\U00010428)", "\U00010428", true, 0, 2)]
    public void AsciiAstralLiteralsCompareExactly(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern, PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("\\W", "\U0001F600", true, 0, 2)]
    [InlineData("\\S", "\U0001F600", true, 0, 2)]
    [InlineData("\\D", "\U0001F600", true, 0, 2)]
    [InlineData("[\\W]", "\U0001F600", true, 0, 2)]
    [InlineData("(?a:\\W)", "\U0001F600", true, 0, 2)]
    [InlineData("(?a:\\S)", "\U0001F600", true, 0, 2)]
    [InlineData("(?a:\\D)", "\U0001F600", true, 0, 2)]
    public void ComplementsMatchWholeNonMemberScalars(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8PythonRegex(pattern);
        AssertSearch(regex, input, expectedSuccess, expectedStart, expectedEnd);
        Assert.Equal(expectedSuccess, regex.Match(Utf8(input)).Success);
        Assert.Equal(expectedSuccess, regex.FullMatch(Utf8(input)).Success);
    }

    [Theory]
    [InlineData("\\W", "\U0001F600", true)]
    [InlineData("\\D", "\U0001F600", true)]
    [InlineData("(?a:\\W)", "\U0001F600", true)]
    public void ComplementFullMatchCoversWholeScalars(string pattern, string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, new Utf8PythonRegex(pattern).FullMatch(Utf8(input)).Success);
    }

    [Fact]
    public void ComplementDetailedMatchFreezesWholeScalarRanges()
    {
        var match = new Utf8PythonRegex("\\W").SearchDetailedData(Utf8(char.ConvertFromUtf32(0x1F600)));
        Assert.True(match.Success);
        Assert.Equal(0, match.Value.StartOffsetInUtf16);
        Assert.Equal(2, match.Value.EndOffsetInUtf16);
        Assert.Equal(char.ConvertFromUtf32(0x1F600), match.Value.ValueText);
    }

    // CPython 3.13.2 searches an astral scalar with a scoped-ASCII complement and
    // misses, while its own match/fullmatch find the whole scalar (an SRE search-hint
    // inconsistency, not the admitted semantics). Our entry points stay mutually
    // consistent per C08 and agree with the match/fullmatch oracle.
    [Theory]
    [InlineData("(?a:\\W)", "\U00010400")]
    [InlineData("(?a:\\D)", "\U0001D7CE")]
    public void ScopedAsciiComplementSearchAgreesWithCpythonMatch(string pattern, string input)
    {
        var regex = new Utf8PythonRegex(pattern);
        var search = regex.Search(Utf8(input));
        var match = regex.Match(Utf8(input));
        var full = regex.FullMatch(Utf8(input));
        Assert.True(search.Success);
        Assert.Equal(match.Success, search.Success);
        Assert.Equal(full.Success, search.Success);
        Assert.Equal(0, search.StartOffsetInUtf16);
        Assert.Equal(2, search.EndOffsetInUtf16);
        Assert.Equal(input, search.GetValueString());
    }

    // Delimited astral membership (see SPEC-PYTHONRE): BMP-only guards cannot see
    // astral word/digit membership, so complements over-match astral members while
    // double negations under-match them. These pin the CPython 3.13.2 expectations
    // until a scalar execution layer exists.
    [Fact(Skip = "Astral word membership needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralLetterComplementStaysPinnedToCpython()
    {
        Assert.False(new Utf8PythonRegex("\\W").Search(Utf8(char.ConvertFromUtf32(0x10400))).Success);
    }

    [Fact(Skip = "Astral word membership needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralBracketedComplementStaysPinnedToCpython()
    {
        Assert.False(new Utf8PythonRegex("[\\W]").Search(Utf8(char.ConvertFromUtf32(0x10400))).Success);
    }

    [Fact(Skip = "Astral digit membership needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralDigitComplementStaysPinnedToCpython()
    {
        Assert.False(new Utf8PythonRegex("\\D").Search(Utf8(char.ConvertFromUtf32(0x1D7CE))).Success);
    }

    [Fact(Skip = "Astral word membership needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralDoubleNegationStaysPinnedToCpython()
    {
        Assert.True(new Utf8PythonRegex("[^\\W]").Search(Utf8(char.ConvertFromUtf32(0x10400))).Success);
    }

    [Fact(Skip = "Astral digit membership needs a scalar execution layer; see SPEC-PYTHONRE.")]
    public void AstralDigitDoubleNegationStaysPinnedToCpython()
    {
        Assert.True(new Utf8PythonRegex("[^\\D]").Search(Utf8(char.ConvertFromUtf32(0x1D7CE))).Success);
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
