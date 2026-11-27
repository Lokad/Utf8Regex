using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixGnuBackreferenceTests
{
    [Theory]
    [InlineData(@"(a|aa)\1", "aaaa", PosixCompileOptions.GnuExtended, 0, 4, 2)]
    [InlineData(@"\(a\|aa\)\1", "zzaaaa", PosixCompileOptions.GnuBasic, 2, 6, 4)]
    [InlineData(@"(é|éé)\1", "xéééé", PosixCompileOptions.GnuExtended, 1, 9, 5)]
    [InlineData(@"\(a\{1,2\}\)\1", "aaaa", PosixCompileOptions.GnuBasic, 0, 4, 2)]
    [InlineData(@"(a)\10", "aa0", PosixCompileOptions.GnuExtended, 0, 3, 1)]
    [InlineData(@"(a?)\1", "", PosixCompileOptions.GnuExtended, 0, 0, 0)]
    [InlineData(@"(k)\1", "KK", PosixCompileOptions.GnuExtended | PosixCompileOptions.IgnoreCase, 0, 4, 1)]
    public void PublicOperationsChooseTheLongestExtentAtTheEarliestStart(string pattern, string text,
        PosixCompileOptions options, int start, int end, int captureEnd)
    {
        using var regex = Utf8PosixRegex.Compile(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input, captures: slots);
        Assert.True(match.Success);
        Assert.Equal((start, end), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.True(slots[1].Success);
        Assert.Equal((start, captureEnd), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var cursorMatch, slots));
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(match, cursorMatch);
        Assert.Equal((start, captureEnd), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
    }

    [Fact]
    public void UnsetGroupsAndFailedBranchesDoNotLeaveACaptureForReferences()
    {
        using var unset = new Utf8PosixRegex(@"(a)?\1", PosixCompileOptions.GnuExtended);
        Assert.Equal(0, unset.Count("a"u8));
        Assert.False(unset.IsMatch("a"u8));
        using var rollback = new Utf8PosixRegex(@"^((a)x|a)\2$", PosixCompileOptions.GnuExtended);
        Assert.False(rollback.IsMatch("aa"u8));
    }

    [Fact]
    public void FiniteRepeatCanSetDifferentEmptyCapturesOnSuccessiveIterations()
    {
        using var regex = new Utf8PosixRegex(@"^((a?)|(b?)){2}\2\3$", PosixCompileOptions.GnuExtended);
        Assert.True(regex.IsMatch([]));
        Assert.True(regex.Match([]).Success);
    }

    [Fact]
    public void BackreferenceCanUseAConsumingRepeatPartitionThatGlibcSkips()
    {
        // Two one-byte iterations followed by the final reference consume aaa.
        // glibc 2.39 returns only the first two bytes for this expression.
        using var unbounded = new Utf8PosixRegex(@"(a*)+\1", PosixCompileOptions.GnuExtended);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        var first = unbounded.Match("aaa"u8, captures: slots);
        Assert.Equal((0, 3), (first.StartOffsetInBytes, first.EndOffsetInBytes));
        Assert.Equal((1, 2), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
    }

    [Fact]
    public void ReferenceReadsAnEarlierIterationWhileCapturePublicationKeepsTheWinnerHierarchy()
    {
        using var regex = new Utf8PosixRegex(@"^((a)|b)+\2$", PosixCompileOptions.GnuExtended);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[3];
        Assert.True(regex.IsMatch("aba"u8));
        Assert.Equal(1, regex.Count("aba"u8));
        Assert.Equal(3, regex.Match("aba"u8, captures: slots).EndOffsetInBytes);
        Assert.Equal((1, 2), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
        Assert.False(slots[2].Success);
    }

    [Theory]
    [InlineData(@"(a|aa)(a{1,4})\1")]
    [InlineData(@"(aa|a)(a{1,4})\1")]
    public void EqualWholeMatchesKeepHierarchicalCapturePriority(string pattern)
    {
        using var regex = new Utf8PosixRegex(pattern, PosixCompileOptions.GnuExtended);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[3];
        Assert.Equal(6, regex.Match("aaaaaa"u8, captures: slots).EndOffsetInBytes);
        Assert.Equal((0, 2), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
        Assert.Equal((2, 4), (slots[2].StartOffsetInBytes, slots[2].EndOffsetInBytes));
    }

    [Fact]
    public void BackreferenceSearchUsesTheOriginalCancellationTokenAndDeadline()
    {
        using var token = new CancellationTokenSource();
        const string pattern = @"^((a|aa)+)\1$";
        var subject = Encoding.UTF8.GetBytes(new string('a', 80) + "b");
        using var regex = new Utf8PosixRegex(pattern, PosixCompileOptions.GnuExtended,
            new Utf8RegexPolicy { MatchTimeout = TimeSpan.FromMilliseconds(20) });
        foreach (var timeout in new[] { false, true })
        {
            var polls = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            {
                if (++polls < 30) return 0;
                if (timeout) return 50;
                token.Cancel();
                return 0;
            });
            if (timeout)
                Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(subject));
            else
                Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(subject, token.Token)).CancellationToken);
        }
    }

    [Fact]
    public void DialectsAreMutuallyExclusive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PosixRegex("a",
            PosixCompileOptions.GnuBasic | PosixCompileOptions.GnuExtended));
    }

    [Fact]
    public void BooleanAndCountStillAdmitStateNeededByReferences()
    {
        const string pattern = @"^((a|aa)+)\1$";
        using var depthLimited = new Utf8PosixRegex(pattern, PosixCompileOptions.GnuExtended,
            new Utf8RegexPolicy { ResourcePolicy = new() { MaxDepth = 1 } });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => depthLimited.IsMatch("aaaaaa"u8)).ResourceKind);
        using var workingLimited = new Utf8PosixRegex(pattern, PosixCompileOptions.GnuExtended,
            new Utf8RegexPolicy { ResourcePolicy = new() { MaxWorkingBytes = 1 } });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => workingLimited.Count("aaaaaa"u8)).ResourceKind);
    }
}
