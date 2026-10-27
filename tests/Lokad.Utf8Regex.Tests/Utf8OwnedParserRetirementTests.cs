using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedParserRetirementTests
{
    private static readonly string[] Patterns =
    [
        @"[\uD800-\uDFFF]+", @"(?x:[\uD800-\uDFFF] + )",
        @"[\uFFFF]+", @"(?x:[\uFFFF] + )", @"[\uFFFE-\uFFFF]+",
        @"[a\uFFFF\u0080-\u0090]+", @"[\u0000-\uFFFF]+",
        @"(?i:[\u0080-\uFFFF]+)", @"[\u0000-\uFFFF-[\uD800-\uDFFF]]+",
        @"(?<x>[\uD800-\uDFFF])\k<x>", @"(?<x>[\uFFFF])+",
        @"(?<=(?<x>a)\k<x>)b", @"(?x:(?<=(?<x>a)\k<x>) b)",
        @"(?x:(?<!(?<x>a)\k<x>) b)", @"(?i:(?<=(?<x>a)\k<x>)b)",
        @"(?(?=a)a|b)+", @"(?x:(?(?=a) a | b)+)",
        @"(?(?!a)b|a)", @"(?(?<=a)b|c)", @"(?(?<!a)b|a)",
        @"(?(?=(?<x>a+))\k<x>|b)", @"(?x:(?(?=(?<x>a+)) \k<x> | b)+)",
        @"(?(?:a)a|b)", @"(?(?>a)a|b)", @"(?(a|b)a|b)",
        @"(?x:(?(a ) a | b)+)", @"(?(?=a)((?I:a))|b)",
        @"(?(?=a)(a)(b)|c)", @"(?(?=a)(?<x>a)(b)|c)",
        @"(?(?=a)(?:a)(b)|(c))", @"(?(?=a)(a)|(b))",
        @"(?(?=a)a|b)(c)(d)", @"(?n:(?(?=a)(a)|b))(?-n:(c))",
        @"(?(?=a)(?(?=a)a|b)|b)",
        @"(?I:ab)+", @"(?+I:ab)+", @"(?i-i+i:ab)+", @"(?i--m+i:ab)+",
        @"(?S:.)+", @"(?M:^a$)", @"(?X: a + b )", @"(?+)(?<x>a)\k<x>",
        @"(?N:(?<x>a))\k<x>", @"(?<a>x)(?<-a>y)",
        @"(?x:(?<a> x )(?<-a> y ))", @"[\101-\132]+", @"[\000-\777]+",
    ];

    private static readonly string[] Texts =
    [
        "", "ab", "aab", "abb", "abbd", "baa", "abba", "AB ab", "aAab",
        "xxyyxy", "\uffff\ufffe\uffff\0a", "\U0001f600ab\U0001f600", "\U00010400\U00010428",
        "z\nab\nba", "\r\nab", "aaabab kKΘθϴ", "éµμΜ123", "AZ az 012\0\u00ff",
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var explicitCapture in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None) |
                (explicitCapture ? RegexOptions.ExplicitCapture : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CanonicalGrammarOwnsFormerCompatibilityForms(string pattern, RegexOptions options)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        Assert.NotNull(Utf8OwnedTreeSliceLowerer.TryLower(tree, options));
        // The diagnosed .NET 10.0.12 compiled U+FFFF class-loop defect is
        // allocation-dependent. Both owned modes keep interpreted semantics.
        // A separate compiled RTL conditional/reference defect changes the
        // earlier match on aaabab. Keep stable interpreted ranges and history.
        var oracleOptions = pattern.Contains(@"\uFFFF", StringComparison.Ordinal) ||
            pattern == @"(?x:(?(?=(?<x>a+)) \k<x> | b)+)" && (options & RegexOptions.RightToLeft) != 0
            ? options & ~RegexOptions.Compiled : options;
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, Texts, oracleOptions);
    }

    [Theory]
    [InlineData(@"(?(?i)a|b)")]
    [InlineData(@"(?(?I:a)a|b)")]
    [InlineData(@"(?(?=a)(?i)b|c)")]
    [InlineData(@"(?(?=a)b(?I:c)|a)")]
    [InlineData(@"(?(?<x>a)a|b)")]
    [InlineData(@"(?(?#a)a|b)")]
    [InlineData(@"(?(1 )a|b)(a)")]
    [InlineData(@"(?İ:a)")]
    [InlineData(@"(?ſ:a)")]
    public void InvalidConditionalAndOptionLexemesHaveOwnedConstructionErrors(string pattern)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace;
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, options));
        // Some formerly invalid conditional option forms are admitted by
        // .NET 11. Pin the owned admission contract independently of the BCL.
        var error = Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options));
        Assert.Equal("pattern", error.ParamName);
    }
}
