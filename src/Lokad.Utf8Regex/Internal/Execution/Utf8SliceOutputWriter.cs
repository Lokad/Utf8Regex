using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

// Exact managed extents and old/new/final overlap share the operation ledger.
// Acquisition/copy failure leaves the prior buffer and logical length intact.
// Successful publication transfers the final array to the caller.
internal struct Utf8SliceOutputWriter
{
    private byte[]? _buffer;
    private int _length;
    private ulong _reservedBytes;

    internal int Length => _length;
    internal readonly ReadOnlySpan<byte> WrittenBytes => _buffer is null ? [] : _buffer.AsSpan(0, _length);

    internal static bool Rent(int initialCapacity, ref Utf8SliceInvocation invocation, out Utf8SliceOutputWriter writer)
    {
        writer = default;
        if (invocation.Budget.TryChargeResultBytes(Utf8ConstructionBudget.ArrayBytes<byte>(initialCapacity))
            != Utf8ExecutionLimitKind.None) return false;
        if (Utf8InvocationOutput.TryAllocateBytes(initialCapacity, new Utf8InvocationBufferControl(ref invocation),
            out var buffer, out var reservation) != Utf8ExecutionLimitKind.None) return false;
        writer._buffer = buffer;
        writer._reservedBytes = reservation;
        return true;
    }

    internal bool Append(ReadOnlySpan<byte> bytes, ref Utf8SliceInvocation invocation)
    {
        if (!EnsureCapacity((long)_length + bytes.Length, ref invocation) || _buffer is not { } buffer) return false;
        Utf8InvocationOutput.Copy(bytes, buffer.AsSpan(_length), new Utf8InvocationBufferControl(ref invocation));
        _length += bytes.Length;
        return true;
    }

    internal bool AppendUtf16(ReadOnlySpan<char> text, ref Utf8SliceInvocation invocation)
    {
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        long needed = _length;
        for (var offset = 0; offset < text.Length;)
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var count = Utf8SliceBufferWork.Utf16BlockLength(text[offset..]);
            needed += Encoding.UTF8.GetByteCount(text.Slice(offset, count));
            offset += count;
        }
        if (!EnsureCapacity(needed, ref invocation) || _buffer is not { } buffer) return false;
        var written = _length;
        for (var offset = 0; offset < text.Length;)
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var count = Utf8SliceBufferWork.Utf16BlockLength(text[offset..]);
            written += Encoding.UTF8.GetBytes(text.Slice(offset, count), buffer.AsSpan(written));
            offset += count;
        }
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        _length = written;
        return true;
    }

    private bool EnsureCapacity(long needed, ref Utf8SliceInvocation invocation)
    {
        if (_buffer is not { } buffer) return false;
        if (needed <= buffer.Length) return true;
        var grown = Math.Max(needed, (long)buffer.Length * 2);
        if (grown > Array.MaxLength) return false;
        if (invocation.Budget.TryChargeResultBytes(Utf8ConstructionBudget.ArrayBytes<byte>((int)grown))
            != Utf8ExecutionLimitKind.None) return false;
        var control = new Utf8InvocationBufferControl(ref invocation);
        if (Utf8InvocationOutput.TryAllocateBytes((int)grown, control, out var next, out var reservation)
            != Utf8ExecutionLimitKind.None) return false;
        var adopted = false;
        try
        {
            Utf8InvocationOutput.Copy(buffer.AsSpan(0, _length), next, control);
            control.ReleaseWorking(_reservedBytes);
            _buffer = next;
            _reservedBytes = reservation;
            adopted = true;
            return true;
        }
        finally { if (!adopted) control.ReleaseWorking(reservation); }
    }

    internal bool TryToArrayAndRelease(ref Utf8SliceInvocation invocation, out byte[] result)
    {
        var control = new Utf8InvocationBufferControl(ref invocation);
        result = [];
        if (invocation.Budget.TryChargeResultBytes(Utf8ConstructionBudget.ArrayBytes<byte>(_length))
            != Utf8ExecutionLimitKind.None) return false;
        if (Utf8InvocationOutput.TryAllocateBytes(_length, control, out var next, out var reservation)
            != Utf8ExecutionLimitKind.None) return false;
        try
        {
            if (_buffer is { } buffer) Utf8InvocationOutput.Copy(buffer.AsSpan(0, _length), next, control);
            Release(ref invocation);
            result = next;
            return true;
        }
        finally { control.ReleaseWorking(reservation); }
    }

    internal byte[] ToArrayAndRelease(ref Utf8SliceInvocation invocation)
    {
        if (!TryToArrayAndRelease(ref invocation, out var result))
            throw new InvalidOperationException("Owned output exceeded the working storage budget.");
        return result;
    }

    internal void Release(ref Utf8SliceInvocation invocation)
    {
        invocation.Ledger.Release(_reservedBytes);
        _reservedBytes = 0;
        _buffer = null;
        _length = 0;
    }
}
