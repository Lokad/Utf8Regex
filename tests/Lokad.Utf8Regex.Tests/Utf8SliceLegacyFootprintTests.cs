using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

// Owned preparation retains useful native facts and scalar candidate coverage.
// Scalar coverage and native preparation facts remain observable through inspection.
public sealed class Utf8SliceLegacyFootprintTests
{
    [Theory]
    [InlineData("ab+")]
    [InlineData("\\p{L}+")]
    [InlineData("\\cA+")]
    [InlineData("(a+)(b+)")]
    [InlineData("a(?!b)")]
    [InlineData("(?<=a)b")]
    [InlineData("(a)\\1")]
    [InlineData("(?>ab)c")]
    [InlineData("(a)?(?(1)ab|cd)")]
    [InlineData("(?(?=a)b|c)")]
    [InlineData("(?<o>a)(?<-o>b)")]
    [InlineData("(\\p{L}+)+$")]
    [InlineData("\\p{L}{8,13}")]
    [InlineData("()")]
    [InlineData("(?<=((a)b))c")]
    [InlineData("(a)(?>b+)(?(1)x|y)")]
    public void OwnedProgramRetainsScalarCoverageAndNecessaryFacts(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        var prepared = regex.Inspection.PreparedRegex;
        Assert.Null(prepared.LiteralUtf8);
        Assert.NotNull(prepared.SemanticRegex.CaptureCatalog);
        Assert.NotNull(prepared.ScalarProgram);
    }
}
