using Lokad.Utf8Regex.Internal.Execution;

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
    private Utf8InvocationStateBuffer<Utf8ReplacementRange> _ranges;
    internal readonly int Count => _ranges.Count;
    internal readonly Utf8ReplacementRange this[int index] => _ranges[index];
    internal readonly ReadOnlySpan<Utf8ReplacementRange> WrittenRanges => _ranges.WrittenItems;

    internal Utf8ExecutionLimitKind TryAdd<TControl>(Utf8ReplacementRange range, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        var breach = control.TryChargeWorkspaceStep();
        if (breach == Utf8ExecutionLimitKind.None)
            breach = control.TryChargeResultBytes(_ranges.PendingGrowthBytes);
        return breach == Utf8ExecutionLimitKind.None ? _ranges.TryPush(range, control) : breach;
    }

    internal void Add<TControl>(Utf8ReplacementRange range, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        var breach = TryAdd(range, control);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned replacement ranges exceeded {breach}.");
    }

    internal void Dispose<TControl>(scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
        => _ranges.Release(control);
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

    internal void Append<TControl>(ReadOnlySpan<byte> value, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        Utf8InvocationOutput.Copy(value, _destination[BytesWritten..], control);
        BytesWritten += value.Length;
    }

    internal void AppendSlice<TControl>(ReadOnlySpan<byte> input, int start, int length, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        if (length != 0)
        {
            Append(input.Slice(start, length), control);
        }
    }
}

internal static class Utf8ReplacementOutput
{
    internal static bool TryCopyUnchanged<TControl>(
        ReadOnlySpan<byte> input,
        Span<byte> destination,
        out int bytesWritten, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        if (input.Length > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        Utf8InvocationOutput.Copy(input, destination, control);
        bytesWritten = input.Length;
        return true;
    }
}
