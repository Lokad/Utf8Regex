using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8AsciiStructuralIdentifierFamilyExecutor
{
    internal static Utf8ExecutionLimitKind TryFindNext(in Utf8StructuralSearchPlan search,
        in AsciiStructuralVerifierProgram verifier, ReadOnlySpan<byte> input, int floor,
        ref Utf8StructuralSearchState state, ref Utf8SliceInvocation invocation,
        out PreparedSearchMatch match, out bool found)
    {
        match = default;
        found = false;
        var pending = state;
        while (true)
        {
            var breach = search.TryFindNextCandidate(input, ref pending, ref invocation, out var candidate, out var exists);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!exists) { state = pending; return Utf8ExecutionLimitKind.None; }
            if (candidate.StartIndex < floor) continue;
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            var accepted = verifier.TryMatch(input, candidate.StartIndex, candidate.MatchLength,
                ref invocation, out var length, out breach);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!accepted) continue;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            match = new PreparedSearchMatch(candidate.StartIndex, length, candidate.LiteralId);
            found = true;
            state = pending;
            return Utf8ExecutionLimitKind.None;
        }
    }

    internal static Utf8ExecutionLimitKind TryCount(in Utf8StructuralSearchPlan search,
        in AsciiStructuralVerifierProgram verifier, ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out int count)
    {
        count = 0;
        var total = 0;
        var floor = 0;
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), default);
        while (floor <= input.Length)
        {
            var breach = TryFindNext(search, verifier, input, floor, ref state, ref invocation, out var match, out var found);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!found) break;
            total++;
            floor = match.Index + Math.Max(1, match.Length);
        }
        var kind = invocation.TryChargeInputStep();
        if (kind != Utf8ExecutionLimitKind.None) return kind;
        invocation.Budget.ThrowIfCancellationRequested();
        count = total;
        return Utf8ExecutionLimitKind.None;
    }
}
