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
        var regex = new Utf8Pcre2Regex("^(?&left)(?&right)(?(DEFINE)(?<left>a|x)(?<right>b|y))$");
        ReadOnlySpan<byte> match1Input = "ab"u8;
        var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var match = regex.Match(match1Input, captures: match1Captures);

        Assert.True(match.Success);
        // A returned subroutine restores caller captures, including unset slots.
        Assert.False(match1Captures[1].Success);
        Assert.False(match1Captures[2].Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
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
        var regex = new Utf8Pcre2Regex("^(?J)(?:(?<x>a)|(?<x>b))(?(x)c|d)$");

        Assert.True(regex.IsMatch("ac"u8));
        Assert.True(regex.IsMatch("bc"u8));
        Assert.False(regex.IsMatch("ad"u8));
    }

    [Fact]
    public void AssertionConditionCapturesFlowThroughEitherDecision()
    {
        var positive2Regex = new Utf8Pcre2Regex("^(?(?=(a))abc|def)");
        ReadOnlySpan<byte> positive2Input = "abc"u8;
        var positive2Captures = new Utf8Pcre2CaptureRange[positive2Regex.CaptureGroupCount + 1];
        var positive = positive2Regex.Match(positive2Input, captures: positive2Captures);
        var negative3Regex = new Utf8Pcre2Regex("^(?(?!(a))def|abc)");
        ReadOnlySpan<byte> negative3Input = "abc"u8;
        var negative3Captures = new Utf8Pcre2CaptureRange[negative3Regex.CaptureGroupCount + 1];
        var negative = negative3Regex.Match(negative3Input, captures: negative3Captures);

        Assert.True(positive.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((positive2Captures[1].Success ? positive2Input.Slice(positive2Captures[1].StartOffsetInBytes, positive2Captures[1].EndOffsetInBytes - positive2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.True(negative.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((negative3Captures[1].Success ? negative3Input.Slice(negative3Captures[1].StartOffsetInBytes, negative3Captures[1].EndOffsetInBytes - negative3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
        Assert.Equal("[aabb] [ab]", Encoding.UTF8.GetString(regex.Replace("aabb ab"u8, "[${pair}]"u8)));
    }

    [Fact]
    public void RecursiveCallsChargeInvocationLimitsAndRemainConcurrent()
    {
        var limited = new Utf8Pcre2Regex("^(?<r>a(?&r)?b)$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 2
            },
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });
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
