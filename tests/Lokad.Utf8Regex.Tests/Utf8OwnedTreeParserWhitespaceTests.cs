using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser whitespace stage: the BCL-oracled battery pins IgnorePatternWhitespace
// with BCL metadata (Unicode White_Space skipping and end-of-line
// comments outside classes, with escapes staying literal), plus decline agreement
// on comments that swallow their own closer.
public sealed class Utf8OwnedTreeParserWhitespaceTests
{
    public static IEnumerable<object?[]> WhitespaceAcceptCases()
    {
        yield return new object?[] { "(?x:ab)", 512, 544 };
        yield return new object?[] { "(?x:a  b)", 512, 544 };
        yield return new object?[] { "(?x: a )", 512, 544 };
        yield return new object?[] { "(?x:a\\tb)", 512, 544 };
        yield return new object?[] { "(?x:a\\rb)", 512, 544 };
        yield return new object?[] { "(?x:a\\fb)", 512, 544 };
        yield return new object?[] { "(?x:a\\vb)", 512, 544 };
        yield return new object?[] { "(?x:a b)", 512, 544 };
        yield return new object?[] { "(?x:[a b])", 512, 544 };
        yield return new object?[] { "(?x:[a#b])", 512, 544 };
        yield return new object?[] { "(?x:\\\\ b)", 512, 544 };
        yield return new object?[] { "(?x:(?#c)a)", 512, 544 };
        yield return new object?[] { "(?#c)a", 512, 512 };
        yield return new object?[] { "a b", 528, 528 };
        yield return new object?[] { "(?x)a b", 512, 544 };
        yield return new object?[] { "(?x)a#b\\nc", 512, 544 };
        yield return new object?[] { "(?x)(a b)", 512, 544 };
        yield return new object?[] { "(?x:a b)", 512, 544 };
        yield return new object?[] { "(?x:a b)", 512, 544 };
        yield return new object?[] { "(?x:a\u2028b)", 512, 544 };
        yield return new object?[] { "(?x:a\u0085b)", 512, 544 };
        yield return new object?[] { "(?x:a﻿b)", 512, 544 };
        yield return new object?[] { "(?x:a​b)", 512, 544 };
        yield return new object?[] { "(?x)a#b\rc", 512, 544 };
        yield return new object?[] { "(?x:a\rb)", 512, 544 };
        yield return new object?[] { "(?x)a#b\nc", 512, 544 };
        yield return new object?[] { "(?x:a\nb)", 512, 544 };
        yield return new object?[] { "(?x:(?<=a#b\nc)d)", 512, 544 };
        yield return new object?[] { "(?x:a\\nb)", 512, 544 };
        yield return new object?[] { "(?x:a(?-x:b c))", 512, 544 };
        yield return new object?[] { "(?x)(?i:a b)", 512, 545 };
        yield return new object?[] { "(?i:(?x:a b))", 512, 513 };
        yield return new object?[] { "(?x)a|b c", 512, 544 };
        yield return new object?[] { "(?x)[ab]+", 512, 544 };
        yield return new object?[] { "(?x)a{2} b", 512, 544 };
    }

    public static IEnumerable<object?[]> WhitespaceDeclineCases()
    {
        yield return new object?[] { "(?x:a#b\\nc)", 512, RegexParseError.InsufficientClosingParentheses, 11 };
        yield return new object?[] { "(?x:a\\\\#b)", 512, RegexParseError.InsufficientClosingParentheses, 10 };
        yield return new object?[] { "(?x:a#b\\rc)", 512, RegexParseError.InsufficientClosingParentheses, 11 };
        yield return new object?[] { "(?x:a#b)", 512, RegexParseError.InsufficientClosingParentheses, 8 };
        yield return new object?[] { "(?x:(?<=a#b\\nc)d)", 512, RegexParseError.InsufficientClosingParentheses, 17 };
    }

    [Theory]
    [MemberData(nameof(WhitespaceAcceptCases))]
    public void OwnedWhitespaceServesWithBclMetadata(string pattern, int options, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
    }

    [Theory]
    [MemberData(nameof(WhitespaceDeclineCases))]
    public void OwnedWhitespaceDeclinesUnsupported(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

}
