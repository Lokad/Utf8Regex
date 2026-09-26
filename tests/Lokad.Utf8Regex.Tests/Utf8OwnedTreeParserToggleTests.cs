using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser toggle stage: the BCL-oracled battery pins bare option
// toggles with BCL metadata (positional option threading with
// group-boundary restore, sequential leading contributions, top composites
// carrying tree options while nested composites keep entry options, dash runs
// collapsing to one separator), plus decline agreement on flagless shapes.
public sealed class Utf8OwnedTreeParserToggleTests
{
    public static IEnumerable<object?[]> ToggleAcceptCases()
    {
        yield return new object?[] { "(?i)", 512, 513 };
        yield return new object?[] { "(?i-i)", 512, 512 };
        yield return new object?[] { "(?i)a", 512, 513 };
        yield return new object?[] { "(?i-i)a", 512, 512 };
        yield return new object?[] { "(a(?i)b)c", 512, 512 };
        yield return new object?[] { "((?i)a)b", 512, 512 };
        yield return new object?[] { "(?i)a|b", 512, 513 };
        yield return new object?[] { "((?i)a)", 512, 512 };
        yield return new object?[] { "(?i)[ab]", 512, 513 };
        yield return new object?[] { "(a\\1(?i:b))", 512, 512 };
        yield return new object?[] { "(?m)", 512, 514 };
        yield return new object?[] { "(?m)a", 512, 514 };
        yield return new object?[] { "(?i)(?m)a", 512, 515 };
        yield return new object?[] { "a(?i)", 512, 512 };
        yield return new object?[] { "(?i)a(?-i)b", 512, 513 };
        yield return new object?[] { "(?i)(a)", 512, 513 };
        yield return new object?[] { "(?i)(?:a)b", 512, 513 };
        yield return new object?[] { "(?i)(?i)a", 512, 513 };
        yield return new object?[] { "(?i-i)(?i)a", 512, 513 };
        yield return new object?[] { "(?s)", 512, 528 };
        yield return new object?[] { "a|(?i)b", 512, 512 };
        yield return new object?[] { "((?i:a)|b)", 512, 512 };
        yield return new object?[] { "((?i)a|b)", 512, 512 };
        yield return new object?[] { "(?=(?i)a|b)", 512, 512 };
        yield return new object?[] { "(?i)(?-i)", 512, 512 };
        yield return new object?[] { "(?i)(?m-i:a)", 512, 514 };
        yield return new object?[] { "(?m)(?i-m:a)", 512, 513 };
        yield return new object?[] { "(?-i)", 512, 512 };
        yield return new object?[] { "(?x)ab", 512, 544 };
        yield return new object?[] { "(?i)(a){0}", 512, 513 };
        yield return new object?[] { "x(?i)", 512, 512 };
        yield return new object?[] { "(?i)x(?-i)y", 512, 513 };
        yield return new object?[] { "(?i)(?-i)a", 512, 512 };
        yield return new object?[] { "(?-i)a", 512, 512 };
        yield return new object?[] { "x(?m:(a)(b))", 512, 512 };
        yield return new object?[] { "x(?s:(a)(b))", 512, 512 };
        yield return new object?[] { "(?m:(a)b)", 512, 514 };
        yield return new object?[] { "(?i:(a)b)", 512, 513 };
        yield return new object?[] { "(?=(?i)a)b", 512, 512 };
        yield return new object?[] { "(?<=(?i)a)b", 512, 512 };
        yield return new object?[] { "(?:(?i)a)b", 512, 512 };
        yield return new object?[] { "(?i:(?i)a)", 512, 513 };
        yield return new object?[] { "((?i)a(?m)b)", 512, 512 };
        yield return new object?[] { "((?i))", 512, 512 };
        yield return new object?[] { "x(?i)y(?m)z", 512, 512 };
        yield return new object?[] { "(?i)a|(?m)b", 512, 513 };
        yield return new object?[] { "(?i--m:a)", 512, 513 };
        yield return new object?[] { "(?--m:a)", 512, 512 };
        yield return new object?[] { "(?i---m:a)", 512, 513 };
        yield return new object?[] { "(?i-m-:a)", 512, 513 };
        yield return new object?[] { "(?i--:a)", 512, 513 };
        yield return new object?[] { "(?i--)", 512, 513 };
        yield return new object?[] { "(?-i-)", 512, 512 };
        yield return new object?[] { "(?i-m-)", 512, 513 };
        yield return new object?[] { "(?i--m)", 512, 513 };
        yield return new object?[] { "(?--m)", 512, 512 };
    }

    [Theory]
    [MemberData(nameof(ToggleAcceptCases))]
    public void OwnedToggleServesWithBclMetadata(string pattern, int options, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
    }

    public static IEnumerable<object?[]> DegenerateOptionClusterCases()
    {
        yield return new object?[] { "(?-)", 512 };
        yield return new object?[] { "(?i)(?-)", 512 };
        yield return new object?[] { "(?--)", 512 };
        yield return new object?[] { "(?-:a)", 512 };
        yield return new object?[] { "(?--:a)", 512 };
    }

    [Theory]
    [MemberData(nameof(DegenerateOptionClusterCases))]
    public void OwnedServesDegenerateOptionCluster(string pattern, int options)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        foreach (var inputText in new string[] { "", "a" })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
    }

}
