using System.Reflection;
using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReRangeBufferTests
{
    [Theory]
    [InlineData(17)] [InlineData(2053)]
    public void FastFindAllGrowthKeepsByteAndDerivedCoordinatesAndDetachedValues(int repetitions)
    {
        var regex = new Utf8PythonRegex("foo|bar");
        var fast = typeof(Utf8PythonRegex).GetField("_canUseUtf8IterationFastPath", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(fast);
        Assert.True(Assert.IsType<bool>(fast.GetValue(regex)));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀fooébarx", repetitions)));
        var matches = regex.FindAll(input);
        var strings = regex.FindAllToStrings(input).ScalarValues;
        var bytes = regex.FindAllToUtf8(input).ScalarValues;
        Assert.Equal(repetitions * 2, matches.Length);
        Assert.Equal(matches.Length, strings.Length);
        Assert.Equal(matches.Length, bytes.Length);
        for (var i = 0; i < matches.Length; i++)
        {
            var block = i / 2; var second = (i & 1) != 0;
            var startByte = 13 * block + (second ? 9 : 4);
            var startUnit = 10 * block + (second ? 6 : 2);
            Assert.Equal((startByte, startByte + 3), (matches[i].StartOffsetInBytes, matches[i].EndOffsetInBytes));
            Assert.Equal((startUnit, startUnit + 3), (matches[i].StartOffsetInUtf16, matches[i].EndOffsetInUtf16));
            Assert.Equal(second ? "bar" : "foo", matches[i].ValueText);
            Assert.Equal(matches[i].ValueText, strings[i]);
            Assert.Equal(Encoding.UTF8.GetBytes(strings[i]), bytes[i]);
        }
        Array.Fill(input, (byte)'x');
        Assert.Equal("foo", matches[0].ValueText);
        Assert.Equal("bar", strings[^1]);
        Assert.Equal("foo"u8.ToArray(), bytes[0]);
    }
}
