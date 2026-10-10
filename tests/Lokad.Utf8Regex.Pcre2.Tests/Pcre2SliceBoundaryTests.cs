using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 boundary slice for the PCRE2 profile: absolute-anchor compounds run
// all operations on the shared slice runner (astral scalars included);
// word-boundary programs decline everywhere because the native boundary
// stays ASCII-scoped while the shared runner applies Unicode semantics.
public sealed class Pcre2SliceBoundaryTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    public static IEnumerable<object?[]> SliceBoundaryShapes()
    {
        yield return new object?[] { "\\A[a-z]+\\z" };
        yield return new object?[] { "\\A[a-z]+\\Z" };
        yield return new object?[] { "\\A.*\\z" };
    }

    [Theory]
    [MemberData(nameof(SliceBoundaryShapes))]
    public void AbsoluteAnchorShapesRouteThroughSlice(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> WordBoundaryShapes()
    {
        yield return new object?[] { "\\bx+" };
        yield return new object?[] { "[a-z]+\\b" };
        yield return new object?[] { "\\b\\w+\\b" };
        yield return new object?[] { "\\bx+\\b" };
        yield return new object?[] { "\\B.+\\B" };
    }

    [Theory]
    [MemberData(nameof(WordBoundaryShapes))]
    public void WordBoundaryShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void NonLfNewlineConventionDeclinesAbsoluteSliceRouting()
    {
        var regex = Utf8Pcre2Regex.Create(
            "\\A[a-z]+\\z",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Crlf },
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "\\A[a-z]+\\z", "abc", 0, true };
        yield return new object?[] { "\\A[a-z]+\\z", "abc\n", 0, false };
        yield return new object?[] { "\\A[a-z]+\\Z", "abc\n", 0, true };
        yield return new object?[] { "\\A.*\\z", "a\U0001F600b", 0, true };
        yield return new object?[] { "\\A(a+)+b$", "aaab", 0, true };
        yield return new object?[] { "\\A(a+)+b$", "xaaab", 0, false };
        yield return new object?[] { "\\A[^x]+\\z", "a\U0001F600b", 0, true };
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
    [InlineData("\\A[a-z]+\\z", "abc", 0, 3)]
    [InlineData("\\A.*\\z", "a\U0001F600b", 0, 6)]
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
    public void DotAllAbsoluteAnchorMatchesNewline()
    {
        var regex = new Utf8Pcre2Regex("\\A.*\\z", Pcre2CompileOptions.DotAll);
        Assert.True(regex.IsMatch(Utf8("a\nb"), 0));
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void OwnedReplaceExpandsAbsoluteGroup()
    {
        var regex = new Utf8Pcre2Regex("\\A([a-z]+)\\z", Pcre2CompileOptions.None);
        Assert.Equal("<abc>", regex.ReplaceToString(Utf8("abc"), "<$1>"));
        Assert.True(regex.DebugCompiledProgram is not null);
    }
}
