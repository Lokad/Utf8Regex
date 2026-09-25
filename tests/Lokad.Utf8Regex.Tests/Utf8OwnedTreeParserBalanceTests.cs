using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser balance stage: the differential battery pins balancing-group
// pops identical to the provider trees (pop nodes carry the resolved slot,
// definitions keep their numbers), plus decline agreement on pops of
// undefined groups and a residual for pops under top-level ExplicitCapture
// where named definitions stay routed to the provider.
public sealed class Utf8OwnedTreeParserBalanceTests
{
    public static IEnumerable<object?[]> BalanceAcceptCases()
    {
        yield return new object?[] { "(?<a>x)(?<-a>y)", 512 };
        yield return new object?[] { "((?<a>x)(?<-a>y))", 512 };
    }

    public static IEnumerable<object?[]> BalanceDeclineCases()
    {
        yield return new object?[] { "(?<a-b>x)", 512 };
        yield return new object?[] { "(?'a-b'x)", 512 };
        yield return new object?[] { "(?'a'x)(?-'a'y)", 512 };
    }

    public static IEnumerable<object?[]> BalanceResidualCases()
    {
        yield return new object?[] { "(?<a>x)(?<-a>y)", 516 };
    }

    [Theory]
    [MemberData(nameof(BalanceAcceptCases))]
    public void OwnedBalanceMatchesProviderTree(string pattern, int options)
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
    [MemberData(nameof(BalanceDeclineCases))]
    public void OwnedBalanceDeclinesUnsupported(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            (RegexOptions)options,
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options)));
    }

    [Theory]
    [MemberData(nameof(BalanceResidualCases))]
    public void OwnedBalanceDeclinesResidual(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.NotNull(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options));
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