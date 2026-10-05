using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedTreeSliceLoweringTests
{
    private static readonly string[] Patterns =
    [
        "(?x: a + b )", "(?x)a + # tail\nb", "(?x:a+ # lazy\n ? b)",
        "(?x: ( a | b ) + )(?-x: c )", "(?x)(?<12> a + )(?-x: b )\\k<12>",
        "(?x: a { 2 } )", "(?x:a{2, 3})", "(?x:a{2 ,3})", "(?x:a{2} ?b)",
        "(?x:a\vb)", "(?x:a\u00a0b)", "(?x:a\u1680b)", "(?x:a\u0085b)",
        "(?x:a\u2028b)", "(?x:a# comment\rb\nc)", "(?x:[ a#b]+)",
        @"(?x:a\ b\#c)", "(?x:(?<x> a )(?i: k )(?-i: k )(?-x: b ))",
        "(?x:(?= (?<x> a + ))\\k<x> b)", "(?x:(?<= (?<x> a + )) b)",
        "(?x:(?> a | ab ) b)", "(?x:(?<x> a )(?<y-x> b )(?(x)c|d))",
        @"(?i:(?<x>\p{Lu}+))(?-i:\k<x>)", @"(?i:[\p{IsBasicLatin}]+)",
        @"(?x:[a-z-[aeiou]] + )", @"(?x:[\d\s]+)", @"(?x:[\s\S]{2,5})",
        @"(?x:[\P{Lu}\P{Lu}]+)", @"(?x:[\P{Lu}\p{Ll}\P{Lu}]+)",
        @"(?x:[\P{L}\p{Lu}\P{N}]+)", @"(?x:(?(a)a|b)+)", @"(?x:[\--0]+)",
        @"(?i:\265+)", @"(?i:\2658+)",
        "(?i:K)", "(?i:ϴ)", "(?i)k",
    ];
    private static readonly string[] Texts =
    [
        "", "a", "aab", "abb", "abbd", "ac", "abc", "a{2}", "a{2,3}",
        " a#b ", "a b#c", " a b aa ", "a\vb", "a\u00a0b", "a\u1680b",
        "a\u0085b", "a\u2028b", "akK b ", "aKk b ", "1\u00a0٢",
        "\U0001f600ab\U0001f600", "x\nac\nab\naab", "µμΜ µ88μ8Μ8", "K kK Θθϴ",
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var verbose in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None) |
                (verbose ? RegexOptions.IgnorePatternWhitespace : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ScopedLexingAndResolvedTreeKeepAllPublicOperations(string pattern, RegexOptions options)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        Assert.NotNull(Utf8OwnedTreeSliceLowerer.TryLower(tree, options));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, Texts, options);
    }

    [Theory]
    [InlineData("(?< n>a)")]
    [InlineData("(?<n >a)")]
    [InlineData(@"(?<n>a)\k< n>")]
    [InlineData(@"\p{ L}")]
    [InlineData(@"\p{L }")]
    [InlineData("(?x:a# comment\rb)")]
    public void RawLexemesAndCommentClosersKeepConstructionErrors(string pattern)
    {
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, RegexOptions.CultureInvariant |
            RegexOptions.IgnorePatternWhitespace, Texts, RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace);
    }
}
