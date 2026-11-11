using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparedAsciiIntervalTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 63)]
    [InlineData(0, 64)]
    [InlineData(63, 64)]
    [InlineData(64, 127)]
    [InlineData(127, 127)]
    [InlineData(0, 127)]
    public void LoweredIntervalsKeepByteEdgesAndCaptureValues(int first, int last)
    {
        var pattern = $"([\\x{first:X2}-\\x{last:X2}]+)";
        var wanted = new string([(char)first, (char)last]);
        var input = Encoding.UTF8.GetBytes("é😀" + wanted + "Ω");
        foreach (var policy in Enum.GetValues<Utf8InputValidation>())
        foreach (var options in new[] { RegexOptions.None, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options, inputValidation: policy);
            var match = regex.MatchDetailed(input);
            Assert.True(match.Success);
            Assert.Equal((6, 2), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(wanted, match.GetGroup(1).GetValueString());
            Assert.Equal(1, regex.Count(input));
            if (policy == Utf8InputValidation.Validate)
                Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff]));
        }
    }

    [Theory]
    [InlineData("([a-ce-g]+)", "abcdex", "abc")]
    [InlineData("([^a-c]+)", "😀éabc", "😀é")]
    [InlineData("(?i:([a-z]+))", "KAb1", "KAb")]
    [InlineData("([a-z]+?)z", "abcz", "abc")]
    [InlineData("([a-z]+)b", "ab", "a")]
    public void OpaqueMasksAndRewindsKeepTheirActualPredicates(string pattern, string text, string expected)
    {
        foreach (var policy in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8Regex(pattern, inputValidation: policy);
            var match = regex.MatchDetailed(Encoding.UTF8.GetBytes(text));
            Assert.True(match.Success);
            Assert.Equal(expected, match.GetGroup(1).GetValueString());
        }
    }
}
