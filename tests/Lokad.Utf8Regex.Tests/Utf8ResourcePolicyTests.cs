using System.Text;
using System.Text.RegularExpressions;
namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ResourcePolicyTests
{
    [Fact]
    public void NullableConstructorDefaultInheritsTimeoutButTypedZeroIsRejected()
    {
        var inherited = new Utf8Regex("a", options: System.Text.RegularExpressions.RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = default
        });
        Assert.Equal(Utf8Regex.DefaultMatchTimeout, inherited.MatchTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("a", options: System.Text.RegularExpressions.RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = default(TimeSpan)
        }));
    }

    [Fact]
    public void UnlimitedHasNoBudgets()
    {
        Assert.Null(Utf8ResourcePolicy.Unlimited.MaxDepth);
        Assert.Null(Utf8ResourcePolicy.Unlimited.MaxWorkingBytes);
        Assert.Null(Utf8ResourcePolicy.Unlimited.MaxResultBytes);
    }

    [Theory]
    [InlineData(0u, null, null)]
    [InlineData(null, 0ul, null)]
    [InlineData(null, null, 0ul)]
    public void ZeroBudgetsAreRejected(uint? depth, ulong? workingBytes, ulong? resultBytes)
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = depth, MaxWorkingBytes = workingBytes, MaxResultBytes = resultBytes };
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("cat", options: System.Text.RegularExpressions.RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        }));
    }

    [Fact]
    public void UnlimitedPolicyConstructsAndRoundTrips()
    {
        var regex = new Utf8Regex("cat", options: System.Text.RegularExpressions.RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited
        });
        Assert.Equal(Utf8ResourcePolicy.Unlimited, regex.ResourcePolicy);
        Assert.True(regex.IsMatch("cat"u8));
    }

    [Theory]
    [InlineData(10u, null, null)]
    [InlineData(null, 1024ul, null)]
    [InlineData(10u, null, 1024ul)]
    public void FiniteBudgetsConstructAndExecute(uint? depth, ulong? workingBytes, ulong? resultBytes)
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = depth, MaxWorkingBytes = workingBytes, MaxResultBytes = resultBytes };
        var regex = new Utf8Regex("cat", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        Assert.Equal(policy, regex.ResourcePolicy); Assert.True(regex.IsMatch("cat"u8));
    }
    [Fact]
    public void ValidFinitePolicyPreservesPatternSyntaxFailure()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = 1024ul };
        Assert.Throws<ArgumentException>(() => new Utf8Regex("[invalid", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        }));
    }

    [Fact]
    public void ZeroPolicyIsRejectedBeforePatternCompilation()
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = 0u };
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("[invalid", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        }));
    }

    [Fact]
    public void DefaultConstructionReportsUnlimitedPolicy()
    {
        // R06 compatibility decision: legacy defaults keep unlimited invocation limits;
        // execution work budgets are opt-in via the policy overload.
        Assert.Equal(Utf8ResourcePolicy.Unlimited, new Utf8Regex("cat", options: RegexOptions.CultureInvariant).ResourcePolicy);
        Assert.Equal(Utf8ResourcePolicy.Unlimited, new Utf8Regex("cat", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout
        }).ResourcePolicy);
    }

    [Theory]
    [InlineData("cat", "xxcatxx")]
    [InlineData("[a-z]+", "xxabcxx")]
    public void DefaultMatchesExplicitUnlimitedAcrossOperations(string pattern, string inputText)
    {
        // R06 compatibility evidence: default construction behaves exactly like
        // an explicit Unlimited policy across value, global, and shaping ops,
        // on both an owned literal route and an owned scalar loop route.
        var options = RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var baseline = new Utf8Regex(pattern, options: options);
        var explicitUnlimited = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited
        });

        Assert.Equal(baseline.IsMatch(input), explicitUnlimited.IsMatch(input));
        var expectedMatch = baseline.Match(input);
        var actualMatch = explicitUnlimited.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.StartOffsetInBytes, actualMatch.StartOffsetInBytes);
        Assert.Equal((expectedMatch.EndOffsetInBytes - expectedMatch.StartOffsetInBytes), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        Assert.Equal(baseline.Count(input), explicitUnlimited.Count(input));
        Assert.Equal(baseline.Replace(input, "Z"u8), explicitUnlimited.Replace(input, "Z"u8));

        var expectedMatches = 0;
        foreach (var item in baseline.EnumerateMatches(input))
        {
            expectedMatches++;
        }

        var actualMatches = 0;
        foreach (var item in explicitUnlimited.EnumerateMatches(input))
        {
            actualMatches++;
        }

        Assert.Equal(expectedMatches, actualMatches);

        var expectedSplits = 0;
        foreach (var item in baseline.EnumerateSplits(input))
        {
            expectedSplits++;
        }

        var actualSplits = 0;
        foreach (var item in explicitUnlimited.EnumerateSplits(input))
        {
            actualSplits++;
        }

        Assert.Equal(expectedSplits, actualSplits);
    }
}
