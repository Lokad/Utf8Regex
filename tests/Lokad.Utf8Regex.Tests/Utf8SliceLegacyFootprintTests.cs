using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

// Slice-first construction: the legacy analysis footprint below is the exact
// contract a slice-first path must reproduce (or deliberately migrate), so it
// is pinned per shape. Probed: fallback-verifier modes vary (anchored,
// direct-plus-end-coverage, anchored-slice), one family kernel survives
// (bounded letter count), and the capturing-lookbehind shape routes to the
// fallback verifier with end coverage while keeping its tree and program.
public sealed class Utf8SliceLegacyFootprintTests
{
    public static IEnumerable<object?[]> SliceFootprintShapes()
    {
        yield return new object?[] { "ab+", "unsupported_loop", false, false, "AnchoredRegex", false, false, "None", true, false };
        yield return new object?[] { "\\p{L}+", "unsupported_loop", false, false, "AnchoredRegex", false, false, "None", true, false };
        yield return new object?[] { "\\cA+", "unsupported_loop", false, false, "AnchoredRegex", false, false, "None", true, false };
        yield return new object?[] { "(a+)(b+)", "unsupported_loop", false, false, "AnchoredRegex", false, false, "None", true, false };
        yield return new object?[] { "a(?!b)", "unsupported_lookaround", false, false, "AnchoredSliceRegex", true, false, "None", true, false };
        yield return new object?[] { "(?<=a)b", "unsupported_lookaround", false, false, "DirectRegex", true, false, "None", true, false };
        yield return new object?[] { "(a)\\1", "unsupported_backreference", false, false, "AnchoredRegex", false, false, "None", true, false };
        yield return new object?[] { "(?>ab)c", "unsupported_atomic", false, false, "DirectRegex", true, false, "None", true, false };
        yield return new object?[] { "(a)?(?(1)ab|cd)", "unsupported_conditional", false, false, "AnchoredRegex", false, false, "None", true, false };
        yield return new object?[] { "(?<o>a)(?<-o>b)", "unsupported_pattern", false, false, "DirectRegex", true, false, "None", true, false };
        yield return new object?[] { "(\\p{L}+)+$", "unsupported_loop", false, false, "AnchoredRegex", false, false, "None", true, false };
        yield return new object?[] { "\\p{L}{8,13}", "unsupported_loop", false, false, "AnchoredRegex", false, false, "UnicodeLetterBoundedCount", true, false };
        yield return new object?[] { "()", "unsupported_pattern", false, false, "AnchoredRegex", false, false, "None", true, false };
        yield return new object?[] { "(?<=((a)b))c", "unsupported_lookaround", false, false, "DirectRegex", true, false, "None", true, false };
        yield return new object?[] { "(a)(?>b+)(?(1)x|y)", "unsupported_conditional", false, false, "AnchoredRegex", false, false, "None", true, false };
    }

    [Theory]
    [MemberData(nameof(SliceFootprintShapes))]
    public void OwnedProgramKeepsLegacyFootprint(
        string pattern,
        string expectedReason,
        bool expectNullTree,
        bool expectNullProgram,
        string expectedVerifierMode,
        bool expectEndCoverage,
        bool expectTrailingCoverage,
        string expectedFamily,
        bool expectNullLiteral,
        bool expectNullRuntimeTree)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(expectedReason, regex.Inspection.FallbackReason);
        var prepared = regex.Inspection.PreparedRegex;
        Assert.Equal(expectNullTree, prepared.ExecutionTree is null);
        Assert.Equal(expectNullProgram, prepared.ExecutionProgram is null);
        Assert.Equal(expectedVerifierMode, prepared.FallbackVerifier.Mode.ToString());
        Assert.Equal(expectEndCoverage, prepared.FallbackVerifier.RequiresCandidateEndCoverage);
        Assert.Equal(expectTrailingCoverage, prepared.FallbackVerifier.RequiresTrailingAnchorCoverage);
        Assert.Equal(expectedFamily, regex.Inspection.DebugFallbackDirectFamilyKind);
        Assert.Equal(expectNullLiteral, prepared.LiteralUtf8 is null);
        Assert.Equal(expectNullRuntimeTree, prepared.SemanticRegex.RuntimeTree is null);
    }
}
