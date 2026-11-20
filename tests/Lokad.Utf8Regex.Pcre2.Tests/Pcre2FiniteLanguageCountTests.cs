using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2FiniteLanguageCountTests
{
    [Theory]
    [InlineData("(a)(b)", "ab ab", 2)]
    [InlineData("(a|aa)a", "aaaaaa", 3)]
    [InlineData("(ab|a)b", "abbab", 2)]
    [InlineData(@"(?<x>a|b)\k<x>", "aa bb ab ba", 2)]
    [InlineData(@"(a)\1", "aa aaa", 2)]
    [InlineData(@"(?<x>😀)(é)", "😀éx😀é", 2)]
    [InlineData(@"(?<x>a)\Kb", "ab ab", 2)]
    [InlineData("(a)?b", "b ab b", 3)]
    [InlineData("(a{2,3}?)(a)", "aaaaaa", 2)]
    public void ControlledCountPreservesTheProvedLanguageAndOrder(string pattern, string text, int expected)
    {
        var regex = Create(pattern, new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 });
        Assert.IsAssignableFrom<Pcre2LiteralFamilyDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, regex.Count(input, 0, Pcre2MatchOptions.None, token.Token));
        // Count publishes no owned result payload, while detailed results still do.
        Assert.Equal(Pcre2ErrorKind.ResultLimit,
            Assert.Throws<Pcre2MatchException>(() =>
            {
                ReadOnlyMemory<byte> result1Input = input;
                using var result1Cursor = regex.CreateMatchCursor(result1Input);
                result1Cursor.MoveNext(out var result1Result);
                result1Cursor.Dispose();
                _ = result1Result;
            }).ErrorKind);
    }

    [Theory]
    [InlineData("(a)?", "😀", 2)]
    [InlineData("(a*)", "aaa", 2)]
    [InlineData("(?<=a)b", "ab abb", 2)]
    [InlineData("(a+)(b)", "ab aaab", 2)]
    [InlineData("(?i)(a)(b)", "AB ab", 2)]
    public void EmptyAndUnprovedPatternsKeepTheirExistingSemantics(string pattern, string text, int expected)
    {
        var regex = Create(pattern, new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 });
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void ValidationAndCountingSpendOneCumulativeAllowance()
    {
        const string pattern = "(a)(b)";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 100)));
        var measured = Create(pattern, new() { MatchLimit = 1000000 });
        var invocation = Pcre2SliceDriver.CreateInvocation(measured.DebugCompiledProgram.Request, default);
        var validated = Pcre2Runner.PrepareInput(input, 0, ref invocation);
        var validationSteps = invocation.Budget.Steps;
        Assert.True(Pcre2GlobalOperationDriver.TryCount(measured.DebugCompiledProgram,
            ref validated, new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
        Assert.Equal(100, count);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var matchingSteps = invocation.Budget.Steps - validationSteps;
        Assert.True(validationSteps > 0 && matchingSteps > 0);
        // Either phase fits separately. Their combined work must still deny.
        var limit = checked((uint)Math.Max(validationSteps, matchingSteps));
        var denied = Create(pattern, new() { MatchLimit = limit });
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => denied.Count(input)).ErrorKind);
    }

    [Fact]
    public void ActualAlternativeFramesReleaseWorkingCapacityOnSuccessAndDenial()
    {
        foreach (var allowance in new[] { 1UL << 20, 1UL })
        {
            // The preferred "ab" arm consumes the byte needed by the suffix.
            // The scalar VM must keep "a" available for a real retry.
            var regex = Create("((ab)|(a)|(c)|(d))b", new() { HeapLimitInBytes = allowance });
            var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
            var input = Pcre2Runner.PrepareInput("ab ab"u8, 0, ref invocation);
            if (allowance == 1)
            {
                Pcre2MatchException? failure = null;
                try
                {
                    Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                        new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _);
                }
                catch (Pcre2MatchException error) { failure = error; }
                Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            }
            else
            {
                Assert.True(Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
                Assert.Equal(2, count);
            }
            Assert.True(invocation.Budget.PeakWorkingBytes > 0);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void StartOptionsAndHeapAdmissionStayAuthoritative()
    {
        var regex = Create("(a)(b)", new() { MatchLimit = 1000000, HeapLimitInBytes = 1 << 20 });
        Assert.Equal(2, regex.Count("😀ab ab"u8, 4));
        Assert.Equal(1, regex.Count("😀ab ab"u8, 5));
        Assert.Equal(0, regex.Count("😀ab ab"u8, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab ab"u8, 1));
        Assert.Equal(1, regex.Count("ab ab"u8, 0, Pcre2MatchOptions.Anchored));
        var tinyLiteral = Create("(a)(b)", new() { HeapLimitInBytes = 1 });
        Assert.Equal(1, tinyLiteral.Count("ab"u8)); // No major scratch is acquired.
        var tinyAlternatives = Create("((ab)|(a))", new() { HeapLimitInBytes = 1 });
        Assert.Equal(2, tinyAlternatives.Count("ab a"u8)); // Native Count needs no rewind frames.
        var tinyTerminal = Create("((ab)|(a)|(c)|(d))", new() { HeapLimitInBytes = 1 });
        Assert.Equal(2, tinyTerminal.Count("ab a"u8)); // The preferred arm can accept without a retry.
        var tiny = Create("((ab)|(a)|(c)|(d))b", new() { HeapLimitInBytes = 1 });
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => tiny.Count("ab ab"u8)).ErrorKind);
        var depth = Create("(?:(a|aa)+)b", new() { DepthLimit = 1 });
        Assert.Equal(Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => depth.Count("aaaab"u8)).ErrorKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountKeepsOriginalInterruptionAndRetainedCaptureRanges(bool cancel)
    {
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(a)(b)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
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
        ReadOnlySpan<byte> retained2Input = "ab"u8;
        var retained2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained2Input, captures: retained2Captures);
        int reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == 5 && cancel) token.Cancel();
            return reads >= 5 && !cancel ? 25 : 0;
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 3000)));
        if (cancel)
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(
                () => regex.Count(input, 0, Pcre2MatchOptions.None, token.Token)).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(
                () => regex.Count(input)).ErrorKind);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((retained2Captures[2].Success ? retained2Input.Slice(retained2Captures[2].StartOffsetInBytes, retained2Captures[2].EndOffsetInBytes - retained2Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(1, regex.Count("ab"u8));
    }

    [Theory]
    [InlineData("(a)(b)", "ab")]
    [InlineData(@"(a)\1", "aa")]
    [InlineData("(?<x>😀)(é)", "😀é")]
    [InlineData(@"(?<x>a)\Kb", "ab")]
    public void SparseFiniteCountsKeepBoundedSeekingAndScalarOffsets(string pattern, string hit)
    {
        var regex = Create(pattern, new() { MatchLimit = 1000, DepthLimit = 256, HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 });
        using var token = new CancellationTokenSource();
        Assert.Equal(0, regex.Count(Encoding.UTF8.GetBytes(new string('x', 65536))));
        foreach (var prefix in new[] { 2047, 2048, 2049 })
        {
            var text = "😀" + new string('x', prefix) + hit + new string('x', 2048) + hit;
            var input = Encoding.UTF8.GetBytes("pad" + text + "pad");
            var length = Encoding.UTF8.GetByteCount(text);
            Assert.Equal(2, regex.Count(input.AsSpan(3, length)));
            Assert.Equal(2, regex.Count(input.AsSpan(3, length), 4,
                Pcre2MatchOptions.None, token.Token));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SparseSeekingChecksTheOriginalAuthorityAfterValidation(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(a)(b)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxWorkingBytes = 1 << 20
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, token.Token);
        var bytes = Encoding.UTF8.GetBytes(new string('x', 192 * 1024));
        var input = Pcre2Runner.PrepareInput(bytes, 0, ref invocation);
        var validatedSteps = invocation.Budget.Steps;
        int checkpoints = 0;
        invocation.CheckpointHook = _ =>
        {
            checkpoints++;
            if (cancel) token.Cancel(); else ticks = 25;
        };
        Exception? failure = null;
        try
        {
            Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _);
        }
        catch (Exception error) { failure = error; }
        Assert.True(checkpoints > 0 && invocation.Budget.Steps > validatedSteps);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        if (cancel)
            Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
            // This direct driver probe is below the public timeout translation.
            Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
    }

    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits) =>
        new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
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
}
