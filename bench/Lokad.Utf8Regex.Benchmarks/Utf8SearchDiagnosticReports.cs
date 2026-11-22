using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

// Tooling-only report assembly; shared with tests by a linked source file.
internal static class Utf8SearchDiagnosticReports
{
    internal static Utf8IsMatchDiagnostics CollectIsMatchDiagnostics(this Utf8Regex regex, ReadOnlySpan<byte> input)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = regex.IsMatch(input);
            return new Utf8IsMatchDiagnostics(
                result,
                regex.Inspection.PreparedRegex.ExecutionKind.ToString(),
                regex.Inspection.PreparedRegex.SearchPlan.Kind.ToString(),
                regex.Inspection.PreparedRegex.SearchPlan.StructuralSearchPlan.RequiresCandidateEndCoverage,
                session.SearchCandidates,
                session.PrefilterWindows,
                session.PrefilterSkippedWindows,
                session.PrefilterPromotedWindows,
                session.PrefilterSkippedBytes,
                session.PrefilterPromotedBytes,
                session.EngineDemotions);
        }
        finally
        {
            session.Complete();
        }
    }

    internal static Utf8CountDiagnostics CollectCountDiagnostics(this Utf8Regex regex, ReadOnlySpan<byte> input)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = regex.Count(input);
            return new Utf8CountDiagnostics(
                result,
                regex.Inspection.PreparedRegex.ExecutionKind.ToString(),
                regex.Inspection.PreparedRegex.SearchPlan.Kind.ToString(),
                regex.Inspection.PreparedRegex.SearchPlan.StructuralSearchPlan.RequiresCandidateEndCoverage,
                FormatRoute(session.ExecutionRoute),
                session.SearchCandidates,
                session.PrefilterWindows,
                session.PrefilterSkippedWindows,
                session.PrefilterPromotedWindows,
                session.PrefilterSkippedBytes,
                session.PrefilterPromotedBytes,
                session.EngineDemotions);
        }
        finally
        {
            session.Complete();
        }
    }

    private static string FormatRoute(Utf8ExecutionRoute route) => route switch
    {
        Utf8ExecutionRoute.None => "<none>",
        Utf8ExecutionRoute.CompiledScalarBoundaryLiteralFamilyCount => "compiled_scalar_boundary_literal_family_count",
        Utf8ExecutionRoute.VectorAsciiLiteralCount => "vector_ascii_literal_count",
        Utf8ExecutionRoute.LiteralFamilyHybridEarliestAutomaton => "literal_family_hybrid_earliest_automaton",
        Utf8ExecutionRoute.LiteralFamilyPreparedNonOverlapping => "literal_family_prepared_non_overlapping",
        Utf8ExecutionRoute.LiteralFamilyPreparedWithRequirementsLengthOnly => "literal_family_prepared_with_requirements_length_only",
        Utf8ExecutionRoute.LiteralFamilyPreparedWithRequirementsMatch => "literal_family_prepared_with_requirements_match",
        Utf8ExecutionRoute.LiteralFamilyPackedNibbleSimdCount => "literal_family_packed_nibble_simd_count",
        Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix => "fallback_direct_anchored_ascii_literal_prefix",
        Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix => "fallback_direct_anchored_ascii_literal_suffix",
        _ => throw new InvalidOperationException("Unknown UTF-8 execution route.")
    };
}

/// <summary>Reports the result, selected execution route, and search counters for a diagnostic match probe.</summary>
internal readonly record struct Utf8IsMatchDiagnostics(
    bool Result,
    string ExecutionKind,
    string SearchKind,
    bool RequiresCandidateEndCoverage,
    int SearchCandidates,
    int PrefilterWindows,
    int PrefilterSkippedWindows,
    int PrefilterPromotedWindows,
    int PrefilterSkippedBytes,
    int PrefilterPromotedBytes,
    int EngineDemotions);

/// <summary>Reports the result, selected execution route, and search counters for a diagnostic count operation.</summary>
internal readonly record struct Utf8CountDiagnostics(
    int Result,
    string ExecutionKind,
    string SearchKind,
    bool RequiresCandidateEndCoverage,
    string ExecutionRoute,
    int SearchCandidates,
    int PrefilterWindows,
    int PrefilterSkippedWindows,
    int PrefilterPromotedWindows,
    int PrefilterSkippedBytes,
    int PrefilterPromotedBytes,
    int EngineDemotions);
