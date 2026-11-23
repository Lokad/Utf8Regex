using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SubroutineCaptureReturnTests
{
    [Theory]
    [InlineData("")]
    [InlineData("(*ACCEPT)never")]
    public void DeterministicReturnsReuseJournalsUnderWorkingLimit(string returnSuffix)
    {
        using var regex = new Utf8Pcre2Regex(
            @"\A(?<caller>é)(?&unit){24}\k<caller>END\z(?(DEFINE)(?<unit>(?<inner>😀)a" + returnSuffix + "))",
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 100_000, MaxWorkingBytes = 2 << 10 } });
        var input = Encoding.UTF8.GetBytes("é" + string.Concat(Enumerable.Repeat("😀a", 24)) + "éEND");
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[4];

        var match = regex.Match(input, captures: captures);

        Assert.True(match.Success);
        Assert.Equal((0, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.True(captures[1].Success);
        Assert.Equal((0, 2), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.False(captures[2].Success);
        Assert.False(captures[3].Success);
    }

    [Fact]
    public void LazyCallRepeatResumesAtTheSavedScalarBoundary()
    {
        using var regex = new Utf8Pcre2Regex(@"\A(?<x>(?&unit){1,4}?)Z\z(?(DEFINE)(?<unit>é😀|a))",
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 1000, MaxDepth = 64, MaxWorkingBytes = 16 << 10 } });
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match).Program;
        Assert.Contains(program.Instructions, instruction => instruction is
            { Kind: Pcre2BacktrackingInstructionKind.Repeat, Preference: Pcre2RepeatPreference.Lazy });
        using var source = new CancellationTokenSource();
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[3];

        var match = regex.Match("é😀é😀aZ"u8, captures: captures, cancellationToken: source.Token);

        Assert.Equal((0, 14), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal((0, 13), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.False(captures[2].Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClassRunLeavesAScalarWhenTheFollowingCallNeedsIt(bool funded)
    {
        using var regex = new Utf8Pcre2Regex(@"^(?<word>[😀a]+)(?&tail)$(?(DEFINE)(?<tail>[😀a]+!))",
            policy: funded ? new Utf8RegexPolicy
            {
                ResourcePolicy = new() { MaxWorkSteps = 1000, MaxDepth = 64, MaxWorkingBytes = 16 << 10 }
            } : null);
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match).Program;
        Assert.Contains(program.Instructions, instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.GreedyClassRepeat);

        using var source = new CancellationTokenSource();
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[3];
        var match = regex.Match("😀a😀!"u8, captures: captures, cancellationToken: funded ? source.Token : default);
        Assert.Equal((0, 10), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal((0, 5), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.False(captures[2].Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SiblingCallRetriesKeepTheRequiredHistoryAfterGrowth(bool retryFirst)
    {
        using var regex = new Utf8Pcre2Regex(@"\A(?<first>(?&unit))(?<rest>(?&unit){12})c\z(?(DEFINE)(?<unit>a|ab))",
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 20_000, MaxDepth = 256, MaxWorkingBytes = 1 << 20 } });
        var tail = new string('a', 12);
        var input = Encoding.UTF8.GetBytes(retryFirst ? "ab" + tail + "c" : tail + "abc");
        var firstEnd = retryFirst ? 2 : 1;
        using var cancellation = new CancellationTokenSource();
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[4];
        var match = regex.Match(input, captures: captures, cancellationToken: cancellation.Token);
        Assert.Equal((0, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal((0, firstEnd), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.Equal((firstEnd, input.Length - 1), (captures[2].StartOffsetInBytes, captures[2].EndOffsetInBytes));
        Assert.False(captures[3].Success);
    }

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
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit,
                    MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                    MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                    MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
            Assert.True(regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
            Assert.Equal(1, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
            ReadOnlySpan<byte> match1Input = input;
            var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var match = regex.Match(match1Input, captures: match1Captures, cancellationToken: cancellation.Token);
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
            var group = match1Captures[slot];
            Assert.Equal(value is not null, group.Success);
            if (value is not null)
            {
                Assert.Equal(value, System.Text.Encoding.UTF8.GetString((group.Success ? match1Input.Slice(group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
    [InlineData(@"\A(?&x)(?(x)b|c)\z(?(DEFINE)(?<x>a(*ACCEPT)))", "ac", true)]
    [InlineData(@"\A(?&x)(?(x)b|c)\z(?(DEFINE)(?<x>a(*ACCEPT)))", "ab", false)]
    [InlineData(@"\A(?<seen>a)(b)\2(?(seen)c|d)\z", "abbc", true)]
    [InlineData(@"\A(?<seen>a)(b)\2(?(seen)c|d)\z", "abbd", false)]
    [InlineData(@"\A(a)(b)(?=\1)a\2\z", "abab", true)]
    public void ReferencesAndConditionsObserveCallerCaptureState(string pattern, string text, bool expected)
    {
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
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected ? 1 : 0, regex.Count(input));
        Assert.Equal(expected, regex.Match(input).Success);
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
        ReadOnlySpan<byte> result3Input = "😀"u8;
        var result3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var result3Result = regex.Match(result3Input, captures: result3Captures);
        Assert.False(result3Captures[1].Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReturnedCapturesSurviveAliasInterruption(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?(DEFINE)(?<x>😀(?&x)?é))(?<w>(?&x))\k<w>", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = "😀é😀é 😀é😀é"u8.ToArray();
        ReadOnlySpan<byte> retained4Input = input;
        var retained4Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained4Input, captures: retained4Captures, cancellationToken: cancellation.Token);
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
        Assert.False(retained4Captures[1].Success);
        Assert.Equal("😀é", System.Text.Encoding.UTF8.GetString((retained4Captures[2].Success ? retained4Input.Slice(retained4Captures[2].StartOffsetInBytes, retained4Captures[2].EndOffsetInBytes - retained4Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.True(regex.Match("😀é😀é"u8).Success);
    }
}
