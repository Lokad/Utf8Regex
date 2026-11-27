using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReHostCompositionCursorTests
{
    [Fact]
    public void OneRangeCapturesLookaheadWithoutPreparingUnusedSuffix()
    {
        var input = Encoding.UTF8.GetBytes("πneedleé😀 " + string.Concat(Enumerable.Repeat("😀", 65536)));
        using var regex = new Utf8PythonRegex("(?P<word>needle)(?=(?P<tail>é😀))", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 4096
            }
        });
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var retained));
        Assert.Equal(2, retained.LastIndex);
        Assert.Equal("needle", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.True(retained.TryGetGroup(2, out var tail));
        Assert.Equal((8, 14), (tail.StartOffsetInBytes, tail.EndOffsetInBytes));
        cursor.Dispose(); regex.Dispose();
        Assert.Equal("é😀", Encoding.UTF8.GetString(retained.GetGroupMemory(2).Span));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void HostLimitedRangeIterationKeepsIndependentLookaroundCaptures(int limit, int expected)
    {
        using var regex = new Utf8PythonRegex("(?P<word>a)(?=(?P<tail>.))");
        using var cursor = regex.CreateMatchCursor("πaé a😀"u8.ToArray());
        var retained = new List<Utf8PythonRangeMatch>();
        while ((limit == 0 || retained.Count < limit) && cursor.MoveNext(out var match)) retained.Add(match);
        cursor.Dispose();
        Assert.Equal(expected, retained.Count);
        Assert.Equal("é", Encoding.UTF8.GetString(retained[0].GetGroupMemory(2).Span));
        if (expected == 2) Assert.Equal("😀", Encoding.UTF8.GetString(retained[1].GetGroupMemory(2).Span));
    }
}
