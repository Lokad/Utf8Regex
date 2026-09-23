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
        if (_buffer is null)
        {
            return false;
        }

        var needed = (long)_length + bytes.Length;
        if (needed > _buffer.Length)
        {
            var grown = (long)_buffer.Length * 2;
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
            Array.Copy(_buffer, next, _length);
            invocation.Ledger.Release(_reservedBytes);
            _buffer = next;
            _reservedBytes = (ulong)grown;
        }

        bytes.CopyTo(_buffer.AsSpan(_length));
        _length += bytes.Length;
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
