using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePublicConstructionTests
{
    [Theory]
    [InlineData(4093)]
    [InlineData(4094)]
    [InlineData(4095)]
    [InlineData(4096)]
    public void ByteFactoryPreservesScalarsAcrossDecodeBlocks(int prefix)
    {
        var pattern = new string('a', prefix) + "😀é";
        var bytes = System.Text.Encoding.UTF8.GetBytes(pattern);
        var regex = Utf8PythonRegex.Compile(bytes, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = Utf8ConstructionPolicy.Default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None);
        Assert.Equal(pattern, regex.Pattern);
        Assert.True(regex.Match(bytes, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(1, regex.Count(bytes));
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void ByteFactoryRetainsAbsoluteMalformedOffsetAfterDecodeBlock()
    {
        var bytes = Enumerable.Repeat((byte)'a', 8193).Append((byte)0xff).ToArray();
        var failure = Assert.Throws<ArgumentException>(() => Utf8PythonRegex.Compile(bytes, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = Utf8ConstructionPolicy.Default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None));
        Assert.Equal("patternUtf8", failure.ParamName);
        Assert.Equal(8193, Assert.IsType<System.Text.DecoderFallbackException>(failure.InnerException).Index);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitCompileNeedsNoLaterFullMatchConstruction(bool bytes)
    {
        var regex = Compile(Utf8ConstructionPolicy.Default, bytes);
        using var deniedPreparation = new Utf8ConstructionScope(1, 1, 1, 1);
        Utf8PythonRangeMatch match = default;
        using (var captureCursor1 = regex.CreateMatchCursor(("😀😀"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out match);
        }
        Assert.True(match.Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((match.Groups[1].Success ? match.GetGroupMemory(match.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(1, match.LastIndex);
        Assert.Equal(0ul, deniedPreparation.Budget.WorkSteps);
        Assert.Equal(0ul, deniedPreparation.Budget.ReservedBytes);
        Assert.Equal(0ul, deniedPreparation.Budget.Reserved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompileUsesConstructionDeadlineInsteadOfMatchDeadline(bool bytes)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++);
        var regex = bytes
            ? Utf8PythonRegex.Compile("(?P<x>😀){2,4}"u8, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = TimeSpan.FromMilliseconds(1),
                ResourcePolicy = Utf8ResourcePolicy.Unlimited,
                ConstructionPolicy = Utf8ConstructionPolicy.Default,
                ConstructionTimeout = TimeSpan.FromSeconds(30)
            }, cancellationToken: CancellationToken.None) : Utf8PythonRegex.Compile("(?P<x>😀){2,4}", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = TimeSpan.FromMilliseconds(1),
                ResourcePolicy = Utf8ResourcePolicy.Unlimited,
                ConstructionPolicy = Utf8ConstructionPolicy.Default,
                ConstructionTimeout = TimeSpan.FromSeconds(30)
            }, cancellationToken: CancellationToken.None);
        Assert.True(ticks > 1);
        Assert.Equal(TimeSpan.FromMilliseconds(1), regex.MatchTimeout);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    private static Utf8PythonRegex Compile(Utf8ConstructionPolicy policy, bool bytes)
        => bytes ? Utf8PythonRegex.Compile("(?P<x>😀){2,4}"u8, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = policy,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None) : Utf8PythonRegex.Compile("(?P<x>😀){2,4}", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = policy,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void EachFiniteConstructionLimitDeniesAndFreshCallRecovers(int limit, bool bytes)
    {
        var policy = limit switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 1 },
            1 => new Utf8ConstructionPolicy { MaxDepth = 1 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 },
        };
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Compile(policy, bytes));
        Assert.Equal(limit switch
        {
            0 => Utf8ResourceLimitKind.ConstructionWorkSteps,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            2 => Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            _ => Utf8ResourceLimitKind.ConstructionBytes,
        }, error.ResourceKind);
        Assert.Equal(1ul, error.Limit);
        Assert.False(Utf8ConstructionScope.IsActive);
        var regex = Compile(Utf8ConstructionPolicy.Default, bytes);
        Utf8PythonRangeMatch result = default;
        using (var captureCursor2 = regex.CreateMatchCursor(("😀😀"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor2.MoveNext(out result);
        }
        Assert.True(result.Success);
        Assert.Equal(1, result.LastIndex);
    }

    [Fact]
    public void PreCancellationPrecedesInvalidByteDecodingAndArguments()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => Utf8PythonRegex.Compile(new byte[] { 0xff }, options: PythonReCompileOptions.Locale, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.Zero,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 0
            },
            ConstructionPolicy = new()
            {
                MaxBytes = 0
            },
            ConstructionTimeout = TimeSpan.Zero
        }, cancellationToken: cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void FundedFourControlPolicyAndConstructionTokenEndAtPublication()
    {
        using var cancellation = new CancellationTokenSource();
        var regex = Utf8PythonRegex.Compile("(?P<x>😀){2,4}"u8, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 64,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            },
            ConstructionPolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 128,
                MaxInstructionCapacity = 10000,
                MaxBytes = 1000000
            },
            ConstructionTimeout = TimeSpan.FromSeconds(30)
        }, cancellationToken: cancellation.Token);
        cancellation.Cancel();
        Utf8PythonRangeMatch result = default;
        using (var captureCursor3 = regex.CreateMatchCursor(("😀😀"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor3.MoveNext(out result);
        }
        Assert.True(result.Success);
        Assert.Equal(1, result.LastIndex);
        Assert.True(regex.Match("😀😀"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void InFlightConstructionCancellationKeepsOriginalToken()
    {
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == 8) cancellation.Cancel();
            return 0;
        });
        var error = Assert.Throws<OperationCanceledException>(() => Utf8PythonRegex.Compile("(?P<x>😀){2,4}"u8, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = Utf8ConstructionPolicy.Default,
            ConstructionTimeout = TimeSpan.FromSeconds(30)
        }, cancellationToken: cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(8, reads);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeadlineUsesOneEagerAuthorityAndLeavesNoActiveOwner(bool bytes)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            if (bytes) _ = Utf8PythonRegex.Compile("😀+"u8, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Utf8ResourcePolicy.Unlimited,
                ConstructionPolicy = Utf8ConstructionPolicy.Default,
                ConstructionTimeout = TimeSpan.FromMilliseconds(1)
            }, cancellationToken: CancellationToken.None);
            else _ = Utf8PythonRegex.Compile("😀+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Utf8ResourcePolicy.Unlimited,
                ConstructionPolicy = Utf8ConstructionPolicy.Default,
                ConstructionTimeout = TimeSpan.FromMilliseconds(1)
            }, cancellationToken: CancellationToken.None);
        });
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        Assert.Equal(bytes ? string.Empty : "😀+", error.Pattern);
        Assert.False(Utf8ConstructionScope.IsActive);
    }
}
