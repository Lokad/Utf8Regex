using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser quantified-scope stage: the BCL-oracled battery pins loops
// directly over scoped groups with BCL metadata. Loop wrappers
// carry the atom options (so a wrapped scope marks the loop), which the merge
// pass and the leading fence leave alone; behavior is unchanged because bodies
// carry the effective scoping either way.
public sealed class Utf8OwnedTreeParserQuantifiedScopeTests
{
    public static IEnumerable<object?[]> QuantifiedScopeAcceptCases()
    {
        yield return new object?[] { "a(?m:b)+", 512, 512 };
        yield return new object?[] { "a(?i:\\d)?", 512, 512 };
        yield return new object?[] { "a(?m:[ab]){1,2}", 512, 512 };
        yield return new object?[] { "a(?m:.)*", 512, 512 };
        yield return new object?[] { "a(?i:^)+", 512, 512 };
        yield return new object?[] { "a(?m:$){2}", 512, 512 };
        yield return new object?[] { "((?m:a)b)+", 512, 512 };
        yield return new object?[] { "(a(?m:b))+", 512, 512 };
        yield return new object?[] { "(?i:(?m:a)+)", 512, 513 };
        yield return new object?[] { "x(?s:y)*", 512, 512 };
        yield return new object?[] { "a(?i:b)+c", 512, 512 };
        yield return new object?[] { "(a(?i)b)+", 512, 512 };
        yield return new object?[] { "(a(?i)b)*", 512, 512 };
        yield return new object?[] { "((?i)a)+", 512, 512 };
        yield return new object?[] { "(x(?i)y)+", 512, 512 };
    }

    [Theory]
    [MemberData(nameof(QuantifiedScopeAcceptCases))]
    public void OwnedQuantifiedScopeServesWithBclMetadata(string pattern, int options, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
    }

}
