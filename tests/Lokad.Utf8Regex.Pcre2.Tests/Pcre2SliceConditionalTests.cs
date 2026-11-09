using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Capture-set conditions overlap the BCL on these ASCII positional groups.
// Assertion conditions preserve PCRE2 capture flow even from a matching
// negative condition; Boolean planning cannot change result semantics.
public sealed class Pcre2SliceConditionalTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    public static IEnumerable<object?[]> SharedCases()
    {
        yield return new object?[] { "(a)?(?(1)b|c)", "ab", true };
        yield return new object?[] { "(a)?(?(1)b|c)", "c", true };
        yield return new object?[] { "(a)(?(1)b|c)(d)", "abd", true };
        yield return new object?[] { "(?(1)(a)|(b))", "b", true };
    }

    [Theory]
    [MemberData(nameof(SharedCases))]
    public void CaptureSetConditionsPreservePositionalCaptures(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        var oracle = Bcl(pattern).Match(inputText);
        var actual = regex.MatchDetailed(Utf8(inputText));
        Assert.Equal(expected, actual.Success);
        for (var number = 0; number < oracle.Groups.Count; number++)
        {
            Assert.Equal(oracle.Groups[number].Success, actual.GetGroup(number).Success);
            if (oracle.Groups[number].Success)
                Assert.Equal(oracle.Groups[number].Value, actual.GetGroup(number).GetValueString());
        }
    }

    public static IEnumerable<object?[]> AssertionCases()
    {
        yield return new object?[] { "(?(?=a)b|c)", "c", true };
        yield return new object?[] { "^(?(?=(a))abc|def)", "abc", true };
        yield return new object?[] { "^(?(?!(a))def|abc)", "abc", true };
    }

    [Theory]
    [MemberData(nameof(AssertionCases))]
    public void AssertionConditionsPreserveBranchResults(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
    }

    [Fact]
    public void NegativeAssertionConditionRetainsMatchedCaptures()
    {
        var negative = new Utf8Pcre2Regex("^(?(?!(a))def|abc)").MatchDetailed("abc"u8);
        Assert.True(negative.Success);
        Assert.Equal("a", negative.GetGroup(1).GetValueString());
    }

    public static IEnumerable<object?[]> ParityCases()
    {
        yield return new object?[] { "(a)?(?(1)b|c)", "ab" };
        yield return new object?[] { "(a)(?(1)b|c)(d)", "abd" };
        yield return new object?[] { "(?(?=a)b|c)", "c" };
    }

    [Theory]
    [MemberData(nameof(ParityCases))]
    public void ConditionalCountMatchesOracle(string pattern, string inputText)
    {
        Assert.Equal(Bcl(pattern).Matches(inputText).Count, new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None).Count(Utf8(inputText)));
    }
}
