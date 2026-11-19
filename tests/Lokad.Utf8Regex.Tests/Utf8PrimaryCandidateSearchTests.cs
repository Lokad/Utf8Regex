using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PrimaryCandidateSearchTests
{
    private const string Pattern = "(?<hit>A.{1,2}B|B.{1,2}A|A.{3,4}B|B.{3,4}A)";

    [Theory]
    [InlineData(2043)]
    [InlineData(2044)]
    [InlineData(2047)]
    [InlineData(2048)]
    public void ExactLeadingLiteralCrossesSearchBlocksAndPreservesBoundaryContext(int offset)
    {
        var text = new string('a', offset - 1) + " await Xéawait Y 😀await Z";
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = Utf8Regex.Compile(@"(?<word>\bawait\b)\s+[A-Z]", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 128 }
        });
        using var source = new CancellationTokenSource();
        var captures = new Utf8CaptureRange[2];
        Assert.True(regex.IsMatch(input, source.Token));
        Assert.Equal(2, regex.Count(input, source.Token));
        Assert.Equal(new Utf8CaptureRange(offset, offset + 7), regex.Match(input, 0, captures, source.Token));
        Assert.Equal(new Utf8CaptureRange(offset, offset + 5), captures[1]);
        using var cursor = regex.CreateMatchCursor(input, offset + 1, source.Token);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(input.Length - 7, match.Value.StartOffsetInBytes);
        Assert.Equal(input.Length, match.Value.EndOffsetInBytes);
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(2, "1/2/03")]
    [InlineData(2, "𝟡1/2/03")]
    [InlineData(2, "1𝟡/2/03")]
    [InlineData(3, "1/2/03")]
    [InlineData(3, "12/2/03")]
    [InlineData(3, "123/2/03")]
    [InlineData(3, "12𝟡/2/03")]
    public void ShortRepeatDelimiterSkipsFalseStartsAndKeepsScalarCaptures(int maximum, string date)
    {
        var pattern = $$"""(?<date>\b\d{1,{{maximum}}}/\d{1,2}/\d{2,4}\b)""";
        var prefix = new string('1', 512) + "x 😀 ";
        var input = Encoding.UTF8.GetBytes(prefix + date + " " + date);
        var expected = new Utf8CaptureRange(Encoding.UTF8.GetByteCount(prefix), Encoding.UTF8.GetByteCount(prefix + date));
        using var regex = Utf8Regex.Compile(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 800 }
        });
        using var source = new CancellationTokenSource();
        var captures = new Utf8CaptureRange[2];
        Assert.True(regex.IsMatch(input, source.Token));
        Assert.Equal(2, regex.Count(input, source.Token));
        Assert.Equal(expected, regex.Match(input, 2, captures, source.Token));
        Assert.Equal(expected, captures[1]);
        using var cursor = regex.CreateMatchCursor(input, 2, source.Token);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(expected, match.Value);
        Assert.Equal(expected, Assert.Single(match.GetCaptures(1).ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SparsePrimaryCapturesUseTheNecessaryPrefixWithinFiniteWork(bool hit)
    {
        var text = "é" + new string('x', 16384) + (hit ? "A12B" : "");
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Singleline, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 256,
                MaxDepth = 4096,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        using var source = new CancellationTokenSource();
        var captures = new Utf8CaptureRange[3];
        captures[2] = new(7, 9);
        Assert.Equal(hit, regex.IsMatch(input, source.Token));
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
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Singleline, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 256
            }
        });
        var input = Encoding.ASCII.GetBytes(new string('A', 8192));
        var captures = new[] { new Utf8CaptureRange(7, 9), new Utf8CaptureRange(7, 9) };
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input, 0, captures)).ResourceKind);
        Assert.All(captures, range => Assert.False(range.Success));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                regex.IsMatch(Encoding.UTF8.GetBytes("é" + new string('A', 8192)))).ResourceKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PrefixSearchRetainsOriginalSynchronousCancellation(int operation)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Singleline);
        var input = Encoding.UTF8.GetBytes(new string('x', 1 << 20) + "A12B");
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            if (operation == 2)
            {
                using var matches = regex.CreateMatchCursor(input, cancellationToken: source.Token);
                Assert.Equal(source.Token,
                    Assert.Throws<OperationCanceledException>(() => matches.MoveNext(out _)).CancellationToken);
            }
            else if (operation == 1) Assert.Equal(source.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Match(input, 0,
                    new Utf8CaptureRange[2], source.Token)).CancellationToken);
            else Assert.Equal(source.Token,
                Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, source.Token)).CancellationToken);
            Assert.True(source.IsCancellationRequested);
        }
        finally { session.Complete(); }
    }
}
