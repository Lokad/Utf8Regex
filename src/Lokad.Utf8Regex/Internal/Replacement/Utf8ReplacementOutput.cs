using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly record struct Utf8ReplacementRange(int Start, int Length);

internal ref struct Utf8ReplacementRangeLedger
{
    private Utf8InvocationStateBuffer<Utf8ReplacementRange> _ranges;
    internal readonly int Count => _ranges.Count;
    internal readonly ulong PendingGrowthBytes => _ranges.PendingGrowthBytes;
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
    internal static void CopyLiteralRanges(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacement, Span<byte> destination,
        ReadOnlySpan<Utf8ReplacementRange> matches, bool reverse, scoped Utf8InvocationBufferControl control)
    {
        var sourcePosition = 0;
        var sink = new Utf8ReplacementOutputSink(destination);
        // Matching order is preserved during discovery. Emit in subject order,
        // including the empty RTL match before a nonempty range with that start.
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[reverse ? matches.Length - index - 1 : index];
            sink.AppendSlice(input, sourcePosition, match.Start - sourcePosition, control);
            sink.Append(replacement, control);
            sourcePosition = match.Start + match.Length;
        }

        sink.Append(input[sourcePosition..], control);
    }

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
