using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

// Owned preparation retains useful native facts and scalar candidate coverage.
// Obsolete BCL verifier modes are retired; public diagnostics report Scalar.
public sealed class Utf8SliceLegacyFootprintTests
{
    public static IEnumerable<object?[]> SliceFootprintShapes()
    {
        yield return new object?[] { "ab+", "unsupported_loop", false, false, "Scalar", false, false, "None", true, false };
        yield return new object?[] { "\\p{L}+", "unsupported_loop", false, false, "Scalar", false, false, "None", true, false };
        yield return new object?[] { "\\cA+", "unsupported_loop", false, false, "Scalar", false, false, "None", true, false };
        yield return new object?[] { "(a+)(b+)", "unsupported_loop", false, false, "Scalar", false, false, "None", true, false };
        yield return new object?[] { "a(?!b)", "unsupported_lookaround", false, false, "Scalar", true, false, "None", true, false };
        yield return new object?[] { "(?<=a)b", "unsupported_lookaround", false, false, "Scalar", true, false, "None", true, false };
        yield return new object?[] { "(a)\\1", "unsupported_backreference", false, false, "Scalar", false, false, "None", true, false };
        yield return new object?[] { "(?>ab)c", "unsupported_atomic", false, false, "Scalar", true, false, "None", true, false };
        yield return new object?[] { "(a)?(?(1)ab|cd)", "unsupported_conditional", false, false, "Scalar", false, false, "None", true, false };
        yield return new object?[] { "(?<o>a)(?<-o>b)", "unsupported_pattern", false, false, "Scalar", true, false, "None", true, false };
        yield return new object?[] { "(\\p{L}+)+$", "unsupported_loop", false, false, "Scalar", false, false, "None", true, false };
        yield return new object?[] { "\\p{L}{8,13}", "unsupported_loop", false, false, "Scalar", false, false, "UnicodeLetterBoundedCount", true, false };
        yield return new object?[] { "()", "unsupported_pattern", false, false, "Scalar", false, false, "None", true, false };
        yield return new object?[] { "(?<=((a)b))c", "unsupported_lookaround", false, false, "Scalar", true, false, "None", true, false };
        yield return new object?[] { "(a)(?>b+)(?(1)x|y)", "unsupported_conditional", false, false, "Scalar", false, false, "None", true, false };
    }

    [Theory]
    [MemberData(nameof(SliceFootprintShapes))]
    public void OwnedProgramRetainsNativeFactsAndScalarCoverage(
        string pattern,
        string expectedReason,
        bool expectNullTree,
        bool expectNullProgram,
        string expectedVerifierMode,
        bool expectEndCoverage,
        bool expectTrailingCoverage,
        string expectedFamily,
        bool expectNullLiteral,
        bool expectNullCaptureCatalog)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(expectedReason, regex.Inspection.FallbackReason);
        var prepared = regex.Inspection.PreparedRegex;
        Assert.Equal(expectNullTree, prepared.ExecutionTree is null);
        Assert.Equal(expectNullProgram, prepared.ExecutionProgram is null);
        Assert.Equal(expectedVerifierMode, regex.CollectIsMatchDiagnostics([]).FallbackVerifierMode);
        Assert.Equal(expectEndCoverage, prepared.ScalarVerifier.RequiresCandidateEndCoverage);
        Assert.Equal(expectTrailingCoverage, prepared.ScalarVerifier.RequiresTrailingAnchorCoverage);
        Assert.Equal(expectedFamily, regex.Inspection.DebugFallbackDirectFamilyKind);
        Assert.Equal(expectNullLiteral, prepared.LiteralUtf8 is null);
        Assert.Equal(expectNullCaptureCatalog, prepared.SemanticRegex.CaptureCatalog is null);
    }
}
