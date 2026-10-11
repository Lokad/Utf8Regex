using System.Text;
using System.Text.RegularExpressions;
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
    [InlineData(100ul, 10u, null, null)]
    [InlineData(null, 10u, null, null)]
    [InlineData(null, null, 1024ul, null)]
    [InlineData(null, 10u, null, 1024ul)]
    public void FiniteBudgetsAreNotSupportedByCore(ulong? work, uint? depth, ulong? workingBytes, ulong? resultBytes)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = work, MaxDepth = depth, MaxWorkingBytes = workingBytes, MaxResultBytes = resultBytes };
        Assert.Throws<System.NotSupportedException>(() => new Utf8Regex("cat", System.Text.RegularExpressions.RegexOptions.None, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout, policy));
    }
    [Fact]
    public void FinitePolicyIsRejectedBeforePatternCompilation()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100ul, MaxDepth = 10u };
        Assert.Throws<System.NotSupportedException>(() => new Utf8Regex("[invalid", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, policy));
    }

    [Fact]
    public void ZeroPolicyIsRejectedBeforePatternCompilation()
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = 0u };
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("[invalid", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, policy));
    }

    [Fact]
    public void DefaultConstructionReportsUnlimitedPolicy()
    {
        // R06 compatibility decision: defaults stay unlimited (BCL-compatible);
        // execution work budgets are opt-in via the policy overload.
        Assert.Equal(Utf8ResourcePolicy.Unlimited, new Utf8Regex("cat", RegexOptions.CultureInvariant).ResourcePolicy);
        Assert.Equal(Utf8ResourcePolicy.Unlimited, new Utf8Regex("cat", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).ResourcePolicy);
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
        var baseline = new Utf8Regex(pattern, options);
        var explicitUnlimited = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited);

        Assert.Equal(baseline.IsMatch(input), explicitUnlimited.IsMatch(input));
        var expectedMatch = baseline.Match(input);
        var actualMatch = explicitUnlimited.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.IndexInUtf16, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.LengthInUtf16, actualMatch.LengthInUtf16);
        Assert.Equal(baseline.Count(input), explicitUnlimited.Count(input));
        Assert.Equal(baseline.Replace(input, "Z"), explicitUnlimited.Replace(input, "Z"));

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
