using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// The former source-length fence no longer selects a BCL engine. The
// instruction-count cap remains explicit construction-ownership debt.
public sealed class Utf8SliceAdmissionTests
{
    private const int HistoricalPatternLengthLimit = 65536;

    [Fact]
    public void BoundaryLengthPatternParses()
    {
        var pattern = string.Concat(
            System.Linq.Enumerable.Repeat("a|", HistoricalPatternLengthLimit / 2));
        Assert.Equal(HistoricalPatternLengthLimit, pattern.Length);
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    [Fact]
    public void OverLengthPatternParsesOwned()
    {
        var pattern = string.Concat(
            System.Linq.Enumerable.Repeat("a|", HistoricalPatternLengthLimit / 2)) + "a";
        Assert.True(pattern.Length > HistoricalPatternLengthLimit);
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
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
    public void OverInstructionSyntheticTreeFailsOwnedAdmission()
    {
        var shared = new Utf8SliceNode.LiteralNode(97, false);
        var branches = new Utf8SliceNode[400000][];
        for (var index = 0; index < branches.Length; index++)
        {
            branches[index] = new Utf8SliceNode[] { shared };
        }

        // Resource exhaustion must not masquerade as unsupported grammar and
        // select a residual engine. Keep the large shared-node witness.
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8SliceLowerer.Lower(new Utf8SliceNode.AlternationNode(branches), null, false, false, false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, failure.ResourceKind);
        Assert.Equal(Utf8ConstructionScope.DefaultInstructionCapacity, failure.Limit);
        Assert.True(failure.Requested > failure.Limit);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void UnderInstructionSyntheticTreeLowers()
    {
        var shared = new Utf8SliceNode.LiteralNode(97, false);
        var branches = new Utf8SliceNode[100000][];
        for (var index = 0; index < branches.Length; index++)
        {
            branches[index] = new Utf8SliceNode[] { shared };
        }

        Assert.NotNull(Utf8SliceLowerer.Lower(new Utf8SliceNode.AlternationNode(branches), null, false, false, false));
    }

    private static string RepeatSliceShape(int repetitions)
    {
        return string.Concat(System.Linq.Enumerable.Repeat("(a+)(b+)|", repetitions));
    }

    [Fact]
    public void UnderLengthRepeatedShapeRoutesOwned()
    {
        var pattern = RepeatSliceShape(6000);
        Assert.True(pattern.Length <= HistoricalPatternLengthLimit);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "length=" + pattern.Length);
        var input = Encoding.UTF8.GetBytes("xxaaabbyy");
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.IsMatch("xxaaabbyy"), regex.IsMatch(input));
        Assert.Equal(oracle.Matches("xxaaabbyy").Count, regex.Count(input));
        Assert.Equal(oracle.Match("xxaaabbyy").Success, regex.Match(input).Success);
    }

    [Fact]
    public void OverLengthRepeatedShapeRoutesOwned()
    {
        var pattern = RepeatSliceShape(9000);
        Assert.True(pattern.Length > HistoricalPatternLengthLimit);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "length=" + pattern.Length);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "construction must stay lazy");
        var input = Encoding.UTF8.GetBytes("xxaaabbyy");
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.IsMatch("xxaaabbyy"), regex.IsMatch(input));
        Assert.Equal(oracle.Matches("xxaaabbyy").Count, regex.Count(input));
        Assert.Equal(oracle.Match("xxaaabbyy").Success, regex.Match(input).Success);
        Assert.Equal(oracle.IsMatch("xxaaayy"), regex.IsMatch(Encoding.UTF8.GetBytes("xxaaayy")));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
