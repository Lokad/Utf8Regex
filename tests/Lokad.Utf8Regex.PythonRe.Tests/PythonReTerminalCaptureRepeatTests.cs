namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTerminalCaptureRepeatTests
{
    private static Utf8PythonRegex Create() => new("(?:|([a-z]+))", PythonReCompileOptions.None,
        Timeout.InfiniteTimeSpan, new()
        {
            MaxWorkSteps = 100000,
            MaxDepth = 32,
            MaxWorkingBytes = 1 << 20,
            MaxResultBytes = 1 << 20
        });

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
        Utf8PythonRangeMatch match = default;
        using (var captureCursor1 = Create().CreateMatchCursor(("ab"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out match);
        }
        Assert.True(match.Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString((match.Groups[1].Success ? match.GetGroupMemory(match.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(1, match.LastIndex);
        Utf8PythonRangeMatch empty = default;
        using (var captureCursor2 = Create().CreateMatchCursor(ReadOnlyMemory<byte>.Empty, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor2.MoveNext(out empty);
        }
        Assert.True(empty.Success);
        Assert.Null(empty.LastIndex);
        Assert.False(empty.Groups[1].Success);
    }
}
