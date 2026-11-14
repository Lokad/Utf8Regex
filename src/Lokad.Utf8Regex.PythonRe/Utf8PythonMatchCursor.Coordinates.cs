using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonMatchCursor
{
    /// <summary>Gets the current numbered capture's zero-based Unicode-scalar start/end positions.</summary>
    /// <returns>True for a participating capture, including empty; false with both positions -1 for an absent or unmatched group.</returns>
    /// <remarks>Works after either detailed or range-only advances. Byte coordinates keep their existing meanings. Distant mixed UTF-8 coordinates lazily extend a working index shared with UTF-16 projection through queried positions; first-block queries and byte-only iteration do not acquire it. Returned integers remain valid after advance/disposal. The optional token applies only to this call alongside the creation token and original deadline. Projection failure faults the cursor and releases its scratch synchronously.</remarks>
    /// <exception cref="InvalidOperationException">The cursor is not positioned on a match or is being accessed concurrently.</exception>
    /// <exception cref="ObjectDisposedException">The cursor is disposed.</exception>
    /// <exception cref="OperationCanceledException">The creation or current-call token is canceled; the original caller token is preserved.</exception>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">The original cursor deadline expired.</exception>
    /// <exception cref="Utf8ResourceLimitExceededException">The cumulative work or live working budget is exceeded.</exception>
    /// <exception cref="Utf8MemoryAdmissionException">The caller-owned allowance refuses the index.</exception>
    public bool TryGetScalarRange(int groupNumber, out int startOffsetInScalars, out int endOffsetInScalars,
        CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            ThrowIfDisposed();
            if (_status is not (Status.Positioned or Status.RangePositioned))
                throw new InvalidOperationException("The cursor is not positioned on a match.");
            if (!cancellationToken.CanBeCanceled)
                return GetScalarRange(groupNumber, out startOffsetInScalars, out endOffsetInScalars);

            // Preserve a shared budget even when projection failure closes discovery.
            _discovery.WorkingInvocation.ShareStorageForCursor();
            var invocation = _discovery.WorkingInvocation;
            var original = invocation.Budget.ExchangeCancellationToken(cancellationToken);
            CancellationTokenSource? linked = null;
            try
            {
                if (original.CanBeCanceled && original != cancellationToken)
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(original, cancellationToken);
                    invocation.Budget.ExchangeCancellationToken(linked.Token);
                }
                return GetScalarRange(groupNumber, out startOffsetInScalars, out endOffsetInScalars);
            }
            catch (OperationCanceledException)
            {
                original.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
            finally
            {
                invocation.Budget.ExchangeCancellationToken(original);
                linked?.Dispose();
            }
        }
        finally { Exit(); }
    }

    private bool GetScalarRange(int groupNumber, out int start, out int end)
    {
        start = end = -1;
        try
        {
            return _discovery.TryGetScalarRange(_input.Span, groupNumber, out start, out end);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            _discovery.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            var failure = _discovery.WorkingInvocation.Budget.CreateResourceFailure();
            Close(Status.Faulted);
            throw failure;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            var failure = _discovery.TimeoutFailure(_input.Span);
            Close(Status.Faulted);
            throw failure;
        }
        catch { Close(Status.Faulted); throw; }
    }

}
