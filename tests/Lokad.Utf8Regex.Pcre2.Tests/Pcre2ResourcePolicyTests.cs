using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ResourcePolicyTests
{
    [Fact]
    public void SharedPolicyMapsToPcre2UnitsAndRetainsErrorVocabulary()
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1,
            MaxDepth = 123,
            MaxWorkingBytes = 123456,
            MaxResultBytes = 654321
        };
        using var regex = new Utf8Pcre2Regex("foo", policy: new() { ResourcePolicy = policy });
        Assert.Equal(policy, regex.ResourcePolicy);
        Assert.Equal(1u, regex.DefaultExecutionLimits.MatchLimit);
        Assert.Equal(123u, regex.DefaultExecutionLimits.DepthLimit);
        Assert.Equal(123456ul, regex.DefaultExecutionLimits.HeapLimitInBytes);
        Assert.Equal(654321ul, regex.DefaultExecutionLimits.ResultLimitInBytes);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8)).ErrorKind);
    }

    [Fact]
    public void OverflowingWorkStepsAreRejected()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = (ulong)uint.MaxValue + 1ul };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex("foo", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = policy,
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        }));
    }

    [Fact]
    public void ZeroBudgetIsRejected()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = 0ul };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex("foo", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = policy,
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        }));
    }

    [Fact]
    public void ResultBytesBudgetIsEnforceable()
    {
        var policy = new Utf8ResourcePolicy { MaxResultBytes = 1024ul };
        var regex = new Utf8Pcre2Regex("foo", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = policy,
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(regex.Match("foo"u8).GetValueBytes()));
    }

    [Fact]
    public void UnlimitedPolicyConstructsBothFlavors()
    {
        var fromText = new Utf8Pcre2Regex("foo", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        var fromBytes = new Utf8Pcre2Regex("foo"u8, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Assert.Equal(3, fromText.Count(Encoding.UTF8.GetBytes("foo foo foo")));
        Assert.Equal(3, fromBytes.Count(Encoding.UTF8.GetBytes("foo foo foo")));
    }
}
