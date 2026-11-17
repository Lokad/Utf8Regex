namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTerminalCaptureRepeatTests
{
    private static Utf8PythonRegex Create() => new("(?:|([a-z]+))", PythonReCompileOptions.None,
        Timeout.InfiniteTimeSpan, new() { MaxWorkSteps = 100000, MaxDepth = 32,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });

    [Fact]
    public void EmptyRetryKeepsNonemptyCaptureAndCompletionMetadata()
    {
        var regex = Create();
        var values = PythonReFindAllAssert.Decode(regex.FindAllToUtf8("ab"u8)).ScalarValues;
        Assert.Equal(new[] { "", "ab", "" }, values);
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(("ab"u8).ToArray(), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var matches = collectedRanges1.ToArray();
        Assert.Equal(3, matches.Length);
        Assert.Null(matches[0].LastIndex);
        Assert.Equal(1, matches[1].LastIndex);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(matches[1].GetGroupMemory(1).Span));
        Assert.Null(matches[2].LastIndex);
        Assert.False(matches[0].Groups[1].Success);
        Assert.False(matches[2].Groups[1].Success);
    }

    [Fact]
    public void DerivedFullMatchKeepsItsEndRequirementAndAlternatives()
    {
        var match = Create().FullMatchDetailed("ab"u8);
        Assert.True(match.Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(match.GetGroup(1).Value.GetValueBytes()));
        Assert.Equal(1, match.LastIndex);
        var empty = Create().FullMatchDetailed([]);
        Assert.True(empty.Success);
        Assert.Null(empty.LastIndex);
        Assert.False(empty.GetGroup(1).Success);
    }
}
