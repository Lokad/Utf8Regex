using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Ledger-charged growable byte buffer for owned result shaping.
/// Growth reserves the new extent while the old buffer is still held, then
/// releases the old extent, so copy overlap is admitted. The finished array is
/// caller-owned retained storage: <see cref="ToArrayAndRelease"/> copies it out
/// and releases the working reservation. Release is idempotent.</summary>
internal struct Utf8SliceOutputWriter
{
    private byte[]? _buffer;
    private int _length;
    private ulong _reservedBytes;

    internal int Length => _length;

    internal static bool Rent(int initialCapacity, ref Utf8SliceInvocation invocation, out Utf8SliceOutputWriter writer)
    {
        writer = default;
        if (!invocation.Ledger.TryReserve((ulong)initialCapacity))
        {
            return false;
        }

        writer._buffer = new byte[initialCapacity];
        writer._reservedBytes = (ulong)initialCapacity;
        return true;
    }

    internal bool Append(ReadOnlySpan<byte> bytes, ref Utf8SliceInvocation invocation)
    {
        if (!EnsureCapacity((long)_length + bytes.Length, ref invocation))
        {
            return false;
        }

        // EnsureCapacity established a buffer; re-read it for null-state flow.
        if (_buffer is not { } buffer)
        {
            return false;
        }

        bytes.CopyTo(buffer.AsSpan(_length));
        _length += bytes.Length;
        return true;
    }

    // Encodes UTF-16 text directly into spare capacity so shaped expansions
    // never stage through temporary byte arrays. Shares the ledger-charged
    // growth protocol with Append.
    internal bool AppendUtf16(ReadOnlySpan<char> text, ref Utf8SliceInvocation invocation)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);
        if (!EnsureCapacity((long)_length + byteCount, ref invocation))
        {
            return false;
        }

        // EnsureCapacity established a buffer; re-read it for null-state flow.
        if (_buffer is not { } buffer)
        {
            return false;
        }

        _length += Encoding.UTF8.GetBytes(text, buffer.AsSpan(_length));
        return true;
    }

    private bool EnsureCapacity(long needed, ref Utf8SliceInvocation invocation)
    {
        var buffer = _buffer;
        if (buffer is null)
        {
            return false;
        }

        if (needed <= buffer.Length)
        {
            return true;
        }

        var grown = (long)buffer.Length * 2;
        if (grown < needed)
        {
            grown = needed;
        }

        if (grown > Array.MaxLength)
        {
            return false;
        }

        if (!invocation.Ledger.TryReserve((ulong)grown))
        {
            return false;
        }

        var next = new byte[(int)grown];
        Array.Copy(buffer, next, _length);
        invocation.Ledger.Release(_reservedBytes);
        _buffer = next;
        _reservedBytes = (ulong)grown;
        return true;
    }

    internal byte[] ToArrayAndRelease(ref Utf8SliceInvocation invocation)
    {
        var result = _buffer is null ? [] : _buffer.AsSpan(0, _length).ToArray();
        Release(ref invocation);
        return result;
    }

    internal void Release(ref Utf8SliceInvocation invocation)
    {
        if (_reservedBytes != 0)
        {
            invocation.Ledger.Release(_reservedBytes);
            _reservedBytes = 0;
        }

        _buffer = null;
        _length = 0;
    }
}
