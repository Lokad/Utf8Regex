using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser toggle stage: the differential battery pins bare option
// toggles identical to the provider trees (positional option threading with
// group-boundary restore, sequential leading contributions, top composites
// carrying tree options while nested composites keep entry options), plus
// decline agreement on flagless and double-dash shapes.
public sealed class Utf8OwnedTreeParserToggleTests
{
    public static IEnumerable<object?[]> ToggleAcceptCases()
    {
        yield return new object?[] { "(?i)", 512 };
        yield return new object?[] { "(?i-i)", 512 };
        yield return new object?[] { "(?i)a", 512 };
        yield return new object?[] { "(?i-i)a", 512 };
        yield return new object?[] { "(a(?i)b)c", 512 };
        yield return new object?[] { "((?i)a)b", 512 };
        yield return new object?[] { "(?i)a|b", 512 };
        yield return new object?[] { "((?i)a)", 512 };
        yield return new object?[] { "(?i)[ab]", 512 };
        yield return new object?[] { "(a\\1(?i:b))", 512 };
        yield return new object?[] { "(?m)", 512 };
        yield return new object?[] { "(?m)a", 512 };
        yield return new object?[] { "(?i)(?m)a", 512 };
        yield return new object?[] { "a(?i)", 512 };
        yield return new object?[] { "(?i)a(?-i)b", 512 };
        yield return new object?[] { "(?i)(a)", 512 };
        yield return new object?[] { "(?i)(?:a)b", 512 };
        yield return new object?[] { "(?i)(?i)a", 512 };
        yield return new object?[] { "(?i-i)(?i)a", 512 };
        yield return new object?[] { "(?s)", 512 };
        yield return new object?[] { "a|(?i)b", 512 };
        yield return new object?[] { "((?i:a)|b)", 512 };
        yield return new object?[] { "((?i)a|b)", 512 };
        yield return new object?[] { "(?=(?i)a|b)", 512 };
        yield return new object?[] { "(?i)(?-i)", 512 };
        yield return new object?[] { "(?i)(?m-i:a)", 512 };
        yield return new object?[] { "(?m)(?i-m:a)", 512 };
        yield return new object?[] { "(?-i)", 512 };
        yield return new object?[] { "(?x)ab", 512 };
        yield return new object?[] { "(?i)(a){0}", 512 };
        yield return new object?[] { "x(?i)", 512 };
        yield return new object?[] { "(?i)x(?-i)y", 512 };
        yield return new object?[] { "(?i)(?-i)a", 512 };
        yield return new object?[] { "(?-i)a", 512 };
        yield return new object?[] { "x(?m:(a)(b))", 512 };
        yield return new object?[] { "x(?s:(a)(b))", 512 };
        yield return new object?[] { "(?m:(a)b)", 512 };
        yield return new object?[] { "(?i:(a)b)", 512 };
        yield return new object?[] { "(?=(?i)a)b", 512 };
        yield return new object?[] { "(?<=(?i)a)b", 512 };
        yield return new object?[] { "(?:(?i)a)b", 512 };
        yield return new object?[] { "(?i:(?i)a)", 512 };
        yield return new object?[] { "((?i)a(?m)b)", 512 };
        yield return new object?[] { "((?i))", 512 };
        yield return new object?[] { "x(?i)y(?m)z", 512 };
        yield return new object?[] { "(?i)a|(?m)b", 512 };
    }

    public static IEnumerable<object?[]> ToggleDeclineCases()
    {
        yield return new object?[] { "(?-)" };
        yield return new object?[] { "(?i)(?-)" };
    }

    [Theory]
    [MemberData(nameof(ToggleAcceptCases))]
    public void OwnedToggleMatchesProviderTree(string pattern, int options)
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
    [MemberData(nameof(ToggleDeclineCases))]
    public void OwnedToggleDeclinesUnsupported(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            RegexOptions.CultureInvariant,
            RuntimeFrontEnd.RegexParser.GetTargetCulture(RegexOptions.CultureInvariant)));
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