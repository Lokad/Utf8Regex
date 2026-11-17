using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Compatible class programs share scalar execution. POSIX class syntax keeps
// profile semantics; negated classes consume complete supplementary scalars.
public sealed class Pcre2SliceClassTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    public static IEnumerable<object?[]> SliceClassShapes()
    {
        yield return new object?[] { "[a-z]+" };
        yield return new object?[] { "[^a-z]+" };
        yield return new object?[] { "[a-z]+?" };
        yield return new object?[] { "([a-z]+)" };
        yield return new object?[] { "[a-z]+b$" };
        yield return new object?[] { "[^a-z]+b" };
        yield return new object?[] { "[abc]+x" };
        yield return new object?[] { "[a-z]*b" };
        yield return new object?[] { "[a-z]+|[0-9]+" };
        yield return new object?[] { "([^a-z]+)" };
    }

    [Theory]
    [MemberData(nameof(SliceClassShapes))]
    public void CompoundClassShapesRouteThroughSlice(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void AnchoredClassUsesSharedScalarExecution()
    {
        var regex = new Utf8Pcre2Regex("^[a-z]+$", Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void PosixClassDeclinesSliceRouting()
    {
        var regex = new Utf8Pcre2Regex("([[:alpha:]]+)", Pcre2CompileOptions.None);
        Assert.Null(regex.DebugCompiledProgram.PrimaryUtf8);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData("[[:alpha:]]+", "1ab_3Cd!", new[] { "ab", "Cd" }, "1<_>_3<_>!")]
    [InlineData("([[:alpha:]]+)", "1ab_3Cd!", new[] { "ab", "Cd" }, "1<_>_3<_>!")]
    [InlineData("x[[:alpha:]]+y", "xaby xa]y", new[] { "xaby" }, "<_> xa]y")]
    [InlineData("[^[:alpha:]]+", "a]b3c", new[] { "]", "3" }, "a<_>b<_>c")]
    [InlineData("[q[:digit:]]+", "1q:23a", new[] { "1q", "23" }, "<_>:<_>a")]
    public void PosixProgramsKeepProfileResultsAcrossOperations(
        string pattern, string text, string[] expected, string replacement)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(text);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(input, 0));
        Assert.Equal(expected.Length, regex.Count(input, 0));
        Assert.Equal(expected[0], System.Text.Encoding.UTF8.GetString(regex.Match(input, 0).GetValueBytes()));
        Assert.Equal(expected[0], System.Text.Encoding.UTF8.GetString(regex.MatchDetailed(input, 0).Value.GetValueBytes()));
        var actual = new List<string>();
        var matches = regex.EnumerateMatches(input, 0);
        while (matches.MoveNext()) actual.Add(System.Text.Encoding.UTF8.GetString(matches.Current.GetValueBytes()));
        Assert.Equal(expected, actual);
        Assert.Equal(replacement, regex.ReplaceToString(input, "<_>"));
        Assert.True(regex.DebugCompiledProgram is not null, pattern);
    }

    [Fact]
    public void PosixCaptureProjectionRetainsPcre2CharacterMembership()
    {
        var regex = new Utf8Pcre2Regex("(?<x>[[:alpha:]]+)!");
        var input = Utf8("1ab! a]!");
        var match = regex.MatchDetailed(input, 0);
        Assert.True(match.Success);
        Assert.Equal("ab!", System.Text.Encoding.UTF8.GetString(match.Value.GetValueBytes()));
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(match.GetGroup(1).GetValueBytes()));
        Assert.Equal("1<ab> a]!", regex.ReplaceToString(input, "<$1>"));
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "([a-z]+)", "xxabyy", 0, true };
        yield return new object?[] { "([a-z]+)", "XXABYY", 0, false };
        yield return new object?[] { "[a-z]+b$", "xxab", 0, true };
        yield return new object?[] { "[^a-z]+b", "xxABb", 0, true };
        yield return new object?[] { "[a-z]+|[0-9]+", "xx12yy", 0, true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText), startOffsetInBytes));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("([^a-z]+)", "x\U0001F600y", 1)]
    [InlineData("([a-z]+)", "xa\U0001F600by", 2)]
    [InlineData("[a-z]+|[0-9]+", "ab12", 2)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("([^a-z]+)", "x\U0001F600y", 1, 5)]
    [InlineData("([a-z]+)", "xxabyy", 0, 6)]
    public void OwnedFirstMatchProjectsScalarBytes(
        string pattern, string inputText, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText), 0);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.StartOffsetInBytes);
        Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughClasses()
    {
        var regex = new Utf8Pcre2Regex("[a-z]+|[0-9]+", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("ab12"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-2", "2-4"], ranges);
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void OwnedDetailedProjectsClassGroup()
    {
        var regex = new Utf8Pcre2Regex("([^a-z]+)", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("xxAByy"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("AB", System.Text.Encoding.UTF8.GetString(detailed.Value.GetValueBytes()));
        Assert.Equal("AB", System.Text.Encoding.UTF8.GetString(detailed.GetGroup(1).GetValueBytes()));
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void OwnedReplaceExpandsClassGroup()
    {
        var regex = new Utf8Pcre2Regex("([a-z]+)", Pcre2CompileOptions.None);
        Assert.Equal("<xxabbyy>", regex.ReplaceToString(Utf8("xxabbyy"), "<$1>"));
        Assert.True(regex.DebugCompiledProgram is not null);
    }
}
