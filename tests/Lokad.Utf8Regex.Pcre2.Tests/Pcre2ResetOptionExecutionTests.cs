using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ResetOptionExecutionTests
{
    [Theory]
    [InlineData("(?^:a#b)", Pcre2CompileOptions.Extended, "a#b", true)]
    [InlineData("(?^:[ a])", Pcre2CompileOptions.ExtendedMore, " ", true)]
    [InlineData("(?^:a)b", Pcre2CompileOptions.Caseless, "Ab", false)]
    [InlineData("(?^:a)b", Pcre2CompileOptions.Caseless, "aB", true)]
    [InlineData("(?^:a.)", Pcre2CompileOptions.DotAll, "a\n", false)]
    [InlineData("(?^:a).", Pcre2CompileOptions.DotAll, "a\n", true)]
    [InlineData("(?^:^a$)", Pcre2CompileOptions.Multiline, "x\na\n", false)]
    [InlineData("(?^:a)$", Pcre2CompileOptions.Multiline, "a\nx", true)]
    [InlineData("(?^i:a)", Pcre2CompileOptions.None, "A", true)]
    [InlineData("(?^x:a # ignored\n b)", Pcre2CompileOptions.None, "ab", true)]
    [InlineData("(?^n:(a))", Pcre2CompileOptions.None, "a", true)]
    [InlineData("(?^:)", Pcre2CompileOptions.None, "😀", true)]
    [InlineData("(?^:.)", Pcre2CompileOptions.DotAll, "😀", true)]
    public void ScopedResetClearsOnlyItsFlagsAndCanEnableSupportedOptions(string pattern,
        Pcre2CompileOptions options, string text, bool expected)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options: options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(expected, regex.Match(bytes, cancellationToken: cancellation.Token).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetRestoresCapturePolicyAndPreservesPublishedScalarCaptures(bool controlled)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?^:(😀))(b)", options: Pcre2CompileOptions.NoAutoCapture | Pcre2CompileOptions.Caseless, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(controlled ? new()
                {
                    MatchLimit = 100000,
                    DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20
                }

                : default)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(controlled ? new()
                {
                    MatchLimit = 100000,
                    DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20
                }

                : default)).MatchLimit,
                MaxDepth = ((Utf8Pcre2ExecutionLimits)(controlled ? new()
                {
                    MatchLimit = 100000,
                    DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20
                }

                : default)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(controlled ? new()
                {
                    MatchLimit = 100000,
                    DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20
                }

                : default)).DepthLimit,
                MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(controlled ? new()
                {
                    MatchLimit = 100000,
                    DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20
                }

                : default)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(controlled ? new()
                {
                    MatchLimit = 100000,
                    DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20
                }

                : default)).HeapLimitInBytes,
                MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(controlled ? new()
                {
                    MatchLimit = 100000,
                    DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20
                }

                : default)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(controlled ? new()
                {
                    MatchLimit = 100000,
                    DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20
                }

                : default)).ResultLimitInBytes
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var bytes = "é😀B😀b"u8.ToArray();
        ReadOnlySpan<byte> match2Input = bytes;
        var match2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match2Input, captures: match2Captures, cancellationToken: cancellation.Token);
        Assert.True(match.Success);
        Assert.Equal((2, 7), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(2, (match.Success ? match2Captures.Length : 0)); // Outer (b) retains NoAutoCapture.
        var retained = match2Captures[1];
        Assert.Equal((2, 6), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((retained.Success ? match2Input.Slice(retained.StartOffsetInBytes, retained.EndOffsetInBytes - retained.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(2, regex.Count(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal("é😀😀", System.Text.Encoding.UTF8.GetString(regex.Replace(bytes, "$1"u8)));
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((retained.Success ? match2Input.Slice(retained.StartOffsetInBytes, retained.EndOffsetInBytes - retained.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("(?^:a+)", Pcre2CompileOptions.Ungreedy, "a")]
    [InlineData("(?U)(?^:a+)", Pcre2CompileOptions.None, "a")]
    [InlineData("(?^:a+)", Pcre2CompileOptions.None, "aaa")]
    public void ResetPreservesUngreedyPolicy(string pattern, Pcre2CompileOptions options, string value)
    {
        Assert.Equal(value, System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex(pattern, options: options).Match("aaa"u8).GetValueBytes()));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    public void ResetPreservesExistingDuplicateNameMode(string text)
    {
        var regex = new Utf8Pcre2Regex("(?J)(?^:(?<x>a)|(?<x>b))");
        ReadOnlyMemory<byte> match4Input = Encoding.UTF8.GetBytes(text);
        using var match4Cursor = regex.CreateMatchCursor(match4Input);
        match4Cursor.MoveNext(out var match4Result);
        match4Cursor.Dispose();
        var match = match4Result;
        Assert.True(match.Success);
        Assert.Equal(3, match.Groups.Length);
        Assert.Equal(2, regex.NameEntryCount);
        Assert.True(match.TryGetGroup(text == "a" ? 1 : 2, out var group)); Assert.True(group.Success);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString((group.Success ? match4Input.Span.Slice(group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("(?^)(a)")]
    [InlineData("(?^n)(?<x>a)")]
    public void GlobalResetExecutesCapturedPrograms(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.NoAutoCapture | Pcre2CompileOptions.Caseless);
        ReadOnlySpan<byte> match5Input = "a"u8;
        var match5Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match5Input, captures: match5Captures);
        Assert.True(match.Success);
        Assert.Equal(2, (match.Success ? match5Captures.Length : 0));
        Assert.False(regex.IsMatch("A"u8));
    }

    [Theory]
    [InlineData("(?^-x:a)")]
    [InlineData("(?^i-x:a)")]
    [InlineData("(?^-x)a")]
    public void ResetRejectsTheDisallowedMinusFormBeforeExecution(string pattern)
    {
        var failure = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, failure.ErrorKind);
    }

    [Fact]
    public void EmptyResetAdvancesByCompleteScalarsUnderFiniteControls()
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?^:)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(2, regex.Count("😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token));
        var cursor = regex.EnumerateMatches("😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token);
        var starts = new List<int>();
        while (cursor.MoveNext()) starts.Add(cursor.Current.StartOffsetInBytes);
        Assert.Equal(new[] { 0, 4 }, starts);
    }

    [Fact]
    public void ResetCaptureIsAvailableToFollowingNumericReferences()
    {
        var regex = new Utf8Pcre2Regex(@"(?^:(😀))\1", options: Pcre2CompileOptions.NoAutoCapture);
        ReadOnlySpan<byte> match6Input = "é😀😀"u8;
        var match6Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match6Input, captures: match6Captures);
        Assert.True(match.Success);
        Assert.Equal((2, 10), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((match6Captures[1].Success ? match6Input.Slice(match6Captures[1].StartOffsetInBytes, match6Captures[1].EndOffsetInBytes - match6Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("é😀", System.Text.Encoding.UTF8.GetString(regex.Replace("é😀😀"u8, "$1"u8)));
    }
}
