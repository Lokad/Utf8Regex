using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2RecursiveControlFlowCompilerTests
{
    [Theory]
    [InlineData("(abc)(?1)", "abcabc", true)]
    [InlineData("(a)(?-1)", "aa", true)]
    [InlineData("(a)(?+1)(b)", "abb", true)]
    [InlineData("(?<word>ab)(?&word)", "abab", true)]
    [InlineData("(?<word>ab)(?P>word)", "abab", true)]
    [InlineData("(?|(abc)|(xyz))(?1)", "xyzabc", true)]
    [InlineData("(?|(abc)|(xyz))(?1)", "xyzxyz", false)]
    public void NumberedRelativeAndNamedCallsUseTheGenericProgram(
        string pattern,
        string input,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("aba", true)]
    [InlineData("abcba", true)]
    [InlineData("abca", false)]
    public void WholePatternRecursionBacktracksWithInvocationLocalCaptures(string input, bool expected)
    {
        var regex = new Utf8Pcre2Regex("^(.|(.)(?1)\\2)$");

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void DefineBlocksCanHoldMultipleForwardReferencedDefinitions()
    {
        var regex = new Utf8Pcre2Regex(
            "^(?&left)(?&right)(?(DEFINE)(?<left>a|x)(?<right>b|y))$");

        var match = regex.MatchDetailed("ab"u8);

        Assert.True(match.Success);
        Assert.Equal("a", match.GetGroup(1).GetValueString());
        Assert.Equal("b", match.GetGroup(2).GetValueString());
        Assert.False(regex.IsMatch("ax"u8));
    }

    [Theory]
    [InlineData("ab", true)]
    [InlineData("c", true)]
    [InlineData("ac", false)]
    [InlineData("b", false)]
    public void CaptureSetConditionSelectsExactlyOneBranch(string input, bool expected)
    {
        var regex = new Utf8Pcre2Regex("^(a)?(?(1)b|c)$");

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void NamedDuplicateCaptureConditionUsesTheFirstSetSlot()
    {
        var regex = new Utf8Pcre2Regex(
            "^(?J)(?:(?<x>a)|(?<x>b))(?(x)c|d)$");

        Assert.True(regex.IsMatch("ac"u8));
        Assert.True(regex.IsMatch("bc"u8));
        Assert.False(regex.IsMatch("ad"u8));
    }

    [Fact]
    public void AssertionConditionCapturesFlowThroughEitherDecision()
    {
        var positive = new Utf8Pcre2Regex("^(?(?=(a))abc|def)").MatchDetailed("abc"u8);
        var negative = new Utf8Pcre2Regex("^(?(?!(a))def|abc)").MatchDetailed("abc"u8);

        Assert.True(positive.Success);
        Assert.Equal("a", positive.GetGroup(1).GetValueString());
        Assert.True(negative.Success);
        Assert.Equal("a", negative.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData("b", true)]
    [InlineData("ba", true)]
    [InlineData("baaa", true)]
    [InlineData("a", false)]
    public void RecursionConditionsObserveTheActiveSubroutineStack(string input, bool expected)
    {
        var regex = new Utf8Pcre2Regex("^(?<r>(?(R&r)a|b)(?&r)?)$");

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void RecursiveCapturesDriveReplacementAndGlobalIteration()
    {
        var regex = new Utf8Pcre2Regex("(?<pair>a(?&pair)?b)");

        Assert.Equal(2, regex.Count("aabb ab"u8));
        Assert.Equal("[aabb] [ab]", Encoding.UTF8.GetString(regex.Replace("aabb ab"u8, "[${pair}]")));
    }

    [Fact]
    public void RecursiveCallsChargeInvocationLimitsAndRemainConcurrent()
    {
        var limited = new Utf8Pcre2Regex(
            "^(?<r>a(?&r)?b)$",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { DepthLimit = 2 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        Assert.Equal(
            Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("aaabbb"u8)).ErrorKind);

        var regex = new Utf8Pcre2Regex("^(?<r>a(?&r)?b)$");
        Parallel.For(0, 256, index =>
        {
            var input = index % 2 == 0 ? "ab"u8 : "aaabbb"u8;
            Assert.True(regex.IsMatch(input));
        });
    }
}
