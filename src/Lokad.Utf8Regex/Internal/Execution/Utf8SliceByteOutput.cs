namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Owns the invocation and admitted buffers for one byte-template operation.</summary>
internal sealed class Utf8SliceByteOutputScope
{
    // Conservative fixed control-storage charges, in addition to array payloads.
    internal const ulong ControlBytes = 256;
    private Utf8SliceByteOutputBuffer? _buffers;
    private bool _closed;
    internal Utf8SliceInvocation Invocation;

    private Utf8SliceByteOutputScope(Utf8SliceInvocation initialInvocation)
    {
        // This moves freshly initialized state once. Every subsequent phase
        // borrows the field by reference; budgets are never copied during work.
        Invocation = initialInvocation;
    }

    // Controlled callers retain the same authority when scope admission fails.
    internal static Utf8SliceByteOutputScope Enter(Utf8SliceInvocation invocation)
    {
        // Budget/deadline owner metadata has separate scope; the actual output
        // scope and its fixed control storage remain admitted before acquisition.
        if (invocation.TryReserveWorking(ControlBytes) != Utf8ExecutionLimitKind.None)
            throw invocation.Budget.CreateResourceFailure();
        try { return CreateAdmittedScope(invocation); }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            invocation.Ledger.Release(ControlBytes);
            throw invocation.Budget.CreateResourceFailure();
        }
        catch { invocation.Ledger.Release(ControlBytes); throw; }
    }

    private static Utf8SliceByteOutputScope CreateAdmittedScope(Utf8SliceInvocation invocation)
    {
        Utf8SliceBufferWork.ChargeResultBytes(ControlBytes, ref invocation);
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        return new(invocation);
    }

    internal Utf8SliceByteOutputBuffer CreateBuffer()
    {
        ThrowIfClosed();
        Utf8SliceBufferWork.Checkpoint(ref Invocation);
        Utf8SliceBufferWork.ChargeResultBytes(ControlBytes, ref Invocation);
        if (new Utf8InvocationBufferControl(ref Invocation).TryReserveWorking(ControlBytes) != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned byte output exceeded the invocation storage budget.");
        try
        {
            var buffer = new Utf8SliceByteOutputBuffer(this) { Next = _buffers };
            _buffers = buffer;
            return buffer;
        }
        catch { Invocation.Ledger.Release(ControlBytes); throw; }
    }

    internal void Retain(Utf8SliceByteOutputBuffer? returnedBuffer)
    {
        // Keep the selected template buffer and release other owned storage.
        // Published byte arrays have independent storage and lifetime.
        var buffer = _buffers;
        _buffers = returnedBuffer;
        while (buffer is not null)
        {
            var next = buffer.Next;
            buffer.Next = null;
            if (!ReferenceEquals(buffer, returnedBuffer)) buffer.Release();
            buffer = next;
        }
    }

    internal void ThrowIfClosed()
    {
        if (_closed) throw new InvalidOperationException("The replacement output scope has ended.");
    }

    internal void Release()
    {
        if (_closed) return;
        _closed = true;
        Retain(null);
        Invocation.Ledger.Release(ControlBytes);
    }
}

/// <summary>Byte-buffer storage owned by a template invocation. Payload and
/// growth overlap are admitted before allocation.
/// Reverse replacement records byte-piece lengths without validating fragments.</summary>
internal sealed class Utf8SliceByteOutputBuffer(Utf8SliceByteOutputScope scope)
{
    private byte[]? _buffer;
    private int _length;
    private int[]? _pieces;
    private int _pieceCount;
    private int _pieceStart;
    private ulong _reservedBytes;
    private ulong _reservedPieces;
    private bool _released;
    internal Utf8SliceByteOutputBuffer? Next;

    internal void Append(ReadOnlySpan<byte> bytes)
    {
        CheckLive();
        Utf8SliceBufferWork.Checkpoint(ref scope.Invocation);
        if (bytes.IsEmpty) return;
        var buffer = EnsureCapacity((long)_length + bytes.Length);
        Utf8SliceBufferWork.Copy(bytes, buffer.AsSpan(_length), ref scope.Invocation);
        _length += bytes.Length;
    }

    private byte[] EnsureCapacity(long needed)
    {
        if (needed > Array.MaxLength) throw new InvalidOperationException("Owned byte output is too large.");
        var buffer = _buffer;
        if (buffer is not null && needed <= buffer.Length) return buffer;
        var capacity = (int)Math.Max(needed, Math.Min(Array.MaxLength, Math.Max(16L, (long)(buffer?.Length ?? 0) * 2)));
        var reservation = Utf8ConstructionBudget.ArrayBytes<byte>(capacity);
        Utf8SliceBufferWork.ChargeResultBytes(reservation, ref scope.Invocation);
        if (new Utf8InvocationBufferControl(ref scope.Invocation).TryReserveWorking(reservation) != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned byte output exceeded the invocation storage budget.");
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref scope.Invocation);
            var next = new byte[capacity];
            if (buffer is not null) Utf8SliceBufferWork.Copy(buffer.AsSpan(0, _length), next, ref scope.Invocation);
            scope.Invocation.Ledger.Release(_reservedBytes);
            _reservedBytes = reservation;
            return _buffer = next;
        }
        catch { scope.Invocation.Ledger.Release(reservation); throw; }
    }

    internal void MarkPiece()
    {
        CheckLive();
        var pieces = _pieces;
        if (pieces is null || _pieceCount == pieces.Length)
        {
            var capacity = Math.Min(Array.MaxLength, Math.Max(16L, (long)(pieces?.Length ?? 0) * 2));
            if (_pieceCount >= capacity) throw new InvalidOperationException("Owned byte output has too many pieces.");
            var reservation = Utf8ConstructionBudget.ArrayBytes<int>((int)capacity);
            Utf8SliceBufferWork.ChargeResultBytes(reservation, ref scope.Invocation);
            if (new Utf8InvocationBufferControl(ref scope.Invocation).TryReserveWorking(reservation) != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned byte pieces exceeded the invocation storage budget.");
            try
            {
                Utf8SliceBufferWork.Checkpoint(ref scope.Invocation);
                var next = new int[(int)capacity];
                if (pieces is not null)
                {
                    for (var offset = 0; offset < _pieceCount;)
                    {
                        Utf8SliceBufferWork.Checkpoint(ref scope.Invocation);
                        var count = Math.Min(1024, _pieceCount - offset);
                        pieces.AsSpan(offset, count).CopyTo(next.AsSpan(offset, count));
                        offset += count;
                    }
                }
                scope.Invocation.Ledger.Release(_reservedPieces);
                _reservedPieces = reservation;
                _pieces = pieces = next;
            }
            catch { scope.Invocation.Ledger.Release(reservation); throw; }
        }
        pieces[_pieceCount++] = _length - _pieceStart;
        _pieceStart = _length;
    }

    internal void ReversePieces()
    {
        CheckLive();
        if (_buffer is not { } buffer || _pieces is not { } pieces) return;
        Reverse(buffer.AsSpan(0, _length));
        var offset = 0;
        for (var piece = _pieceCount - 1; piece >= 0; piece--)
        {
            if ((piece & 1023) == 0) Utf8SliceBufferWork.Checkpoint(ref scope.Invocation);
            Reverse(buffer.AsSpan(offset, pieces[piece]));
            offset += pieces[piece];
        }
    }

    private void Reverse(Span<byte> bytes)
    {
        for (var left = 0; left < bytes.Length / 2; left++)
        {
            if ((left & 2047) == 0) Utf8SliceBufferWork.Checkpoint(ref scope.Invocation);
            var right = bytes.Length - left - 1;
            (bytes[left], bytes[right]) = (bytes[right], bytes[left]);
        }
    }

    internal ReadOnlySpan<byte> GetValidatedBytes()
    {
        CheckLive();
        var bytes = _buffer is null ? ReadOnlySpan<byte>.Empty : _buffer.AsSpan(0, _length);
        Utf8SliceBufferWork.ValidateUtf8(bytes, ref scope.Invocation);
        return bytes;
    }

    internal byte[] ToArray()
    {
        var bytes = GetValidatedBytes();
        var reservation = Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length);
        Utf8SliceBufferWork.ChargeResultBytes(reservation, ref scope.Invocation);
        if (new Utf8InvocationBufferControl(ref scope.Invocation).TryReserveWorking(reservation) != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned byte result exceeded the invocation storage budget.");
        try
        {
            var result = bytes.IsEmpty ? [] : new byte[bytes.Length];
            Utf8SliceBufferWork.Copy(bytes, result, ref scope.Invocation);
            return result;
        }
        finally { scope.Invocation.Ledger.Release(reservation); }
    }

    internal System.Buffers.OperationStatus CopyUtf8(Span<byte> destination, out int written)
    {
        written = 0;
        var bytes = GetValidatedBytes();
        if (destination.Length < bytes.Length) return System.Buffers.OperationStatus.DestinationTooSmall;
        // The assembled buffer already owns the bytes. Retain it throughout the
        // bounded copy; destination sizing needs no second encoded allocation.
        Utf8SliceBufferWork.Copy(bytes, destination, ref scope.Invocation);
        written = bytes.Length;
        return System.Buffers.OperationStatus.Done;
    }

    internal void Release()
    {
        if (_released) return;
        _released = true;
        scope.Invocation.Ledger.Release(_reservedBytes);
        scope.Invocation.Ledger.Release(_reservedPieces);
        scope.Invocation.Ledger.Release(Utf8SliceByteOutputScope.ControlBytes);
        _buffer = null;
        _pieces = null;
        Next = null;
    }

    private void CheckLive()
    {
        scope.ThrowIfClosed();
        if (_released) throw new InvalidOperationException("The replacement output buffer has ended.");
    }
}
