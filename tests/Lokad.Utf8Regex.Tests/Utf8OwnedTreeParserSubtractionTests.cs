using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser subtraction stage: the differential battery pins character
// class subtraction identical to the provider trees (single subtraction with
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
        yield return new object?[] { "[a-\\d-[b]]" };
        yield return new object?[] { "[a-[b]-c]" };
        yield return new object?[] { "[a--[b]]" };
        yield return new object?[] { "[a-[b]-[c]]" };
        yield return new object?[] { "[a-[b]c]" };
        yield return new object?[] { "[a-[]" };
        yield return new object?[] { "[a-[]b]" };
        yield return new object?[] { "[-[a]]" };
        yield return new object?[] { "[[a]]" };
        yield return new object?[] { "[a-[b]" };
        yield return new object?[] { "[a-[]]" };
        yield return new object?[] { "[a-[b]-[\\d]]" };
        yield return new object?[] { "[a-[b]-\\d]" };
        yield return new object?[] { "[a-\\p{Lu}-[b]]" };
        yield return new object?[] { "[[a]-[b]]" };
        yield return new object?[] { "[a-[b-c]-[d]]" };
    }

    [Theory]
    [MemberData(nameof(SubtractionAcceptCases))]
    public void OwnedSubtractionMatchesProviderTree(string pattern, int options)
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
    [MemberData(nameof(SubtractionDeclineCases))]
    public void OwnedSubtractionDeclinesUnsupported(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            RegexOptions.CultureInvariant,
            RuntimeFrontEnd.RegexParser.GetTargetCulture(RegexOptions.CultureInvariant)));
    }

    public static IEnumerable<object?[]> SubtractionResidualCases()
    {
        // Fork-widening debt (no-vendor rule): BCL rejects the nested upper-bound
        // shorthand escape while the pristine fork accepts it. Pins owned-decline plus
        // legacy-accept; the provider gatekeeper still declines it.
        yield return new object?[] { "[a-[b-\\d]]" };
    }

    [Theory]
    [MemberData(nameof(SubtractionResidualCases))]
    public void OwnedSubtractionDeclinesResidual(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.NotNull(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, RegexOptions.CultureInvariant));
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