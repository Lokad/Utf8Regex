using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Supplies shared invocation state to metered native literal operations.</summary>
public sealed partial class Utf8Regex
{
    // The adapter's literal result/count root shares this invocation with
    // validation and counting, instead of starting a second budget at shaping.
    internal int CountForResult(ReadOnlySpan<byte> input, int startOffsetInBytes,
        ref Utf8SliceInvocation invocation)
    {
        // Count exposes no coordinates. Its callers retain this validity
        // authority, without deriving and discarding a subject description.
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        if (!CanCountNativeLiteralForResult)
            return OwnedSliceCountFromByte(input, _ownedSliceProgram, startOffsetInBytes, ref invocation);
        var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(_preparedRegex.SearchPlan,
            input[startOffsetInBytes..], false, ref invocation, out var count);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
        return count;
    }

    internal bool CanFindNativeLiteralForResult => CanUseNativeLiteralSearch(_ownedSliceProgram);

    // Count discards captures and borrows the same bounded eight-needle search
    // as Core Count. Result discovery retains its existing three-needle proof.
    internal bool CanCountNativeLiteralForResult => CanUseNativeLiteralSearch(_ownedSliceProgram, maximumAlternatives: 8);

    // Borrow caller-valid input and the existing invocation. The Core cursor
    // can also confirm its own scalar word boundaries before projecting a range.
    // Returning false declines the route without searching.
    internal bool TryFindNativeLiteralForResult(ReadOnlySpan<byte> input, int startOffsetInBytes,
        ref Utf8SliceInvocation invocation, out bool found, out int start, out int end, bool forCoreCursor = false)
    {
        found = false;
        start = end = -1;
        var eligible = forCoreCursor
            ? CanUseNativeLiteralSearch(_ownedSliceProgram, maximumAlternatives: 8, allowBoundaries: true)
            : CanFindNativeLiteralForResult;
        if (!eligible) return false;
        var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
            input, startOffsetInBytes, input.Length, false, ref invocation, out var match, out found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal result discovery exceeded {breach}.");
        Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
        if (found)
        {
            start = match.Index;
            end = match.Index + match.Length;
        }
        return true;
    }

    // Shared semantic eligibility, independent of the optional Core owner's
    // policy. An adapter can supply finite limits through its borrowed invocation.
    private bool CanUseNativeLiteralSearch(Utf8SliceProgram program, int maximumAlternatives = 3, bool allowBoundaries = false) =>
        !program.RightToLeft && !program.IgnoreCase &&
        !program.HasBalancing && !program.HasSharedCaptureDeclarations &&
        !program.HasExplicitCaptureNumbers && !program.HasContiguousAnchor &&
        // A prepared trailing literal proves the complete positive lookahead;
        // the shared metered confirmer checks it before publishing a result.
        (!program.SawLookaround || _preparedRegex.SearchPlan.HasTrailingLiteralRequirement) && !program.HasConditionalTest &&
        !_requiresKelvinSignFallback && !program.CanMatchEmpty &&
        (_preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal &&
            _preparedRegex.LiteralUtf8 is { Length: > 0 } ||
         _preparedRegex.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            _preparedRegex.SearchPlan.AlternateLiteralsUtf8 is { Length: >= 2 } literals &&
            literals.Length <= maximumAlternatives) &&
        (allowBoundaries || !_preparedRegex.SearchPlan.HasBoundaryRequirements);

    // Decide eligibility before constructing the invocation. Each selected
    // search starts its deadline and checks admission before inspecting input.
    private bool CanStartNativeLiteralInvocation(CancellationToken token, bool forCount = false)
    {
        var kind = _preparedRegex.ExecutionKind;
        if (kind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals))
            return false;

        // Unmetered Count uses the specialized counters.
        if (forCount &&
            MatchTimeout == System.Text.RegularExpressions.Regex.InfiniteMatchTimeout && !token.CanBeCanceled &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null)
            return false;

        // Empty literals use the shared scalar program and its boundary progression.
        return !_requiresKelvinSignFallback &&
            _ownedSliceProgram is not ({ HasBalancing: true } or { HasSharedCaptureDeclarations: true } or { HasExplicitCaptureNumbers: true }) &&
            (!_ownedSliceProgram.IgnoreCase || kind is NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) &&
            _preparedRegex.LiteralUtf8 is not { Length: 0 };
    }
}
