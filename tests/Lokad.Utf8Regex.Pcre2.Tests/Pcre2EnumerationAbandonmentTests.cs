using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R06/R07 lazy-access cleanup: abandoning a lazy enumeration early, including
// by limit breach, must leave the instance fully reusable.
public sealed class Pcre2EnumerationAbandonmentTests
{
    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Fact]
    public void AbandonedEnumerationLeavesInstanceReusable()
    {
        var input = Encoding.UTF8.GetBytes("aaab aaab");
        Assert.Equal(2, Create("(a+)+b", default).Count(input));

        var reused = Create("(a+)+b", default);
        var enumerator = reused.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());

        Assert.Equal(2, reused.Count(input));
    }

    [Fact]
    public void BreachingEnumerationLeavesInstanceDeterministic()
    {
        // MatchLimit counts candidates, so even a single match can breach a
        // limit of one: reuse after breach must fail deterministically by
        // policy (same kind), never by cursor corruption.
        var input = Encoding.UTF8.GetBytes("aaab aaab");
        var regex = Create("(a+)+b", new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });

        // The stack-only enumerator cannot be captured by a lambda, so the
        // breach is collected with the same try/while idiom as the limit
        // enforcement battery.

        Pcre2MatchException? caught = null;
        try
        {
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext())
            {
            }
        }
        catch (Pcre2MatchException exception)
        {
            caught = exception;
        }

        Assert.NotNull(caught);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, caught.ErrorKind);

        var repeat = Assert.Throws<Pcre2MatchException>(() => regex.Count(input));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, repeat.ErrorKind);
    }
}
