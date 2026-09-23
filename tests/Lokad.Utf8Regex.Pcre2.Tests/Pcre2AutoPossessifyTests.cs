using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Auto-possessification soundness (native pcre2_auto_possess.c analog): a
// greedy single-token repeat followed by a disjoint plain literal may run
// atomically, but overlapping classes, nullable suffixes, and end-of-pattern
// must decline. Engagement is proven by the lowered-program counter; behavior
// is proven by BCL parity on shapes valid in both syntaxes. See the R08 pcre2
// disposition in PLAN.md.
public sealed class Pcre2AutoPossessifyTests
{
    private static void AssertAutoPossessCount(string pattern, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(expected, program.Program.AutoPossessiveRepeatCount);
    }

    private static void AssertMatchesBcl(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        var expected = oracle.Match(inputText);
        var actual = regex.Match(input);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Value, actual.GetValueString());
        }

        Assert.Equal(oracle.Count(inputText), regex.Count(input));
    }

    [Theory]
    [InlineData(@"\d+b", "123b")]
    [InlineData(@"\d+b", "123c")]
    [InlineData(@"\d+bc", "12bc")]
    [InlineData(@"\d+bc", "12bd")]
    public void EngagedShapesMatchBcl(string pattern, string inputText)
    {
        AssertAutoPossessCount(pattern, 1);
        AssertMatchesBcl(pattern, inputText);
    }

    [Theory]
    [InlineData("[ab]+b", "abb")]
    [InlineData("[ab]+b", "aba")]
    [InlineData("a+b?", "aab")]
    [InlineData("a+b?", "aa")]
    [InlineData("a+", "aaa")]
    [InlineData("a+", "b")]
    public void DeclinedShapesMatchBcl(string pattern, string inputText)
    {
        AssertMatchesBcl(pattern, inputText);
    }

    [Theory]
    [InlineData("[ab]+b")]
    public void OverlappingOrNullableShapesDecline(string pattern)
    {
        AssertAutoPossessCount(pattern, 0);
    }
}
