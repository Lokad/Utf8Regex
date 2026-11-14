using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSingleCallbackProjectionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void OneCallbackProjectsLookaheadWithoutMappingUnusedSuffix(int kind)
    {
        var subject = "πneedleé😀 " + string.Concat(Enumerable.Repeat("😀", 65536));
        var input = Encoding.UTF8.GetBytes(subject);
        using var regex = new Utf8PythonRegex("(?P<word>needle)(?=(?P<tail>é😀))", resourcePolicy: new()
        { MaxWorkingBytes = 1UL << 20, MaxResultBytes = 4UL << 20, MaxWorkSteps = 1_000_000 });
        var callbacks = new List<Utf8PythonDetailedMatchData>();
        var actual = Run(regex, input, kind, 1, callbacks, default);
        Assert.Equal(subject.Replace("needle", "X", StringComparison.Ordinal), actual);
        var match = Assert.Single(callbacks);
        Assert.Equal(2, match.LastIndex); Assert.Equal("tail", match.LastGroup);
        Assert.Equal((8, 14, 7, 10), (match.Groups[2].StartOffsetInBytes, match.Groups[2].EndOffsetInBytes,
            match.Groups[2].StartOffsetInUtf16, match.Groups[2].EndOffsetInUtf16));
        Array.Fill(input, (byte)'!');
        _ = regex.Count("no match"u8);
        Assert.Equal("needle", match.Value.ValueText);
        Assert.Equal("é😀", match.Groups[2].ValueText);
    }

    [Theory]
    [InlineData(0, 0, 2)] [InlineData(1, 0, 2)] [InlineData(2, 0, 2)]
    [InlineData(0, 1, 1)] [InlineData(1, 1, 1)] [InlineData(2, 1, 1)]
    [InlineData(0, 2, 2)] [InlineData(1, 2, 2)] [InlineData(2, 2, 2)]
    public void CountControlsKeepProgressionAndIndependentCallbackSnapshots(int kind, int count, int expected)
    {
        using var regex = new Utf8PythonRegex("(?P<word>a)(?=(?P<tail>.))");
        var callbacks = new List<Utf8PythonDetailedMatchData>();
        var actual = Run(regex, "πaé a😀"u8.ToArray(), kind, count, callbacks, default);
        Assert.Equal(expected == 1 ? "πXé a😀" : "πXé X😀", actual);
        Assert.Equal(expected, callbacks.Count);
        Assert.Equal("é", callbacks[0].Groups[2].ValueText);
        if (expected == 2)
        {
            Assert.Equal("😀", callbacks[1].Groups[2].ValueText);
            Assert.Equal((5, 7), (callbacks[1].Groups[2].StartOffsetInUtf16, callbacks[1].Groups[2].EndOffsetInUtf16));
        }
        callbacks.Clear();
        Assert.Equal("πz😀", Run(regex, "πz😀"u8.ToArray(), kind, count, callbacks, default));
        Assert.Empty(callbacks);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public void FinalCallbackRetainsOriginalCancellationAndDeadline(int kind, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var regex = new Utf8PythonRegex("(?P<word>a)(?=(?P<tail>.))", matchTimeout: TimeSpan.FromMilliseconds(20));
        using var cancellation = new CancellationTokenSource();
        var snapshots = new List<Utf8PythonDetailedMatchData>();
        void Interrupt() { if (expire) ticks = 50; else cancellation.Cancel(); }
        if (expire)
            Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, "a😀"u8.ToArray(), kind, 1, snapshots, cancellation.Token, Interrupt));
        else
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                Run(regex, "a😀"u8.ToArray(), kind, 1, snapshots, cancellation.Token, Interrupt)).CancellationToken);
        Assert.Equal("😀", Assert.Single(snapshots).Groups[2].ValueText);
    }

    [Fact]
    public void OneRawCallbackStillRejectsMalformedAssembledOutput()
    {
        using var regex = new Utf8PythonRegex("(a)");
        Assert.Throws<ArgumentException>(() => regex.Subn("a😀"u8, 0,
            (Utf8PythonUtf8MatchEvaluator<int>)((_, _) => [0xff]), 1, 0));
    }

    private static string Run(Utf8PythonRegex regex, byte[] input, int kind, int count,
        List<Utf8PythonDetailedMatchData> snapshots, CancellationToken token, Action? interrupt = null)
    {
        string Text(int _, Utf8PythonDetailedMatchData match) { snapshots.Add(match); interrupt?.Invoke(); return "X"; }
        byte[] Bytes(int _, Utf8PythonDetailedMatchData match) { snapshots.Add(match); interrupt?.Invoke(); return "X"u8.ToArray(); }
        return kind switch
        {
            0 => regex.SubnToString(input, 0, (Utf8PythonMatchEvaluator<int>)Text, count, 0, token).ResultText,
            1 => Encoding.UTF8.GetString(regex.Subn(input, 0, (Utf8PythonMatchEvaluator<int>)Text, count, 0, token).ResultBytes),
            _ => Encoding.UTF8.GetString(regex.Subn(input, 0, (Utf8PythonUtf8MatchEvaluator<int>)Bytes, count, 0, token).ResultBytes),
        };
    }
}
