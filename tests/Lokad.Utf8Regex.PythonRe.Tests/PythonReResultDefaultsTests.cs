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
    public void CursorModesPublishDefaultOnMiss(int operation, bool controlled)
    {
        var regex = new Utf8PythonRegex("(?P<x>a)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            controlled ? new() { MaxWorkSteps = 100000, MaxDepth = 64, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 } : default);
        using var cancellation = new CancellationTokenSource();
        var token = controlled ? cancellation.Token : default;
        Utf8PythonRangeMatch data = default;
        using (var captureCursor1 = operation switch
        {
            0 => regex.CreateMatchCursor(("😀b"u8).ToArray(), 4, token, mode: Utf8PythonMatchMode.Search),
            1 => regex.CreateMatchCursor(("😀b"u8).ToArray(), 4, token, mode: Utf8PythonMatchMode.Match),
            _ => regex.CreateMatchCursor(("😀b"u8).ToArray(), 4, token, mode: Utf8PythonMatchMode.FullMatch),
        })
        {
            captureCursor1.MoveNext(out data);
        }
        AssertEmpty(data);

    }

    [Fact]
    public void RetainedSnapshotsKeepOptionalCapturesAcrossLaterCalls()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)?(?P<y>😀)");
        Utf8PythonRangeMatch retained = default;
        using (var captureCursor2 = regex.CreateMatchCursor(("😀"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor2.MoveNext(out retained);
        }
        Assert.True(retained.Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.Equal("y", retained.LastGroup);
        Assert.True(retained.TryGetGroup(1, out var absent));
        Assert.False(absent.Success);
        Assert.Empty(System.Text.Encoding.UTF8.GetString((absent.Success ? retained.GetGroupMemory(absent.Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.True(regex.FullMatch("a😀"u8).Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.Equal((0, 4), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.False(retained.TryGetGroup(-1, out var missing));
        Assert.Empty(System.Text.Encoding.UTF8.GetString((missing.Success ? retained.GetGroupMemory(missing.Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    private static void AssertEmpty(Utf8PythonRangeMatch result)
    {
        Assert.False(result.Success);
        Assert.Equal(0, System.Text.Encoding.UTF8.GetString(result.GetValueMemory().Span).Length);
        Assert.Equal(0, result.Groups.Length);
        Assert.Empty(result.NameEntries.ToArray());
        Assert.Equal(0, result.NameEntries.Length);
        Assert.Null(result.LastIndex);
        Assert.Null(result.LastGroup);
        Assert.False(result.TryGetGroup(0, out var group));
        Assert.Empty(System.Text.Encoding.UTF8.GetString((group.Success ? result.GetGroupMemory(group.Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.False(result.TryGetFirstSetGroup("x", out group));
        Assert.Empty(System.Text.Encoding.UTF8.GetString((group.Success ? result.GetGroupMemory(group.Number).Span : ReadOnlySpan<byte>.Empty)));
    }
}
