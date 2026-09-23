using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 shorthand slice for the PCRE2 profile: every shorthand program
// declines the slice overlay because native shorthands stay ASCII-scoped
// while the shared runner applies Unicode semantics (UCP parity is future
// work). The pins below prove native behavior survives on divergence edges
// (non-ASCII digits, letters, and spaces all miss).
public sealed class Pcre2SliceShorthandTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    public static IEnumerable<object?[]> ShorthandShapes()
    {
        yield return new object?[] { "\\d+" };
        yield return new object?[] { "\\D+" };
        yield return new object?[] { "\\w+" };
        yield return new object?[] { "\\W+" };
        yield return new object?[] { "\\s+" };
        yield return new object?[] { "\\S+" };
        yield return new object?[] { "(\\d+)" };
        yield return new object?[] { "\\d+:\\w+" };
        yield return new object?[] { "[\\d]+" };
    }

    [Theory]
    [MemberData(nameof(ShorthandShapes))]
    public void ShorthandShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData("\\d+")]
    [InlineData("\\w+")]
    [InlineData("\\s+")]
    public void ShorthandShapesDeclineSliceRoutingUnderUcp(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Ucp);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData("\\d+", "١", false)]
    [InlineData("\\d+", "1", true)]
    [InlineData("\\w+", "é", false)]
    [InlineData("\\w+", "e", true)]
    [InlineData("\\s+", "\u00A0", false)]
    [InlineData("\\s+", " ", true)]
    [InlineData("\\D+", "١", true)]
    [InlineData("\\W+", "é", true)]
    [InlineData("\\S+", "\u00A0", true)]
    public void NativeAsciiShorthandsKeepAsciiSemantics(
        string pattern, string inputText, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText), 0));
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }
}
