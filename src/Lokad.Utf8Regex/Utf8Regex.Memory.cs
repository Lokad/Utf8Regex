using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;
using Lokad.Utf8Regex.Internal;
using System.Buffers;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    // The public entry holds any program/cache admission until this returns. Cold
    // key/parser loans stay live through output, including uncached large plans.
    internal OperationStatus TryReplaceFunded(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementUtf8,
        Span<byte> destination, out int bytesWritten, CancellationToken token, IUtf8MemoryAllowance working)
    {
        token.ThrowIfCancellationRequested();
        bytesWritten = 0;
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token, working);
        try
        {
            Utf8SliceReplacementResult result;
            using (var construction = Utf8ConstructionScope.Enter(
                new() { MaxBytes = Utf8ConstructionScope.DefaultBufferBytes }, Timeout.InfiniteTimeSpan,
                token, invocation.BorrowWorkingMemoryAllowance()))
            {
                var deadline = invocation.Budget.Deadline;
                construction.Budget.Deadline = deadline;
                var control = new ReplacementPreparationControl(deadline, token);
                ValidateReplacementTemplateUtf8(replacementUtf8, control);
                var replacement = GetParsedReplacement(replacementUtf8, control);
                // Keep the template loans through output and refund them before reporting its status.
                result = OwnedSliceReplace(input, _ownedSliceProgram, replacement,
                    Utf8SliceReplacementResultKind.Destination, destination, invocation);
                construction.Complete();
            }
            FinishFundedInvocation(ref invocation);
            bytesWritten = result.BytesWritten;
            return result.Status;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw CreateExecutionResourceFailure(ref invocation); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(ReadOnlySpan<byte>.Empty); }
        finally { invocation.Ledger.Release(invocation.Ledger.ReservedBytes); }
    }

    /// <summary>Tests for a match with optional shared admission for temporary matching storage.</summary>
    /// <remarks>Null allowance inherits program funding when present, otherwise numeric limits only. An explicit allowance overrides working funding, separate from compilation and borrowed input. Numeric limits, original token and deadline still apply. Covered loans refund synchronously; refusal is terminal.</remarks>
    public bool IsMatch(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workingMemoryAllowance is not null || _programMemory is not null)
            return IsMatchFunded(input, cancellationToken, workingMemoryAllowance);

        try { return IsMatchCore(input, cancellationToken); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }

    private bool IsMatchFunded(ReadOnlySpan<byte> input, CancellationToken cancellationToken,
        IUtf8MemoryAllowance? workingMemoryAllowance)
    {
        var programMemory = _programMemory;
        programMemory?.AcquireUse();
        try
        {
            workingMemoryAllowance ??= programMemory!.Allowance;
            Utf8SliceInvocation.InitializeExclusive(out var invocation, InvocationLimits, MatchTimeout, cancellationToken, workingMemoryAllowance);
            try
            {
                var result = OwnedSliceIsMatchFromByte(input, _ownedSliceProgram,
                    _ownedSliceProgram.RightToLeft ? input.Length : 0, ref invocation);
                FinishFundedInvocation(ref invocation);
                return result;
            }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            { throw CreateExecutionResourceFailure(ref invocation); }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(ReadOnlySpan<byte>.Empty); }
            finally { invocation.Ledger.Release(invocation.Ledger.ReservedBytes); }
        }
        finally { programMemory?.ReleaseUse(); }
    }

    /// <summary>Counts matches with optional shared admission for temporary matching storage.</summary>
    /// <remarks>Null allowance inherits program funding when present, otherwise numeric limits only. An explicit allowance overrides working funding, separate from compilation and borrowed input. Covered loans refund synchronously on success, failure and interruption. Refusal is terminal.</remarks>
    public int Count(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workingMemoryAllowance is not null || _programMemory is not null)
            return CountFunded(input, cancellationToken, workingMemoryAllowance);

        try { return CountCore(input, cancellationToken); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }

    private int CountFunded(ReadOnlySpan<byte> input, CancellationToken cancellationToken,
        IUtf8MemoryAllowance? workingMemoryAllowance)
    {
        var programMemory = _programMemory;
        programMemory?.AcquireUse();
        try
        {
            workingMemoryAllowance ??= programMemory!.Allowance;
            var invocation = Utf8SliceInvocation.CreateExclusive(InvocationLimits, MatchTimeout, cancellationToken, workingMemoryAllowance);
            try
            {
                Utf8SliceBufferWork.Checkpoint(ref invocation);
                var result = OwnedSliceCountFromByte(input, _ownedSliceProgram,
                    _ownedSliceProgram.RightToLeft ? input.Length : 0, ref invocation);
                FinishFundedInvocation(ref invocation);
                return result;
            }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
                invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            { throw CreateExecutionResourceFailure(ref invocation); }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(ReadOnlySpan<byte>.Empty); }
            finally { invocation.Ledger.Release(invocation.Ledger.ReservedBytes); }
        }
        finally { programMemory?.ReleaseUse(); }
    }

    private void FinishFundedInvocation(ref Utf8SliceInvocation invocation)
    {
        var breach = invocation.CheckInput();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None) throw CreateExecutionResourceFailure(ref invocation);
    }
}
