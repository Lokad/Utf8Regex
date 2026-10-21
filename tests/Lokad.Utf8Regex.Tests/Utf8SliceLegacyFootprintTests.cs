using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

// Owned preparation retains useful native facts and scalar candidate coverage.
// Obsolete BCL verifier modes are retired; public diagnostics report Scalar.
public sealed class Utf8SliceLegacyFootprintTests
{
    public static IEnumerable<object?[]> SliceFootprintShapes()
    {
        yield return new object?[] { "ab+", "unsupported_loop", "None" };
        yield return new object?[] { "\\p{L}+", "unsupported_loop", "None" };
        yield return new object?[] { "\\cA+", "unsupported_loop", "None" };
        yield return new object?[] { "(a+)(b+)", "unsupported_loop", "None" };
        yield return new object?[] { "a(?!b)", "unsupported_lookaround", "None" };
        yield return new object?[] { "(?<=a)b", "unsupported_lookaround", "None" };
        yield return new object?[] { "(a)\\1", "unsupported_backreference", "None" };
        yield return new object?[] { "(?>ab)c", "unsupported_atomic", "None" };
        yield return new object?[] { "(a)?(?(1)ab|cd)", "unsupported_conditional", "None" };
        yield return new object?[] { "(?<o>a)(?<-o>b)", "unsupported_pattern", "None" };
        yield return new object?[] { "(\\p{L}+)+$", "unsupported_loop", "None" };
        yield return new object?[] { "\\p{L}{8,13}", "unsupported_loop", "UnicodeLetterBoundedCount" };
        yield return new object?[] { "()", "unsupported_pattern", "None" };
        yield return new object?[] { "(?<=((a)b))c", "unsupported_lookaround", "None" };
        yield return new object?[] { "(a)(?>b+)(?(1)x|y)", "unsupported_conditional", "None" };
    }

    [Theory]
    [MemberData(nameof(SliceFootprintShapes))]
    public void OwnedProgramRetainsNativeFactsAndScalarCoverage(
        string pattern,
        string expectedReason,
        string expectedFamily)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(expectedReason, regex.Inspection.FallbackReason);
        var prepared = regex.Inspection.PreparedRegex;
        Assert.Equal(expectedFamily == "None", prepared.ExecutionTree is null);
        Assert.Equal("Scalar", regex.CollectIsMatchDiagnostics([]).FallbackVerifierMode);
        Assert.Equal(expectedFamily, regex.Inspection.DebugFallbackDirectFamilyKind);
        Assert.Null(prepared.LiteralUtf8);
        Assert.NotNull(prepared.SemanticRegex.CaptureCatalog);
        Assert.NotNull(prepared.ScalarProgram);
    }
}
