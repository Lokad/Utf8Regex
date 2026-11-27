using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WholeMatchRangeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicationObservesOriginalInterruptionWithoutCoordinateScanning(bool expire)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 0),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        // Place publication at a real diagnostic checkpoint after discovery.
        for (var i = 0; i < 63; i++)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.CheckProgress());
        invocation.CheckpointHook = _ => { if (expire) clock.Advance(250); else source.Cancel(); };
        var result = default(Utf8CaptureRange);
        Exception? failure = null;
        try { Utf8SliceBufferWork.TryCreateMatchRange(2, 6, ref invocation, out result); }
        catch (Exception error) { failure = error; }
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(default(Utf8CaptureRange), result);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void DefaultMissAndSuccessfulEmptyRangeRemainDistinct(RegexOptions options)
    {
        var miss = new Utf8Regex("a", options: options).Match("😀"u8);
        Assert.Equal(default(Utf8CaptureRange), miss);
        Assert.False(miss.Success);
        Assert.Equal((0,0), (miss.StartOffsetInBytes, miss.EndOffsetInBytes));
        var empty = new Utf8Regex("^", options: options).Match("😀"u8);
        Assert.True(empty.Success);
        Assert.Equal((0,0), (empty.StartOffsetInBytes, empty.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralMatchReturnsTheSelectedCompleteScalar(bool reverse)
    {
        var regex = new Utf8Regex("😀", options: RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var match = regex.Match("x😀y😀"u8);
        Assert.True(match.Success);
        Assert.Equal(reverse ? (6, 10) : (1, 5), (match.StartOffsetInBytes, match.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void ValueCursorCopiesRetainIndependentPositionsAndPublishedRanges(RegexOptions options)
    {
        var regex = new Utf8Regex("(.)", options: options | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        var input = "a😀b"u8.ToArray();
        var original = regex.EnumerateMatches(input);
        Assert.True(original.MoveNext());
        var retained = original.Current;
        var alias = original;
        var expected = (options & RegexOptions.RightToLeft) != 0
            ? new[] { (1, 5), (0, 1) } : new[] { (1, 5), (5, 6) };
        foreach (var range in expected)
        {
            Assert.True(alias.MoveNext());
            Assert.Equal(range, (alias.Current.StartOffsetInBytes, alias.Current.EndOffsetInBytes));
        }
        Assert.False(alias.MoveNext());
        Assert.Equal(retained, original.Current);
        foreach (var range in expected)
        {
            Assert.True(original.MoveNext());
            Assert.Equal(range, (original.Current.StartOffsetInBytes, original.Current.EndOffsetInBytes));
        }
        Assert.False(original.MoveNext());
        Assert.Equal((options & RegexOptions.RightToLeft) != 0 ? (5, 6) : (0, 1),
            (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
    }
}
