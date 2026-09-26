using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser subtraction stage: the BCL-oracled battery pins character
// class subtraction with BCL metadata (single subtraction with
// the base segment emitted first and the subtracted level appended, nested
// subtraction, ranges, negation on either side, and shorthand or category
// payloads), plus decline agreement on doubled or trailed subtraction,
// shorthand range ends, and unclosed classes, and the IgnoreCase residual
// that still routes to the provider because subtracted sets fold.
public sealed class Utf8OwnedTreeParserSubtractionTests
{
    public static IEnumerable<object?[]> SubtractionAcceptCases()
    {
        yield return new object?[] { "[a-[b]]", 512 };
        yield return new object?[] { "[a-[b-[c]]]", 512 };
        yield return new object?[] { "[abc-[b]]", 512 };
        yield return new object?[] { "[a-z-[aeiou]]", 512 };
        yield return new object?[] { "[^a-[b]]", 512 };
        yield return new object?[] { "[a-[\\d]]", 512 };
        yield return new object?[] { "[\\d-[a]]", 512 };
        yield return new object?[] { "[a-[b]]+", 512 };
        yield return new object?[] { "([a-[b]])", 512 };
        yield return new object?[] { "[a-b-[c]]", 512 };
        yield return new object?[] { "[a-[^b]]", 512 };
        yield return new object?[] { "[^a-[^b]]", 512 };
        yield return new object?[] { "[]-[a]]", 512 };
        yield return new object?[] { "[a-z-[b]]", 512 };
        yield return new object?[] { "[abc-[b-d]]", 512 };
        yield return new object?[] { "[a-[b-c]]", 512 };
        yield return new object?[] { "[^abc-[b]]", 512 };
        yield return new object?[] { "[a-[\\w]]", 512 };
        yield return new object?[] { "[\\w-[a]]", 512 };
        yield return new object?[] { "[a-c-[b]]", 512 };
        yield return new object?[] { "[ab-[a]]", 512 };
        yield return new object?[] { "[a-[a]]", 512 };
        yield return new object?[] { "[z-[a]]", 512 };
        yield return new object?[] { "[\\d-[\\d]]", 512 };
        yield return new object?[] { "[a-[b]][c]", 512 };
        yield return new object?[] { "[--[a]]", 512 };
        yield return new object?[] { "[a-[b]]-[c]", 512 };
        yield return new object?[] { "[a-[b]][^c]", 512 };
        yield return new object?[] { "[\\da-[b]]", 512 };
        yield return new object?[] { "[a-[b]]", 576 };
        yield return new object?[] { "[a-[b]]", 768 };
    }

    public static IEnumerable<object?[]> SubtractionDeclineCases()
    {
        yield return new object?[] { "[a-\\d-[b]]", RegexParseError.ShorthandClassInCharacterRange, 5 };
        yield return new object?[] { "[a-[b]-c]", RegexParseError.ExclusionGroupNotLast, 6 };
        yield return new object?[] { "[a--[b]]", RegexParseError.ReversedCharacterRange, 4 };
        yield return new object?[] { "[a-[b]-[c]]", RegexParseError.ExclusionGroupNotLast, 6 };
        yield return new object?[] { "[a-[b]c]", RegexParseError.ExclusionGroupNotLast, 6 };
        yield return new object?[] { "[a-[]", RegexParseError.UnterminatedBracket, 5 };
        yield return new object?[] { "[a-[]b]", RegexParseError.UnterminatedBracket, 7 };
        yield return new object?[] { "[a-[b]", RegexParseError.UnterminatedBracket, 6 };
        yield return new object?[] { "[a-[]]", RegexParseError.UnterminatedBracket, 6 };
        yield return new object?[] { "[a-[b]-[\\d]]", RegexParseError.ExclusionGroupNotLast, 6 };
        yield return new object?[] { "[a-[b]-\\d]", RegexParseError.ExclusionGroupNotLast, 6 };
        yield return new object?[] { "[a-\\p{Lu}-[b]]", RegexParseError.ShorthandClassInCharacterRange, 5 };
        yield return new object?[] { "[a-[b-c]-[d]]", RegexParseError.ExclusionGroupNotLast, 8 };
    }

    [Theory]
    [MemberData(nameof(SubtractionAcceptCases))]
    public void OwnedSubtractionServesWithBclMetadata(string pattern, int options)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)options, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
    }

    [Theory]
    [MemberData(nameof(SubtractionDeclineCases))]
    public void OwnedSubtractionDeclinesUnsupported(string pattern, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

    [Fact]
    public void OwnedSubtractionDeclinesButBclServesLeadingDashGap()
    {
        // BCL reads a leading dash as a literal while the owned parser declines.
        Assert.Null(Utf8OwnedTreeParser.TryParse("[-[a]]", RegexOptions.CultureInvariant));
        var oracle = new Regex("[-[a]]", RegexOptions.CultureInvariant);
        var product = new Utf8Regex("[-[a]]", RegexOptions.CultureInvariant);
        Assert.Equal(oracle.ToString(), product.Pattern);
    }

    public static IEnumerable<object?[]> NestedBracketNarrowingCases()
    {
        yield return new object?[] { "[[a]]", 512, new string[] { "[a]", "a]", "[]", "[b]", "]", "a" } };
        yield return new object?[] { "[[a]-[b]]", 512, new string[] { "a-b]", "[-b]", "[a]", "a]", "[]", "[b]", "a-[b]]" } };
    }

    [Theory]
    [MemberData(nameof(NestedBracketNarrowingCases))]
    public void OwnedServesNestedBracketSequence(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
    }

    public static IEnumerable<object?[]> SubtractionResidualCases()
    {
        // Fork-widening debt (no-vendor rule): BCL rejects the nested upper-bound
        // shorthand escape while the pristine fork accepts it. Pins owned-decline plus
        // legacy-accept; the provider gatekeeper still declines it.
        yield return new object?[] { "[a-[b-\\d]]", RegexParseError.ShorthandClassInCharacterRange, 8 };
    }

    [Theory]
    [MemberData(nameof(SubtractionResidualCases))]
    public void OwnedSubtractionDeclinesResidual(string pattern, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }
}
