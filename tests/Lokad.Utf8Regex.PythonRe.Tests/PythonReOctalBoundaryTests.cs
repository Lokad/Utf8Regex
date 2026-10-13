using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReOctalBoundaryTests
{
    [Theory]
    [InlineData("")] [InlineData("😀")]
    public void EveryThreeDigitOctalPreservesScalarValuesAndRangeErrors(string prefix)
    {
        for (var value = 0; value < 512; value++)
        {
            var digits = Convert.ToString(value, 8).PadLeft(3, '0');
            foreach (var inClass in new[] { false, true })
            {
                var atom = inClass ? @"[\" + digits + "]" : @"\" + digits;
                var pattern = "^" + prefix + atom + "z$";
                if (value > 255)
                {
                    var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
                    Assert.Equal(@"octal escape value \" + digits + " outside of range 0-0o377", error.Message);
                    Assert.Equal(1 + prefix.Length + (inClass ? 1 : 0), error.Position);
                    continue;
                }
                var input = Encoding.UTF8.GetBytes(prefix + (char)value + "z");
                var regex = new Utf8PythonRegex(pattern);
                var match = regex.FullMatch(input);
                Assert.True(match.Success, pattern);
                Assert.Equal(0, match.StartOffsetInBytes);
                Assert.Equal(input.Length, match.EndOffsetInBytes);
                Assert.False(regex.FullMatch(input.AsSpan(0, input.Length - 1)).Success);
            }
        }
    }

    [Theory]
    [InlineData(@"\0z", "\0z")]
    [InlineData(@"\07z", "\az")]
    [InlineData(@"\0777", "?7")]
    [InlineData(@"[\7]z", "\az")]
    [InlineData(@"[\77]z", "?z")]
    [InlineData(@"[\377]z", "ÿz")]
    [InlineData(@"\1410", "a0")]
    [InlineData(@"(a)(b)\2\1", "abba")]
    public void OneDigitContinuationAndNumericReferencesKeepTheirBoundaries(string pattern, string subject)
    {
        var regex = new Utf8PythonRegex("^😀" + pattern + "$", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 10000, MaxDepth = 64, MaxWorkingBytes = 65536, MaxResultBytes = 65536 });
        Assert.True(regex.FullMatch(Encoding.UTF8.GetBytes("😀" + subject)).Success);
    }
}
