using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 verbose/explicit front ends for the PCRE2 profile: native extended
// skips vertical tab while Core IgnorePatternWhitespace keeps it significant,
// so Extended and ExtendedMore decline slice routing. PCRE2 has no
// ExplicitCapture option, so core explicit programs only arise through
// delegation with BCL-exact numbering.
public sealed class Pcre2SliceVerboseTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Theory]
    [InlineData("a +b")]
    [InlineData("( a | b ) +c")]
    [InlineData("[a-z]+")]
    public void ExtendedShapesDeclineSliceRouting(string pattern)
    {
        foreach (var options in new[] { Pcre2CompileOptions.Extended, Pcre2CompileOptions.ExtendedMore })
        {
            var regex = new Utf8Pcre2Regex(pattern, options);
            Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        }
    }

    [Fact]
    public void ExtendedVerticalTabDivergenceStaysNative()
    {
        // Pinned divergence that forces the decline above: native extended
        // skips vertical tab while Core keeps it significant. .NET 11's BCL
        // now strips it too, so it cannot stand in for the owned Core contract.
        var pattern = "a" + char.ConvertFromUtf32(0x0B) + "b";
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Extended);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(Utf8("ab")));
        Assert.False(regex.IsMatch(Utf8("a\vb")));
        var core = new Utf8Regex(pattern,
            System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.False(core.IsMatch("ab"u8));
        Assert.True(core.IsMatch("a\vb"u8));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "a +b", "aab", true };
        yield return new object?[] { "( a | b ) +c", "abac", true };
        yield return new object?[] { "a #c", "a", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void ExtendedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Extended);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }
}
