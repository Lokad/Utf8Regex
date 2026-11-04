using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DetailedAsciiPrefixProjectionTests
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
    public void EveryCaptureRetainsAbsoluteByteAndUtf16Coordinates(string pattern, string text)
    {
        var oracle = new Regex(pattern);
        var regex = new Utf8Regex(pattern);
        var bytes = Encoding.UTF8.GetBytes(text);
        byte[] storage = [0xff, .. bytes, 0xff];
        var actual = regex.MatchDetailed(storage.AsSpan(1, bytes.Length));
        var expected = oracle.Match(text);
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Value, actual.GetValueString());
        if (!expected.Success) return;
        foreach (var number in oracle.GetGroupNumbers())
        {
            var group = actual.GetGroup(number);
            var wanted = expected.Groups[number];
            Assert.Equal(wanted.Success, group.Success);
            Assert.Equal(wanted.Value, group.GetValueString());
            Assert.Equal(wanted.Captures.Count, group.CaptureCount);
            for (var index = 0; index < group.CaptureCount; index++)
            {
                var capture = group.GetCapture(index);
                var source = wanted.Captures[index];
                Assert.Equal(source.Value, capture.GetValueString());
                Assert.Equal(source.Index, capture.IndexInUtf16);
                Assert.Equal(source.Length, capture.LengthInUtf16);
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, source.Index)), capture.IndexInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(source.Value), capture.LengthInBytes);
            }
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
        const string text = "aaab aac 😀";
        var oracle = new Regex(pattern, option);
        var regex = new Utf8Regex(pattern, option);
        var actual = regex.MatchDetailed(Encoding.UTF8.GetBytes(text));
        var expected = oracle.Match(text);
        Assert.Equal(expected.Value, actual.GetValueString());
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Groups["run"].Value, actual.GetGroup("run").GetValueString());
    }

    [Fact]
    public void SmallResultBudgetAdmitsEarlyCapturesButNotFarUnicodeCaptureHistory()
    {
        var policy = new Utf8ResourcePolicy { MaxResultBytes = 4096, MaxWorkingBytes = 1 << 20, MaxWorkSteps = 1_000_000 };
        var regex = new Utf8Regex(@"(?<run>a+)(?:b|c)", RegexOptions.None, Timeout.InfiniteTimeSpan, policy);
        var input = Encoding.UTF8.GetBytes("aaab" + new string('x', 8192) + "😀");
        using var token = new CancellationTokenSource();
        var result = regex.MatchDetailed(input, token.Token);
        Assert.Equal("aaa", result.GetGroup("run").GetValueString());
        var far = new Utf8Regex(@"a(?=(?<far>.*))(?<far>)", RegexOptions.None, Timeout.InfiniteTimeSpan, policy);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => { _ = far.MatchDetailed(input); });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => { _ = regex.MatchDetailed(malformed); });
        var working = new Utf8Regex(regex.Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => { _ = working.MatchDetailed(input); }).ResourceKind);
    }

    [Fact]
    public void BorrowedContextAndCaptureAliasesSurviveLaterCalls()
    {
        var regex = new Utf8Regex(@"(?<run>a+)(?:b|c)");
        var input = Encoding.UTF8.GetBytes("aaab other😀");
        var first = regex.MatchDetailed(input);
        var alias = first;
        var group = first.GetGroup("run");
        var capture = group.GetCapture(0);
        _ = regex.MatchDetailed("éaac"u8);
        Assert.Equal("aaab", alias.GetValueString());
        Assert.Equal("aaa", group.GetValueString());
        Assert.Equal("aaa", capture.GetValueString());
        Assert.Equal(0, capture.IndexInBytes);
        Assert.Equal(3, capture.LengthInBytes);
    }

    [Fact]
    public void ExplicitUtf16OffsetsRetainTheirAdmittedMapAndWholeContext()
    {
        var regex = new Utf8Regex(@"(?<run>a+)(?:b|c)");
        var text = "é aaab 😀 aac";
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(regex.Pattern).Match(text, 6);
        var actual = regex.MatchDetailedFromUtf16Offset(input, 6);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        Assert.Equal(expected.Groups["run"].Value, actual.GetGroup("run").GetValueString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionAfterMatchingObservesOriginalTokenAndDeadline(bool timeout)
    {
        var regex = new Utf8Regex(@"(?<run>a+)(?:b|c)", RegexOptions.None, TimeSpan.FromMilliseconds(20));
        var input = Encoding.UTF8.GetBytes(new string('a', 300000) + "b😀");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var sawMatching = false;
        var enteredProjection = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            var frames = new StackTrace().GetFrames();
            if (frames.Any(frame => frame.GetMethod()?.DeclaringType?.Name == nameof(Utf8SliceRunner)))
            { sawMatching = true; return; }
            if (!sawMatching || enteredProjection) return;
            enteredProjection = true;
            if (timeout) ticks = 25; else token.Cancel();
        });
        void Run() { _ = regex.MatchDetailed(input, token.Token); }
        try
        {
            if (timeout) Assert.Throws<RegexMatchTimeoutException>(Run);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(enteredProjection);
        }
        finally { session.Complete(); }
    }
}
