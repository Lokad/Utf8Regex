namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ResourcePolicyTests
{
    [Fact]
    public void UnlimitedHasNoBudgets()
    {
        Assert.Null(Utf8ResourcePolicy.Unlimited.MaxWorkSteps);
        Assert.Null(Utf8ResourcePolicy.Unlimited.MaxDepth);
        Assert.Null(Utf8ResourcePolicy.Unlimited.MaxWorkingBytes);
        Assert.Null(Utf8ResourcePolicy.Unlimited.MaxResultBytes);
    }

    [Theory]
    [InlineData(0ul, null, null, null)]
    [InlineData(null, 0u, null, null)]
    [InlineData(null, null, 0ul, null)]
    [InlineData(null, null, null, 0ul)]
    public void ZeroBudgetsAreRejected(ulong? work, uint? depth, ulong? workingBytes, ulong? resultBytes)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = work, MaxDepth = depth, MaxWorkingBytes = workingBytes, MaxResultBytes = resultBytes };
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("cat", System.Text.RegularExpressions.RegexOptions.None, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout, policy));
    }

    [Fact]
    public void UnlimitedPolicyConstructsAndRoundTrips()
    {
        var regex = new Utf8Regex("cat", System.Text.RegularExpressions.RegexOptions.None, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited);
        Assert.Equal(Utf8ResourcePolicy.Unlimited, regex.ResourcePolicy);
        Assert.True(regex.IsMatch("cat"u8));
    }

    [Theory]
    [InlineData(100ul, null, null, null)]
    [InlineData(null, 10u, null, null)]
    [InlineData(null, null, 1024ul, null)]
    [InlineData(null, null, null, 1024ul)]
    public void FiniteBudgetsAreNotSupportedByCore(ulong? work, uint? depth, ulong? workingBytes, ulong? resultBytes)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = work, MaxDepth = depth, MaxWorkingBytes = workingBytes, MaxResultBytes = resultBytes };
        Assert.Throws<System.NotSupportedException>(() => new Utf8Regex("cat", System.Text.RegularExpressions.RegexOptions.None, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout, policy));
    }
}
