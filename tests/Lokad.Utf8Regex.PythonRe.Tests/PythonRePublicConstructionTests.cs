using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePublicConstructionTests
{
    private static Utf8PythonRegex Compile(Utf8ConstructionPolicy policy, bool bytes)
        => bytes ? Utf8PythonRegex.Compile("(?P<x>😀){2,4}"u8, PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy,
            Timeout.InfiniteTimeSpan, CancellationToken.None)
        : Utf8PythonRegex.Compile("(?P<x>😀){2,4}", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy,
            Timeout.InfiniteTimeSpan, CancellationToken.None);

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
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
        var result = regex.SearchDetailedData("😀😀"u8);
        Assert.True(result.Success);
        Assert.Equal("x", result.LastGroup);
        Assert.Equal(1, result.LastIndex);
    }

    [Fact]
    public void PreCancellationPrecedesInvalidByteDecodingAndArguments()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => Utf8PythonRegex.Compile(new byte[] { 0xff },
            PythonReCompileOptions.Locale, TimeSpan.Zero, new() { MaxWorkSteps = 0 },
            new() { MaxBytes = 0 }, TimeSpan.Zero, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void FundedFourControlPolicyAndConstructionTokenEndAtPublication()
    {
        using var cancellation = new CancellationTokenSource();
        var regex = Utf8PythonRegex.Compile("(?P<x>😀){2,4}"u8, PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, new() { MaxWorkSteps = 100000, MaxDepth = 64,
                MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 },
            new() { MaxWorkSteps = 1000000, MaxDepth = 128,
                MaxInstructionCapacity = 10000, MaxBytes = 1000000 },
            TimeSpan.FromSeconds(30), cancellation.Token);
        cancellation.Cancel();
        var result = regex.SearchDetailedData("😀😀"u8);
        Assert.True(result.Success);
        Assert.Equal("x", result.LastGroup);
        Assert.Equal(1, result.LastIndex);
        Assert.True(regex.FullMatch("😀😀"u8).Success); // Later lazy preparation has its own backstop.
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
        var error = Assert.Throws<OperationCanceledException>(() => Utf8PythonRegex.Compile("(?P<x>😀){2,4}"u8,
            PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited,
            Utf8ConstructionPolicy.Default, TimeSpan.FromSeconds(30), cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(8, reads);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeadlineUsesOneEagerAuthorityAndLeavesNoActiveOwner(bool bytes)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            if (bytes) _ = Utf8PythonRegex.Compile("😀+"u8, PythonReCompileOptions.None,
                Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
                TimeSpan.FromMilliseconds(1), CancellationToken.None);
            else _ = Utf8PythonRegex.Compile("😀+", PythonReCompileOptions.None,
                Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
                TimeSpan.FromMilliseconds(1), CancellationToken.None);
        });
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        Assert.Equal(bytes ? string.Empty : "😀+", error.Pattern);
        Assert.False(Utf8ConstructionScope.IsActive);
    }
}
