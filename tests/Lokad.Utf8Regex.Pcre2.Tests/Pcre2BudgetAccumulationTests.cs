using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Finite work spans the public call and all advances. These fixture limits
// include byte-start admission, discovery and byte-result publication.
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
    public void CountSucceedsWithEnoughAdmittedWork()
    {
        Assert.Equal(3, Create(7).Count(Encoding.UTF8.GetBytes(InputText)));
    }

    [Fact]
    public void CountBreachesWhenLimitIsOneShort()
    {
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(6).Count(Encoding.UTF8.GetBytes(InputText)));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }

    [Fact]
    public void EnumerationYieldsExactlyTheBudgetedMatchesThenBreaches()
    {
        // The stack-only enumerator cannot be captured by a lambda, so the
        // breach is collected with the same try/while idiom as the limit
        // enforcement battery.
        var enumerator = Create(5).EnumerateMatches(Encoding.UTF8.GetBytes(InputText));
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
    public void EnumerationExhaustsCleanlyWithEnoughAdmittedWork()
    {
        var enumerator = Create(7).EnumerateMatches(Encoding.UTF8.GetBytes(InputText));
        var yielded = 0;
        while (enumerator.MoveNext())
        {
            yielded++;
        }

        Assert.Equal(3, yielded);
    }
}
