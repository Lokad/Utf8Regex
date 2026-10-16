using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CaptureRollbackTests
{
    [Theory]
    [InlineData(@"(?<x>a|ab)c\k<x>", "abcab", 1, "ab")]
    [InlineData(@"(?<x>a)(?:b|bc)\k<x>", "abca", 1, "a")]
    [InlineData(@"(?:x|(?<x>ab))c\k<x>", "abcab", 1, "ab")]
    [InlineData(@"(a)(?<b>b)?\1", "aa", 2, null)]
    [InlineData(@"(a)(?<b>b)?\1", "aba", 2, "b")]
    [InlineData(@"(?=(?<x>a|ab)c)\k<x>c", "abc", 1, "ab")]
    [InlineData(@"(?!(?<x>a)b)c", "c", 1, null)]
    [InlineData(@"(?(DEFINE)(?<x>a(?&x)?b))(?<w>(?&x))\k<w>", "aabbaabb", 2, "aabb")]
    [InlineData(@"(?>a(?<x>b)|a)c\k<x>", "abcb", 1, "b")]
    [InlineData(@"(?J)(?:(?<x>a)(*PRUNE)b|(?<x>c))\k<x>", "cc", 2, "c")]
    [InlineData(@"(?<x>😀)(?:é|éé)\k<x>", "😀éé😀", 1, "😀")]
    public void CapturesSurviveExistingAndLaterRollbackTargets(
        string pattern, string text, int slot, string? expectedCapture)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        var match = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.Equal(text, match.GetValueString());
        Assert.Equal((0, input.Length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        var group = match.GetGroup(slot);
        Assert.Equal(expectedCapture is not null, group.Success);
        if (expectedCapture is not null) Assert.Equal(expectedCapture, group.GetValueString());
        Assert.Equal(1, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
    }

    [Fact]
    public void LinearReferencesFitWithoutAnUnusedUndoArrayAndStillEnforceLimits()
    {
        const string pattern = @"(?-)(?<n>a)(b)\1";
        var small = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 256,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(1, small.Count("aba"u8));

        var insufficient = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => insufficient.Count("aba"u8)).ErrorKind);
        var work = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => work.Count("aba"u8)).ErrorKind);
        var result = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { ResultLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit,
            Assert.Throws<Pcre2MatchException>(() => result.MatchDetailed("aba"u8)).ErrorKind);
        var depth = new Utf8Pcre2Regex(@"(?:(?<x>a|ab)c|d)\k<x>",
            Pcre2CompileOptions.None, default, new() { DepthLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => depth.Count("abcab"u8)).ErrorKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedScalarCapturesAndAliasesKeepTheOriginalInterruption(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?<x>😀)(?:é|éé)\k<x>",
            Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var input = "😀éé😀 😀éé😀"u8.ToArray();
        var retained = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.Equal("😀", retained.GetGroup(1).GetValueString());
        var cursor = regex.EnumerateMatches(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(cursor.MoveNext());
        var alias = cursor;
        if (cancel) cancellation.Cancel(); else ticks = 25;
        Exception? failure = null;
        try { _ = alias.MoveNext(); } catch (Exception error) { failure = error; }
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal("😀", retained.GetGroup(1).GetValueString());
        Assert.True(regex.MatchDetailed("😀éé😀"u8).Success);
    }
}
