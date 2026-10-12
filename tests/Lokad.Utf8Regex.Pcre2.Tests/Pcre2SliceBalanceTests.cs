using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 balancing captures for the PCRE2 profile: native PCRE2 has no
// balancing, so balancing shapes compile with the core grammar. Its owned
// scalar program executes under the profile's original invocation, including
// drained pops, restored/interval captures and construction failures for
// undefined or forward pop targets. BCL comparisons below overlap on ASCII.
public sealed class Pcre2SliceBalanceTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Theory]
    [InlineData("(?<open>a)+(?<-open>b)+")]
    [InlineData("(?<open>a)(?<-open>b)")]
    [InlineData("(?<a>x)(?<b-a>y)")]
    [InlineData("(?<a>x)(?<b-a>y)(?<c-b>z)")]
    public void BalanceShapesDelegateToCore(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex",
            regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?<open>a)+(?<-open>b)+", "aab", true };
        yield return new object?[] { "(?<open>a)+(?<-open>b)+", "bbb", false };
        yield return new object?[] { "(?<open>a)(?<-open>b)", "ab", true };
        yield return new object?[] { "(?<open>a)(?<-open>b)", "ac", false };
        yield return new object?[] { "(?<a>x)(?<b-a>y)", "xy", true };
        yield return new object?[] { "(?<a>x)(?<b-a>y)", "xx", false };
        yield return new object?[] { "(?<a>x)(?<b-a>y)(?<c-b>z)", "xyz", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void DelegatedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<open>a)+(?<-open>b)+", "aab aab", 0, 2)]
    [InlineData("(?<open>a)(?<-open>b)", "ab ab", 0, 2)]
    public void DelegatedCountMatchesOracle(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void DelegatedDetailedDrainsPoppedCaptures()
    {
        // A fully popped stack reads unset, exactly like the BCL oracle.
        var regex = new Utf8Pcre2Regex("(?<o>a)(?<-o>b)", Pcre2CompileOptions.None);
        var drained = regex.MatchDetailed(Utf8("ab"), 0);
        Assert.True(drained.Success);
        Assert.False(drained.GetGroup(1).Success);
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void DelegatedDetailedKeepsLastRemainingCapture()
    {
        // One of two captures survives the single pop: last-capture wins.
        var regex = new Utf8Pcre2Regex("(?<open>a)+(?<-open>b)+", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("xaab"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void DelegatedDetailedProjectsIntervalCaptures()
    {
        var regex = new Utf8Pcre2Regex("(?<a>x)(?<b-a>y)(?<c-b>z)", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("xyz"), 0);
        Assert.True(detailed.Success);
        Assert.False(detailed.GetGroup(1).Success);
        Assert.False(detailed.GetGroup(2).Success);
        Assert.Equal("y", detailed.GetGroup(3).GetValueString());
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void DelegatedReplaceMatchesOracle()
    {
        var regex = new Utf8Pcre2Regex("(?<o>a)(?<-o>b)", Pcre2CompileOptions.None);
        Assert.Equal(Bcl("(?<o>a)(?<-o>b)").Replace("ab", "X"), regex.ReplaceToString(Utf8("ab"), "X"));
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void DelegatedEnumerationAdvancesThroughBalanceMatches()
    {
        var regex = new Utf8Pcre2Regex("(?<open>a)+(?<-open>b)+", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aab aab"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-3", "4-7"], ranges);
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Theory]
    [InlineData("(?<-open>b)")]
    [InlineData("(?<a-b>x)")]
    public void UndefinedBalanceShapesStayUnsupported(string pattern)
    {
        // No backward-defined pop target exists, so neither native PCRE2
        // nor core delegation admits the shape: every operation reports
        // the explicit unsupported vocabulary instead of running.
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None",
            regex.DebugDescribeExecutionPlan());
        Assert.Throws<NotSupportedException>(() => regex.IsMatch(Utf8("b")));
    }

    [Fact]
    public void ForwardPopTargetDelegatesToCoreAndMisses()
    {
        // A forward-defined pop target constructs (BCL-legal) but never
        // matches; the core slice declines it, so PCRE2 delegates to the
        // core legacy route with identical miss behavior.
        Assert.False(Bcl("(?<-b>a)(?<b>c)").IsMatch("c"));
        var regex = new Utf8Pcre2Regex("(?<-b>a)(?<b>c)", Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex",
            regex.DebugDescribeExecutionPlan());
        Assert.False(regex.IsMatch(Utf8("c")));
    }
}
