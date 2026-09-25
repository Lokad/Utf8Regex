using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser whitespace stage: the differential battery pins IgnorePatternWhitespace
// identical to the provider trees (Unicode White_Space skipping and end-of-line
// comments outside classes, with escapes staying literal), plus decline agreement
// on comments that swallow their own closer.
public sealed class Utf8OwnedTreeParserWhitespaceTests
{
    public static IEnumerable<object?[]> WhitespaceAcceptCases()
    {
        yield return new object?[] { "(?x:ab)", 512 };
        yield return new object?[] { "(?x:a  b)", 512 };
        yield return new object?[] { "(?x: a )", 512 };
        yield return new object?[] { "(?x:a\\tb)", 512 };
        yield return new object?[] { "(?x:a\\rb)", 512 };
        yield return new object?[] { "(?x:a\\fb)", 512 };
        yield return new object?[] { "(?x:a\\vb)", 512 };
        yield return new object?[] { "(?x:a b)", 512 };
        yield return new object?[] { "(?x:[a b])", 512 };
        yield return new object?[] { "(?x:[a#b])", 512 };
        yield return new object?[] { "(?x:\\\\ b)", 512 };
        yield return new object?[] { "(?x:(?#c)a)", 512 };
        yield return new object?[] { "(?#c)a", 512 };
        yield return new object?[] { "a b", 528 };
        yield return new object?[] { "(?x)a b", 512 };
        yield return new object?[] { "(?x)a#b\\nc", 512 };
        yield return new object?[] { "(?x)(a b)", 512 };
        yield return new object?[] { "(?x:a b)", 512 };
        yield return new object?[] { "(?x:a b)", 512 };
        yield return new object?[] { "(?x:a\u2028b)", 512 };
        yield return new object?[] { "(?x:a\u0085b)", 512 };
        yield return new object?[] { "(?x:a﻿b)", 512 };
        yield return new object?[] { "(?x:a​b)", 512 };
        yield return new object?[] { "(?x)a#b\rc", 512 };
        yield return new object?[] { "(?x:a\rb)", 512 };
        yield return new object?[] { "(?x)a#b\nc", 512 };
        yield return new object?[] { "(?x:a\nb)", 512 };
        yield return new object?[] { "(?x:(?<=a#b\nc)d)", 512 };
        yield return new object?[] { "(?x:a\\nb)", 512 };
        yield return new object?[] { "(?x:a(?-x:b c))", 512 };
        yield return new object?[] { "(?x)(?i:a b)", 512 };
        yield return new object?[] { "(?i:(?x:a b))", 512 };
        yield return new object?[] { "(?x)a|b c", 512 };
        yield return new object?[] { "(?x)[ab]+", 512 };
        yield return new object?[] { "(?x)a{2} b", 512 };
    }

    public static IEnumerable<object?[]> WhitespaceDeclineCases()
    {
        yield return new object?[] { "(?x:a#b\\nc)", 512 };
        yield return new object?[] { "(?x:a\\\\#b)", 512 };
        yield return new object?[] { "(?x:a#b\\rc)", 512 };
        yield return new object?[] { "(?x:a#b)", 512 };
        yield return new object?[] { "(?x:(?<=a#b\\nc)d)", 512 };
    }

    [Theory]
    [MemberData(nameof(WhitespaceAcceptCases))]
    public void OwnedWhitespaceMatchesProviderTree(string pattern, int options)
    {
        var expected = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        Assert.NotNull(expected);
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Equal(expected.CaptureCount, actual.CaptureCount);
        Assert.Equal(expected.CaptureNames ?? [], actual.CaptureNames ?? []);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Culture, actual.Culture);
        Assert.True(TreesEqual(expected.Root, actual.Root));
    }

    [Theory]
    [MemberData(nameof(WhitespaceDeclineCases))]
    public void OwnedWhitespaceDeclinesUnsupported(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            (RegexOptions)options,
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options)));
    }

    private static bool TreesEqual(RuntimeFrontEnd.RegexNode left, RuntimeFrontEnd.RegexNode right)
    {
        return right.Kind == left.Kind &&
            right.Options == left.Options &&
            right.Ch == left.Ch &&
            right.Str == left.Str &&
            right.Str2 == left.Str2 &&
            right.M == left.M &&
            right.N == left.N &&
            right.ChildCount == left.ChildCount &&
            Enumerable.Range(0, left.ChildCount).All(index => TreesEqual(left.Child(index), right.Child(index)));
    }
}
