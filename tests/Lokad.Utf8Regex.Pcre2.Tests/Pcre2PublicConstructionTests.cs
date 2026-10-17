using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PublicConstructionTests
{
    private const string Pattern = @"(?<x>😀){2,4}\k<x>";

    private static Utf8Pcre2Regex Compile(Utf8ConstructionPolicy policy, bool bytes,
        CancellationToken cancellationToken = default)
        => bytes ? Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(Pattern), Pcre2CompileOptions.None,
            default, Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy,
            Timeout.InfiniteTimeSpan, cancellationToken)
        : Utf8Pcre2Regex.Compile(Pattern, Pcre2CompileOptions.None, default,
            Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy,
            Timeout.InfiniteTimeSpan, cancellationToken);

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    public void EachConstructionCapDeniesAndFreshCompilationRecovers(int limit, bool bytes)
    {
        var policy = limit switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 1 },
            1 => new Utf8ConstructionPolicy { MaxDepth = 1 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 },
        };
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Compile(policy, bytes));
        Assert.Equal(limit switch
        {
            0 => Utf8ResourceLimitKind.ConstructionWorkSteps,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            2 => Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            _ => Utf8ResourceLimitKind.ConstructionBytes,
        }, failure.ResourceKind);
        Assert.Equal(1ul, failure.Limit);
        Assert.False(Utf8ConstructionScope.IsActive);
        var result = Compile(Utf8ConstructionPolicy.Default, bytes).MatchDetailed("z😀😀😀z"u8);
        Assert.True(result.Success);
        Assert.Equal((1, 13), (result.Value.StartOffsetInBytes, result.Value.EndOffsetInBytes));
        Assert.Equal("😀", result.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FundedPoliciesPreserveScalarsAndConstructionTokenEndsAtPublication(bool bytes)
    {
        using var cancellation = new CancellationTokenSource();
        var policy = new Utf8ConstructionPolicy { MaxWorkSteps = 1000000, MaxDepth = 256,
            MaxInstructionCapacity = 10000, MaxBytes = 1000000 };
        var execution = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
            MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 };
        var regex = bytes ? Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(Pattern), Pcre2CompileOptions.None,
            default, Timeout.InfiniteTimeSpan, execution, policy, TimeSpan.FromSeconds(30), cancellation.Token)
            : Utf8Pcre2Regex.Compile(Pattern, Pcre2CompileOptions.None, default,
                Timeout.InfiniteTimeSpan, execution, policy, TimeSpan.FromSeconds(30), cancellation.Token);
        cancellation.Cancel();
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.Equal(100000u, regex.DefaultExecutionLimits.MatchLimit);
        var result = regex.MatchDetailed("z😀😀😀z"u8);
        Assert.True(result.Success);
        Assert.Equal("😀😀😀", result.GetValueString());
        Assert.Equal("😀", result.GetGroup(1).GetValueString());
    }

    [Fact]
    public void ExecutionPolicyStillEnforcesItsOwnLimitAfterCompilation()
    {
        var regex = Utf8Pcre2Regex.Compile("foo", Pcre2CompileOptions.None, default,
            Timeout.InfiniteTimeSpan, new() { MaxWorkSteps = 1 }, Utf8ConstructionPolicy.Default,
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8)).ErrorKind);
    }

    [Fact]
    public void PreCancellationPrecedesMalformedPatternAndInvalidArguments()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => Utf8Pcre2Regex.Compile(new byte[] { 0xff },
            (Pcre2CompileOptions)(1 << 29), default, TimeSpan.Zero, new() { MaxWorkSteps = 0 },
            new() { MaxBytes = 0 }, TimeSpan.Zero, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(0, "constructionPolicy")] [InlineData(1, "resourcePolicy")]
    [InlineData(2, "constructionTimeout")] [InlineData(3, "matchTimeout")]
    [InlineData(4, "options")] [InlineData(5, "compileSettings")]
    public void InvalidArgumentsPrecedeByteWorkAndRestoreOwner(int invalid, string parameter)
    {
        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => Utf8Pcre2Regex.Compile(new byte[] { 0xff },
            invalid >= 4 && invalid != 5 ? (Pcre2CompileOptions)(1 << 29) : Pcre2CompileOptions.None,
            invalid == 5 ? new() { Bsr = (Pcre2BsrConvention)999 } : default,
            invalid <= 3 ? TimeSpan.Zero : Timeout.InfiniteTimeSpan,
            invalid <= 1 ? new() { MaxWorkSteps = 0 } : Utf8ResourcePolicy.Unlimited,
            invalid == 0 ? new() { MaxBytes = 0 } : Utf8ConstructionPolicy.Default,
            invalid <= 2 ? TimeSpan.Zero : Timeout.InfiniteTimeSpan, CancellationToken.None));
        Assert.Equal(parameter, failure.ParamName);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ConstructionTimeoutIsIndependentOfMatchTimeout(bool bytes)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++);
        var regex = bytes ? Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(Pattern), Pcre2CompileOptions.None,
            default, TimeSpan.FromMilliseconds(1), Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
            TimeSpan.FromSeconds(30), CancellationToken.None)
            : Utf8Pcre2Regex.Compile(Pattern, Pcre2CompileOptions.None, default, TimeSpan.FromMilliseconds(1),
                Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default, TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.True(ticks > 1);
        Assert.Equal(TimeSpan.FromMilliseconds(1), regex.MatchTimeout);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InFlightCancellationRetainsOriginalTokenAndFreshCallRecovers(bool bytes)
    {
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == 8) cancellation.Cancel();
            return 0;
        }))
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
            {
                if (bytes) _ = Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(Pattern), Pcre2CompileOptions.None,
                    default, Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
                    TimeSpan.FromSeconds(30), cancellation.Token);
                else _ = Utf8Pcre2Regex.Compile(Pattern, Pcre2CompileOptions.None, default, Timeout.InfiniteTimeSpan,
                    Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default, TimeSpan.FromSeconds(30), cancellation.Token);
            });
            Assert.Equal(cancellation.Token, failure.CancellationToken);
            Assert.Equal(8, reads);
            Assert.False(Utf8ConstructionScope.IsActive);
        }
        Assert.True(Compile(Utf8ConstructionPolicy.Default, bytes).IsMatch("😀😀😀"u8));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeadlinePreventsPublicationAndUsesConstructionException(bool bytes)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var failure = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            if (bytes) _ = Utf8Pcre2Regex.Compile("😀+"u8, Pcre2CompileOptions.None, default,
                Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
                TimeSpan.FromMilliseconds(1), CancellationToken.None);
            else _ = Utf8Pcre2Regex.Compile("😀+", Pcre2CompileOptions.None, default, Timeout.InfiniteTimeSpan,
                Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default, TimeSpan.FromMilliseconds(1), CancellationToken.None);
        });
        Assert.Equal(TimeSpan.FromMilliseconds(1), failure.MatchTimeout);
        Assert.Equal(bytes ? string.Empty : "😀+", failure.Pattern);
        Assert.Equal(string.Empty, failure.Input);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(4093)] [InlineData(4094)] [InlineData(4095)] [InlineData(4096)]
    public void ByteValidationAndDecodePreserveBlockBoundaryScalars(int prefix)
    {
        var pattern = new string('a', prefix) + "😀é";
        var bytes = Encoding.UTF8.GetBytes(pattern);
        var regex = Utf8Pcre2Regex.Compile(bytes, Pcre2CompileOptions.None, default,
            Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        Assert.Equal(pattern, regex.Pattern);
        Assert.True(regex.IsMatch(bytes));
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(4093)] [InlineData(4094)] [InlineData(4095)] [InlineData(4096)] [InlineData(8191)]
    public void MalformedByteBlocksKeepOriginalExceptionAndAbsoluteOffset(int prefix)
    {
        byte[][] tails = [[0xff], [0xc2], [0xed, 0xa0, 0x80], [0xf0, 0x80, 0x80, 0x80],
            [0xf4, 0x90, 0x80, 0x80], [0x80, 0x80, 0x80, 0x80, 0x80], [0xe2, 0x82, 0x41]];
        foreach (var tail in tails)
        {
            var input = Enumerable.Repeat((byte)'a', prefix).Concat(tail).Concat("😀"u8.ToArray()).ToArray();
            var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(input));
            var failure = Assert.Throws<ArgumentException>(() => Utf8Pcre2Regex.Compile(input,
                Pcre2CompileOptions.None, default, Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited,
                Utf8ConstructionPolicy.Default, Timeout.InfiniteTimeSpan, CancellationToken.None));
            Assert.Equal(expected.ParamName, failure.ParamName);
            Assert.Equal(expected.Message, failure.Message);
            Assert.False(Utf8ConstructionScope.IsActive);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ByteValidationObservesInterruptionBeforeLaterMalformedTail(bool timeout)
    {
        var input = Enumerable.Repeat((byte)'a', 10000).Append((byte)0xff).ToArray();
        using var cancellation = new CancellationTokenSource();
        Utf8ConstructionBudget? budget = null;
        long ticks = 0;
        var checkpoints = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (budget is null)
            {
                budget = Utf8ConstructionScope.CurrentBudget;
                budget.CheckpointHook = _ =>
                {
                    if (++checkpoints != 2) return;
                    if (timeout) ticks = 25; else cancellation.Cancel();
                };
            }
            return ticks;
        });
        var failure = Record.Exception(() => Utf8Pcre2Regex.Compile(input, Pcre2CompileOptions.None, default,
            Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
            TimeSpan.FromMilliseconds(20), cancellation.Token));
        if (timeout) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.NotNull(budget);
        Assert.Equal((ulong)input.Length, budget.WorkSteps);
        Assert.Equal(0ul, budget.PeakBytes);
        Assert.Equal(0ul, budget.ReservedBytes);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void ExplicitFactoryCannotReplaceAnActiveOwner()
    {
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Throws<InvalidOperationException>(() => Compile(Utf8ConstructionPolicy.Default, false));
        Assert.Same(owner.Budget, Utf8ConstructionScope.CurrentBudget);
        Assert.Equal(0ul, owner.Budget.WorkSteps);
        Assert.True(new Utf8Pcre2Regex("😀+").IsMatch("😀"u8));
    }

    [Fact]
    public void GrammarErrorsKeepPcre2IdentityAndLeaveNoOwner()
    {
        Assert.Throws<Pcre2CompileException>(() => Utf8Pcre2Regex.Compile("(?<123>a)", Pcre2CompileOptions.None,
            default, Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
            Timeout.InfiniteTimeSpan, CancellationToken.None));
        Assert.False(Utf8ConstructionScope.IsActive);
    }
}
