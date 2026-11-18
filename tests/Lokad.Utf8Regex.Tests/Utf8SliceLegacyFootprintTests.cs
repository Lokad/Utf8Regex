using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

// Owned preparation retains useful native facts and scalar candidate coverage.
// Scalar coverage and native preparation facts remain observable through inspection.
public sealed class Utf8SliceLegacyFootprintTests
{
    public static IEnumerable<object?[]> SliceFootprintShapes()
    {
        yield return new object?[] { "ab+", "unsupported_loop" };
        yield return new object?[] { "\\p{L}+", "unsupported_loop" };
        yield return new object?[] { "\\cA+", "unsupported_loop" };
        yield return new object?[] { "(a+)(b+)", "unsupported_loop" };
        yield return new object?[] { "a(?!b)", "unsupported_lookaround" };
        yield return new object?[] { "(?<=a)b", "unsupported_lookaround" };
        yield return new object?[] { "(a)\\1", "unsupported_backreference" };
        yield return new object?[] { "(?>ab)c", "unsupported_atomic" };
        yield return new object?[] { "(a)?(?(1)ab|cd)", "unsupported_conditional" };
        yield return new object?[] { "(?<o>a)(?<-o>b)", "unsupported_pattern" };
        yield return new object?[] { "(\\p{L}+)+$", "unsupported_loop" };
        yield return new object?[] { "\\p{L}{8,13}", "unsupported_loop" };
        yield return new object?[] { "()", "unsupported_pattern" };
        yield return new object?[] { "(?<=((a)b))c", "unsupported_lookaround" };
        yield return new object?[] { "(a)(?>b+)(?(1)x|y)", "unsupported_conditional" };
    }

    [Theory]
    [MemberData(nameof(SliceFootprintShapes))]
    public void OwnedProgramRetainsScalarCoverageAndNecessaryFacts(
        string pattern,
        string expectedReason)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(expectedReason, regex.Inspection.FallbackReason);
        var prepared = regex.Inspection.PreparedRegex;
        Assert.Null(prepared.LiteralUtf8);
        Assert.NotNull(prepared.SemanticRegex.CaptureCatalog);
        Assert.NotNull(prepared.ScalarProgram);
    }
}
