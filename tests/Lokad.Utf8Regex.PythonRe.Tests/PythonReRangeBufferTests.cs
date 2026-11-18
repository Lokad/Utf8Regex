using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReRangeBufferTests
{
    [Theory]
    [InlineData(17)]
    [InlineData(2053)]
    public void HostRetainedCursorRangesSurviveLaterCalls(int repetitions)
    {
        using var regex = new Utf8PythonRegex("foo|bar");
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀fooébarx", repetitions)));
        var matches = new List<Utf8PythonRangeMatch>();
        using (var cursor = regex.CreateMatchCursor(input))
            while (cursor.MoveNext(out var match))
                matches.Add(match);
        Assert.Equal(repetitions * 2, matches.Count);
        Assert.True(regex.Match("foo"u8).Success);
        for (var index = 0; index < matches.Count; index++)
        {
            var second = (index & 1) != 0;
            var start = 13 * (index / 2) + (second ? 9 : 4);
            Assert.Equal((start, start + 3), (matches[index].Value.StartOffsetInBytes, matches[index].Value.EndOffsetInBytes));
            Assert.Equal(second ? "bar" : "foo", Encoding.UTF8.GetString(matches[index].GetValueMemory().Span));
        }
    }
}
