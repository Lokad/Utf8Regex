using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AnchoredProfileQualificationTests
{
    private const string Number = @"\G-?[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?";
    private const string Quoted = "\\G\"(?:[^\"\\\\]|\\\\.)*\"";
    private const string Decimal = @"^[-+]?\d*\.?\d*$";
    private const string Color = @"^#?([a-f0-9]{6}|[a-f0-9]{3})$";
    private const string Cell = @"^(?<col>[a-z])(?<row>(\d)+)$";
    private const string Range = @"^(?<col1>[a-z])(?<row1>(\d)+):?(?<col2>[a-z])(?<row2>(\d)+)$";
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
    private static void Oracle(string pattern, RegexOptions options, IEnumerable<string> subjects)
    {
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options: options);
        foreach (var text in subjects)
        {
            var expected = oracle.Match(text);
            var actual = regex.Match(Encoding.ASCII.GetBytes(text));
            Assert.Equal(expected.Success, actual.Success);
            if (actual.Success) Assert.Equal((expected.Index, expected.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
        }
    }private static void PublicRange(string pattern, RegexOptions options, string text, int length, bool rtl)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var lane in rtl ? new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled } : new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options | lane); var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            Assert.Equal((0, length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))); Assert.Equal((0, length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
        }
    }

    [Fact]
    public void MissingOptionalFeaturesCannotSelectASupersetNativeProfile()
    {
        foreach (var item in new[] {
            (@"\G[0-9]+", RegexOptions.CultureInvariant, "-1"),
            (@"\G-?[0-9]+", RegexOptions.CultureInvariant, "1.2e3"),
            (@"^\d*\d*$", RegexOptions.CultureInvariant, "+.1"),
            (@"^([a-f0-9]{6}|[a-f0-9]{3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "#abc"),
            (@"^[a-z]\d+[a-z]\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "a1:b2") })
        {
            var expected = new Regex(item.Item1, item.Item2).Match(item.Item3);
            foreach (var lane in new[] { RegexOptions.None, RegexOptions.Compiled })
            {
                var actual = new Utf8Regex(item.Item1, options: item.Item2 | lane).Match(Encoding.ASCII.GetBytes(item.Item3));
                Assert.Equal(expected.Success, actual.Success);
                if (actual.Success) Assert.Equal((expected.Index, expected.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            }
        }
    }    [Fact]
    public void ParsedNumberAliasesKeepRequiredOptionalGroupsAndRollbackEndpoints()
    {
        const string alias = @"\G\x2d?[0-9]+(?:\x2e[0-9]+)?(?:\x65[\x2b\x2d]?[0-9]+)?";
        Oracle(alias, RegexOptions.CultureInvariant, Subjects("01.-+eEx", 4).Concat(["-12.5e-2", "1.e2", "1e+", "1.2e+", "1e2.3"]));
        PublicRange(alias, RegexOptions.CultureInvariant, "-1.2e3😀", 6, false);
        var unicode = Number.Replace("[0-9]", @"\p{Nd}");
        PublicRange(unicode, RegexOptions.CultureInvariant, "١.٢e3😀", 7, false);
    }

    [Fact]
    public void SignedDecimalAliasesKeepNullableDigitsAndFinalLfAnchor()
    {
        const string alias = @"\A[+\x2d]?[\p{Nd}]*\x2e?[\p{Nd}]*\Z";
        Oracle(alias, RegexOptions.CultureInvariant, Subjects("01.+-x\n", 4));
        PublicRange(alias, RegexOptions.CultureInvariant, "-١.٢\n", 6, true);
    }

    [Fact]
    public void WholeColorAndSpreadsheetExecutorsRespectFinalLfRatherThanStrictEnd()
    {
        var color = new Utf8Regex(Color, options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (var text in new[] { "abc\n", "#AbC\n", "#abcdef\n" }) Assert.Equal(text.Length - 1, color.Match(Encoding.ASCII.GetBytes(text)).EndOffsetInBytes);
        Assert.Equal(3, new Utf8Regex(Cell).Match("a12\n"u8).EndOffsetInBytes);
        Assert.Equal(7, new Utf8Regex(Range).Match("a12:b34\n"u8).EndOffsetInBytes);
        foreach (var text in new[] { "abc\n\n", "#abc\r\n", "a1\n\n", "a1:b2\r\n" })
            Assert.False(new Utf8Regex(text.StartsWith("a1") ? Range : Color, options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).IsMatch(Encoding.ASCII.GetBytes(text)));
    }

    [Fact]
    public void ColorAndCellAliasesProveLexicalClassesAndCaptureByteRanges()
    {
        const string color = @"\A\x23?(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\Z";
        Oracle(color, RegexOptions.CultureInvariant, Subjects("aA0#x\n", 4).Concat(["#abcdef\n", "ABCDEF", "#ABCDEG"]));
        const string cell = @"(?i:\A(?<col>[a-z])(?<row>\p{Nd}+)\Z)";
        Oracle(cell, RegexOptions.CultureInvariant, Subjects("aA09:\n", 4));
        PublicRange(cell, RegexOptions.CultureInvariant, "K١٢\n", 7, true);
        var match = Utf8MatchSequence.First(new Utf8Regex(cell, options: RegexOptions.CultureInvariant).CreateMatchCursor("K١٢\n"u8.ToArray()));
        Assert.True(match.TryGetGroup("col", out var col)); Assert.True(match.TryGetGroup("row", out var row));
        Assert.Equal((0, 3), (col.StartOffsetInBytes, (col.EndOffsetInBytes - col.StartOffsetInBytes))); Assert.Equal((3, 4), (row.StartOffsetInBytes, (row.EndOffsetInBytes - row.StartOffsetInBytes)));
    }

    [Fact]
    public void RangeAliasesKeepOptionalColonAndConstructionCulture()
    {
        const string pattern = @"\A(?<a>[a-zA-Z])\d+(?:\x3a)?(?<b>[a-zA-Z])\d+\Z";
        Oracle(pattern, RegexOptions.CultureInvariant, Subjects("aA1:\n", 5).Concat(["a12:b34\n", "a1b2", "a1::b2"]));
        PublicRange(pattern, RegexOptions.CultureInvariant, "a١:B٢\n", 7, true);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
var regex = new Utf8Regex(Cell, options: RegexOptions.IgnoreCase);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.False(regex.IsMatch("I1"u8)); Assert.True(regex.IsMatch("i1"u8)); Assert.True(regex.IsMatch("İ1"u8));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void QuotedPrefixAllowsUnescapedLfAndConsumesOneScalarAfterBackslash()
    {
        foreach (var text in new[] { "\"a\nb\"", "\"\\😀\"", "\"😀\n\\𐐀\"" })
            PublicRange(Quoted, RegexOptions.CultureInvariant, text, Encoding.UTF8.GetByteCount(text), false);
        Assert.False(new Utf8Regex(Quoted).IsMatch("\"\\\n\""u8));
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Quoted, options: options); Assert.Equal(2, regex.Count("\"😀\"\"a\""u8));
            var values = regex.EnumerateMatches("\"😀\"\"a\""u8); Assert.True(values.MoveNext());
            Assert.Equal((0, 6), (values.Current.StartOffsetInBytes, values.Current.EndOffsetInBytes)); Assert.True(values.MoveNext());
            Assert.Equal((6, 9), (values.Current.StartOffsetInBytes, values.Current.EndOffsetInBytes)); Assert.False(values.MoveNext());
        }
    }

    [Fact]
    public void QuotedAliasesProveFullNegativeClassAndLexicalDotLanguage()
    {
        var alias = Quoted.Replace("\\G\"", @"\G\x22").Replace("\\\\.", @"\x5c[^\n]");
        Oracle(alias, RegexOptions.CultureInvariant, Subjects("a\"\\\n\0", 5));

        PublicRange(Quoted, RegexOptions.Singleline | RegexOptions.CultureInvariant, "\"\\\n\"", 4, false);
    }
}
