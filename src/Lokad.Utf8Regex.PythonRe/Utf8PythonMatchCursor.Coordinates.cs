using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonMatchCursor
{
    private const int ScalarPrefixBlockBytes = 256;
    private int[]? _scalarPrefixes;
    private ulong _scalarPrefixReservation;
    private int _scalarPrefixCount;
    private int _asciiPrefixEnd;

    /// <summary>Gets the current numbered capture's zero-based Unicode-scalar start/end positions.</summary>
    /// <returns>True for a participating capture, including empty; false with both positions -1 for an absent or unmatched group.</returns>
    /// <remarks>Works after either detailed or range-only advances. Byte coordinates keep their existing meanings. Distant mixed UTF-8 coordinates lazily extend a compact working index through queried positions; first-block queries and byte-only iteration do not. Returned integers remain valid after advance/disposal. The optional token applies only to this call alongside the creation token and original deadline. Projection failure faults the cursor and releases its scratch synchronously.</remarks>
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
            _discovery.Checkpoint();
            if (!_discovery.TryGetCaptureByteRange(groupNumber, out var byteStart, out var byteEnd)) return false;
            if (_discovery.InputDescription?.IsAscii == true || byteEnd <= _asciiPrefixEnd) { start = byteStart; end = byteEnd; }
            else if (byteEnd <= ScalarPrefixBlockBytes)
            {
                // The existing first index block can be counted directly;
                // no whole-subject fact or index is needed for these offsets.
                start = CountScalarStarts(input[..byteStart]);
                end = start + CountScalarStarts(input[byteStart..byteEnd]);
            }
            else if (_scalarPrefixes is null && IsAsciiPrefix(input, byteEnd)) { start = byteStart; end = byteEnd; }
            else
            {
                EnsureScalarPrefixes(input, byteEnd);
                start = ScalarOffset(input, byteStart);
                end = ScalarOffset(input, byteEnd);
            }
            _discovery.Checkpoint();
            return true;
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

    private bool IsAsciiPrefix(ReadOnlySpan<byte> input, int end)
    {
        // These facts describe only the queried prefix, never discovery's whole
        // subject description. A later capture may contain non-ASCII text.
        if (!Utf8Validation.SubjectIsAscii(input[_asciiPrefixEnd..end], ref _discovery.WorkingInvocation)) return false;
        _asciiPrefixEnd = end;
        return true;
    }

    private void EnsureScalarPrefixes(ReadOnlySpan<byte> input, int end)
    {
        var count = end / ScalarPrefixBlockBytes + 1;
        if (_scalarPrefixCount >= count) return;
        var prefixes = _scalarPrefixes;
        var capacity = prefixes?.Length ?? 0;
        var reservation = 0UL;
        ref var invocation = ref _discovery.WorkingInvocation;
        if (prefixes is null || prefixes.Length < count)
        {
            // Geometric capacity avoids copying the prefix for every new block.
            // Admit old/new overlap, bounded by the subject's possible entries.
            capacity = Math.Min(input.Length / ScalarPrefixBlockBytes + 1,
                Math.Max(count, Math.Max(2, capacity * 2)));
            reservation = Utf8ConstructionBudget.ArrayBytes<int>(capacity);
            if (invocation.TryReserveWorking(reservation) != Utf8ExecutionLimitKind.None)
                throw invocation.Budget.CreateResourceFailure();
        }
        try
        {
            _discovery.Checkpoint();
            if (reservation != 0)
            {
                var grown = new int[capacity];
                for (var offset = 0; offset < _scalarPrefixCount;)
                {
                    _discovery.Checkpoint();
                    var length = Math.Min(256, _scalarPrefixCount - offset);
                    prefixes!.AsSpan(offset, length).CopyTo(grown.AsSpan(offset, length));
                    offset += length;
                }
                prefixes = grown;
            }
            for (var block = Math.Max(1, _scalarPrefixCount); block < count; block++)
            {
                var to = block * ScalarPrefixBlockBytes;
                prefixes![block] = to <= _asciiPrefixEnd ? to : prefixes[block - 1] +
                    CountScalarStarts(input.Slice(to - ScalarPrefixBlockBytes, ScalarPrefixBlockBytes));
            }
            _discovery.Checkpoint();
        }
        catch { invocation.Ledger.Release(reservation); throw; }
        _scalarPrefixes = prefixes;
        _scalarPrefixCount = count;
        if (reservation != 0)
        {
            var previous = _scalarPrefixReservation;
            _scalarPrefixReservation = reservation;
            invocation.Ledger.Release(previous);
        }
    }

    private int ScalarOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        var block = byteOffset / ScalarPrefixBlockBytes;
        var count = _scalarPrefixes![block];
        var start = block * ScalarPrefixBlockBytes;
        return count + CountScalarStarts(input.Slice(start, byteOffset - start));
    }

    private int CountScalarStarts(ReadOnlySpan<byte> bytes)
    {
        // The valid-text guarantee and borrowed-input lifetime cover every query.
        // Counting starts also works when a block cuts through a scalar.
        var count = 0;
        for (var offset = 0; offset < bytes.Length;)
        {
            // Admit one bounded 32-byte unit before scanning it, with the
            // invocation's original work, cancellation and deadline authority.
            _discovery.Checkpoint();
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
        _scalarPrefixCount = 0;
        _asciiPrefixEnd = 0;
    }
}
