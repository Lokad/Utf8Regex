using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;

// value entry points, split from the Python facade along the value-operation ownership boundary.
/// <summary>Value entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{

    /// <summary>Tests for a match at or after a zero-based UTF-8 byte offset, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsMatchCore(input, startOffsetInBytes, cancellationToken);
    }

    private bool IsMatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        if (_canUseZeroOffsetUtf8SearchValueFastPath && startOffsetInBytes == 0 &&
            !HasFiniteExecutionLimits && !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan && _utf8Regex is not null)
            return _utf8Regex.IsMatch(input, cancellationToken);
        return IsMatchScalar(input, startOffsetInBytes, cancellationToken);
    }

    private Utf8PythonValueMatch SearchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        if (_canUseZeroOffsetUtf8SearchValueFastPath && startOffsetInBytes == 0 && _utf8Regex is not null)
        {
            return MatchValueNative(input, startOffsetInBytes, 0, cancellationToken);
        }
        return MatchValueScalar(input, startOffsetInBytes, 0, cancellationToken);
    }

    private Utf8PythonValueMatch MatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        if (_canUseZeroOffsetUtf8SearchValueFastPath && startOffsetInBytes == 0 &&
            !HasFiniteExecutionLimits && !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan && _utf8Regex is not null)
        {
            return MatchValueNative(input, startOffsetInBytes, 1, cancellationToken);
        }
        return MatchValueScalar(input, startOffsetInBytes, 1, cancellationToken);
    }

    /// <summary>Matches a Unicode scalar subject and optionally fills caller-owned last-capture byte ranges.</summary>
    /// <remarks>Match anchors at the byte start; Search seeks from it, FullMatch requires the complete remaining prefix through end. Start/end preserve absolute prefix and lookbehind context and must fall on scalar boundaries. Short capture buffers request prefix slots including zero; excess entries stay untouched. Caller storage is excluded from result-byte admission. Pre-cancellation leaves output unchanged; later failure clears the requested prefix. Returned value bytes borrow unchanged input independently of capture-slot reuse. Completion-order metadata remains available on CreateMatchCursor. A null working account inherits program/numeric funding; supplied refusal is terminal.</remarks>
    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes = 0,
        scoped Span<Utf8PythonCaptureRange> captures = default, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null, Utf8PythonMatchMode mode = Utf8PythonMatchMode.Match,
        int? endOffsetInBytes = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = EnterProgramUse();
        var output = captures[..Math.Min(captures.Length, _scalarProgram.GroupCount + 1)];
        try
        {
            if ((uint)mode > (uint)Utf8PythonMatchMode.FullMatch)
                throw new ArgumentOutOfRangeException(nameof(mode));
            var end = GetScalarEndOffset(input, endOffsetInBytes);
            var subject = input[..end];
            if (workingMemoryAllowance is null && (output.IsEmpty || _scalarProgram.GroupCount == 0))
            {
                // Keep genuine no-capture native/repeat workers and their original
                // interruption/admission intent. A sole group-zero slot needs no VM arrays.
                var result = mode switch
                {
                    Utf8PythonMatchMode.Search => SearchCore(subject, startOffsetInBytes, cancellationToken),
                    Utf8PythonMatchMode.FullMatch => FullMatchCore(subject, startOffsetInBytes, cancellationToken),
                    _ => MatchCore(subject, startOffsetInBytes, cancellationToken)
                };
                if (!output.IsEmpty)
                    output[0] = result.Success ? new Utf8PythonCaptureRange { Success = true,
                        StartOffsetInBytes = result.StartOffsetInBytes, EndOffsetInBytes = result.EndOffsetInBytes } : default;
                return result;
            }
            return MatchValueScalar(subject, startOffsetInBytes, (int)mode, cancellationToken,
                output, workingMemoryAllowance);
        }
        catch { output.Clear(); throw; }
    }

    private Utf8PythonValueMatch FullMatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        ValidateScalarStartOffset(input, startOffsetInBytes);
        if (CanUseLiteralFullMatch)
        {
            return MatchValueNative(input, startOffsetInBytes, 2, cancellationToken);
        }
        if (_scalarProgram.GroupCount == 0 &&
            _scalarProgram.Instructions is { Length: >= 5 and <= 64 } code &&
            code[0].Kind == Utf8SliceInstructionKind.RepeatGreedy &&
            code[1].Kind == Utf8SliceInstructionKind.Literal)
            return MatchLiteralRepeatFullValue(input, startOffsetInBytes, cancellationToken);
        return MatchValueScalar(input, startOffsetInBytes, 2, cancellationToken);
    }

    private Utf8PythonValueMatch MatchValueNative(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, CancellationToken cancellationToken)
    {
        var invocation = CreateInvocation(cancellationToken);
        return MaterializeNativeMatchValue(input, startOffsetInBytes, kind, ref invocation);
    }

    private int CountCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        Utf8ValidatedInput.GetBytePosition(input, startOffsetInBytes, nameof(startOffsetInBytes));
        // The native rendering intentionally has exact references. Other shapes
        // keep lexical profile comparison, apart from the proven exact literal.
        if (_canUseZeroOffsetUtf8SearchValueFastPath && startOffsetInBytes == 0 &&
            !HasFiniteExecutionLimits && !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan && _utf8Regex is not null)
            return _utf8Regex.Count(input, cancellationToken);
        // Count discards captures. Complete native eligibility proves the whole
        // match even when the returned-capture iteration classifier declines it.
        if (_utf8Regex is { } prepared && prepared.CanCountNativeLiteralForResult)
            return CountPreparedLiteral(prepared, input, startOffsetInBytes, cancellationToken);
        return CountOwnedSlicePythonStyle(input, startOffsetInBytes, ScalarProgram, cancellationToken);
    }

    private int CountOwnedSlicePythonStyle(ReadOnlySpan<byte> input, int startOffsetInBytes,
        Utf8SliceProgram program, CancellationToken cancellationToken)
    {
        // Count observes no captures. Reuse the retained Core's dependency proof
        // for iteration-compatible roots, but execute this profile's program:
        // lexical folding/classes remain authoritative. Unknown/dependent roots
        // and nullable progression retain their capture cursor.
        if (!program.CanMatchEmpty && (program.GroupCount == 0 || Utf8SliceRunner.CanCountTerminalScalar(program) ||
            _canUseUtf8IterationFastPath && _utf8Regex is { CanOmitValueCaptureSlots: true }))
            return CountNonEmptyScalar(input, startOffsetInBytes, program, cancellationToken);
        var cursor = new PythonReScalarMatchCursor(input, program,
            startOffsetInBytes, MatchTimeout, _utf8Regex, this,
            CreateInvocation(cancellationToken));
        try
        {
            var count = 0;
            while (cursor.MoveNext()) count++;
            return count;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            cursor.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw cursor.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw cursor.TimeoutFailure(); }
        finally { cursor.Dispose(); }
    }

    private int CountNonEmptyScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        Utf8SliceProgram program, CancellationToken cancellationToken)
    {
        var invocation = CreateInvocation(cancellationToken);
        try
        {
            Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
            var impossible = program.Instructions[0].Kind == Utf8SliceInstructionKind.Fail ||
                !program.IgnoreCase && _utf8Regex is { } required &&
                required.RejectMissingExactRequiredLiteral(input, ref invocation);
            var count = 0;
            if (!impossible)
            {
                var result = Utf8SliceRunner.CountFromBytes(program, input, startOffsetInBytes,
                    [], [], ref invocation, _utf8Regex?.Inspection.SearchPlan ?? default);
                if (result.Breach != Utf8ExecutionLimitKind.None)
                    throw invocation.Budget.CreateResourceFailure();
                count = result.Count;
            }
            if (invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
                throw invocation.Budget.CreateResourceFailure();
            invocation.Budget.ThrowIfCancellationRequested();
            return count;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw invocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw CreateMatchTimeoutException(input, Pattern); }
    }

    /// <summary>Counts non-overlapping matches at or after a zero-based UTF-8 byte offset, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CountCore(input, startOffsetInBytes, cancellationToken);
    }

}
