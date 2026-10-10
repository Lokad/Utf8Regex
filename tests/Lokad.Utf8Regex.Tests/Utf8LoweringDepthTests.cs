using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// Deep patterns retain owned parsing, capture metadata and scalar execution.
/// Preparation uses explicit frames; nesting does not select BCL execution.
/// </summary>
public sealed class Utf8LoweringDepthTests
{
    [Theory]
    [InlineData(2000)]
    [InlineData(5000)]
    [InlineData(20000)]
    public void DeepNestingKeepsOwnedExecutionAndCorrectResults(int depth)
    {
        var pattern = new string('(', depth) + "abc" + new string(')', depth);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxabcxx")));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("xxabxx")));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(8000)]
    public void WideAlternationConstructsWithCorrectResults(int branches)
    {
        var builder = new StringBuilder("(?:");
        for (var i = 0; i < branches; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            builder.Append("alt");
            builder.Append(i);
        }

        builder.Append(')');
        var regex = new Utf8Regex(builder.ToString(), RegexOptions.CultureInvariant);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxalt123xx")));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("xxzzzqqxx")));
    }

    [Fact]
    public void DeepNestingKeepsGroupMetadataCorrect()
    {
        var pattern = new string('(', 500) + "(?<word>abc)" + new string(')', 500);
        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var runtime = new Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(runtime.GetGroupNames(), utf8.GetGroupNames());
        Assert.Equal(runtime.GetGroupNumbers(), utf8.GetGroupNumbers());
        Assert.Equal(runtime.GroupNumberFromName("word"), utf8.GroupNumberFromName("word"));
        Assert.Equal("word", utf8.GroupNameFromNumber(utf8.GroupNumberFromName("word")));
        Assert.True(utf8.IsMatch(Encoding.UTF8.GetBytes("xxabcxx")));
        Assert.True(utf8.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
