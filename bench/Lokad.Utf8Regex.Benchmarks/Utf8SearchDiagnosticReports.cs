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
                regex.Inspection.PreparedRegex.StructuralSearchPlan.RequiresCandidateEndCoverage,
                regex.Inspection.PreparedRegex.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Window,
                session.SearchCandidates,
                session.FixedCheckRejects,
                session.VerifierInvocations,
                session.VerifierMatches,
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
                regex.Inspection.PreparedRegex.StructuralSearchPlan.RequiresCandidateEndCoverage,
                regex.Inspection.PreparedRegex.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Window,
                session.ExecutionRoute.Format(),
                session.SearchCandidates,
                session.FixedCheckRejects,
                session.VerifierInvocations,
                session.VerifierMatches,
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

}

/// <summary>Reports the result, selected execution route, and search counters for a diagnostic match probe.</summary>
internal readonly record struct Utf8IsMatchDiagnostics(
    bool Result,
    string ExecutionKind,
    string SearchKind,
    bool RequiresCandidateEndCoverage,
    bool RequiresTrailingAnchorCoverage,
    int SearchCandidates,
    int FixedCheckRejects,
    int VerifierInvocations,
    int VerifierMatches,
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
    bool RequiresTrailingAnchorCoverage,
    string ExecutionRoute,
    int SearchCandidates,
    int FixedCheckRejects,
    int VerifierInvocations,
    int VerifierMatches,
    int PrefilterWindows,
    int PrefilterSkippedWindows,
    int PrefilterPromotedWindows,
    int PrefilterSkippedBytes,
    int PrefilterPromotedBytes,
    int EngineDemotions);
