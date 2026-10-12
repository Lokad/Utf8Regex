using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R06/R07 work-budget accumulation: on the native literal path exactly one
// candidate plus two projected endpoints are charged per yielded match; the budget spans the whole public
// call (never resets per candidate, per match, or per MoveNext), and the
// terminal miss probe charges nothing.
public sealed class Pcre2BudgetAccumulationTests
{
    // Routes every operation through the native literal cursor (verified via
    // DebugDescribeExecutionPlan during authoring).
    private const string Pattern = "(?#comment)ab";
    private const string InputText = "xxab xxab xxab";

    private static Utf8Pcre2Regex Create(uint matchLimit)
    {
        return new Utf8Pcre2Regex(Pattern, Pcre2CompileOptions.None, default, new Utf8Pcre2ExecutionLimits { MatchLimit = matchLimit }, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Fact]
    public void CountSucceedsWhenLimitEqualsProjectedMatchWork()
    {
        Assert.Equal(3, Create(9).Count(Encoding.UTF8.GetBytes(InputText)));
    }

    [Fact]
    public void CountBreachesWhenLimitIsOneShort()
    {
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(8).Count(Encoding.UTF8.GetBytes(InputText)));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }

    [Fact]
    public void EnumerationYieldsExactlyTheBudgetedMatchesThenBreaches()
    {
        // The stack-only enumerator cannot be captured by a lambda, so the
        // breach is collected with the same try/while idiom as the limit
        // enforcement battery.
        var enumerator = Create(6).EnumerateMatches(Encoding.UTF8.GetBytes(InputText));
        var yielded = 0;
        Pcre2MatchException? caught = null;
        try
        {
            while (enumerator.MoveNext())
            {
                yielded++;
            }
        }
        catch (Pcre2MatchException exception)
        {
            caught = exception;
        }

        Assert.Equal(2, yielded);
        Assert.NotNull(caught);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, caught.ErrorKind);
    }

    [Fact]
    public void EnumerationExhaustsCleanlyWhenLimitEqualsProjectedMatchWork()
    {
        var enumerator = Create(9).EnumerateMatches(Encoding.UTF8.GetBytes(InputText));
        var yielded = 0;
        while (enumerator.MoveNext())
        {
            yielded++;
        }

        Assert.Equal(3, yielded);
    }
}
