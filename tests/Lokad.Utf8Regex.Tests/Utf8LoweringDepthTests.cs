using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R02 extraction 5: pattern-nesting depth is bounded in lowering and validation;
/// deeper trees decline to the BCL fallback route with correct results instead of
/// overflowing the CLR stack. The BCL oracle survives the same inputs, so fallback
/// (not rejection) is the compatible mechanics.
/// </summary>
public sealed class Utf8LoweringDepthTests
{
    [Theory]
    [InlineData(2000)]
    [InlineData(5000)]
    public void DeepNestingFallsBackWithCorrectResults(int depth)
    {
        var pattern = new string('(', depth) + "abc" + new string(')', depth);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxabcxx")));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("xxabxx")));
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
    }
}
