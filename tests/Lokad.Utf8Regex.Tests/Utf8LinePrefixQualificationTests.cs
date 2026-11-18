using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LinePrefixQualificationTests
{
    private const string Until = @"\G///[^\n]*\n";
    private const string Trimmed = @"^\s*(\[\|\s*)?///\s?(?<doc>.*)$";
    private const string Title = @"^\s*///(?<title>#.*)$";
    private const string Digits = @"^([0-9]+)(\-| |$)(.*)$";
    private const string Import = "^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$";
    private static void PublicRange(string pattern, RegexOptions options, string text, int index, int length, bool rtl)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var lane in rtl ? new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled } : new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options | lane); var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            Assert.Equal((index, length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))); Assert.Equal((index, length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
        }
    }
    private static IEnumerable<string> Subjects(string alphabet, int maximum)
    {
        yield return "";
        for (var length = 1; length <= maximum; length++)
        {
            var count = (int)Math.Pow(alphabet.Length, length);
            for (var value = 0; value < count; value++)
            {
                var code = value; var chars = new char[length];
                for (var index = 0; index < length; index++) { chars[index] = alphabet[code % alphabet.Length]; code /= alphabet.Length; }
                yield return new string(chars);
            }
        }
    }

    [Fact]
    public void UntilTerminatorProofBorrowsFragmentedLiteralsAndNulScalarBoundaries()
    {
        PublicRange(@"\G(?<a>\x2f)(?:\x2f)/[^\n]*\x0a", RegexOptions.CultureInvariant, "///😀\nrest", 0, 8, false);
        PublicRange("\\Gx[^\\0]*\\0", RegexOptions.CultureInvariant, "x😀\0", 0, 6, false);
    }    [Fact]
    public void TrimmedOptionalWhitespaceCanConsumeLfIncludingTheFinalLf()
    {
        foreach (var text in new[] { "///\n", "///\nbody", " \n[| ///\nbody" }) PublicRange(Trimmed, RegexOptions.CultureInvariant, text, 0, text.Length, false);
        PublicRange(Trimmed, RegexOptions.CultureInvariant, " /// 😀", 0, 11, false);
        Assert.True(Utf8MatchSequence.First(new Utf8Regex(Trimmed).CreateMatchCursor("///\n😀"u8.ToArray())).TryGetGroup("doc", out var group));
        Assert.Equal((4, 8), (group.StartOffsetInBytes, group.EndOffsetInBytes));
    }    [Fact]
    public void TrimmedAliasesAndLexicalAnchorsPreserveNativeAsciiEndpoints()
    {
        const string alias = @"(?-m:\A\s*(?:\x5b\x7c\s*)?\x2f(?<x>/)/\s?(?<doc>[^\n]*)\Z)";
        var oracle = new Regex(alias, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        foreach (var tail in Subjects("a \n\0", 4))
        foreach (var prefix in new[] { "///", "[| ///", " \n///" })
        {
            var text = prefix + tail; var expected = oracle.Match(text);
            var actual = new Utf8Regex(alias, options: RegexOptions.Multiline | RegexOptions.CultureInvariant).Match(Encoding.ASCII.GetBytes(text));
            var found = actual.Success; var length = actual.EndOffsetInBytes;
            Assert.Equal(expected.Success, found); if (found) Assert.Equal(expected.Length, length);
        }
    }

    [Fact]
    public void PublicTitleCountsConsumeUnicodeIndentAndCompletePrefixes()
    {
        var input = " ///#😀\n\t///#x\n"u8;
        Assert.Equal(2, new Utf8Regex(Title, options: RegexOptions.Multiline).Count(input));
        const string alias = @"^\s*(?:\x2f/)(?<title>/\x23[^\n]*)$";
        foreach (var options in new[] { RegexOptions.Multiline, RegexOptions.Multiline | RegexOptions.Compiled })
            Assert.Equal(2, new Utf8Regex(alias, options: options).Count(input));
        Assert.True(Utf8MatchSequence.First(new Utf8Regex(alias, options: RegexOptions.Multiline).CreateMatchCursor(((global::System.ReadOnlySpan<byte>)input).ToArray())).TryGetGroup("title", out var capture));
        Assert.Equal((5, 6), (capture.StartOffsetInBytes, (capture.EndOffsetInBytes - capture.StartOffsetInBytes)));
        Assert.Equal(1, new Utf8Regex(@"^a\na.*$", options: RegexOptions.Multiline).Count("a\na\na\n"u8));
    }

    [Fact]
    public void PublicQuotedLinesUseExactPrefixesAndCompleteUnicodeWhitespaceAndBody()
    {
        foreach (var item in new[] { ("import2 \"x\"", "import2", 1), ("import\n\"😀\"", "import", 1), ("import \"a\nb\"", "import", 1), ("import \"a\\b\"", "import", 0) })
        {
            var pattern = "^" + Regex.Escape(item.Item2) + "\\s+\"[^\"\\\\]*\".*$";
            Assert.Equal(item.Item3, new Utf8Regex(pattern, options: RegexOptions.Multiline).Count(Encoding.UTF8.GetBytes(item.Item1)));
        }
        const string alias = "^\\x69(?<i>m)port\\s+(?:shared\\s+)?\"(?<p>(\\.|[^\\\\\"]*))\"[^\\n]*$";
        PublicRange(alias, RegexOptions.Multiline | RegexOptions.CultureInvariant, "import shared \"😀\nx\"tail", 0, 28, false);
    }

    [Fact]
    public void PublicQuotedLineAndTitleCountsAgreeWithIndependentOverlappingOracle()
    {
        var imports = new Regex(Import, RegexOptions.Multiline); var titles = new Regex(Title, RegexOptions.Multiline);
        foreach (var a in new[] { "", "import \"\"", "import shared \"x\"", "import\n\"a\nb\"", "import \".\"", "import \"a\\b\"", "import \"x\"" })
        foreach (var b in new[] { "", "\nimport \"z\"", " import \"x\"", "\n///#x", "\n ///#y" })
        {
            var text = a + b; var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(imports.Count(text), new Utf8Regex(Import, options: RegexOptions.Multiline).Count(input));
            Assert.Equal(titles.Count(text), new Utf8Regex(Title, options: RegexOptions.Multiline).Count(input));
        }
    }

    [Fact]
    public void LeadingDigitTailRequiresEndArmAndDisjointNonLfSeparators()
    {

        PublicRange(@"^[0-9]+(1|$).*$", RegexOptions.CultureInvariant, "111x", 0, 4, true);
        PublicRange(@"^[0-9]+(\n|$).*$", RegexOptions.CultureInvariant, "1\n", 0, 2, false);
        const string alias = @"\A\p{Nd}+(?:\x2d|\x20|\Z)[^\n]*\Z";
        var oracle = new Regex(alias, RegexOptions.CultureInvariant);
        foreach (var text in Subjects("1- x\n", 5))
        {
            var expected = oracle.Match(text); var match = new Utf8Regex(alias).Match(Encoding.ASCII.GetBytes(text)); var actual = match.Success; var length = match.EndOffsetInBytes;
            Assert.Equal(expected.Success, actual); if (actual) Assert.Equal(expected.Length, length);
        }
        PublicRange(alias, RegexOptions.CultureInvariant, "١-😀", 0, 7, true);
    }
}
