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
}
