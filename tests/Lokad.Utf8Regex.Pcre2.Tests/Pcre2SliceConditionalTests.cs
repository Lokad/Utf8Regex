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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        var oracle = Bcl(pattern).Match(inputText);
        ReadOnlySpan<byte> actual1Input = Utf8(inputText);
        var actual1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var actual = regex.Match(actual1Input, captures: actual1Captures);
        Assert.Equal(expected, actual.Success);
        for (var number = 0; number < oracle.Groups.Count; number++)
        {
            Assert.Equal(oracle.Groups[number].Success, actual1Captures[number].Success);
            if (oracle.Groups[number].Success)
                Assert.Equal(oracle.Groups[number].Value, System.Text.Encoding.UTF8.GetString((actual1Captures[number].Success ? actual1Input.Slice(actual1Captures[number].StartOffsetInBytes, actual1Captures[number].EndOffsetInBytes - actual1Captures[number].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
    }

    [Fact]
    public void NegativeAssertionConditionRetainsMatchedCaptures()
    {
        var negative2Regex = new Utf8Pcre2Regex("^(?(?!(a))def|abc)");
        ReadOnlySpan<byte> negative2Input = "abc"u8;
        var negative2Captures = new Utf8Pcre2CaptureRange[negative2Regex.CaptureGroupCount + 1];
        var negative = negative2Regex.Match(negative2Input, captures: negative2Captures);
        Assert.True(negative.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((negative2Captures[1].Success ? negative2Input.Slice(negative2Captures[1].StartOffsetInBytes, negative2Captures[1].EndOffsetInBytes - negative2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
        Assert.Equal(Bcl(pattern).Matches(inputText).Count, new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None).Count(Utf8(inputText)));
    }
}
