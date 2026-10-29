namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Describes a numbered Python capture using primary UTF-8 byte coordinates.</summary>
public readonly struct Utf8PythonCaptureRange
{
    /// <summary>Gets the capture number; zero denotes the whole match or an ordinary split segment.</summary>
    public int Number { get; init; }

    /// <summary>Gets whether the capture participated; successful empty captures remain distinct from unmatched captures.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the zero-based start byte offset, or zero for an unmatched capture.</summary>
    public int StartOffsetInBytes { get; init; }

    /// <summary>Gets the exclusive end byte offset, or zero for an unmatched capture.</summary>
    public int EndOffsetInBytes { get; init; }
}

/// <summary>Owns immutable capture ranges and names while borrowing one unchanged UTF-8 memory subject.</summary>
/// <remarks>Range metadata survives cursor advances and disposal. Keep the original subject unchanged and its memory manager alive while accessing bytes. No captured text or whole-subject coordinate map is materialized.</remarks>
public readonly struct Utf8PythonRangeMatch
{
    private readonly ReadOnlyMemory<byte> _input;
    private readonly Utf8PythonCaptureRange[]? _groups;
    private readonly PythonReNameEntry[]? _names;
    private readonly int? _lastIndex;

    internal Utf8PythonRangeMatch(ReadOnlyMemory<byte> input, Utf8PythonCaptureRange[] groups,
        PythonReNameEntry[] names, int? lastIndex)
    {
        _input = input;
        _groups = groups;
        _names = names;
        _lastIndex = lastIndex;
    }

    /// <summary>Gets whether group zero represents a successful match.</summary>
    public bool Success => _groups is { Length: > 0 } && _groups[0].Success;

    /// <summary>Gets the whole-match range, or its default value after no match.</summary>
    public Utf8PythonCaptureRange Value => Success ? _groups![0] : default;

    /// <summary>Gets the capture ranges in number order, including unmatched slots and group zero.</summary>
    public ReadOnlySpan<Utf8PythonCaptureRange> Groups => _groups;

    /// <summary>Gets the immutable named-group mappings.</summary>
    public ReadOnlySpan<PythonReNameEntry> NameEntries => _names;

    /// <summary>Gets the last capture to close, excluding group zero, or null.</summary>
    public int? LastIndex => Success ? _lastIndex : null;

    /// <summary>Gets the name of the last capture to close, or null for no capture or an unnamed capture.</summary>
    public string? LastGroup
    {
        get
        {
            if (LastIndex is int number)
                foreach (var name in NameEntries) if (name.Number == number) return name.Name;
            return null;
        }
    }

    /// <summary>Attempts to get a numbered range, including an unmatched slot.</summary>
    public bool TryGetGroup(int number, out Utf8PythonCaptureRange group)
    {
        if (_groups is null || (uint)number >= (uint)_groups.Length) { group = default; return false; }
        group = _groups[number];
        return true;
    }

    /// <summary>Attempts to resolve a named capture, including an unmatched slot.</summary>
    public bool TryGetFirstSetGroup(string name, out Utf8PythonCaptureRange group)
    {
        foreach (var entry in NameEntries)
            if (string.Equals(entry.Name, name, StringComparison.Ordinal)) return TryGetGroup(entry.Number, out group);
        group = default;
        return false;
    }

    /// <summary>Gets the borrowed whole-match bytes, or empty memory after no match.</summary>
    public ReadOnlyMemory<byte> GetValueMemory() => Success
        ? _input[Value.StartOffsetInBytes..Value.EndOffsetInBytes] : default;

    /// <summary>Gets the borrowed captured bytes; unmatched and empty captures both return empty memory.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The capture slot does not exist.</exception>
    public ReadOnlyMemory<byte> GetGroupMemory(int number)
    {
        if (!TryGetGroup(number, out var group)) throw new ArgumentOutOfRangeException(nameof(number));
        return group.Success ? _input[group.StartOffsetInBytes..group.EndOffsetInBytes] : default;
    }
}
