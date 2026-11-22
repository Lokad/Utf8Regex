using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8UnobservedValueCaptureTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in new[]
        {
            "abcdef([0-9]+)", "((ab)+)-", "(?i:abcdef)([a-z]+)", "é(😀+)X",
            "(?<value>[a-z]+)-([0-9]+)", @"abcdef(?<value>[0-9]+)\k<value>",
            "abcdef(?<value>[0-9]+)(?(value)X|Y)", "abcdef(?=(?<value>[0-9]+))[0-9]+",
            "abcdef(?<open>a)+(?<-open>b)+(?(open)(?!))", "abcdef(?<same>[0-9]+)(?<same>[a-z]+)",
            "abcdef(?<12>[0-9]+)", "(?:abcdef([0-9]+)|ghijkl([a-z]+))"
        })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
            yield return [pattern, options | RegexOptions.CultureInvariant];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ValueBooleanCountAndDetailedOperationsPreserveMatchingDependencies(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "abcdef123", "xabcdef123X", "abcdef123123", "abcdef123abc", "abcdefaaabbb",
             "abab- ab-", "é😀😀X", "abc-12 éghijklxyz", "abcdefABC", "ghijklabc"], options);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ValuesFitSmallWorkingBudgetWhileCursorCapturesRequireAdmission(bool rtl)
    {
        var regex = new Utf8Regex("abcdef([0-9]+)", options: rtl ? RegexOptions.RightToLeft : RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                // Elided capture storage must not determine the value allowance.
                MaxWorkingBytes = rtl ? 1280UL : 128UL,
                MaxWorkSteps = 100000
            }
        });
        var input = "xxabcdef123"u8.ToArray();
        using var token = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(1, regex.Count(input, token.Token));
        var value = regex.Match(input, cancellationToken: token.Token);
        Assert.True(value.Success);
        Assert.Equal(2, value.StartOffsetInBytes);
        Assert.Equal(9, (value.EndOffsetInBytes - value.StartOffsetInBytes));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext(out _)).ResourceKind);
    }

    [Theory]
    [InlineData(@"abcdef(?<v>[0-9]+)\k<v>", "abcdef123123")]
    [InlineData("abcdef(?<v>[0-9]+)(?(v)X|Y)", "abcdef123X")]
    [InlineData(@"abcdef(?<v>[0-9]+)(?=\k<v>)", "abcdef123123")]
    [InlineData("abcdef(?<v>a)+(?<-v>b)+(?(v)(?!))", "abcdefaaabbb")]
    public void CaptureReadersRetainTheirCaptureWorkingStorage(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 256,
                MaxWorkSteps = 100000
            }
        });
        Assert.True(new Utf8Regex(pattern).IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input)).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input)).ResourceKind);
    }

    [Fact]
    public void SparseSlotUsesWorkingStorageWhenTheCallerRequestsCaptures()
    {
        const string pattern = "abcdef(?<12>[0-9]+)";
        var input = "abcdef123"u8.ToArray();
        var admitted = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 256,
                MaxWorkSteps = 100000
            }
        });
        Assert.True(admitted.IsMatch(input));
        Assert.Equal(9, (admitted.Match(input).EndOffsetInBytes - admitted.Match(input).StartOffsetInBytes));
        var captured = Utf8MatchSequence.First(admitted.CreateMatchCursor(input));
        Assert.Single(captured.GetCaptures(12).ToArray());
        Assert.True(captured.GetGroupMemory(12).Span.SequenceEqual("123"u8));
        var denied = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1,
                MaxWorkSteps = 100000
            }
        });
        Assert.True(denied.IsMatch(input));
        Assert.Equal(1, denied.Count(input));
        Assert.Equal(new Utf8CaptureRange(0, 9), denied.Match(input));
        var captures = new Utf8CaptureRange[2];
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Match(input, captures: captures)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryPublicWorkCutoffIsStructuredAndNeverChangesSuccessfulRange(bool boolean)
    {
        var input = Encoding.UTF8.GetBytes("é😀 abcdef123");
        var baseline = new Utf8Regex("abcdef([0-9]+)").Match(input);
        var admitted = false;
        for (ulong limit = 1; limit <= 256; limit++)
        {
            var regex = new Utf8Regex("abcdef([0-9]+)", options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = limit,
                    MaxWorkingBytes = 256
                }
            });
            try
            {
                if (boolean) Assert.True(regex.IsMatch(input));
                else Assert.Equal(baseline, regex.Match(input));
                admitted = true;
                break;
            }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
        }
        Assert.True(admitted);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CaptureFreeSearchObservesOriginalCancellationAndDeadline(bool boolean, bool expire)
    {
        var regex = new Utf8Regex("abcdef([0-9]+)", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxWorkingBytes = 256
            }
        });
        var input = Encoding.ASCII.GetBytes(new string('x', 262144) + "abcdef123");
        using var token = new CancellationTokenSource(); long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (expire) ticks = 50; else token.Cancel(); });
        try
        {
            var error = Record.Exception(() => { if (boolean) regex.IsMatch(input, token.Token); else regex.Match(input, cancellationToken: token.Token); });
            if (expire) Assert.IsType<RegexMatchTimeoutException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void NonBacktrackingStillRetainsItsCaptureSemantics()
        => Utf8SliceSparseGroupTests.AssertOwnedOperations("abcdef([0-9]+)", RegexOptions.NonBacktracking,
            ["xxabcdef123", "abcdef12 abcdef345"], RegexOptions.NonBacktracking);
}
