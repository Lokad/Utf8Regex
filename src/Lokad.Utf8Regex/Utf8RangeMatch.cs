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
    private readonly string[]? _names;

    internal Utf8RangeMatch(ReadOnlyMemory<byte> input, Utf8CaptureRange[] captures,
        int[] offsets, int[] numbers, string[] names)
    { _input = input; _captures = captures; _offsets = offsets; _numbers = numbers; _names = names; }

    /// <summary>Whether group zero represents a successful match.</summary>
    public bool Success => _captures is { Length: > 0 };
    /// <summary>The whole-match byte range, or default after no match.</summary>
    public Utf8CaptureRange Value => Success ? _captures![0] : default;
    /// <summary>Actual capture slot numbers, including zero; numbers can be sparse.</summary>
    public ReadOnlySpan<int> GroupNumbers => _numbers;
    /// <summary>Immutable names in the same order as GroupNumbers.</summary>
    public ReadOnlySpan<string> GroupNames => _names;

    /// <summary>Gets the last capture of an existing slot; an unmatched slot returns a default range.</summary>
    public bool TryGetGroup(int number, out Utf8CaptureRange group)
    {
        var ordinal = _numbers is null ? -1 : Array.BinarySearch(_numbers, number);
        group = ordinal >= 0 && _offsets![ordinal] < _offsets[ordinal + 1]
            ? _captures![_offsets[ordinal + 1] - 1] : default;
        return ordinal >= 0;
    }

    /// <summary>Resolves a name to its last capture, including an unmatched slot.</summary>
    public bool TryGetGroup(string name, out Utf8CaptureRange group)
    {
        ArgumentNullException.ThrowIfNull(name);
        for (var slot = 0; slot < GroupNames.Length; slot++)
            if (string.Equals(_names![slot], name, StringComparison.Ordinal))
                return TryGetGroup(_numbers![slot], out group);
        group = default;
        return false;
    }

    /// <summary>Gets capture history in closing order, including Core RTL order. NonBacktracking keeps only the last capture.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The slot does not exist.</exception>
    public ReadOnlySpan<Utf8CaptureRange> GetCaptures(int number)
    {
        var ordinal = _numbers is null ? -1 : Array.BinarySearch(_numbers, number);
        if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(number));
        return _captures.AsSpan(_offsets![ordinal], _offsets[ordinal + 1] - _offsets[ordinal]);
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
