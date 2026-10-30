using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonMatchCursor
{
    private const int ScalarPrefixBlockBytes = 256;
    private int[]? _scalarPrefixes;
    private ulong _scalarPrefixReservation;

    /// <summary>Gets the current numbered capture's zero-based Unicode-scalar start/end positions.</summary>
    /// <returns>True for a participating capture, including empty; false with both positions -1 for an absent or unmatched group.</returns>
    /// <remarks>Works after either detailed or range-only advances. Byte coordinates keep their existing meanings. Mixed UTF-8 input lazily acquires a compact working index; byte-only iteration does not. Returned integers remain valid after advance/disposal. The optional token applies only to this call alongside the creation token and original deadline. Projection failure faults the cursor and releases its scratch synchronously.</remarks>
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
            var input = _input.Span;
            _discovery.Checkpoint(input);
            if (!_discovery.TryGetCaptureByteRange(groupNumber, out var byteStart, out var byteEnd)) return false;
            if (_discovery.IsAscii) { start = byteStart; end = byteEnd; }
            else
            {
                EnsureScalarPrefixes(input);
                start = ScalarOffset(input, byteStart);
                end = ScalarOffset(input, byteEnd);
            }
            _discovery.Checkpoint(input);
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            var failure = _discovery.TimeoutFailure(_input.Span);
            Close(Status.Faulted);
            throw failure;
        }
        catch { Close(Status.Faulted); throw; }
    }

    private void EnsureScalarPrefixes(ReadOnlySpan<byte> input)
    {
        if (_scalarPrefixes is not null) return;
        var count = input.Length / ScalarPrefixBlockBytes + 1;
        if (count == 1) { _scalarPrefixes = []; return; }
        var bytes = Utf8ConstructionBudget.ArrayBytes<int>(count);
        ref var invocation = ref _discovery.WorkingInvocation;
        if (invocation.TryReserveWorking(bytes) != Utf8ExecutionLimitKind.None)
            throw invocation.Budget.CreateResourceFailure();
        try
        {
            _discovery.Checkpoint(input);
            var prefixes = new int[count];
            for (var block = 1; block < prefixes.Length; block++)
                prefixes[block] = prefixes[block - 1] + CountScalarStarts(input.Slice((block - 1) * ScalarPrefixBlockBytes, ScalarPrefixBlockBytes));
            _discovery.Checkpoint(input);
            _scalarPrefixes = prefixes;
            _scalarPrefixReservation = bytes;
        }
        catch { invocation.Ledger.Release(bytes); throw; }
    }

    private int ScalarOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        var block = byteOffset / ScalarPrefixBlockBytes;
        var count = _scalarPrefixes!.Length == 0 ? 0 : _scalarPrefixes[block];
        var start = block * ScalarPrefixBlockBytes;
        return count + CountScalarStarts(input.Slice(start, byteOffset - start));
    }

    private int CountScalarStarts(ReadOnlySpan<byte> bytes)
    {
        // Validation and the borrowed-input lifetime precede every query.
        // Counting starts also works when a block cuts through a scalar.
        var count = 0;
        for (var offset = 0; offset < bytes.Length;)
        {
            // Admit one bounded 32-byte unit before scanning it, with the
            // invocation's original work, cancellation and deadline authority.
            _discovery.Checkpoint(_input.Span);
            var block = bytes.Slice(offset, Math.Min(32, bytes.Length - offset));
            foreach (var value in block) if ((value & 0xc0) != 0x80) count++;
            offset += block.Length;
        }
        return count;
    }

    private void ReleaseScalarPrefixes()
    {
        _scalarPrefixes = null;
        _discovery.WorkingInvocation.Ledger.Release(_scalarPrefixReservation);
        _scalarPrefixReservation = 0;
    }
}
