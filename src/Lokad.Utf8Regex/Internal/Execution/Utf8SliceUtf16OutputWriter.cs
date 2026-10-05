using System.Text;
using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Admitted UTF-16 output, preserving lone halves until the completed
/// result is encoded. Growth admits overlap; block copying and final encoding
/// share the invocation deadline/token. Reverse discovery records piece lengths
/// and reverses piece order without reversing each piece's character order.</summary>
internal struct Utf8SliceUtf16OutputWriter
{
    private char[]? _buffer;
    private int _length;
    private int[]? _pieces;
    private int _pieceCount;
    private int _pieceStart;
    private ulong _reservedChars;
    private ulong _reservedPieces;

    internal void Append(ReadOnlySpan<char> text, ref Utf8SliceInvocation invocation)
    {
        if (text.IsEmpty) return;
        var needed = (long)_length + text.Length;
        if (needed > Array.MaxLength) throw new InvalidOperationException("Owned UTF-16 output is too large.");
        var buffer = _buffer;
        if (buffer is null || needed > buffer.Length)
        {
            var capacity = (int)Math.Max(needed, Math.Min(Array.MaxLength, Math.Max(16L, (long)(buffer?.Length ?? 0) * 2)));
            var reservation = (ulong)capacity * 2;
            if (!invocation.Ledger.TryReserve(reservation))
                throw new InvalidOperationException("Owned UTF-16 output exceeded the invocation storage budget.");
            try
            {
                var next = new char[capacity];
                if (buffer is not null) Utf8SliceBufferWork.Copy(buffer.AsSpan(0, _length), next, ref invocation);
                invocation.Ledger.Release(_reservedChars);
                _buffer = buffer = next;
                _reservedChars = reservation;
            }
            catch
            {
                invocation.Ledger.Release(reservation);
                throw;
            }
        }
        Utf8SliceBufferWork.Copy(text, buffer.AsSpan(_length), ref invocation);
        _length += text.Length;
    }

    internal void MarkPiece(ref Utf8SliceInvocation invocation)
    {
        var pieces = _pieces;
        if (pieces is null || _pieceCount == pieces.Length)
        {
            var capacity = Math.Min(Array.MaxLength, Math.Max(16L, (long)(pieces?.Length ?? 0) * 2));
            if (_pieceCount >= capacity) throw new InvalidOperationException("Owned output has too many pieces.");
            var reservation = (ulong)capacity * 4;
            if (!invocation.Ledger.TryReserve(reservation))
                throw new InvalidOperationException("Owned output pieces exceeded the invocation storage budget.");
            try
            {
                var next = new int[(int)capacity];
                if (pieces is not null)
                {
                    for (var offset = 0; offset < _pieceCount; offset += Math.Min(1024, _pieceCount - offset))
                    {
                        Utf8SliceBufferWork.Checkpoint(ref invocation);
                        pieces.AsSpan(offset, Math.Min(1024, _pieceCount - offset)).CopyTo(next.AsSpan(offset));
                    }
                }
                invocation.Ledger.Release(_reservedPieces);
                _pieces = pieces = next;
                _reservedPieces = reservation;
            }
            catch
            {
                invocation.Ledger.Release(reservation);
                throw;
            }
        }
        pieces[_pieceCount++] = _length - _pieceStart;
        _pieceStart = _length;
    }

    internal void ReversePieces(ref Utf8SliceInvocation invocation)
    {
        if (_buffer is not { } buffer || _pieces is not { } pieces) return;
        Reverse(buffer.AsSpan(0, _length), ref invocation);
        var offset = 0;
        for (var piece = _pieceCount - 1; piece >= 0; piece--)
        {
            if ((piece & 1023) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
            Reverse(buffer.AsSpan(offset, pieces[piece]), ref invocation);
            offset += pieces[piece];
        }
    }

    private static void Reverse(Span<char> text, ref Utf8SliceInvocation invocation)
    {
        for (var left = 0; left < text.Length / 2; left++)
        {
            if ((left & 1023) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
            var right = text.Length - left - 1;
            (text[left], text[right]) = (text[right], text[left]);
        }
    }

    internal string ToString(ref Utf8SliceInvocation invocation)
    {
        var text = _buffer is null ? ReadOnlySpan<char>.Empty : _buffer.AsSpan(0, _length);
        var reservation = (ulong)_length * 2;
        if (!invocation.Ledger.TryReserve(reservation))
            throw new InvalidOperationException("Owned string output exceeded the invocation storage budget.");
        try
        {
            var state = new StringCopyState(text, ref invocation);
            return string.Create(_length, state, static (destination, copy) =>
                Utf8SliceBufferWork.Copy(copy.Text, destination, ref copy.Invocation));
        }
        finally { invocation.Ledger.Release(reservation); }
    }

    private ref struct StringCopyState
    {
        internal StringCopyState(ReadOnlySpan<char> text, ref Utf8SliceInvocation invocation)
        {
            Text = text;
            Invocation = ref invocation;
        }
        internal ReadOnlySpan<char> Text;
        internal ref Utf8SliceInvocation Invocation;
    }

    internal byte[] ToUtf8(ref Utf8SliceInvocation invocation)
    {
        var result = Encode(ref invocation, out var reservation);
        invocation.Ledger.Release(reservation);
        return result;
    }

    internal OperationStatus CopyUtf8(Span<byte> destination, out int bytesWritten, ref Utf8SliceInvocation invocation)
    {
        bytesWritten = 0;
        var result = Encode(ref invocation, out var reservation);
        try
        {
            if (result.Length > destination.Length) return OperationStatus.DestinationTooSmall;
            for (var offset = 0; offset < result.Length;)
            {
                Utf8SliceBufferWork.Checkpoint(ref invocation);
                var count = Math.Min(4096, result.Length - offset);
                result.AsSpan(offset, count).CopyTo(destination.Slice(offset, count));
                offset += count;
            }
            bytesWritten = result.Length;
            return OperationStatus.Done;
        }
        finally { invocation.Ledger.Release(reservation); }
    }

    private byte[] Encode(ref Utf8SliceInvocation invocation, out ulong reservation)
    {
        reservation = 0;
        var text = _buffer is null ? ReadOnlySpan<char>.Empty : _buffer.AsSpan(0, _length);
        long byteCount = 0;
        for (var offset = 0; offset < text.Length;)
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var count = EncodingBlockLength(text[offset..]);
            byteCount += Encoding.UTF8.GetByteCount(text.Slice(offset, count));
            offset += count;
        }
        if (byteCount > Array.MaxLength) throw new InvalidOperationException("Owned UTF-8 output is too large.");
        if (!invocation.Ledger.TryReserve((ulong)byteCount))
            throw new InvalidOperationException("Owned UTF-8 output exceeded the invocation storage budget.");
        reservation = (ulong)byteCount;
        try
        {
            var result = new byte[(int)byteCount];
            var written = 0;
            for (var offset = 0; offset < text.Length;)
            {
                Utf8SliceBufferWork.Checkpoint(ref invocation);
                var count = EncodingBlockLength(text[offset..]);
                written += Encoding.UTF8.GetBytes(text.Slice(offset, count), result.AsSpan(written));
                offset += count;
            }
            return result;
        }
        catch
        {
            invocation.Ledger.Release(reservation);
            reservation = 0;
            throw;
        }
    }

    private static int EncodingBlockLength(ReadOnlySpan<char> remaining)
    {
        var length = Math.Min(2048, remaining.Length);
        if (length < remaining.Length && char.IsHighSurrogate(remaining[length - 1]) && char.IsLowSurrogate(remaining[length])) length--;
        return length;
    }

    internal void Release(ref Utf8SliceInvocation invocation)
    {
        invocation.Ledger.Release(_reservedChars);
        invocation.Ledger.Release(_reservedPieces);
        this = default;
    }
}

internal enum Utf8SliceUtf16ResultKind { Text, Bytes, Destination }

/// <summary>One completed output form; destination status is meaningful only
/// for the destination form, whose buffer remains owned by its caller.</summary>
internal readonly record struct Utf8SliceUtf16Result(string Text, byte[] Bytes, OperationStatus Status, int BytesWritten);
