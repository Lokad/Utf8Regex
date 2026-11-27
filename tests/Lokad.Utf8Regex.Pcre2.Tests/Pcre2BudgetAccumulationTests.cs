using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Finite work spans the public call and all advances. These fixture limits
// include byte-start admission, discovery and operation completion.
public sealed class Pcre2BudgetAccumulationTests
{
    // Count uses prepared literal search. Enumeration publishes each byte range.
    private const string Pattern = "(?#comment)ab";
    private const string InputText = "xxab xxab xxab";

    private static Utf8Pcre2Regex Create()
    {
        return new Utf8Pcre2Regex(Pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 4096
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
    }

    [Fact]
    public void CountSucceedsWithEnoughAdmittedWork()
    {
        // One admission step, three search steps and one completion step.
        Assert.Equal(3, Create().Count(Encoding.UTF8.GetBytes(InputText)));
    }

    [Fact]
    public void EnumerationExhaustsCleanlyWithEnoughAdmittedWork()
    {
        var enumerator = Create().EnumerateMatches(Encoding.UTF8.GetBytes(InputText));
        var yielded = 0;
        while (enumerator.MoveNext())
        {
            yielded++;
        }

        Assert.Equal(3, yielded);
    }
}
