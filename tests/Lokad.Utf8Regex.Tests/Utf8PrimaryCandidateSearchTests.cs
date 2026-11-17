using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PrimaryCandidateSearchTests
{
    private const string Pattern = "(?<hit>A.{1,2}B|B.{1,2}A|A.{3,4}B|B.{3,4}A)";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SparsePrimaryCapturesUseTheNecessaryPrefixWithinFiniteWork(bool hit)
    {
        var text = "é" + new string('x', 16384) + (hit ? "A12B" : "");
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(Pattern, RegexOptions.Singleline, resourcePolicy: new()
            { MaxWorkSteps = 256, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        using var source = new CancellationTokenSource();
        var captures = new Utf8CaptureRange[3];
        captures[2] = new(7, 9);
        var whole = regex.Match(input, 2, captures, source.Token);
        Assert.Equal(hit, whole.Success);
        Assert.Equal(new Utf8CaptureRange(7, 9), captures[2]);
        if (hit)
        {
            Assert.Equal(new Utf8CaptureRange(16386, 16390), whole);
            Assert.Equal(whole, captures[1]);
        }
        else Assert.All(captures[..2], range => Assert.False(range.Success));
        using var cursor = regex.CreateMatchCursor(input, 2, source.Token);
        Assert.Equal(hit, cursor.MoveNext(out var retained));
        if (hit)
        {
            Assert.Equal(whole, retained.Value);
            Assert.Equal(whole, retained.GetCaptures(1)[0]);
            Assert.False(cursor.MoveNext(out _));
            Assert.Equal(whole, retained.Value);
        }
    }

    [Fact]
    public void DenseFalseCandidatesStillConsumeWorkAndClearWrittenSlots()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.Singleline, resourcePolicy: new() { MaxWorkSteps = 256 });
        var input = Encoding.ASCII.GetBytes(new string('A', 8192));
        var captures = new[] { new Utf8CaptureRange(7, 9), new Utf8CaptureRange(7, 9) };
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input, 0, captures)).ResourceKind);
        Assert.All(captures, range => Assert.False(range.Success));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrefixSearchRetainsOriginalSynchronousCancellation(bool cursor)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex(Pattern, RegexOptions.Singleline);
        var input = Encoding.UTF8.GetBytes(new string('x', 1 << 20) + "A12B");
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            if (cursor)
            {
                using var matches = regex.CreateMatchCursor(input, cancellationToken: source.Token);
                Assert.Equal(source.Token,
                    Assert.Throws<OperationCanceledException>(() => matches.MoveNext(out _)).CancellationToken);
            }
            else Assert.Equal(source.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Match(input, 0,
                    new Utf8CaptureRange[2], source.Token)).CancellationToken);
            Assert.True(source.IsCancellationRequested);
        }
        finally { session.Complete(); }
    }
}
