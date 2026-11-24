using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SliceBackrefTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    // These case-sensitive patterns share their participation and retry rules with .NET.
    private static Regex Bcl(string pattern) => new(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(a)\\1", "aa", true };
        yield return new object?[] { "(?<x>a)\\k<x>", "aa", true };
        yield return new object?[] { "(?<x>a)\\k<x>", "ab", false };
        yield return new object?[] { "(\\2a|(b))+", "bba", true };
        yield return new object?[] { "(a)\\1", "ab", false };
        yield return new object?[] { "(é)\\1", "éé", true };
        yield return new object?[] { "(a|b)+\\1", "abb", true };
        yield return new object?[] { "(a|b)+\\1", "aba", false };
        yield return new object?[] { "(a*)\\1", "aaa", true };
        yield return new object?[] { "(x)?b\\1", "b", false };
        yield return new object?[] { "(x)?b\\1", "bxb", false };
        yield return new object?[] { "(a)(b)\\2\\1", "abba", true };
        yield return new object?[] { "(ab|a)+\\1z", "ababz", true };
        yield return new object?[] { "(ab|a)+\\1z", "ababaz", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(
        string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        using var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
    }

    [Theory]
    [InlineData("(a)\\1", "aa aa", 0, 2)]
    [InlineData("(a|b)+\\1", "abb abb", 0, 2)]
    [InlineData("(x)?b\\1", "bbb", 0, 0)]
    [InlineData("(a)\\1", "aa aa", 3, 1)]
    [InlineData("(ab|a)+\\1z", "ababz ababz", 0, 2)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        if (startOffsetInBytes == 0)
        {
            Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        }

        using var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
    }

    [Theory]
    [InlineData("(a|ab)\\1", "xabab", true, 1, 5)]
    [InlineData("(a)(b)\\2\\1", "xxabbayy", true, 2, 6)]
    [InlineData("(é)\\1", "😀éé", true, 4, 8)]
    [InlineData("(a)\\1", "ab", false, 0, 0)]
    public void OwnedFirstMatchProjectsBytes(
        string pattern, string inputText, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        using var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText));
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
            Assert.Equal((expectedStart, expectedEnd), (found.StartOffsetInBytes, found.EndOffsetInBytes));
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughBackrefs()
    {
        using var regex = new Utf8Pcre2Regex("(a)\\1", options: Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aa aa"), 0);
        var ranges = new List<(int Start, int End)>();
        while (enumerator.MoveNext())
        {
            ranges.Add((enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        }

        Assert.Equal([(0, 2), (3, 5)], ranges);
    }

    [Fact]
    public void PrimaryMatchWritesBackreferenceGroups()
    {
        using var regex = new Utf8Pcre2Regex("(a)(b)\\2\\1", options: Pcre2CompileOptions.None);
        var captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var detailed = regex.Match("xxabbayy"u8, captures: captures);
        Assert.True(detailed.Success);
        Assert.Equal("abba"u8.ToArray(), detailed.GetValueBytes().ToArray());
        Assert.True(captures[1].Success);
        Assert.True(captures[2].Success);
        Assert.Equal((2, 3), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.Equal((3, 4), (captures[2].StartOffsetInBytes, captures[2].EndOffsetInBytes));
    }

    [Fact]
    public void OwnedReplaceExpandsBackrefMatch()
    {
        using var regex = new Utf8Pcre2Regex("(a)\\1", options: Pcre2CompileOptions.None);
        Assert.Equal("<aa> ab"u8.ToArray(), regex.Replace("aa ab"u8, "<$0>"u8));
    }
}
