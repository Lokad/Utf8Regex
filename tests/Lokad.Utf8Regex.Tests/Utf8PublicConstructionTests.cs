using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PublicConstructionTests
{
    private static Utf8Regex Compile(Utf8ConstructionPolicy policy, CancellationToken token = default)
        => Utf8Regex.Compile("(?<x>😀){2,4}", RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout, new Utf8ResourcePolicy { MaxWorkSteps = 100000 },
            policy, Regex.InfiniteMatchTimeout, token);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EachFiniteConstructionLimitDeniesAndFreshCallRecovers(int limit)
    {
        var policy = limit switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 1 },
            1 => new Utf8ConstructionPolicy { MaxDepth = 1 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 },
        };
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Compile(policy));
        Assert.Equal(limit switch
        {
            0 => Utf8ResourceLimitKind.ConstructionWorkSteps,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            2 => Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            _ => Utf8ResourceLimitKind.ConstructionBytes,
        }, failure.ResourceKind);
        Assert.Equal(1ul, failure.Limit);
        Assert.True(failure.Requested > 1);
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.True(Compile(Utf8ConstructionPolicy.Default).IsMatch("😀😀"u8));
    }

    [Fact]
    public void FundedConstructionPreservesCapturesAndIndependentExecutionPolicy()
    {
        var regex = Compile(new() { MaxWorkSteps = 1000000, MaxDepth = 128,
            MaxInstructionCapacity = 10000, MaxBytes = 1000000 });
        var match = regex.Match("x😀😀y"u8);
        Assert.True(match.Success);
        Assert.Equal(1, match.IndexInBytes);
        Assert.Equal(8, match.LengthInBytes);
        Assert.Equal(100000ul, regex.ResourcePolicy.MaxWorkSteps);
    }

    [Fact]
    public void PreCancellationPrecedesAllArgumentAndPatternFailures()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => Utf8Regex.Compile("(",
            (RegexOptions)(-1), TimeSpan.Zero, new() { MaxWorkSteps = 0 },
            new() { MaxBytes = 0 }, TimeSpan.Zero, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void InFlightCancellationPreservesOriginalTokenAndAllowsFreshCompilation()
    {
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == 8) cancellation.Cancel();
            return 0;
        }))
        {
            var error = Assert.Throws<OperationCanceledException>(() => Utf8Regex.Compile("(?<x>😀){2,4}",
                RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited,
                Utf8ConstructionPolicy.Default, TimeSpan.FromSeconds(30), cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
        }
        Assert.Equal(8, reads);
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.True(Compile(Utf8ConstructionPolicy.Default).IsMatch("😀😀"u8));
    }

    [Fact]
    public void ExplicitLargeLimitsCannotRelaxFixedExpansionBackstop()
    {
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8Regex.Compile("a{1000000000}",
            RegexOptions.NonBacktracking, Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited,
            new() { MaxInstructionCapacity = ulong.MaxValue, MaxBytes = ulong.MaxValue,
                MaxWorkSteps = ulong.MaxValue, MaxDepth = uint.MaxValue },
            Regex.InfiniteMatchTimeout, CancellationToken.None));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, error.ResourceKind);
        Assert.Equal(Utf8ConstructionScope.DefaultInstructionCapacity, error.Limit);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void InvalidArgumentsAndSyntaxLeaveNoConstructionOwner()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => Utf8Regex.Compile("(", RegexOptions.None,
            Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
            TimeSpan.Zero, CancellationToken.None));
        Assert.Equal("constructionTimeout", error.ParamName);
        Assert.Throws<ArgumentException>(() => Utf8Regex.Compile("(", RegexOptions.None,
            Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
            Regex.InfiniteMatchTimeout, CancellationToken.None));
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void DeadlineSpansConstructionAndDoesNotPublishAnExpiredExpression()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => Utf8Regex.Compile("😀+",
            RegexOptions.None, Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited,
            Utf8ConstructionPolicy.Default, TimeSpan.FromMilliseconds(1), CancellationToken.None));
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        Assert.Equal("😀+", error.Pattern);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ZeroConstructionBudgetsAreInvalidBeforeParsing(int limit)
    {
        var policy = limit switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 0 },
            1 => new Utf8ConstructionPolicy { MaxDepth = 0 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 0 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 0 },
        };
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => Compile(policy));
        Assert.Equal("constructionPolicy", error.ParamName);
        Assert.False(Utf8ConstructionScope.IsActive);
    }
}
