using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAnchoredStartTests
{
    [Theory]
    [InlineData("(needle)([0-9]+)", "xneedle12")]
    [InlineData("(?=bad)(bad)([0-9]+)", "item-1234 bad42")]
    [InlineData("(?:[a-z]+|[0-9]+)end", "😀123end")]
    [InlineData("(a)?(b)", "cb")]
    [InlineData("(?=(a?))(b)", "cb")]
    [InlineData("(?<=é)(a+)", "éaa")]
    public void AnchoredRejectionDoesNotSkipToALaterValidStart(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        {
            using var regex = new Utf8PythonRegex(pattern);
            Assert.False(regex.Match(input).Success);
            Assert.False(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.False(regex.Match(input).Success);
            Utf8PythonRangeMatch snapshot1 = default;
            using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Match))
            {
                captureCursor1.MoveNext(out snapshot1);
            }
            Assert.False(snapshot1.Success);
            Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.Search).Success);
        }
    }
    [Theory]
    [InlineData("(a)?(b)", "b", 0, "b")]
    [InlineData("(?=bad)(bad)([0-9]+)", "bad123", 0, "bad123")]
    [InlineData("(?i:(k))([0-9]+)", "K12", 0, "K12")]
    [InlineData("(a*)", "", 0, "")]
    [InlineData("a*(b)", "b", 0, "b")]
    [InlineData("(?=(a?))(b)", "b", 0, "b")]
    [InlineData("(?<=é)(a+)", "éaa", 2, "aa")]
    [InlineData("([a-z]+)-([0-9]+)", "é😀 item-123", 7, "item-123")]
    public void NullableFoldedAndOffsetStartsKeepValuesAndCoordinates(string pattern, string text, int start, string expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        {
            using var regex = new Utf8PythonRegex(pattern);
            var match = regex.Match(input, start);
            Assert.True(match.Success);
            Assert.Equal(start, match.StartOffsetInBytes);
            Assert.Equal(start + Encoding.UTF8.GetByteCount(expected), match.EndOffsetInBytes);
            Utf8PythonRangeMatch snapshot2 = default;
            using (var captureCursor2 = regex.CreateMatchCursor(input, start, mode: Utf8PythonMatchMode.Match))
            {
                captureCursor2.MoveNext(out snapshot2);
            }
            Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(snapshot2.GetValueMemory().Span));
            PythonReMatchAssert.Equal(expected, regex.Match(input, start, mode: Utf8PythonMatchMode.FullMatch));
        }
    }
    [Fact]
    public void ProvenMismatchNeedsNoSubjectTraversalOrWorkingScratchUnderTrustedInput()
    {
        var input = Encoding.ASCII.GetBytes(new string('x', 1 << 20) + "needle");
        using var regex = new Utf8PythonRegex("(needle)", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.False(regex.Match(input).Success);
        Assert.False(regex.Match(input).Success);
        using var strict = new Utf8PythonRegex("(needle)", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxDepth = 4096
            }
        });
        Assert.False(strict.Match(input).Success);
    }
    [Fact]
    public void KnownLookaheadMismatchRejectsBeforeItsWholeSubjectScreen()
    {
        var input = Encoding.ASCII.GetBytes("item-1234 " + new string('a', 1 << 20));
        using var regex = new Utf8PythonRegex("(?=bad)(bad)([0-9]+)", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxDepth = 64,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        Assert.False(regex.Match(input).Success);
        Assert.False(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PossibleAmbiguousStartsKeepNecessaryLiteralScreening(bool full)
    {
        var input = Encoding.ASCII.GetBytes(new string('a', 8192));
        using var regex = new Utf8PythonRegex("(a+)+missing", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxDepth = 64,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        Assert.False(full ? regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success : regex.Match(input).Success);
    }
    [Fact]

    public void RejectionStillObservesOriginalCancellationAndDeadline()
    {
        using var regex = new Utf8PythonRegex("(needle)");
        using var token = new CancellationTokenSource();
        token.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.Match("x"u8, 0, cancellationToken: token.Token));
        Assert.Equal(token.Token, failure.CancellationToken);
        Assert.False(regex.Match("x"u8).Success);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks += 100);
        using var deadline = new Utf8PythonRegex("(needle)", policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.Throws<RegexMatchTimeoutException>(() => deadline.Match("x"u8));
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    public void AnchoredRejectionDoesNotHideInvalidOffsets(int start)
    {
        using var regex = new Utf8PythonRegex("(needle)");
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("é"u8, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("é"u8, start, mode: Utf8PythonMatchMode.FullMatch));
    }
}
