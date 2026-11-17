using System.Reflection;
using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReRangeBufferTests
{
    [Theory]
    [InlineData(17)] [InlineData(2053)]
    public void ByteCollectionGrowthAndCursorMetadataPreserveRanges(int repetitions)
    {
        var regex = new Utf8PythonRegex("foo|bar");
        var fast = typeof(Utf8PythonRegex).GetField("_canUseUtf8IterationFastPath", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(fast);
        Assert.True(Assert.IsType<bool>(fast.GetValue(regex)));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀fooébarx", repetitions)));
        var wholeMatches12 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor12 = regex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor12.MoveNext(out var rangeMatch12))
            {
                wholeMatches12.Add(rangeMatch12);
            }
        }
        var matches = wholeMatches12.ToArray();
        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)).ScalarValues;
        var bytes = regex.FindAllToUtf8(input).ScalarValues;
        Assert.Equal(repetitions * 2, matches.Length);
        Assert.Equal(matches.Length, strings.Length);
        Assert.Equal(matches.Length, bytes.Length);
        for (var i = 0; i < matches.Length; i++)
        {
            var block = i / 2; var second = (i & 1) != 0;
            var startByte = 13 * block + (second ? 9 : 4);
            Assert.Equal((startByte, startByte + 3), (matches[i].Value.StartOffsetInBytes, matches[i].Value.EndOffsetInBytes));
            Assert.Equal(second ? "bar" : "foo", System.Text.Encoding.UTF8.GetString(matches[i].GetValueMemory().Span));
            Assert.Equal(System.Text.Encoding.UTF8.GetString(matches[i].GetValueMemory().Span), strings[i]);
            Assert.Equal(Encoding.UTF8.GetBytes(strings[i]), bytes[i]);
        }
        Array.Fill(input, (byte)'x');

        Assert.Equal("bar", strings[^1]);
        Assert.Equal("foo"u8.ToArray(), bytes[0]);
    }
}
