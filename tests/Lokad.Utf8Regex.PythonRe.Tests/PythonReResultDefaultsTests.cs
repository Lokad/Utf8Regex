namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReResultDefaultsTests
{
    [Fact]
    public void DefaultAndZeroInitializedResultsExposeEmptyTextAndCollections()
    {
        Assert.Empty(default(Utf8PythonMatchData).ValueText);
        Assert.Empty(new Utf8PythonMatchData { Success = false }.ValueText);
        Assert.Empty(default(Utf8PythonGroupMatchData).ValueText);
        Assert.Empty(new Utf8PythonGroupMatchData { Success = false }.ValueText);
        AssertEmpty(default);
        AssertEmpty(new Utf8PythonDetailedMatchData { LastIndex = null });
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public void PublicDetailedMissesAgreeWithBorrowedViews(int operation, bool controlled)
    {
        var regex = new Utf8PythonRegex("(?P<x>a)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            controlled ? new() { MaxWorkSteps = 100000, MaxDepth = 64,
                MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 } : default);
        using var cancellation = new CancellationTokenSource();
        var token = controlled ? cancellation.Token : default;
        var data = operation switch
        {
            0 => regex.SearchDetailedData("😀b"u8, 4, token),
            1 => regex.MatchDetailedData("😀b"u8, 4, token),
            _ => regex.FullMatchDetailedData("😀b"u8, 4, token),
        };
        AssertEmpty(data);
        var view = operation switch
        {
            0 => regex.SearchDetailed("😀b"u8, 4, token),
            1 => regex.MatchDetailed("😀b"u8, 4, token),
            _ => regex.FullMatchDetailed("😀b"u8, 4, token),
        };
        Assert.False(view.Success);
        Assert.Equal(view.Value.GetValueString(), data.Value.ValueText);
    }

    [Fact]
    public void SuccessfulResultsPreserveSuppliedArraysOptionalCapturesAndRetainedValues()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)?(?P<y>😀)");
        var retained = regex.FullMatchDetailedData("😀"u8);
        Assert.True(retained.Success);
        Assert.Equal("😀", retained.Value.ValueText);
        Assert.Equal("y", retained.LastGroup);
        Assert.True(retained.TryGetGroup(1, out var absent));
        Assert.False(absent.Success);
        Assert.Empty(absent.ValueText);
        var supplied = new Utf8PythonDetailedMatchData
        {
            Groups = retained.Groups, NameEntries = retained.NameEntries, LastIndex = retained.LastIndex,
        };
        Assert.Same(retained.Groups, supplied.Groups);
        Assert.Same(retained.NameEntries, supplied.NameEntries);
        Assert.Equal("y", supplied.LastGroup);
        Assert.True(regex.FullMatch("a😀"u8).Success);
        Assert.Equal("😀", retained.Value.ValueText);
        Assert.Equal((0, 4), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.False(retained.TryGetGroup(-1, out var missing));
        Assert.Empty(missing.ValueText);
    }

    private static void AssertEmpty(Utf8PythonDetailedMatchData result)
    {
        Assert.False(result.Success);
        Assert.Equal(0, result.Value.ValueText.Length);
        Assert.Empty(result.Groups);
        Assert.Empty(result.NameEntries);
        Assert.Equal(0, result.CaptureSlotCount);
        Assert.Equal(0, result.NameEntryCount);
        Assert.Null(result.LastIndex);
        Assert.Null(result.LastGroup);
        Assert.False(result.TryGetGroup(0, out var group));
        Assert.Empty(group.ValueText);
        Assert.False(result.TryGetFirstSetGroup("x", out group));
        Assert.Empty(group.ValueText);
    }
}
