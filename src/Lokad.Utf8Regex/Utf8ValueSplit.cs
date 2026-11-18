namespace Lokad.Utf8Regex;

/// <summary>Provides a stack-only byte view of one split segment over the original UTF-8 input.</summary>
/// <remarks>The view and any spans obtained from it must not outlive the unchanged input supplied to the split operation.</remarks>
public readonly ref struct Utf8ValueSplit
{
    private readonly ReadOnlySpan<byte> _input;

    internal Utf8ValueSplit(ReadOnlySpan<byte> input, int indexInBytes, int lengthInBytes)
    {
        _input = input;
        IndexInBytes = indexInBytes;
        LengthInBytes = lengthInBytes;
    }

    /// <summary>Gets the segment start as a zero-based byte offset in the original UTF-8 input.</summary>
    public int IndexInBytes { get; }

    /// <summary>Gets the segment length in bytes in the original UTF-8 input.</summary>
    public int LengthInBytes { get; }

    /// <summary>Gets the segment bytes from the original UTF-8 input.</summary>
    public ReadOnlySpan<byte> GetValueBytes() => _input.Slice(IndexInBytes, LengthInBytes);
}
