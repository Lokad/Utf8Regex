using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LeadingDigitsBooleanTests
{
    private const string Pattern = @"^([0-9]+)(\-| |$)(.*)$";
    private const string Alias = @"\A(?<head>\p{Nd}+)(?<separator>[é\x2d\x20]|\Z)(?<tail>[^\n]*)\Z";

    private static bool Scalar(Utf8Regex regex, byte[] input)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var match = Utf8SliceRunner.TryFindFirst(program, input, program.RightToLeft ? input.Length : 0,
                slots.Starts, slots.Ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach);
            return match.Found;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("\n", false)]
    [InlineData("123", true)]
    [InlineData("123\n", true)]
    [InlineData("100- hello", true)]
    [InlineData("100 ", true)]
    [InlineData("1-😀é\u2028\r\0", true)]
    [InlineData("1-😀\n", true)]
    [InlineData("1-\n\n", false)]
    [InlineData("1-hello\nrest", false)]
    [InlineData("x123- hi", false)]
    [InlineData("123x", false)]
    [InlineData("12\r\n", false)]
    [InlineData("100\tfoo", false)]
    public void AsciiPrefixAndScalarTailPreserveFundedResults(string text, bool expected)
    {
        var buffer = Encoding.UTF8.GetBytes("pad" + text + "tail");
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1000000,
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            var input = buffer.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(expected, regex.IsMatch(input));
                Assert.Equal(expected, regex.IsMatch(input, token.Token));
            }
            finally { session.Complete(); }
        }
    }

    [Fact]
    public void WiderPrefixesAliasesAndExcludedModesAgreeWithIndependentScalarDiscovery()
    {
        string[] texts = ["", "1-😀", "1é😀", "١-😀", "1١-😀", "𝟙𝟚 é", "123", "12x", "123\n", "1-é\n\n", "x\n1-hey"];
        foreach (var pattern in new[] { Pattern, Alias, @"^[0-9]+(1|$).*$", @"^[0-9]+(\n|$).*$" })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking, RegexOptions.RightToLeft, RegexOptions.Multiline, RegexOptions.Singleline })
        {
            var regex = new Utf8Regex(pattern, options: mode);
            foreach (var text in texts)
            {
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Equal(Scalar(regex, input), regex.IsMatch(input));
                using var token = new CancellationTokenSource();
                Assert.Equal(Scalar(regex, input), regex.IsMatch(input, token.Token));
            }
        }
        Assert.True(new Utf8Regex(Alias).IsMatch("١-😀"u8));
        Assert.True(new Utf8Regex(Alias).IsMatch("1é😀"u8));
        Assert.False(new Utf8Regex(Pattern).IsMatch("١-😀"u8));
    }

    [Fact]
    public void CapturesOffsetsAndRetainedResultsKeepTheirExistingExecutors()
    {
        var regex = new Utf8Regex(Alias);
        var input = "123-😀é\n"u8.ToArray();
        var first = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(first.Success);
        Assert.Equal("123", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromUtf8Name("head"u8)).Span));
        Assert.Equal("-", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromUtf8Name("separator"u8)).Span));
        Assert.Equal("😀é", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromUtf8Name("tail"u8)).Span));
        Assert.Equal(1, regex.Count(input));
        Assert.True(regex.Match(input, 0, []).Success);
        Assert.False(regex.Match(input, 1, []).Success);
        Assert.False(regex.Match(input, 2, []).Success);
        Assert.True(regex.IsMatch("7-ok"u8));
        Assert.Equal("😀é", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromUtf8Name("tail"u8)).Span));
    }

    [Theory]
    [InlineData("1-okay")] [InlineData("not a header")]
    public void MalformedTailsAreStillRejectedBeforeConclusiveBooleans(string prefix)
    {
        var input = Encoding.UTF8.GetBytes(prefix).Append((byte)0xff).ToArray();
        var regex = new Utf8Regex(Pattern);
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundedSearchObservesOriginalInterruptionDuringLongScans(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        foreach (var leading in new[] { false, true })
        {
            var input = Encoding.UTF8.GetBytes(leading ? new string('1', 320000) + "-tail" : "1-" + new string('x', 320000));
            var entered = false;
            var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
            var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = timeout,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 10000000,
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            using var currentToken = new CancellationTokenSource();
            var session = Utf8SearchDiagnosticsSession.Start(_ =>
            {
                if (entered) return;
                entered = true;
                if (expire) ticks += 50; else currentToken.Cancel();
            });
            try
            {
                if (expire)
                {
                    var failure = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, currentToken.Token));
                    Assert.Equal(Pattern, failure.Pattern); Assert.Equal(timeout, failure.MatchTimeout);
                }
                else Assert.Equal(currentToken.Token,
                    Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, currentToken.Token)).CancellationToken);
                Assert.True(entered);
            }
            finally { session.Complete(); }
            Assert.True(regex.IsMatch("1-ok"u8));
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4097)]
    public void SeparatorsAndInteriorNewlinesAtWindowBoundariesPreserveTheWholeMatch(int boundary)
    {
        var regex = new Utf8Regex(Pattern);
        foreach (var text in new[]
        {
            new string('1', boundary) + "-😀\n",
            "1-" + new string('x', boundary) + "\n",
            "1-" + new string('x', boundary) + "\nrest",
            new string('1', boundary) + "x",
        })
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(Scalar(regex, input), regex.IsMatch(input));
        }
    }

    [Fact]
    public void PublicationObservesCancellationFromItsLastDeadlinePoll()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) token.Cancel();
            return 0;
        });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 8192,
                MaxWorkingBytes = 8 << 20,
                MaxResultBytes = 1
            }
        });
        Assert.True(regex.IsMatch("123-😀é"u8, token.Token));
        Assert.True(reads > 1);
        cancelAt = reads; reads = 0;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch("123-😀é"u8, token.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
