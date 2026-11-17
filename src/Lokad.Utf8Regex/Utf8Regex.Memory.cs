using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;
using Lokad.Utf8Regex.Internal;
using System.Buffers;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    // The owning facade holds program/cache admission until this returns. Cold
    // key/parser loans stay live through output, including uncached large plans.
    internal OperationStatus TryReplaceFunded(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementUtf8,
        Span<byte> destination, out int bytesWritten, CancellationToken token, IUtf8MemoryAllowance working)
    {
        token.ThrowIfCancellationRequested();
        bytesWritten = 0;
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token, working);
        try
        {
            using var construction = Utf8ConstructionScope.Enter(
                new() { MaxBytes = Utf8ConstructionScope.DefaultBufferBytes }, Timeout.InfiniteTimeSpan,
                token, invocation.BorrowWorkingMemoryAllowance());
            var deadline = invocation.Budget.Deadline;
            construction.Budget.Deadline = deadline;
            var control = new ReplacementPreparationControl(deadline, token);
            ValidateReplacementTemplateUtf8(replacementUtf8, control);
            var replacement = GetParsedReplacement(replacementUtf8, control);
            // Funded calls use the existing controlled scalar replacement route,
            // including its native literal candidate discovery and NB/RTL rules.
            var result = OwnedSliceReplace(input, _ownedSliceProgram, replacement,
                Utf8SliceReplacementResultKind.Destination, destination, invocation);
            FinishFundedInvocation(ref invocation);
            bytesWritten = result.BytesWritten;
            construction.Complete();
            return result.Status;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw CreateExecutionResourceFailure(ref invocation); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(ReadOnlySpan<byte>.Empty); }
        finally { invocation.Ledger.Release(invocation.Ledger.ReservedBytes); }
    }

    /// <summary>Tests for a match with optional shared admission for temporary matching storage.</summary>
    /// <remarks>Null allowance uses the engine numeric-policy route. Non-null allowance admits temporary storage separately from compilation and borrowed input. Numeric limits, original token and deadline still apply. Covered loans refund synchronously; refusal is terminal.</remarks>
    public bool IsMatch(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workingMemoryAllowance is null)
        {
            try { return IsMatchCore(input, cancellationToken); }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        }
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken, workingMemoryAllowance);
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            bool result;
            if (CanUseNativeLiteralSearch(_ownedSliceProgram))
            {
                var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
                    input, 0, input.Length, false, ref invocation, out _, out result);
                if (breach != Utf8ExecutionLimitKind.None) throw CreateExecutionResourceFailure(ref invocation);
            }
            else result = OwnedSliceIsMatchFromByte(input, _ownedSliceProgram,
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

    /// <summary>Counts matches with optional shared admission for temporary matching storage.</summary>
    /// <remarks>Null allowance uses the engine numeric-policy route. Non-null allowance admits temporary storage separately from compilation and borrowed input. Work/result policies stay cumulative; covered loans refund synchronously on success, failure and interruption. Refusal is terminal.</remarks>
    public int Count(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workingMemoryAllowance is null)
        {
            try { return CountCore(input, cancellationToken); }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        }
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken, workingMemoryAllowance);
        try
        {
            int result;
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            if (CanUseNativeLiteralSearch(_ownedSliceProgram, maximumAlternatives: 8))
            {
                var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(_preparedRegex.SearchPlan,
                    input, false, ref invocation, out result);
                if (breach != Utf8ExecutionLimitKind.None) throw CreateExecutionResourceFailure(ref invocation);
            }
            else result = OwnedSliceCountFromByte(input, _ownedSliceProgram,
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

    private void FinishFundedInvocation(ref Utf8SliceInvocation invocation)
    {
        var breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None) throw CreateExecutionResourceFailure(ref invocation);
    }
}
