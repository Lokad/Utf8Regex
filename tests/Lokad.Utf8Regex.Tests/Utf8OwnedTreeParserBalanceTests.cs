using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser balance stage: the BCL-oracled battery pins balancing-group
// pops with BCL metadata (pop nodes carry the resolved slot,
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
        yield return new object?[] { "(?<a-b>x)", 512, RegexParseError.UndefinedNamedReference, 6 };
        yield return new object?[] { "(?'a-b'x)", 512, RegexParseError.UndefinedNamedReference, 6 };
        yield return new object?[] { "(?'a'x)(?-'a'y)", 512, RegexParseError.InvalidGroupingConstruct, 11 };
    }

    public static IEnumerable<object?[]> BalanceResidualCases()
    {
        yield return new object?[] { "(?<a>x)(?<-a>y)", 516 };
    }

    [Theory]
    [MemberData(nameof(BalanceAcceptCases))]
    public void OwnedBalanceServesWithBclMetadata(string pattern, int options)
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
    [MemberData(nameof(BalanceDeclineCases))]
    public void OwnedBalanceDeclinesUnsupported(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

    [Theory]
    [MemberData(nameof(BalanceResidualCases))]
    public void OwnedBalanceDeclinesTopLevelExplicitResidual(string pattern, int options)
    {
        // Top-level ExplicitCapture leaves this pop shape to the provider today;
        // the BCL fallback constructs it, so the flip keeps serving BCL-exactly.
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(new[] { 0, 1 }, oracle.GetGroupNumbers());
        Assert.Equal(new[] { "0", "a" }, oracle.GetGroupNames());
    }
}
