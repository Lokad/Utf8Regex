using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8TrimmedPrefixValueMatchTests
{
    private const string Pattern = @"^\s*(\[\|\s*)?///\s?(?<doc>.*)$";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    private static Utf8CaptureRange Scalar(Utf8Regex regex, byte[] input)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
        invocation.AnchorOriginByte = program.RightToLeft ? input.Length : 0;
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, invocation.AnchorOriginByte,
                slots.Starts, slots.Ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
            return outcome.Found ? new Utf8CaptureRange(outcome.StartByte, (outcome.StartByte) + (outcome.EndByte - outcome.StartByte)) : default(Utf8CaptureRange);
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Theory]
    [InlineData("///")] [InlineData("/// text")] [InlineData(" \t[| /// text\r\n")]
    [InlineData("///\n")] [InlineData("///\ntext")]
    [InlineData("[|\n/// text\n")] [InlineData("///\n\n")]
    [InlineData("/// text\n\n")] [InlineData("[|wrong")]
    [InlineData("")] [InlineData(" // text")]
    public void AsciiRangesPreserveFundedScalarResults(string text)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded with
                {
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            var input = Encoding.ASCII.GetBytes(text);
            byte[] storage = [0xff, .. input, 0xff];
            var expected = Scalar(regex, input);
            using var token = new CancellationTokenSource();
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(expected, regex.Match(storage.AsSpan(1, input.Length), cancellationToken: token.Token));
                Assert.Equal(expected, regex.Match(input));
            }
            finally { session.Complete(); }
        }
    }

    [Fact]
    public void WiderSubjectsAliasesAndExcludedModesKeepScalarRanges()
    {
        foreach (var pattern in new[] { Pattern, Pattern.Replace("^", @"\A").Replace("$", @"\Z"),
            Pattern.Replace("$", @"\z"), Pattern.Replace("^", "(?m:^)") })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            using var token = new CancellationTokenSource();
            foreach (var text in new[] { "/// text", "///\n", "/// text\n", "///\r\n", "///\n\n",
                "\u2003[|\u00a0/// 😀", "/// é😀", "///\n😀", "x\n/// text", "[|///" + new string('x', 2049) })
            {
                var input = Encoding.UTF8.GetBytes(text);
                var expected = Scalar(regex, input);
                Assert.Equal(expected, regex.Match(input));
                Assert.Equal(expected, regex.Match(input, cancellationToken: token.Token));
            }
        }
    }

    [Fact]
    public void DetailedCapturesOffsetsAndRetainedValuesRemainValid()
    {
        var regex = new Utf8Regex(Pattern);
        var input = "[| /// text\n"u8.ToArray();
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        var value = regex.Match(input);
        Assert.Equal("text", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(regex.GroupNumberFromUtf8Name("doc"u8)).Span));
        Utf8MatchSequence.EqualValue(value, regex.Match(input, 0, []));
        Assert.False(regex.Match(input, Encoding.UTF8.GetByteCount(Encoding.UTF8.GetString(input).AsSpan(0, 1)), []).Success);
        Assert.Equal(1, regex.Count(input));
        Assert.Equal("text", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(regex.GroupNumberFromUtf8Name("doc"u8)).Span));
        byte[] malformed = [.. input, 0xff];
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, cancellationToken: token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData("/// text", true)]
    [InlineData("// text", false)]
    public void WorkDenialNeverPublishesAnUnsuccessfulMatch(string text, bool success)
    {
        var input = Encoding.ASCII.GetBytes(text);
        var succeeded = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded with
                {
                    MaxWorkSteps = work,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            try { Assert.Equal(success, regex.Match(input).Success); succeeded = true; break; }
            catch (Utf8ResourceLimitExceededException error) { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
        }
        Assert.True(succeeded);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalRangePublicationObservesOriginalInterruption(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == trigger) { if (expire) ticks = 100; else token.Cancel(); }
            return ticks;
        });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50),
            ResourcePolicy = Funded
        });
        Assert.True(regex.Match("/// text"u8, cancellationToken: token.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("/// text"u8, cancellationToken: token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("/// text"u8, cancellationToken: token.Token)).CancellationToken);
    }
}
