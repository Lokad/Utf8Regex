namespace Lokad.Utf8Regex;

/// <summary>A participating capture in primary UTF-8 byte coordinates.</summary>
public readonly struct Utf8CaptureRange
{
    internal Utf8CaptureRange(int start, int end)
    { Success = true; StartOffsetInBytes = start; EndOffsetInBytes = end; }

    /// <summary>True for a participating capture, including an empty capture.</summary>
    public bool Success { get; }
    /// <summary>The inclusive byte start, or zero for an unmatched capture.</summary>
    public int StartOffsetInBytes { get; }
    /// <summary>The exclusive byte end, or zero for an unmatched capture.</summary>
    public int EndOffsetInBytes { get; }
}

/// <summary>Immutable Core capture ranges borrowing an unchanged UTF-8 memory subject.</summary>
/// <remarks>Ranges and capture histories survive cursor advances and disposal. Keep the subject and its memory manager alive while accessing bytes. No text or whole-subject coordinate map is materialized.</remarks>
public readonly struct Utf8RangeMatch
{
    private readonly ReadOnlyMemory<byte> _input;
    private readonly Utf8CaptureRange[]? _captures;
    private readonly int[]? _offsets;
    private readonly int[]? _numbers;

    internal Utf8RangeMatch(ReadOnlyMemory<byte> input, Utf8CaptureRange[] captures,
        int[]? offsets, int[] numbers)
    { _input = input; _captures = captures; _offsets = offsets; _numbers = numbers; }

    /// <summary>Whether group zero represents a successful match.</summary>
    public bool Success => _captures is { Length: > 0 };
    /// <summary>The whole-match byte range, or default after no match.</summary>
    public Utf8CaptureRange Value => Success ? _captures![0] : default;
    /// <summary>Actual capture slot numbers, including zero; numbers can be sparse.</summary>
    public ReadOnlySpan<int> GroupNumbers => _numbers;

    /// <summary>Gets the last capture of an existing slot; an unmatched slot returns a default range.</summary>
    public bool TryGetGroup(int number, out Utf8CaptureRange group)
    {
        if (number == 0)
        {
            group = Value;
            return group.Success;
        }
        var ordinal = FindGroupOrdinal(number);
        if (ordinal < 0)
        {
            group = default;
            return false;
        }

        if (_offsets is not { } offsets) group = _captures![ordinal];
        else
        {
            var end = offsets[ordinal + 1];
            group = offsets[ordinal] < end ? _captures![end - 1] : default;
        }
        return true;
    }

    /// <summary>Gets capture history in closing order, including Core RTL order. NonBacktracking keeps only the last capture.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The slot does not exist.</exception>
    public ReadOnlySpan<Utf8CaptureRange> GetCaptures(int number)
    {
        if (number == 0 && Success) return _captures.AsSpan(0, 1);
        var ordinal = FindGroupOrdinal(number);
        if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(number));
        var offsets = _offsets;
        if (offsets is null) return _captures.AsSpan(ordinal, 1);
        var start = offsets[ordinal];
        return _captures.AsSpan(start, offsets[ordinal + 1] - start);
    }

    private int FindGroupOrdinal(int number)
    {
        if (_numbers is not { } numbers) return -1;
        return (uint)number < (uint)numbers.Length && numbers[number] == number
            ? number : Array.BinarySearch(numbers, number);
    }

    /// <summary>Gets borrowed whole-match memory, or empty memory after no match.</summary>
    public ReadOnlyMemory<byte> GetValueMemory() => Success
        ? _input[Value.StartOffsetInBytes..Value.EndOffsetInBytes] : default;

    /// <summary>Gets borrowed bytes for the last capture of a slot. Unmatched and empty captures both return empty memory.</summary>
    public ReadOnlyMemory<byte> GetGroupMemory(int number)
    {
        if (!TryGetGroup(number, out var group)) throw new ArgumentOutOfRangeException(nameof(number));
        return group.Success ? _input[group.StartOffsetInBytes..group.EndOffsetInBytes] : default;
    }
}
