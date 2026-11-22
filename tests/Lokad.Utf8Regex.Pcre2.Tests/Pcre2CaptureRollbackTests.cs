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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        ReadOnlyMemory<byte> match1Input = input;
        using var match1Cursor = regex.CreateMatchCursor(match1Input, 0, Pcre2MatchOptions.None, cancellation.Token);
        match1Cursor.MoveNext(out var match1Result);
        match1Cursor.Dispose();
        var match = match1Result;
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString((match.Value.Success ? match1Input.Span.Slice(match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal((0, input.Length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        var group = match.Groups[slot];
        Assert.Equal(expectedCapture is not null, group.Success);
        if (expectedCapture is not null) Assert.Equal(expectedCapture, System.Text.Encoding.UTF8.GetString((group.Success ? match1Input.Span.Slice(group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(1, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
    }

    [Fact]
    public void LinearReferencesUseOnlyRequiredStateAndStillEnforceLimits()
    {
        const string pattern = @"(?-)(?<n>a)(b)\1";
        var small = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = 256,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(1, small.Count("aba"u8));

        var insufficient = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(1, insufficient.Count("aba"u8)); // The proved count needs no capture vectors.
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() =>
            {
                ReadOnlyMemory<byte> result2Input = ("aba"u8).ToArray();
                using var result2Cursor = insufficient.CreateMatchCursor(result2Input);
                result2Cursor.MoveNext(out var result2Result);
                result2Cursor.Dispose();
                _ = result2Result;
            }).ErrorKind);
        var work = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => work.Count("aba"u8)).ErrorKind);
        var result = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxResultBytes = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.ResultLimit,
            Assert.Throws<Pcre2MatchException>(() =>
            {
                ReadOnlyMemory<byte> result3Input = ("aba"u8).ToArray();
                using var result3Cursor = result.CreateMatchCursor(result3Input);
                result3Cursor.MoveNext(out var result3Result);
                result3Cursor.Dispose();
                _ = result3Result;
            }).ErrorKind);
        var depth = new Utf8Pcre2Regex(@"(?:(?<x>a|ab)c|d)\k<x>", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(1, depth.Count("abcab"u8));
        ReadOnlyMemory<byte> result4Input = ("abcab"u8).ToArray();
        using var result4Cursor = depth.CreateMatchCursor(result4Input);
        result4Cursor.MoveNext(out var result4Result);
        result4Cursor.Dispose();
        // Disproved alternatives no longer require pending frames here.
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString((result4Result.Groups[1].Success ? result4Input.Span.Slice(result4Result.Groups[1].StartOffsetInBytes, result4Result.Groups[1].EndOffsetInBytes - result4Result.Groups[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        const string ambiguous = @"(?:(?<x>a+|a+b)c|a)\k<x>";
        var result5Regex = new Utf8Pcre2Regex(ambiguous);
        ReadOnlyMemory<byte> result5Input = ("aabcaab"u8).ToArray();
        using var result5Cursor = result5Regex.CreateMatchCursor(result5Input);
        result5Cursor.MoveNext(out var result5Result);
        result5Cursor.Dispose();
        Assert.True(result5Result.Success);
        var pending = new Utf8Pcre2Regex(ambiguous, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() =>
            {
                ReadOnlyMemory<byte> result6Input = ("aabcaab"u8).ToArray();
                using var result6Cursor = pending.CreateMatchCursor(result6Input);
                result6Cursor.MoveNext(out var result6Result);
                result6Cursor.Dispose();
                _ = result6Result;
            }).ErrorKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongRollbackChecksTheOriginalInterruption(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var limits = new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 65_536 };
        var invocation = new Utf8SliceInvocation(limits.ToSliceLimits(), TimeSpan.FromMilliseconds(20), cancellation.Token);
        var budget = new Pcre2ResourceBudget(limits, invocation, collectDiagnostics: false);
        var journal = new Pcre2InvocationStateStack<Pcre2CaptureMutation>(65, trackRents: false);
        int[] starts = [-1], ends = [-1], openStarts = [-1], owners = [-1], openOwners = [-1];
        try
        {
            for (var index = 0; index < 65; index++)
                Assert.True(journal.TryPush(new(0, index, index + 1, -1, -1, -1), ref budget));

            if (cancel) cancellation.Cancel();
            else ticks = 25;
            Exception? failure = null;
            try
            {
                Pcre2BacktrackingRunner.RollbackCaptures(0, ref journal, starts, ends, openStarts,
                    owners, openOwners, ref budget);
            }
            catch (Exception error) { failure = error; }

            // The first mutation is restored. The 64-entry checkpoint
            // observes the interruption before touching the next one.
            Assert.Equal(64, journal.Count);
            Assert.Equal((64, 65), (starts[0], ends[0]));
            if (cancel)
                Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            else
                Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        }
        finally { journal.Dispose(ref budget); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedScalarCapturesAndAliasesKeepTheOriginalInterruption(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?<x>😀)(?:é|éé)\k<x>", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = "😀éé😀 😀éé😀"u8.ToArray();
        ReadOnlyMemory<byte> retained7Input = input;
        using var retained7Cursor = regex.CreateMatchCursor(retained7Input, 0, Pcre2MatchOptions.None, cancellation.Token);
        retained7Cursor.MoveNext(out var retained7Result);
        retained7Cursor.Dispose();
        var retained = retained7Result;
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained7Input.Span.Slice(retained.Groups[1].StartOffsetInBytes, retained.Groups[1].EndOffsetInBytes - retained.Groups[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained7Input.Span.Slice(retained.Groups[1].StartOffsetInBytes, retained.Groups[1].EndOffsetInBytes - retained.Groups[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        ReadOnlyMemory<byte> result8Input = ("😀éé😀"u8).ToArray();
        using var result8Cursor = regex.CreateMatchCursor(result8Input);
        result8Cursor.MoveNext(out var result8Result);
        result8Cursor.Dispose();
        Assert.True(result8Result.Success);
    }
}
