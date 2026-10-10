using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 mixed-class shorthands for the PCRE2 profile: every hybrid program
// declines slice routing (native shorthands stay ASCII-scoped while the
// shared runner applies Unicode categories to members). The decline is
// load-bearing: BCL-Unicode member matches on non-ASCII digits disagree
// with native ASCII misses, pinned below on both sides.
public sealed class Pcre2SliceHybridTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Theory]
    [InlineData("[\\da]+")]
    [InlineData("[^\\da]+")]
    [InlineData("[\\w-]+")]
    [InlineData("[\\d\\s]+")]
    [InlineData("[\\Da]+")]
    [InlineData("[a\\d]+")]
    public void HybridShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void NativeAsciiScopeProvesDeclineMatters()
    {
        // BCL-Unicode members match the BMP digit; native ASCII scope
        // misses it, so the slice must not serve these shapes.
        var arabicDigit = char.ConvertFromUtf32(0x660);
        var input = Utf8(arabicDigit);
        Assert.True(Bcl("[\\da]+").IsMatch(arabicDigit));
        Assert.False(new Utf8Pcre2Regex("[\\da]+", Pcre2CompileOptions.None).IsMatch(input));
        Assert.False(Bcl("[^\\da]+").IsMatch(arabicDigit));
        Assert.True(new Utf8Pcre2Regex("[^\\da]+", Pcre2CompileOptions.None).IsMatch(input));
    }

    [Theory]
    [InlineData("[\\da]+", "a1", true)]
    [InlineData("[\\da]+", "xy", false)]
    [InlineData("[^\\da]+", "-", true)]
    public void NativeIsMatchMatchesOracleOnAscii(
        string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[\\da]+", "a1b22", 2)]
    public void NativeCountMatchesOracleOnAscii(
        string pattern, string inputText, int expected)
    {
        Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void NativeFirstMatchProjectsBytes()
    {
        var regex = new Utf8Pcre2Regex("([a\\d]+)", Pcre2CompileOptions.None);
        var found = regex.Match(Utf8("xxa1b"), 0);
        Assert.True(found.Success);
        Assert.Equal(2, found.StartOffsetInBytes);
        Assert.Equal(4, found.EndOffsetInBytes);
        Assert.True(regex.DebugCompiledProgram is not null);
    }
}