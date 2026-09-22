using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ResourcePolicyTests
{
    [Fact]
    public void PolicyMapsExactlyOntoLegacyLimits()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1ul };
        var viaPolicy = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, policy, Utf8Pcre2Regex.DefaultMatchTimeout);
        var viaLegacy = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }, Utf8Pcre2Regex.DefaultMatchTimeout);
        var input = Encoding.UTF8.GetBytes("foo foo foo");
        Assert.Equal(3, new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout).Count(input));
        var policyError = Assert.Throws<Pcre2MatchException>(() => viaPolicy.Count(input));
        var legacyError = Assert.Throws<Pcre2MatchException>(() => viaLegacy.Count(input));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, policyError.ErrorKind);
        Assert.Equal(legacyError.ErrorKind, policyError.ErrorKind);
    }

    [Fact]
    public void OverflowingWorkStepsAreRejected()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = (ulong)uint.MaxValue + 1ul };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, policy, Utf8Pcre2Regex.DefaultMatchTimeout));
    }

    [Fact]
    public void ZeroBudgetIsRejected()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = 0ul };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, policy, Utf8Pcre2Regex.DefaultMatchTimeout));
    }

    [Fact]
    public void ResultBytesBudgetIsReserved()
    {
        var policy = new Utf8ResourcePolicy { MaxResultBytes = 1024ul };
        Assert.Throws<System.NotSupportedException>(() => new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, policy, Utf8Pcre2Regex.DefaultMatchTimeout));
    }

    [Fact]
    public void UnlimitedPolicyConstructsBothFlavors()
    {
        var fromText = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, Utf8ResourcePolicy.Unlimited, Utf8Pcre2Regex.DefaultMatchTimeout);
        var fromBytes = new Utf8Pcre2Regex("foo"u8, Pcre2CompileOptions.None, default, Utf8ResourcePolicy.Unlimited, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(3, fromText.Count(Encoding.UTF8.GetBytes("foo foo foo")));
        Assert.Equal(3, fromBytes.Count(Encoding.UTF8.GetBytes("foo foo foo")));
    }
    [Theory]
    [InlineData("(a+)+b", "aaab")]
    [InlineData("(a+)+b", "aaaa")]
    [InlineData("[ab]+", "ab ab ab")]
    public void LegacyZeroLimitsMatchUnlimitedPolicy(string pattern, string inputText)
    {
        // R06 compatibility evidence: legacy all-zero limits behave exactly like
        // an explicit Unlimited policy, on a backtracking shape with a hit, a
        // miss, and a multi-match input.
        var input = Encoding.UTF8.GetBytes(inputText);
        var baseline = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var explicitUnlimited = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, Utf8ResourcePolicy.Unlimited, Utf8Pcre2Regex.DefaultMatchTimeout);

        Assert.Equal(baseline.IsMatch(input), explicitUnlimited.IsMatch(input));
        Assert.Equal(baseline.Count(input), explicitUnlimited.Count(input));

        var expectedMatches = 0;
        var expectedEnumerator = baseline.EnumerateMatches(input);
        while (expectedEnumerator.MoveNext())
        {
            expectedMatches++;
        }

        var actualMatches = 0;
        var actualEnumerator = explicitUnlimited.EnumerateMatches(input);
        while (actualEnumerator.MoveNext())
        {
            actualMatches++;
        }

        Assert.Equal(expectedMatches, actualMatches);
    }
}
