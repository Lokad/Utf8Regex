using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R01 source-compatibility evidence: the limits constructor overloads must keep
// accepting legacy call shapes (positional/named bare `default`), while
// policy-based construction lives on the unambiguous Create factories.
// These tests fail to compile if the CS0121 ambiguity returns.
public sealed class Pcre2ConstructorCompatibilityTests
{
    [Fact]
    public void BareDefaultLimitsOverloadBinds()
    {
        var regex = new Utf8Pcre2Regex("ab", Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal("ab", regex.Pattern);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxab")));
    }

    [Fact]
    public void BareDefaultSpanLimitsOverloadBinds()
    {
        var regex = new Utf8Pcre2Regex("ab"u8, Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal("ab", regex.Pattern);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxab")));
    }

    [Fact]
    public void NamedArgumentsWithDefaultsBindLimitsOverload()
    {
        var regex = new Utf8Pcre2Regex(
            pattern: "ab",
            options: Pcre2CompileOptions.None,
            compileSettings: default,
            defaultExecutionLimits: default,
            matchTimeout: Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxab")));
    }

    [Fact]
    public void PolicyFactoryAcceptsBareDefaultPolicy()
    {
        var regex = Utf8Pcre2Regex.Create("ab", Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes("ab")));
    }

    [Fact]
    public void PolicySpanFactoryAcceptsBareDefaultPolicy()
    {
        var regex = Utf8Pcre2Regex.Create("ab"u8, Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes("ab")));
    }

    [Fact]
    public void PolicyFactoriesConvertToMethodGroups()
    {
        Func<string, Pcre2CompileOptions, Utf8Pcre2CompileSettings, Utf8ResourcePolicy, TimeSpan, Utf8Pcre2Regex> fromText = Utf8Pcre2Regex.Create;
        Func<ReadOnlySpan<byte>, Pcre2CompileOptions, Utf8Pcre2CompileSettings, Utf8ResourcePolicy, TimeSpan, Utf8Pcre2Regex> fromBytes = Utf8Pcre2Regex.Create;
        Assert.True(fromText("ab", Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout).IsMatch("ab"u8));
        Assert.True(fromBytes("ab"u8, Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout).IsMatch("ab"u8));
    }

    [Fact]
    public void UnlimitedFactoryMatchesDefaultConstruction()
    {
        var baseline = new Utf8Pcre2Regex("(a+)+b", Pcre2CompileOptions.None);
        var viaFactory = Utf8Pcre2Regex.Create("(a+)+b", Pcre2CompileOptions.None, default, Utf8ResourcePolicy.Unlimited, Utf8Pcre2Regex.DefaultMatchTimeout);
        var hit = Encoding.UTF8.GetBytes("aaab");
        var miss = Encoding.UTF8.GetBytes("aaaa");
        Assert.Equal(baseline.IsMatch(hit), viaFactory.IsMatch(hit));
        Assert.Equal(baseline.IsMatch(miss), viaFactory.IsMatch(miss));
        Assert.Equal(baseline.Count(hit), viaFactory.Count(hit));
    }

    [Fact]
    public void SpanFactoryValidatesPolicyBeforeDecodingPattern()
    {
        var zero = new Utf8ResourcePolicy { MaxDepth = 0u };
        var invalidBytes = new byte[] { 0x66, 0x6F, 0x6F, 0xFF };
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8Pcre2Regex.Create(invalidBytes, Pcre2CompileOptions.None, default, zero, Utf8Pcre2Regex.DefaultMatchTimeout));
        Assert.Throws<ArgumentException>(() => Utf8Pcre2Regex.Create(invalidBytes, Pcre2CompileOptions.None, default, Utf8ResourcePolicy.Unlimited, Utf8Pcre2Regex.DefaultMatchTimeout));
    }
}
