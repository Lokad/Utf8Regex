using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly record struct Utf8ReplacementRange(int Start, int Length, int Tag)
{
    internal Utf8ReplacementRange(int start, int length)
        : this(start, length, -1)
    {
    }

    internal bool IsSet => Start >= 0;
}

internal ref struct Utf8ReplacementRangeLedger
{
    private Utf8ReplacementRange[] _ranges;

    public Utf8ReplacementRangeLedger()
    {
        _ranges = [];
        Count = 0;
    }

    internal int Count { get; private set; }

    internal readonly Utf8ReplacementRange this[int index] => _ranges[index];

    internal readonly ReadOnlySpan<Utf8ReplacementRange> WrittenRanges => _ranges.AsSpan(0, Count);

    internal void Add(Utf8ReplacementRange range)
    {
        if (Count == _ranges.Length)
        {
            Grow();
        }

        _ranges[Count] = range;
        Count++;
    }

    internal void Dispose()
    {
        if (_ranges.Length != 0)
        {
            ArrayPool<Utf8ReplacementRange>.Shared.Return(_ranges);
            _ranges = [];
            Count = 0;
        }
    }

    private void Grow()
    {
        var newLength = _ranges.Length == 0 ? 16 : checked(_ranges.Length * 2);
        var grown = ArrayPool<Utf8ReplacementRange>.Shared.Rent(newLength);
        try
        {
            _ranges.AsSpan(0, Count).CopyTo(grown);
        }
        catch
        {
            ArrayPool<Utf8ReplacementRange>.Shared.Return(grown);
            throw;
        }

        var previous = _ranges;
        _ranges = grown;
        if (previous.Length != 0)
        {
            ArrayPool<Utf8ReplacementRange>.Shared.Return(previous);
        }
    }
}

internal struct Utf8ReplacementOutputLength
{
    internal Utf8ReplacementOutputLength(int inputLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputLength);
        Value = inputLength;
    }

    internal int Value { get; private set; }

    internal void ReplaceRange(int removedLength, int emittedLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(removedLength);
        ArgumentOutOfRangeException.ThrowIfNegative(emittedLength);
        Value = checked(Value - removedLength + emittedLength);
    }
}

internal ref struct Utf8ReplacementOutputSink
{
    private readonly Span<byte> _destination;

    internal Utf8ReplacementOutputSink(Span<byte> destination)
    {
        _destination = destination;
        BytesWritten = 0;
    }

    internal int BytesWritten { get; private set; }

    internal void Append(ReadOnlySpan<byte> value)
    {
        value.CopyTo(_destination[BytesWritten..]);
        BytesWritten += value.Length;
    }

    internal void AppendSlice(ReadOnlySpan<byte> input, int start, int length)
    {
        if (length != 0)
        {
            Append(input.Slice(start, length));
        }
    }
}

internal static class Utf8ReplacementOutput
{
    internal static bool TryCopyUnchanged(
        ReadOnlySpan<byte> input,
        Span<byte> destination,
        out int bytesWritten)
    {
        if (input.Length > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        input.CopyTo(destination);
        bytesWritten = input.Length;
        return true;
    }
}
