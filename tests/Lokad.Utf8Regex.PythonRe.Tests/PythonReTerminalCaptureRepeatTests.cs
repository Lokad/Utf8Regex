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
        var values = regex.FindAllToStrings("ab"u8).ScalarValues;
        Assert.Equal(new[] { "", "ab", "" }, values);
        var matches = regex.FindIterDetailed("ab"u8);
        Assert.Equal(3, matches.Length);
        Assert.Null(matches[0].LastIndex);
        Assert.Equal(1, matches[1].LastIndex);
        Assert.Equal("ab", matches[1].Groups[1].ValueText);
        Assert.Null(matches[2].LastIndex);
        Assert.False(matches[0].Groups[1].Success);
        Assert.False(matches[2].Groups[1].Success);
    }

    [Fact]
    public void DerivedFullMatchKeepsItsEndRequirementAndAlternatives()
    {
        var match = Create().FullMatchDetailedData("ab"u8);
        Assert.True(match.Success);
        Assert.Equal("ab", match.Groups[1].ValueText);
        Assert.Equal(1, match.LastIndex);
        var empty = Create().FullMatchDetailedData([]);
        Assert.True(empty.Success);
        Assert.Null(empty.LastIndex);
        Assert.False(empty.Groups[1].Success);
    }
}
