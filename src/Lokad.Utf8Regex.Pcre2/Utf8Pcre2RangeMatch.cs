namespace Lokad.Utf8Regex.Pcre2;

/// <summary>A PCRE2 capture in primary byte coordinates, with explicit UTF-8 slice validity.</summary>
public readonly struct Utf8Pcre2CaptureRange
{
    internal Utf8Pcre2CaptureRange(int number, bool success, int start, int end, bool wellFormed)
    { Number = number; Success = success; StartOffsetInBytes = start; EndOffsetInBytes = end; IsUtf8SliceWellFormed = wellFormed; }
    /// <summary>The numeric slot, including zero for the whole match.</summary>
    public int Number { get; }
    /// <summary>Whether the capture participated, including an empty capture.</summary>
    public bool Success { get; }
    /// <summary>The inclusive byte start, or zero for an unmatched slot.</summary>
    public int StartOffsetInBytes { get; }
    /// <summary>The exclusive byte end, or zero for an unmatched slot.</summary>
    public int EndOffsetInBytes { get; }
    /// <summary>Whether this participating range is a complete UTF-8 slice. Opted-in \C may split a scalar.</summary>
    public bool IsUtf8SliceWellFormed { get; }
}

/// <summary>Immutable PCRE2 last-capture byte ranges borrowing unchanged subject memory.</summary>
/// <remarks>Captures and MARK survive advances and cursor/program close. Keep subject bytes unchanged and their memory manager alive while accessing values. Resolve capture names on the compiled expression.</remarks>
public readonly struct Utf8Pcre2RangeMatch
{
    private readonly ReadOnlyMemory<byte> _input;
    private readonly Utf8Pcre2CaptureRange[]? _groups;
    internal Utf8Pcre2RangeMatch(ReadOnlyMemory<byte> input, Utf8Pcre2CaptureRange[] groups, string? mark)
    { _input = input; _groups = groups; Mark = mark; }
    /// <summary>Whether group zero represents a successful match.</summary>
    public bool Success => _groups is { Length: > 0 } && _groups[0].Success;
    /// <summary>The whole-match range, or default after no match.</summary>
    public Utf8Pcre2CaptureRange Value => Success ? _groups![0] : default;
    /// <summary>Last captures in slot order, including unmatched slots.</summary>
    public ReadOnlySpan<Utf8Pcre2CaptureRange> Groups => _groups;
    /// <summary>The PCRE2 mark returned by this successful match, if any.</summary>
    public string? Mark { get; }
    /// <summary>Gets an existing numeric slot, including unmatched slots.</summary>
    public bool TryGetGroup(int number, out Utf8Pcre2CaptureRange group)
    {
        if (_groups is null || (uint)number >= (uint)_groups.Length) { group = default; return false; }
        group = _groups[number]; return true;
    }
    /// <summary>Gets borrowed whole-match bytes. Check Value.IsUtf8SliceWellFormed before decoding a \C result.</summary>
    public ReadOnlyMemory<byte> GetValueMemory() => Success
        ? _input[Value.StartOffsetInBytes..Value.EndOffsetInBytes] : default;
    /// <summary>Gets borrowed bytes of a numeric slot; unmatched/empty captures both return empty memory.</summary>
    public ReadOnlyMemory<byte> GetGroupMemory(int number)
    {
        if (!TryGetGroup(number, out var group)) throw new ArgumentOutOfRangeException(nameof(number));
        return group.Success ? _input[group.StartOffsetInBytes..group.EndOffsetInBytes] : default;
    }
}
