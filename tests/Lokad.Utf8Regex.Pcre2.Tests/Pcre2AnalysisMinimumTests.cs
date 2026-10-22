using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AnalysisMinimumTests
{
    [Theory]
    [InlineData("abc", "abc", 3)]
    [InlineData(".", "a", 1)]
    [InlineData("[é]", "é", 1)]
    [InlineData("[😀]", "😀", 1)]
    [InlineData("[é][😀]", "é😀", 2)]
    [InlineData(@"\X", "😀", 1)]
    [InlineData("[A-Z]{2,4}", "AB", 2)]
    [InlineData("a++", "a", 1)]
    [InlineData("a{3}", "aaa", 3)]
    [InlineData("(a)", "a", 1)]
    [InlineData("(?i)k", "K", 1)]
    [InlineData("a*", "", 0)]
    [InlineData("[é]?", "", 0)]
    [InlineData("^$", "", 0)]
    public void AnalysisCoversSpecializedProgramsWithConservativeByteBounds(
        string pattern, string subject, int expectedBound)
    {
        var input = Encoding.UTF8.GetBytes(subject);
        foreach (var regex in new[] { new Utf8Pcre2Regex(pattern), new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern)) })
        {
            var bound = regex.Analyze().MinRequiredLengthInBytes;
            Assert.Equal(expectedBound, bound);
            var match = regex.Match(input);
            Assert.True(match.Success);
            Assert.True(bound <= match.EndOffsetInBytes - match.StartOffsetInBytes);
        }
    }
}
