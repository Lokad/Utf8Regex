using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8StructuralSearchExecutor
{
    internal static Utf8ExecutionLimitKind TryFindNextCandidate(this Utf8StructuralSearchPlan plan,
        ReadOnlySpan<byte> input, ref Utf8StructuralSearchState state, ref Utf8SliceInvocation invocation,
        out Utf8StructuralCandidate candidate, out bool found)
    {
        candidate = default;
        found = false;
        var pending = state;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            found = plan.TryFindNextCandidate(input, ref pending, out candidate);
            state = pending;
            return Utf8ExecutionLimitKind.None;
        }
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (plan.Stages is not { Length: > 0 }) return Utf8ExecutionLimitKind.None;
        var searcher = default(PreparedSearcher);
        var ascii = default(PreparedAsciiFindPlan);
        var windows = default(PreparedWindowSearch);
        var transform = default(Utf8FallbackStartTransform);
        foreach (var stage in plan.Stages)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.FindLiteralFamily: searcher = stage.Searcher; break;
                case Utf8StructuralSearchStageKind.FindAscii: ascii = stage.AsciiFindPlan; break;
                case Utf8StructuralSearchStageKind.FindWindow: windows = stage.WindowSearch; break;
                case Utf8StructuralSearchStageKind.TransformCandidateStart: transform = stage.StartTransform; break;
            }
        }
        while (true)
        {
            var raw = default(PreparedSearchMatch);
            var window = default(PreparedWindowMatch);
            var exists = false;
            if (plan.YieldKind == Utf8StructuralSearchYieldKind.Window)
            {
                var nextWindow = pending.WindowState;
                breach = windows.TryFindNextWindow(input, ref nextWindow, ref invocation, out window, out exists);
                pending = new Utf8StructuralSearchState(default, nextWindow);
                raw = window.Leading;
            }
            else if (plan.YieldKind == Utf8StructuralSearchYieldKind.Start)
            {
                var nextSearch = pending.SearchState;
                if (searcher.HasValue)
                    breach = searcher.TryFindNextOverlappingMatch(input, ref nextSearch, ref invocation, out raw, out exists);
                else
                {
                    breach = Utf8AsciiFindExecutor.TryFindNextFixedDistanceCandidate(input, ascii, nextSearch.NextStart,
                        ref invocation, out var index, out var length, out exists);
                    if (exists) raw = new PreparedSearchMatch(index, length, 0);
                    nextSearch = new PreparedSearchScanState(exists ? index + 1 : input.Length, default);
                }
                pending = new Utf8StructuralSearchState(nextSearch, default);
            }
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!exists) break;
            breach = TryTransformStart(input, transform, raw.Index, ref invocation, out var start, out var requirementBase);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (start < 0) continue;
            if (plan.YieldKind == Utf8StructuralSearchYieldKind.Window)
            {
                breach = TrySatisfyWindowRequirements(input, plan.Stages, window, ref invocation, out var accepted);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!accepted) continue;
                raw = new PreparedSearchMatch(start, window.Trailing.Index + window.Trailing.Length - start, raw.LiteralId);
            }
            else
            {
                breach = TrySatisfyStartRequirements(input, plan.Stages, requirementBase, raw.Length, ref invocation, out var accepted);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!accepted) continue;
            }
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            candidate = plan.YieldKind == Utf8StructuralSearchYieldKind.Window
                ? Utf8StructuralCandidate.Window(start, window.Trailing.Index + window.Trailing.Length,
                    window.Leading.Length, window.Leading.LiteralId, window.Trailing.Index, window.Trailing.Length, window.Trailing.LiteralId)
                : Utf8StructuralCandidate.Start(start, GetExactCandidateEnd(plan.Stages, start, input.Length), raw.Length, raw.LiteralId);
            found = true;
            state = pending;
            return Utf8ExecutionLimitKind.None;
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        state = pending;
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8ExecutionLimitKind TryFindLastCandidate(this Utf8StructuralSearchPlan plan,
        ReadOnlySpan<byte> input, int end, ref Utf8SliceInvocation invocation,
        out Utf8StructuralCandidate candidate, out bool found)
    {
        candidate = default;
        found = false;
        if ((uint)end > (uint)input.Length) end = input.Length;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (plan.Stages is not { Length: > 0 }) return Utf8ExecutionLimitKind.None;
        var searcher = default(PreparedSearcher);
        var ascii = default(PreparedAsciiFindPlan);
        var transform = default(Utf8FallbackStartTransform);
        foreach (var stage in plan.Stages)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.FindLiteralFamily: searcher = stage.Searcher; break;
                case Utf8StructuralSearchStageKind.FindAscii: ascii = stage.AsciiFindPlan; break;
                case Utf8StructuralSearchStageKind.TransformCandidateStart: transform = stage.StartTransform; break;
            }
        }
        var limit = end;
        var floor = 0;
        var pending = default(Utf8StructuralCandidate);
        var acceptedAny = false;
        while (plan.YieldKind == Utf8StructuralSearchYieldKind.Start && limit > 0)
        {
            var raw = default(PreparedSearchMatch);
            var exists = false;
            if (searcher.HasValue)
                breach = searcher.TryFindMatch(input, 0, limit, true, ref invocation, out raw, out exists);
            else
            {
                breach = Utf8AsciiFindExecutor.TryFindNextFixedDistanceCandidate(input[..end], ascii, floor,
                    ref invocation, out var index, out var length, out exists);
                if (exists) raw = new PreparedSearchMatch(index, length, 0);
            }
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!exists) break;
            breach = TryTransformStart(input, transform, raw.Index, ref invocation, out var start, out var requirementBase);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (start >= 0)
            {
                breach = TrySatisfyStartRequirements(input, plan.Stages, requirementBase, raw.Length, ref invocation, out var accepted);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (accepted)
                {
                    pending = Utf8StructuralCandidate.Start(start, GetExactCandidateEnd(plan.Stages, start, input.Length), raw.Length, raw.LiteralId);
                    acceptedAny = true;
                    if (searcher.HasValue) break;
                }
            }
            if (searcher.HasValue) limit = raw.Index;
            else floor = raw.Index + 1;
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        candidate = pending;
        found = acceptedAny;
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TryTransformStart(ReadOnlySpan<byte> input,
        Utf8FallbackStartTransform transform, int index, ref Utf8SliceInvocation invocation,
        out int start, out int requirementBase)
    {
        start = -1;
        requirementBase = -1;
        var adjusted = (long)index - transform.Offset;
        if (adjusted < 0 || adjusted > input.Length) return Utf8ExecutionLimitKind.None;
        var current = (int)adjusted;
        if (transform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace)
        {
            var count = 0;
            while (current > 0 && Utf8AsciiBytePredicates.IsSixByteWhitespace(input[current - 1]))
            {
                if ((count++ & 2047) == 0)
                {
                    var breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                }
                current--;
            }
        }
        var baseIndex = current;
        if (transform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace)
        {
            var count = 0;
            while (baseIndex < input.Length && Utf8AsciiBytePredicates.IsSixByteWhitespace(input[baseIndex]))
            {
                if ((count++ & 2047) == 0)
                {
                    var breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                }
                baseIndex++;
            }
        }
        start = current;
        requirementBase = baseIndex;
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TrySatisfyStartRequirements(ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages, int start, int length, ref Utf8SliceInvocation invocation, out bool accepted)
    {
        accepted = false;
        foreach (var stage in stages)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.RequireByteAtOffset:
                    var index = (long)start + stage.ByteOffset;
                    if (index < 0 || index >= input.Length || stage.HasLiteralByte && input[(int)index] != stage.LiteralByte ||
                        stage.HasSet && !stage.Set.Contains(input[(int)index]))
                        return Utf8ExecutionLimitKind.None;
                    break;
                case Utf8StructuralSearchStageKind.RequireLiteralAtOffset:
                case Utf8StructuralSearchStageKind.RequireTrailingLiteral:
                    if (stage.LiteralUtf8.Length == 0) break;
                    var at = (long)start + (stage.Kind == Utf8StructuralSearchStageKind.RequireTrailingLiteral ? length : stage.ByteOffset);
                    if (at < 0 || at > input.Length - stage.LiteralUtf8.Length) return Utf8ExecutionLimitKind.None;
                    breach = Utf8SearchExecutor.TryCompareLiteral(input[(int)at..], stage.LiteralUtf8, false, ref invocation, out var equal);
                    if (breach != Utf8ExecutionLimitKind.None || !equal) return breach;
                    break;
                case Utf8StructuralSearchStageKind.RequireMinLength:
                case Utf8StructuralSearchStageKind.RequireExactLength:
                    if ((long)start + stage.MinLength > input.Length) return Utf8ExecutionLimitKind.None;
                    break;
                case Utf8StructuralSearchStageKind.RequireLeadingBoundary:
                case Utf8StructuralSearchStageKind.RequireTrailingBoundary:
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(stage.BoundaryRequirement, input,
                            stage.Kind == Utf8StructuralSearchStageKind.RequireLeadingBoundary ? start : start + length))
                        return Utf8ExecutionLimitKind.None;
                    break;
            }
        }
        accepted = true;
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8ExecutionLimitKind TrySatisfyWindowRequirements(ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages, PreparedWindowMatch window, ref Utf8SliceInvocation invocation, out bool accepted)
    {
        accepted = false;
        foreach (var stage in stages)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.RequireWithinByteSpan:
                    if (window.Trailing.Index + window.Trailing.Length - window.Leading.Index > stage.MaxSpan)
                        return Utf8ExecutionLimitKind.None;
                    break;
                case Utf8StructuralSearchStageKind.RequireWithinLineSpan:
                    if (stage.MaxLines <= 0) return Utf8ExecutionLimitKind.None;
                    var lines = 1;
                    var previousCr = false;
                    for (var i = window.Leading.Index; i < window.Trailing.Index; i++)
                    {
                        if (((i - window.Leading.Index) & 2047) == 0)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                        }
                        var value = input[i];
                        var newline = value == (byte)'\r' || value == (byte)'\n' && !previousCr;
                        previousCr = value == (byte)'\r';
                        if (newline && ++lines > stage.MaxLines) return Utf8ExecutionLimitKind.None;
                    }
                    break;
                case Utf8StructuralSearchStageKind.RequireLeadingBoundary:
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(stage.BoundaryRequirement, input, window.Leading.Index))
                        return Utf8ExecutionLimitKind.None;
                    break;
                case Utf8StructuralSearchStageKind.RequireTrailingBoundary:
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(stage.BoundaryRequirement, input, window.Trailing.Index + window.Trailing.Length))
                        return Utf8ExecutionLimitKind.None;
                    break;
            }
        }
        accepted = true;
        return Utf8ExecutionLimitKind.None;
    }
}
