using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// R02b construction admission: pattern length and lowered instruction caps
// decline oversized slice shapes to legacy routing with identical behavior.
// Construction never fails on size; the caps bound retained programs.
public sealed class Utf8SliceAdmissionTests
{
    [Fact]
    public void BoundaryLengthPatternParses()
    {
        var pattern = string.Concat(
            System.Linq.Enumerable.Repeat("a|", Utf8SliceParser.MaxPatternLength / 2));
        Assert.Equal(Utf8SliceParser.MaxPatternLength, pattern.Length);
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    [Fact]
    public void OverLengthPatternDeclines()
    {
        var pattern = string.Concat(
            System.Linq.Enumerable.Repeat("a|", Utf8SliceParser.MaxPatternLength / 2)) + "a";
        Assert.True(pattern.Length > Utf8SliceParser.MaxPatternLength);
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    [Fact]
    public void HugeRepeatBoundParsesWithoutUnrolling()
    {
        const string pattern = "x{0,2147483647}y";
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var input = Encoding.UTF8.GetBytes("xyx");
        Assert.Equal(oracle.IsMatch("xyx"), regex.IsMatch(input));
        Assert.Equal(oracle.Matches("xyx").Count, regex.Count(input));
    }

    [Fact]
    public void OverInstructionSyntheticTreeDeclines()
    {
        var shared = new Utf8SliceNode.LiteralNode(97);
        var branches = new Utf8SliceNode[400000][];
        for (var index = 0; index < branches.Length; index++)
        {
            branches[index] = new Utf8SliceNode[] { shared };
        }

        Assert.Null(Utf8SliceLowerer.Lower(new Utf8SliceNode.AlternationNode(branches), null));
    }

    [Fact]
    public void UnderInstructionSyntheticTreeLowers()
    {
        var shared = new Utf8SliceNode.LiteralNode(97);
        var branches = new Utf8SliceNode[100000][];
        for (var index = 0; index < branches.Length; index++)
        {
            branches[index] = new Utf8SliceNode[] { shared };
        }

        Assert.NotNull(Utf8SliceLowerer.Lower(new Utf8SliceNode.AlternationNode(branches), null));
    }

    private static string RepeatSliceShape(int repetitions)
    {
        return string.Concat(System.Linq.Enumerable.Repeat("(a+)(b+)|", repetitions));
    }

    [Fact]
    public void UnderLengthRepeatedShapeRoutesOwned()
    {
        var pattern = RepeatSliceShape(6000);
        Assert.True(pattern.Length <= Utf8SliceParser.MaxPatternLength);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "length=" + pattern.Length);
        var input = Encoding.UTF8.GetBytes("xxaaabbyy");
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.IsMatch("xxaaabbyy"), regex.IsMatch(input));
        Assert.Equal(oracle.Matches("xxaaabbyy").Count, regex.Count(input));
        Assert.Equal(oracle.Match("xxaaabbyy").Success, regex.Match(input).Success);
    }

    [Fact]
    public void OverLengthRepeatedShapeDeclinesToLegacyRouting()
    {
        var pattern = RepeatSliceShape(9000);
        Assert.True(pattern.Length > Utf8SliceParser.MaxPatternLength);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "length=" + pattern.Length);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "construction must stay lazy");
        var input = Encoding.UTF8.GetBytes("xxaaabbyy");
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.IsMatch("xxaaabbyy"), regex.IsMatch(input));
        Assert.Equal(oracle.Matches("xxaaabbyy").Count, regex.Count(input));
        Assert.Equal(oracle.Match("xxaaabbyy").Success, regex.Match(input).Success);
        Assert.Equal(oracle.IsMatch("xxaaayy"), regex.IsMatch(Encoding.UTF8.GetBytes("xxaaayy")));
    }
}
