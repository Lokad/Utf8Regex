using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReResultArrayLifetimeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(2048)]
    public void RetainedCaptureSnapshotsSurviveLaterCallsAndProgramClose(int count)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("item-12 ", count)));
        using var regex = new Utf8PythonRegex("([a-z]+)-([0-9]+)");
        var retained = new List<Utf8PythonRangeMatch>();
        using (var cursor = regex.CreateMatchCursor(input))
            while (cursor.MoveNext(out var match))
                retained.Add(match);
        Assert.Equal(count, retained.Count);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            using var fresh = regex.CreateMatchCursor("other-987"u8.ToArray());
            Assert.True(fresh.MoveNext(out var match));
            Assert.Equal("other", Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
            Assert.Equal("987", Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));
        }
        regex.Dispose();
        for (var index = 0; index < retained.Count; index++)
        {
            Assert.Equal((index * 8, index * 8 + 7), (retained[index].Value.StartOffsetInBytes, retained[index].Value.EndOffsetInBytes));
            Assert.Equal("item", Encoding.UTF8.GetString(retained[index].GetGroupMemory(1).Span));
            Assert.Equal("12", Encoding.UTF8.GetString(retained[index].GetGroupMemory(2).Span));
        }
    }
}
