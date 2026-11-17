using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCaptureRangeTests
{
    [Theory]
    [InlineData(".", false)]
    [InlineData(".", true)]
    [InlineData("(.)", false)]
    [InlineData("(.)", true)]
    public void MatchAndCaptureConsumeOneCompleteScalar(string pattern, bool compiled)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = "😀"u8.ToArray();
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var match));
        Assert.True(match.Success);
        Assert.Equal((0, 4), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal(input, match.GetValueMemory().ToArray());
        foreach (var number in match.GroupNumbers)
        {
            Assert.True(match.TryGetGroup(number, out var group));
            Assert.Equal(match.Value, group);
            Assert.Equal(input, match.GetGroupMemory(number).ToArray());
            Assert.Equal([group], match.GetCaptures(number).ToArray());
        }
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(input, match.GetValueMemory().ToArray());
    }
}
