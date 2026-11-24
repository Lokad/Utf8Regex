using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SliceDotTests
{
    [Fact]
    public void DotAllConsumesLineFeed()
    {
        using var regex = new Utf8Pcre2Regex("a.*b", options: Pcre2CompileOptions.DotAll);
        Assert.True(regex.IsMatch("xxa\nbzz"u8));
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Theory]
    [InlineData("a.*b", "xxa1bzz", true)]
    [InlineData("a.*b", "xxayy", false)]
    [InlineData("a.*b", "xxa\nbzz", false)]
    [InlineData("(a.c)+", "xxabcaycyy", true)]
    [InlineData("(a.c)+", "xa\U0001F600c", true)]
    public void OwnedIsMatchMatchesNativeSemantics(string pattern, string inputText, bool expected)
    {
        using var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText), 0));
    }

    [Theory]
    [InlineData("(a.c)+", "xa\U0001F600c", 1)]
    [InlineData("a.*b", "a\U0001F600b", 1)]
    [InlineData("..", "\U0001F600", 0)]
    [InlineData("..", "\U0001F600\U0001F600", 1)]
    [InlineData("(a.c)+", "abc ayc", 2)]
    public void OwnedCountMatchesNativeSemantics(string pattern, string inputText, int expected)
    {
        using var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
    }

    [Theory]
    [InlineData(".", "\U0001F600", 0, 4)]
    [InlineData("..", "\U0001F600\U0001F600", 0, 8)]
    [InlineData("(a.c)+", "xa\U0001F600c", 1, 7)]
    [InlineData("a.*b", "xxa1bzz", 2, 5)]
    [InlineData("a.*?b", "xxa1b2byy", 2, 5)]
    [InlineData(".*", "\U0001F600a", 0, 5)]
    [InlineData(".*b", "a\U0001F600b", 0, 6)]
    [InlineData("x*|y.", "y\U0001F600", 0, 0)]
    [InlineData("a|.*", "zz", 0, 2)]
    public void OwnedFirstMatchProjectsScalarBytes(
        string pattern, string inputText, int expectedStart, int expectedEnd)
    {
        using var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText));
        Assert.True(found.Success);
        Assert.Equal((expectedStart, expectedEnd), (found.StartOffsetInBytes, found.EndOffsetInBytes));
    }

    [Fact]
    public void OwnedEnumerationAdvancesByScalarsOnAstralInput()
    {
        using var regex = new Utf8Pcre2Regex("a.c", options: Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("xa\U0001F600cxaXc"), 0);
        var ranges = new List<(int Start, int End)>();
        while (enumerator.MoveNext())
        {
            ranges.Add((enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        }

        Assert.Equal([(1, 7), (8, 11)], ranges);
    }

    [Fact]
    public void OwnedDetailedProjectsDotGroupOnAstralInput()
    {
        using var regex = new Utf8Pcre2Regex("(.+)", options: Pcre2CompileOptions.None);
        var input = "x😀y"u8;
        var captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var detailed = regex.Match(input, captures: captures);
        Assert.True(detailed.Success);
        Assert.Equal(input.ToArray(), detailed.GetValueBytes().ToArray());
        Assert.True(captures[1].Success);
        Assert.Equal((0, input.Length), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
    }

    [Fact]
    public void OwnedReplaceExpandsDotGroup()
    {
        using var regex = new Utf8Pcre2Regex("(a.*b)", options: Pcre2CompileOptions.None);
        Assert.Equal("xx<aabb>YY"u8.ToArray(), regex.Replace("xxaabbYY"u8, "<$1>"u8));
    }
}
