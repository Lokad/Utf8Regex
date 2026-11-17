namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReResultDefaultsTests
{

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void PublicDetailedMissesAgreeWithBorrowedViews(int operation, bool controlled)
    {
        var regex = new Utf8PythonRegex("(?P<x>a)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            controlled ? new() { MaxWorkSteps = 100000, MaxDepth = 64, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 } : default);
        using var cancellation = new CancellationTokenSource();
        var token = controlled ? cancellation.Token : default;
        var data = operation switch
        {
            0 => regex.SearchDetailed("😀b"u8, 4, token),
            1 => regex.MatchDetailed("😀b"u8, 4, token),
            _ => regex.FullMatchDetailed("😀b"u8, 4, token),
        };
        AssertEmpty(data);

    }

    [Fact]
    public void BorrowedContextsKeepOptionalCapturesAcrossLaterCalls()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)?(?P<y>😀)");
        var retained = regex.FullMatchDetailed("😀"u8);
        Assert.True(retained.Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.Equal("y", retained.LastGroup);
        Assert.True(retained.TryGetGroup(1, out var absent));
        Assert.False(absent.Success);
        Assert.Empty(System.Text.Encoding.UTF8.GetString(absent.Value.GetValueBytes()));
        Assert.True(regex.FullMatch("a😀"u8).Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.Equal((0, 4), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.False(retained.TryGetGroup(-1, out var missing));
        Assert.Empty(System.Text.Encoding.UTF8.GetString(missing.Value.GetValueBytes()));
    }

    private static void AssertEmpty(Utf8PythonMatchContext result)
    {
        Assert.False(result.Success);
        Assert.Equal(0, System.Text.Encoding.UTF8.GetString(result.Value.GetValueBytes()).Length);
        Assert.Equal(0,result.CaptureSlotCount);
        Assert.Empty(result.GetNameEntries());
        Assert.Equal(0, result.CaptureSlotCount);
        Assert.Equal(0, result.NameEntryCount);
        Assert.Null(result.LastIndex);
        Assert.Null(result.LastGroup);
        Assert.False(result.TryGetGroup(0, out var group));
        Assert.Empty(System.Text.Encoding.UTF8.GetString(group.Value.GetValueBytes()));
        Assert.False(result.TryGetFirstSetGroup("x", out group));
        Assert.Empty(System.Text.Encoding.UTF8.GetString(group.Value.GetValueBytes()));
    }
}
