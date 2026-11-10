using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Supplies shared invocation state to metered native literal operations.</summary>
public sealed partial class Utf8Regex
{
    // A result-producing adapter borrows discovery's actual invocation. Keep
    // complete-subject validation and the prepared literal route inside it;
    // derived coordinates are unnecessary for the borrowed byte range.
    internal bool FindLiteralForResult(ReadOnlySpan<byte> input, int startOffsetInBytes, ref Utf8SliceInvocation invocation,
        out int start, out int end, out Utf8ValidationResult validation)
    {
        start = end = -1;
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal) ||
            _preparedRegex.LiteralUtf8 is not { } literal || UsesRightToLeft())
            throw new InvalidOperationException("Literal result discovery requires an exact LTR literal.");
        validation = Utf8Validation.Subject(input, ref invocation);
        // Validation and search own their bounded checkpoints; the adapter
        // retains its final publication check.
        if (literal.Length == 0)
        {
            start = end = startOffsetInBytes;
            return true;
        }
        var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
            input, startOffsetInBytes, input.Length, false, ref invocation, out var match, out var found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal result discovery exceeded {breach}.");
        if (!found) return false;
        start = match.Index;
        end = match.Index + match.Length;
        return true;
    }

    // The adapter's literal result/count root shares this invocation with
    // validation and counting, instead of starting a second budget at shaping.
    internal int CountForResult(ReadOnlySpan<byte> input, int startOffsetInBytes,
        ref Utf8SliceInvocation invocation)
    {
        // No coordinate metadata escapes Count. Keep unmetered validation's
        // established strategy; controlled calls only need strict validity.
        if (invocation.IsUnmetered && invocation.InputValidation == Utf8InputValidation.Validate)
            _ = Utf8Validation.Subject(input, ref invocation);
        else Utf8Validation.CheckSubject(input, ref invocation);
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

    // A profile can borrow the complete ASCII language proof and bounded
    // boolean executor, under its own validated subject and invocation.
    internal bool CanUseAsciiIpv4Boolean =>
        _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount &&
        CanUseNativeScalarTokenSearch(_ownedSliceProgram, 0);

    // Borrow the adapter's already validated subject and invocation. Returning
    // false declines the route without searching; found describes an accepted
    // complete match, not a necessary start hint. The caller owns projection.
    internal bool TryFindNativeLiteralForResult(ReadOnlySpan<byte> input, int startOffsetInBytes,
        ref Utf8SliceInvocation invocation, out bool found, out int start, out int end)
    {
        found = false;
        start = end = -1;
        if (!CanFindNativeLiteralForResult) return false;
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
    // Count can borrow the existing eight-needle bounded search. Result paths
    // retain their established three-alternative eligibility.
    private bool CanUseNativeLiteralSearch(Utf8SliceProgram program, int maximumAlternatives = 3) =>
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
        !_preparedRegex.SearchPlan.HasBoundaryRequirements;

    // Metered literal value operations start their one deadline before validation.
    // Unlimited operations keep the established specialized kernels. Semantic
    // exclusions match native token enumeration; other profiles retain their VM.
    private bool TryStartNativeLiteralInvocation(ReadOnlySpan<byte> input, CancellationToken token,
        out Utf8SliceInvocation invocation)
    {
        invocation = default;
        if (InputValidation == Utf8InputValidation.Validate &&
            MatchTimeout == System.Text.RegularExpressions.Regex.InfiniteMatchTimeout && !token.CanBeCanceled &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null)
            return false;
        if (_requiresKelvinSignFallback ||
            _ownedSliceProgram is { HasBalancing: true } or { HasSharedCaptureDeclarations: true } or { HasExplicitCaptureNumbers: true } ||
            _ownedSliceProgram is { IgnoreCase: true } && _preparedRegex.ExecutionKind is not
                (NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) ||
            _preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or
                NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) ||
            _preparedRegex.LiteralUtf8 is { Length: 0 })
            return false;
        // Empty literals use the shared scalar program and its boundary progression.
        invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token) { InputValidation = InputValidation };
        // These roots consume bytes or project their selected range separately.
        // Keep full metered validity without deriving discarded UTF-16 metadata.
        Utf8Validation.CheckSubject(input, ref invocation);
        return true;
    }
}
