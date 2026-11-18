using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureSlotTests
{
    [Theory]
    [InlineData(Utf8PythonMatchMode.Search, "π😀éx", 0, 8, 2, 8)]
    [InlineData(Utf8PythonMatchMode.Match, "π😀éx", 2, 8, 2, 8)]
    [InlineData(Utf8PythonMatchMode.FullMatch, "π😀éx", 2, 8, 2, 8)]
    [InlineData(Utf8PythonMatchMode.Search, "π😀x", 0, 6, 2, 6)]
    [InlineData(Utf8PythonMatchMode.Match, "π😀x", 2, 6, 2, 6)]
    [InlineData(Utf8PythonMatchMode.FullMatch, "π😀x", 2, 6, 2, 6)]
    public void SlotsPreserveByteCoordinatesAcrossModesAndBounds(Utf8PythonMatchMode mode,
        string text, int start, int end, int expectedStart, int expectedEnd)
    {
        using var regex = new Utf8PythonRegex("(?P<outer>(?P<inner>😀))(?P<maybe>é)?", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 128,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        Span<Utf8PythonCaptureRange> captures = stackalloc Utf8PythonCaptureRange[6];
        captures.Fill(new() { Number = 99, Success = true });
        var match = regex.Match(bytes, start, captures, mode: mode, endOffsetInBytes: end);
        Assert.True(match.Success);
        Assert.Equal((expectedStart, expectedEnd), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal((expectedStart, expectedEnd), (captures[0].StartOffsetInBytes, captures[0].EndOffsetInBytes));
        for (var number = 1; number <= 2; number++)
        {
            Assert.Equal(number, captures[number].Number); Assert.True(captures[number].Success);
            Assert.Equal((2, 6), (captures[number].StartOffsetInBytes, captures[number].EndOffsetInBytes));
        }
        Assert.Equal(3, captures[3].Number);
        Assert.Equal(end == 8, captures[3].Success);
        Assert.Equal(end == 8 ? (6, 8) : (0, 0), (captures[3].StartOffsetInBytes, captures[3].EndOffsetInBytes));
        Assert.Equal(99, captures[4].Number); Assert.Equal(99, captures[5].Number);
        var expected = bytes.AsSpan(expectedStart, expectedEnd - expectedStart).ToArray();
        captures.Clear();
        Assert.True(match.GetValueBytes().SequenceEqual(expected));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void ShortBuffersDoNotChangeBacktrackingOrLastCapture(int size)
    {
        using var regex = new Utf8PythonRegex("(?P<x>a|ab)+(?P<last>b)?");
        Span<Utf8PythonCaptureRange> captures = stackalloc Utf8PythonCaptureRange[size];
        captures.Fill(new() { Number = 99, Success = true });
        var match = regex.Match("abab"u8, captures: captures, mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(match.Success); Assert.Equal((0, 4), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        if (size > 0) Assert.Equal((0, 4), (captures[0].StartOffsetInBytes, captures[0].EndOffsetInBytes));
        if (size > 1) Assert.Equal((2, 3), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        if (size > 2) Assert.Equal((3, 4), (captures[2].StartOffsetInBytes, captures[2].EndOffsetInBytes));
        for (var i = 3; i < size; i++) Assert.Equal(99, captures[i].Number);
    }

    [Theory]
    [InlineData(Utf8PythonMatchMode.Match, 0, 2, false)]
    [InlineData(Utf8PythonMatchMode.Match, 2, 2, true)]
    [InlineData(Utf8PythonMatchMode.FullMatch, 2, 2, true)]
    [InlineData(Utf8PythonMatchMode.FullMatch, 2, 6, false)]
    public void EmptyAndMissingCapturesKeepParticipation(Utf8PythonMatchMode mode, int start, int end, bool success)
    {
        using var regex = new Utf8PythonRegex("(?P<e>)(?<=π)(?P<x>a)?");
        var captures = new Utf8PythonCaptureRange[4];
        Array.Fill(captures, new() { Number = 99, Success = true });
        var match = regex.Match("π😀"u8, start, captures, mode: mode, endOffsetInBytes: end);
        Assert.Equal(success, match.Success);
        if (success)
        {
            Assert.True(captures[1].Success); Assert.Equal((2, 2), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
            Assert.False(captures[2].Success); Assert.Equal(2, captures[2].Number);
        }
        else Assert.All(captures[..3], range => Assert.False(range.Success));
        Assert.Equal(99, captures[3].Number);
    }

    [Fact]
    public void DefaultIsAnchoredAndEndRetainsAbsoluteLookbehind()
    {
        using var regex = new Utf8PythonRegex("(?<=π)(?P<x>😀)(?=é)");
        var captures = new Utf8PythonCaptureRange[2];
        var input = "π😀éx"u8.ToArray();
        Assert.False(regex.Match(input, captures: captures).Success);
        Assert.True(regex.Match(input, 2, captures, endOffsetInBytes: 8).Success);
        Assert.Equal((2, 6), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.False(regex.Match(input, 2, captures, endOffsetInBytes: 6).Success);
        Assert.All(captures, range => Assert.False(range.Success));
    }

    [Fact]
    public void PreCancellationLeavesCallerStorageAndAllowanceUntouched()
    {
        using var regex = new Utf8PythonRegex("(?P<x>a)");
        using var source = new CancellationTokenSource(); source.Cancel();
        var captures = new Utf8PythonCaptureRange[3]; Array.Fill(captures, new() { Number = 99 });
        var account = new Account();
        var error = Assert.Throws<OperationCanceledException>(() => regex.Match("a"u8, -1, captures,
            source.Token, account, (Utf8PythonMatchMode)99, -1));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.All(captures, range => Assert.Equal(99, range.Number)); Assert.Equal(0, account.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdmissionRefusalOrCancellationClearsOnlyRequestedSlotsAndRefunds(bool cancel)
    {
        using var regex = new Utf8PythonRegex("(?P<x>a)");
        using var source = new CancellationTokenSource();
        var captures = new Utf8PythonCaptureRange[3]; Array.Fill(captures, new() { Number = 99, Success = true });
        var account = new Account { Refuse = !cancel, AfterGrant = cancel ? source.Cancel : null };
        Exception? observed = null;
        try { regex.Match("a"u8, captures: captures, cancellationToken: source.Token, workingMemoryAllowance: account); }
        catch (Exception error) { observed = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        else { Assert.IsType<Utf8MemoryAdmissionException>(observed); Assert.Equal(1, account.Attempts); }
        Assert.Equal(0UL, account.Used);
        Assert.All(captures[..2], range => Assert.False(range.Success)); Assert.Equal(99, captures[2].Number);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void InvalidEndRejectsAndClearsRequestedPrefix(int end)
    {
        using var regex = new Utf8PythonRegex("(?P<x>.)");
        var captures = new Utf8PythonCaptureRange[3]; Array.Fill(captures, new() { Number = 99, Success = true });
        Assert.Equal("endOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() =>
            regex.Match("😀"u8, captures: captures, endOffsetInBytes: end)).ParamName);
        Assert.All(captures[..2], range => Assert.False(range.Success)); Assert.Equal(99, captures[2].Number);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        internal int Attempts;
        internal bool Refuse;
        internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            Attempts++;
            if (Refuse) return false;
            Used += bytes; AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}
