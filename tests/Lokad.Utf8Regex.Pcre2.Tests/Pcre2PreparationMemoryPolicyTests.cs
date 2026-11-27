using System.Reflection;
using System.Text;
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
        var regex = new Utf8Pcre2Regex("(?<x>a)b", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 64
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() => regex.Replace("ab"u8, System.Text.Encoding.UTF8.GetBytes(template), 0, options)).ErrorKind);
        Assert.Equal(0, regex.DebugReplacementCacheEntryCount);
        Assert.NotEmpty(new Utf8Pcre2Regex("(?<x>a)b").Replace("ab"u8, System.Text.Encoding.UTF8.GetBytes(template), 0, options));
    }

    [Theory] [MemberData(nameof(Templates))]
    public void PreparationPeakStaysWithInvocationAndNestedJoinsDoNotRefund(string template, Pcre2SubstitutionOptions options)
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)b");
        var get = Preparation(regex);
        var templateUtf8 = Encoding.UTF8.GetBytes(template);
        var invocation = new Utf8SliceInvocation(new(256, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        using (var control = Pcre2ReplacementPreparationControl.Borrow(invocation, default))
        {
            var retained = get(templateUtf8, options);
            var admitted = invocation.Ledger.ReservedBytes;
            Assert.True(admitted > 13);
            using (var joined = Pcre2ReplacementPreparationControl.JoinOrEnter(Timeout.InfiniteTimeSpan))
                Assert.Equal(retained, get(templateUtf8, options));
            Assert.Equal(admitted, invocation.Ledger.ReservedBytes);
        }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Theory] [MemberData(nameof(Templates))]
    public void WarmPlanRemainsValidWhenOutputIsDenied(string template, Pcre2SubstitutionOptions options)
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)b", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 64
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var get = Preparation(regex);
        var templateUtf8 = Encoding.UTF8.GetBytes(template);
        var retained = get(templateUtf8, options);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() => regex.Replace("ab"u8, templateUtf8, 0, options)).ErrorKind);
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
        Assert.Equal(retained, get(templateUtf8, options));
    }

    internal static Func<ReadOnlySpan<byte>, Pcre2SubstitutionOptions, Pcre2ReplacementPreparation> Preparation(Utf8Pcre2Regex regex)
        => typeof(Utf8Pcre2Regex).GetMethod("GetTemplatePreparation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ReadOnlySpan<byte>, Pcre2SubstitutionOptions, Pcre2ReplacementPreparation>>(regex);

    // Output-only boundary fixtures deliberately borrow a previously prepared
    // immutable plan; cold preparation admission has separate public tests here.
    internal static void Warm(Utf8Pcre2Regex regex, string template)
        => _ = Preparation(regex)(Encoding.UTF8.GetBytes(template), Pcre2SubstitutionOptions.None);
}
