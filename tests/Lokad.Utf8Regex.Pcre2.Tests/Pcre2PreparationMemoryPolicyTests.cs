using System.Reflection;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PreparationMemoryPolicyTests
{
    public static IEnumerable<object[]> Templates()
    {
        yield return ["Z", Pcre2SubstitutionOptions.None];
        yield return [new string('Z', 4096), Pcre2SubstitutionOptions.None];
        yield return ["${x}", Pcre2SubstitutionOptions.None];
        yield return ["${x:+Z:${x:-Q}}", Pcre2SubstitutionOptions.Extended];
    }

    [Theory] [MemberData(nameof(Templates))]
    public void ColdHeapDenialDoesNotPublishPreparation(string template, Pcre2SubstitutionOptions options)
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)b", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, HeapLimitInBytes = 64 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() => regex.Replace("ab"u8, template, 0, options)).ErrorKind);
        Assert.Equal(0, regex.DebugReplacementCacheEntryCount);
        Assert.NotEmpty(new Utf8Pcre2Regex("(?<x>a)b").Replace("ab"u8, template, 0, options));
    }

    [Theory] [MemberData(nameof(Templates))]
    public void PreparationPeakStaysWithInvocationAndNestedJoinsDoNotRefund(string template, Pcre2SubstitutionOptions options)
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)b");
        var get = Preparation(regex);
        var invocation = new Utf8SliceInvocation(new(100000, 256, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        using (var control = Pcre2ReplacementPreparationControl.Borrow(invocation, default))
        {
            var retained = get(template, options);
            var admitted = invocation.Ledger.ReservedBytes;
            Assert.True(admitted > 13);
            using (var joined = Pcre2ReplacementPreparationControl.JoinOrEnter(Timeout.InfiniteTimeSpan))
                Assert.Equal(retained, get(template, options));
            Assert.Equal(admitted, invocation.Ledger.ReservedBytes);
        }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Theory] [MemberData(nameof(Templates))]
    public void WarmPlanRemainsValidWhenOutputIsDenied(string template, Pcre2SubstitutionOptions options)
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)b", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, HeapLimitInBytes = 64 }, Timeout.InfiniteTimeSpan);
        var get = Preparation(regex);
        var retained = get(template, options);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() => regex.Replace("ab"u8, template, 0, options)).ErrorKind);
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
        Assert.Equal(retained, get(template, options));
    }

    private static Func<string, Pcre2SubstitutionOptions, Pcre2ReplacementPreparation> Preparation(Utf8Pcre2Regex regex)
        => typeof(Utf8Pcre2Regex).GetMethod("GetReplacementPreparation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<string, Pcre2SubstitutionOptions, Pcre2ReplacementPreparation>>(regex);

    // Output-only boundary fixtures deliberately borrow a previously prepared
    // immutable plan; cold preparation admission has separate public tests here.
    internal static void Warm(Utf8Pcre2Regex regex, string template)
        => _ = Preparation(regex)(template, Pcre2SubstitutionOptions.None);
}
