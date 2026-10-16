using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SubroutineCaptureReturnTests
{
    // Native PCRE2 10.47 witnesses: captures are caller state after a return,
    // even when a different value was set in a deeper recursion level.
    [Theory]
    [InlineData(@"(?(DEFINE)(?<x>a(?&x)?b))(?<w>(?&x))\k<w>", "aabbaabb", 1, null, -1, -1)]
    [InlineData(@"(?(DEFINE)(?<x>a(?&x)?b))(?<w>(?&x))\k<w>", "aabbaabb", 2, "aabb", 0, 4)]
    [InlineData(@"(?<x>a)(?&x)\k<x>", "aaa", 1, "a", 0, 1)]
    [InlineData(@"(?<x>a(?&x)?b)\k<x>", "aabbaabb", 1, "aabb", 0, 4)]
    [InlineData(@"(a)(?1)\1", "aaa", 1, "a", 0, 1)]
    [InlineData(@"(?+1)(a)", "aa", 1, "a", 1, 2)]
    [InlineData(@"(?:(?<x>a)|b(?R)c)", "bbacc", 1, null, -1, -1)]
    [InlineData(@"(?(DEFINE)(?<x>a|ab))(?&x)c", "abc", 1, null, -1, -1)]
    [InlineData(@"(?(DEFINE)(?<x>a(*ACCEPT)b))(?<w>(?&x))c", "ac", 1, null, -1, -1)]
    [InlineData(@"(?(DEFINE)(?<x>a(*ACCEPT)b))(?<w>(?&x))c", "ac", 2, "a", 0, 1)]
    [InlineData(@"(?(DEFINE)(?<x>😀(?&x)?é))(?<w>(?&x))\k<w>", "😀😀éé😀😀éé", 1, null, -1, -1)]
    [InlineData(@"(?(DEFINE)(?<x>😀(?&x)?é))(?<w>(?&x))\k<w>", "😀😀éé😀😀éé", 2, "😀😀éé", 0, 12)]
    [InlineData(@"(?<x>😀)(?&x)\k<x>", "😀😀😀", 1, "😀", 0, 4)]
    public void ReturnedCallsRestoreUnsetOrPriorCallerCaptures(
        string pattern, string text, int slot, string? value, int start, int end)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var cancellation = new CancellationTokenSource();
        foreach (var limits in new[] { default(Utf8Pcre2ExecutionLimits), new Utf8Pcre2ExecutionLimits
        {
            MatchLimit = 1000000, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20
        } })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                limits, Timeout.InfiniteTimeSpan);
            Assert.True(regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
            Assert.Equal(1, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
            var match = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
            Assert.Equal(text, match.GetValueString());
            var group = match.GetGroup(slot);
            Assert.Equal(value is not null, group.Success);
            if (value is not null)
            {
                Assert.Equal(value, group.GetValueString());
                Assert.Equal((start, end), (group.StartOffsetInBytes, group.EndOffsetInBytes));
            }
        }
    }

    [Theory]
    [InlineData(@"(?(DEFINE)(?<x>ab))(?&x)\k<x>", "abab", false)]
    [InlineData(@"(?(DEFINE)(a))(?1)\1", "aa", false)]
    [InlineData(@"(?(DEFINE)(a))(?-1)\1", "aa", false)]
    [InlineData(@"(?(DEFINE)(?<x>ab))(?&x)(?(x)c|d)", "abc", false)]
    [InlineData(@"(?(DEFINE)(?<x>ab))(?&x)(?(x)c|d)", "abd", true)]
    [InlineData(@"(?(DEFINE)(?<x>))(?&x)(?(x)a|b)", "a", false)]
    [InlineData(@"(?(DEFINE)(?<x>))(?&x)(?(x)a|b)", "b", true)]
    [InlineData(@"(?(DEFINE)(?<x>a(*MARK:tag)))(?&x)(?(x)c|d)", "ad", true)]
    [InlineData(@"(?(DEFINE)(?<x>a(*PRUNE)))(?&x)(?(x)c|d)", "ad", true)]
    [InlineData(@"(?(DEFINE)(?<x>a))(?=(?&x))a(?(x)c|d)", "ac", false)]
    [InlineData(@"(?(DEFINE)(?<x>a))(?=(?&x))a(?(x)c|d)", "ad", true)]
    public void ReferencesAndConditionsObserveCallerCaptureState(string pattern, string text, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected ? 1 : 0, regex.Count(input));
        Assert.Equal(expected, regex.MatchDetailed(input).Success);
    }

    [Fact]
    public void EmptyCallsAdvanceByScalarWithoutPublishingTheirCaptures()
    {
        var regex = new Utf8Pcre2Regex(@"(?(DEFINE)(?<x>))(?&x)");
        var cursor = regex.EnumerateMatches("😀"u8);
        Assert.True(cursor.MoveNext());
        Assert.Equal(0, cursor.Current.StartOffsetInBytes);
        Assert.True(cursor.MoveNext());
        Assert.Equal(4, cursor.Current.StartOffsetInBytes);
        Assert.False(cursor.MoveNext());
        Assert.False(regex.MatchDetailed("😀"u8).GetGroup(1).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReturnedCapturesSurviveAliasInterruption(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?(DEFINE)(?<x>😀(?&x)?é))(?<w>(?&x))\k<w>",
            Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var input = "😀é😀é 😀é😀é"u8.ToArray();
        var retained = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
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
        Assert.False(retained.GetGroup(1).Success);
        Assert.Equal("😀é", retained.GetGroup(2).GetValueString());
        Assert.True(regex.MatchDetailed("😀é😀é"u8).Success);
    }
}
