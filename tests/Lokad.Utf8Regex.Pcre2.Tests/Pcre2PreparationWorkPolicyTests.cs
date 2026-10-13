using System.Reflection;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PreparationWorkPolicyTests
{
    public static IEnumerable<object[]> Templates()
    {
        yield return ["Z", Pcre2SubstitutionOptions.None, "Z"];
        yield return [new string('Z', 4096), Pcre2SubstitutionOptions.None, new string('Z', 4096)];
        yield return ["${x}", Pcre2SubstitutionOptions.None, "a"];
        yield return ["${x:+Z:${x:-Q}}", Pcre2SubstitutionOptions.Extended, "Z"];
    }

    [Theory] [MemberData(nameof(Templates))]
    public void ColdWorkDenialDoesNotPublishPreparation(string template, Pcre2SubstitutionOptions options, string expected)
    {
        var denied = Create(3);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
            denied.Replace("ab"u8, template, 0, options)).ErrorKind);
        Assert.Equal(0, denied.DebugReplacementCacheEntryCount);
        Assert.Equal(expected, Create(100000).ReplaceToString("ab"u8, template, 0, options));
    }

    [Theory] [MemberData(nameof(Templates))]
    public void WarmReuseObservesInvocationWorkAndPreservesCachedValue(string template, Pcre2SubstitutionOptions options, string expected)
    {
        var regex = Create(3);
        var get = typeof(Utf8Pcre2Regex).GetMethod("GetReplacementPreparation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<string, Pcre2SubstitutionOptions, Pcre2ReplacementPreparation>>(regex);
        var retained = get(template, options);
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
            regex.Replace("ab"u8, template, 0, options)).ErrorKind);
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
        Assert.Equal(retained, get(template, options));
        Assert.Equal(expected, Create(100000).ReplaceToString("ab"u8, template, 0, options));
    }

    [Fact]
    public void FixedConstructionBackstopsAndWarmFiniteCallsRemainUsable()
    {
        var regex = Create(100000);
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 1, ulong.MaxValue))
        {
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Replace("ab"u8, "${x}"));
            Assert.Equal(0, regex.DebugReplacementCacheEntryCount);
            Assert.Equal(0UL, owner.Budget.ReservedBytes);
        }
        Assert.Equal("a", regex.ReplaceToString("ab"u8, "${x}"));
        Assert.Equal("a", regex.ReplaceToString("ab"u8, "${x}"));
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
    }

    private static Utf8Pcre2Regex Create(uint work) => new("(?<x>a)b", Pcre2CompileOptions.None, default,
        new() { MatchLimit = work, DepthLimit = 256, HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
}
