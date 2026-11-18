using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureSnapshotBoundsTests
{
    [Theory]
    [InlineData(@"(?<run>a+)(?:b|c)", "zaaab rest😀")]
    [InlineData(@"(?<run>a+)(?:b|c)", "ézaaab rest😀")]
    [InlineData(@"(?<run>a+)(?:b|c)", "zaaab érest")]
    [InlineData(@"(?<run>a+)(?:b|c)", "é miss😀")]
    [InlineData(@"(?<each>a)+b", "zaaab rest😀")]
    [InlineData(@"a(?=(?<far>.*))(?<far>)", "a😀z")]
    [InlineData(@"(?<=(?<past>.*))a", "😀a")]
    [InlineData(@"(?<empty>(?=a))", "a😀")]
    [InlineData(@"(?<run>a*)(?:b|c)", "b😀")]
    [InlineData(@"a*", "😀")]
    public void EveryCaptureRetainsOriginalByteCoordinates(string pattern, string text)
    {
        var oracle = new Regex(pattern);
        var regex = new Utf8Regex(pattern);
        var bytes = Encoding.UTF8.GetBytes(text);
        byte[] storage = [0xff, .. bytes, 0xff];
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(storage.AsMemory(1, bytes.Length)));
        var expected = oracle.Match(text);
        Utf8MatchSequence.AssertCaptures(expected, actual, bytes, text);
    }

    [Theory]
    [InlineData(@"(?<x>é)", "é")]
    [InlineData(@"(?=(?<x>é😀))é", "é😀")]
    [InlineData(@"(?<x>é)+", "éé")]
    [InlineData(@"(?<=(?<past>中🙂))é", "é")]
    public void TrustedEarlyCapturesSkipUnrelatedSuffixPreparation(string pattern, string target)
    {
        var text = "x中🙂" + target + new string('!', 1 << 20);
        var input = Encoding.UTF8.GetBytes(text);
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 128, MaxDepth = 128, MaxWorkingBytes = 64 << 10, MaxResultBytes = 8192 };
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = policy
            });
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Utf8MatchSequence.AssertCaptures(oracle.Match(text), actual, input, text);
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void ExistingModesKeepTheirSelectedMatchAndGroups(RegexOptions option)
    {
        const string pattern = @"(?<run>a+)(?:b|c)";
        const string text = "é aaab aac 😀";
        var oracle = new Regex(pattern, option);
        var expected = oracle.Match(text);
        {
            var regex = new Utf8Regex(pattern, options: option);
            var input = Encoding.UTF8.GetBytes(text);
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
        }
    }

    [Fact]
    public void ResultBudgetAdmitsBorrowedRangesButBoundsCaptureHistoryStorage()
    {
        var policy = new Utf8ResourcePolicy { MaxResultBytes = 4096, MaxWorkingBytes = 1 << 20, MaxWorkSteps = 1_000_000 };
        var regex = new Utf8Regex(@"(?<run>a+)(?:b|c)", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var input = Encoding.UTF8.GetBytes("éaaab" + new string('x', 8192) + "😀");
        using var token = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token));
            Assert.Equal("aaa"u8.ToArray(), result.GetGroupMemory(regex.GroupNumberFromName("run")).ToArray());
        }
        finally { session.Complete(); }
        var far = new Utf8Regex(@"a(?=(?<far>.*))(?<far>)", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var farResult = Utf8MatchSequence.First(far.CreateMatchCursor(input));
        Assert.True(farResult.Success);
        Assert.Equal(2, farResult.GetCaptures(far.GroupNumberFromName("far")).Length);
        // Distant endpoints borrow the subject; per-scalar history needs owned ranges.
        var history = new Utf8Regex("(?<each>.)+", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(history.CreateMatchCursor(input)));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
        var working = new Utf8Regex(regex.Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(working.CreateMatchCursor(input))).ResourceKind);
    }

    [Fact]
    public void RetainedRangesAndBorrowedMemorySurviveLaterCalls()
    {
        var input = Encoding.UTF8.GetBytes("éaaab other😀");
        {
            var regex = new Utf8Regex(@"(?<run>a+)(?:b|c)");
            var first = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            var alias = first;
            var number = regex.GroupNumberFromName("run");
            var groupMemory = first.GetGroupMemory(number);
            var capture = first.GetCaptures(number)[0];
            _ = Utf8MatchSequence.First(regex.CreateMatchCursor("éaac"u8.ToArray()));
            Assert.Equal("aaab"u8.ToArray(), alias.GetValueMemory().ToArray());
            Assert.Equal("aaa"u8.ToArray(), groupMemory.ToArray());
            Assert.Equal("aaa"u8.ToArray(), input.AsSpan(capture.StartOffsetInBytes,
                capture.EndOffsetInBytes - capture.StartOffsetInBytes).ToArray());
            Assert.Equal((2, 5), (capture.StartOffsetInBytes, capture.EndOffsetInBytes));
        }
    }

    [Fact]
    public void ByteCaptureCursorRetainsWholeContext()
    {
        var regex = new Utf8Regex(@"(?<run>a+)(?:b|c)");
        var text = "é aaab 😀 aac";
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(regex.Pattern).Match(text, 6);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, 6))));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
    }

}
