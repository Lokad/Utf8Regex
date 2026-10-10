using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 conditionals for the PCRE2 profile: group tests ride the shared runner
// with identical behavior (BCL and positional numbering agree on unnamed
// shapes); expression tests stay native because native PCRE2 flows assertion
// captures (even from negative tests) while the shared runner follows BCL
// (failed negatives leak nothing; group tests commit once).
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
    public void GroupTestsRouteShared(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Contains("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> NativeCases()
    {
        yield return new object?[] { "(?(?=a)b|c)", "c", true };
        yield return new object?[] { "^(?(?=(a))abc|def)", "abc", true };
        yield return new object?[] { "^(?(?!(a))def|abc)", "abc", true };
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void ExpressionTestsStayNative(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.DoesNotContain("Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
    }

    [Fact]
    public void NativeNegativeAssertionCapturesFlow()
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