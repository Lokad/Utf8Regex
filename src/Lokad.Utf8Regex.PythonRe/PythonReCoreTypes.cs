using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Maps a Python named group to its numeric capture slot.</summary>
public readonly struct PythonReNameEntry
{
    /// <summary>Gets the group name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the zero-based capture slot number.</summary>
    public int Number { get; init; }
}

internal readonly struct PythonReUtf8IndexMap
{
    private readonly int[]? _byteOffsetsByUtf16Index;

    private PythonReUtf8IndexMap(int[] byteOffsetsByUtf16Index)
    {
        _byteOffsetsByUtf16Index = byteOffsetsByUtf16Index;
    }

    public static PythonReUtf8IndexMap Create(ReadOnlySpan<byte> input, string decoded)
    {
        if (input.Length == decoded.Length)
        {
            return default;
        }

        var offsets = new int[decoded.Length + 1];
        var utf16Index = 0;
        var byteOffset = 0;
        while (utf16Index < decoded.Length)
        {
            offsets[utf16Index] = byteOffset;
            var value = decoded[utf16Index];
            if (char.IsHighSurrogate(value) &&
                utf16Index + 1 < decoded.Length &&
                char.IsLowSurrogate(decoded[utf16Index + 1]))
            {
                offsets[utf16Index + 1] = byteOffset;
                utf16Index += 2;
                byteOffset += 4;
                offsets[utf16Index] = byteOffset;
                continue;
            }

            byteOffset += value <= 0x7f ? 1 : value <= 0x7ff ? 2 : 3;
            utf16Index++;
            offsets[utf16Index] = byteOffset;
        }

        if (byteOffset != input.Length)
        {
            throw new InvalidOperationException("The decoded subject does not map back to its UTF-8 input.");
        }

        return new PythonReUtf8IndexMap(offsets);
    }

    public int GetByteOffset(int utf16Index) => _byteOffsetsByUtf16Index is null
        ? utf16Index
        : _byteOffsetsByUtf16Index[utf16Index];
}

internal readonly struct PythonReGroupData
{
    private const int NonContiguousByteStartSentinel = 1;
    private const int NonContiguousByteEndSentinel = 0;

    public int Number { get; init; }

    public bool Success { get; init; }

    public int StartOffsetInBytes { get; init; }

    public int EndOffsetInBytes { get; init; }

    public int StartOffsetInUtf16 { get; init; }

    public int EndOffsetInUtf16 { get; init; }

    public bool HasContiguousByteRange => Success && StartOffsetInBytes <= EndOffsetInBytes;

    public static PythonReGroupData FromUtf16(
        int number,
        Group group,
        PythonReUtf8IndexMap indexMap) => FromUtf16(number, group, indexMap, 0);

    public static PythonReGroupData FromUtf16(
        int number,
        Group group,
        PythonReUtf8IndexMap indexMap,
        int utf16BaseOffset)
    {
        if (!group.Success)
        {
            return new PythonReGroupData
            {
                Number = number,
                Success = false,
            };
        }

        var startOffsetInUtf16 = utf16BaseOffset + group.Index;
        var endOffsetInUtf16 = startOffsetInUtf16 + group.Length;
        return new PythonReGroupData
        {
            Number = number,
            Success = true,
            StartOffsetInBytes = indexMap.GetByteOffset(startOffsetInUtf16),
            EndOffsetInBytes = indexMap.GetByteOffset(endOffsetInUtf16),
            StartOffsetInUtf16 = startOffsetInUtf16,
            EndOffsetInUtf16 = endOffsetInUtf16,
        };
    }

    public static PythonReGroupData FromUtf8Group(int number, Utf8GroupContext group) =>
        FromUtf8Group(number, group, 0, 0);

    public static PythonReGroupData FromUtf8Group(
        int number,
        Utf8GroupContext group,
        int byteBaseOffset,
        int utf16BaseOffset)
    {
        if (!group.Success)
        {
            return new PythonReGroupData
            {
                Number = number,
                Success = false,
            };
        }

        var hasContiguousByteRange = group.TryGetByteRange(out var indexInBytes, out var lengthInBytes);
        return new PythonReGroupData
        {
            Number = number,
            Success = true,
            StartOffsetInBytes = hasContiguousByteRange ? byteBaseOffset + indexInBytes : NonContiguousByteStartSentinel,
            EndOffsetInBytes = hasContiguousByteRange ? byteBaseOffset + indexInBytes + lengthInBytes : NonContiguousByteEndSentinel,
            StartOffsetInUtf16 = utf16BaseOffset + group.IndexInUtf16,
            EndOffsetInUtf16 = utf16BaseOffset + group.IndexInUtf16 + group.LengthInUtf16,
        };
    }

    public static PythonReGroupData FromUtf8Match(Utf8ValueMatch match) => FromUtf8Match(match, 0, 0);

    public static PythonReGroupData FromUtf8Match(
        Utf8ValueMatch match,
        int byteBaseOffset,
        int utf16BaseOffset)
    {
        if (!match.Success)
        {
            return new PythonReGroupData
            {
                Number = 0,
                Success = false,
            };
        }

        var hasContiguousByteRange = match.TryGetByteRange(out var indexInBytes, out var lengthInBytes);
        return new PythonReGroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = hasContiguousByteRange ? byteBaseOffset + indexInBytes : NonContiguousByteStartSentinel,
            EndOffsetInBytes = hasContiguousByteRange ? byteBaseOffset + indexInBytes + lengthInBytes : NonContiguousByteEndSentinel,
            StartOffsetInUtf16 = utf16BaseOffset + match.IndexInUtf16,
            EndOffsetInUtf16 = utf16BaseOffset + match.IndexInUtf16 + match.LengthInUtf16,
        };
    }
}

internal static class PythonReValueTextExtractor
{
    public static string GetValueString(ReadOnlySpan<byte> input, PythonReGroupData data)
    {
        if (!data.Success)
        {
            return string.Empty;
        }

        if (data.HasContiguousByteRange)
        {
            return Encoding.UTF8.GetString(input[data.StartOffsetInBytes..data.EndOffsetInBytes]);
        }

        var decoded = Encoding.UTF8.GetString(input);
        if (IsValidUtf16Boundary(decoded, data.StartOffsetInUtf16) &&
            IsValidUtf16Boundary(decoded, data.EndOffsetInUtf16))
        {
            return decoded[data.StartOffsetInUtf16..data.EndOffsetInUtf16];
        }

        var start = GetUtf16IndexFromRuneIndex(decoded, data.StartOffsetInUtf16);
        var end = GetUtf16IndexFromRuneIndex(decoded, data.EndOffsetInUtf16);
        return decoded[start..end];
    }

    public static byte[] GetValueBytes(ReadOnlySpan<byte> input, PythonReGroupData data)
    {
        if (!data.Success)
        {
            return [];
        }

        if (data.HasContiguousByteRange)
        {
            return input[data.StartOffsetInBytes..data.EndOffsetInBytes].ToArray();
        }

        return Encoding.UTF8.GetBytes(GetValueString(input, data));
    }

    private static bool IsValidUtf16Boundary(string value, int index)
    {
        if ((uint)index > (uint)value.Length)
        {
            return false;
        }

        return index == 0 ||
            index == value.Length ||
            !(char.IsLowSurrogate(value[index]) && char.IsHighSurrogate(value[index - 1]));
    }

    private static int GetUtf16IndexFromRuneIndex(string value, int runeIndex)
    {
        if (runeIndex <= 0)
        {
            return 0;
        }

        var utf16Index = 0;
        var currentRuneIndex = 0;
        while (utf16Index < value.Length && currentRuneIndex < runeIndex)
        {
            utf16Index += char.IsHighSurrogate(value[utf16Index]) &&
                utf16Index + 1 < value.Length &&
                char.IsLowSurrogate(value[utf16Index + 1])
                ? 2
                : 1;
            currentRuneIndex++;
        }

        return utf16Index;
    }
}

/// <summary>Provides a stack-only match view over the original UTF-8 input.</summary>
/// <remarks>The view and returned spans must not outlive the input supplied to the matching operation.</remarks>
public readonly ref struct Utf8PythonValueMatch
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly PythonReGroupData _data;

    internal Utf8PythonValueMatch(ReadOnlySpan<byte> input, PythonReGroupData data)
    {
        _input = input;
        _data = data;
    }

    /// <summary>Gets whether the operation found a match.</summary>
    public bool Success => _data.Success;

    /// <summary>Gets the match start as a zero-based byte offset, or zero after no match.</summary>
    public int StartOffsetInBytes => _data.StartOffsetInBytes;

    /// <summary>Gets the exclusive match end as a zero-based byte offset, or zero after no match.</summary>
    public int EndOffsetInBytes => _data.EndOffsetInBytes;

    /// <summary>Gets the match start as a zero-based UTF-16 code-unit offset, or zero after no match.</summary>
    public int StartOffsetInUtf16 => _data.StartOffsetInUtf16;

    /// <summary>Gets the exclusive match end as a zero-based UTF-16 code-unit offset, or zero after no match.</summary>
    public int EndOffsetInUtf16 => _data.EndOffsetInUtf16;

    /// <summary>Gets whether the successful match maps to one contiguous range in the original UTF-8 input.</summary>
    public bool HasContiguousByteRange => _data.HasContiguousByteRange;

    /// <summary>Gets the match value from the original UTF-8 input.</summary>
    /// <returns>An empty span after no match.</returns>
    /// <exception cref="InvalidOperationException">The successful match has no contiguous byte range.</exception>
    public ReadOnlySpan<byte> GetValueBytes()
    {
        if (!Success)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        if (!HasContiguousByteRange)
        {
            throw new InvalidOperationException("This match does not map to a contiguous byte range.");
        }

        return _input[StartOffsetInBytes..EndOffsetInBytes];
    }

    /// <summary>Gets the match value as a UTF-16 string, or an empty string after no match.</summary>
    public string GetValueString() => PythonReValueTextExtractor.GetValueString(_input, _data);

    internal static Utf8PythonValueMatch Create(ReadOnlySpan<byte> input, PythonReGroupData data) => new(input, data);
}

/// <summary>Stores an owned projection of one Python-compatible match.</summary>
public readonly struct Utf8PythonMatchData
{
    /// <summary>Gets whether the operation found a match.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the match start as a zero-based byte offset, or zero after no match.</summary>
    public int StartOffsetInBytes { get; init; }

    /// <summary>Gets the exclusive match end as a zero-based byte offset, or zero after no match.</summary>
    public int EndOffsetInBytes { get; init; }

    /// <summary>Gets the match start as a zero-based UTF-16 code-unit offset, or zero after no match.</summary>
    public int StartOffsetInUtf16 { get; init; }

    /// <summary>Gets the exclusive match end as a zero-based UTF-16 code-unit offset, or zero after no match.</summary>
    public int EndOffsetInUtf16 { get; init; }

    /// <summary>Gets the matched text, or an empty string after no match.</summary>
    public string ValueText { get; init; }

    internal static Utf8PythonMatchData Create(ReadOnlySpan<byte> input, PythonReGroupData data)
    {
        return new Utf8PythonMatchData
        {
            Success = data.Success,
            StartOffsetInBytes = data.Success ? data.StartOffsetInBytes : 0,
            EndOffsetInBytes = data.Success ? data.EndOffsetInBytes : 0,
            StartOffsetInUtf16 = data.Success ? data.StartOffsetInUtf16 : 0,
            EndOffsetInUtf16 = data.Success ? data.EndOffsetInUtf16 : 0,
            ValueText = data.Success ? Encoding.UTF8.GetString(input[data.StartOffsetInBytes..data.EndOffsetInBytes]) : string.Empty,
        };
    }
}

/// <summary>Describes CPython <c>findall</c> result shaping from the pattern's capture count.</summary>
public enum Utf8PythonFindAllShape
{
    /// <summary>Each item is the entire match because the pattern has no capture groups.</summary>
    FullMatch,

    /// <summary>Each item is the sole capture because the pattern has one capture group.</summary>
    SingleGroup,

    /// <summary>Each item is a capture tuple because the pattern has multiple capture groups.</summary>
    GroupTuple,
}

/// <summary>Contains UTF-16 string values returned with CPython <c>findall</c> shaping.</summary>
public readonly struct Utf8PythonFindAllResult
{
    /// <summary>Gets whether each result is a scalar or capture tuple.</summary>
    public Utf8PythonFindAllShape Shape { get; init; }

    /// <summary>Gets full-match or single-group values for a scalar result shape.</summary>
    public string[] ScalarValues { get; init; }

    /// <summary>Gets capture tuples for the tuple result shape.</summary>
    public string[][] TupleValues { get; init; }

    /// <summary>Gets the number of matches represented by the active result collection.</summary>
    public int Count => Shape == Utf8PythonFindAllShape.GroupTuple
        ? TupleValues?.Length ?? 0
        : ScalarValues?.Length ?? 0;
}

/// <summary>Contains owned UTF-8 values returned with CPython <c>findall</c> shaping.</summary>
public readonly struct Utf8PythonFindAllUtf8Result
{
    /// <summary>Gets whether each result is a scalar or capture tuple.</summary>
    public Utf8PythonFindAllShape Shape { get; init; }

    /// <summary>Gets full-match or single-group UTF-8 values for a scalar result shape.</summary>
    public byte[][] ScalarValues { get; init; }

    /// <summary>Gets UTF-8 capture tuples for the tuple result shape.</summary>
    public byte[][][] TupleValues { get; init; }

    /// <summary>Gets the number of matches represented by the active result collection.</summary>
    public int Count => Shape == Utf8PythonFindAllShape.GroupTuple
        ? TupleValues?.Length ?? 0
        : ScalarValues?.Length ?? 0;
}

/// <summary>Contains the UTF-16 result and replacement count from a CPython <c>subn</c> operation.</summary>
public readonly struct Utf8PythonSubnResult
{
    /// <summary>Gets the replaced UTF-16 text.</summary>
    public string ResultText { get; init; }

    /// <summary>Gets the number of substitutions performed.</summary>
    public int ReplacementCount { get; init; }
}

/// <summary>Contains the UTF-8 result and replacement count from a CPython <c>subn</c> operation.</summary>
public readonly struct Utf8PythonSubnUtf8Result
{
    /// <summary>Gets the replaced text as owned UTF-8 bytes.</summary>
    public byte[] ResultBytes { get; init; }

    /// <summary>Gets the number of substitutions performed.</summary>
    public int ReplacementCount { get; init; }
}

/// <summary>Stores an owned projection of one numbered Python capture group.</summary>
public readonly struct Utf8PythonGroupMatchData
{
    /// <summary>Gets the zero-based capture slot number.</summary>
    public int Number { get; init; }

    /// <summary>Gets whether the group participated in the match.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the group start as a zero-based byte offset, or zero when unmatched.</summary>
    public int StartOffsetInBytes { get; init; }

    /// <summary>Gets the exclusive group end as a zero-based byte offset, or zero when unmatched.</summary>
    public int EndOffsetInBytes { get; init; }

    /// <summary>Gets the group start as a zero-based UTF-16 code-unit offset, or zero when unmatched.</summary>
    public int StartOffsetInUtf16 { get; init; }

    /// <summary>Gets the exclusive group end as a zero-based UTF-16 code-unit offset, or zero when unmatched.</summary>
    public int EndOffsetInUtf16 { get; init; }

    /// <summary>Gets whether the successful group maps to one contiguous UTF-8 byte range.</summary>
    public bool HasContiguousByteRange { get; init; }

    /// <summary>Gets the captured UTF-16 text, or an empty string when unmatched.</summary>
    public string ValueText { get; init; }
}

/// <summary>Stores an owned detailed match with numbered and named group metadata.</summary>
public readonly struct Utf8PythonDetailedMatchData
{
    /// <summary>Gets whether group zero represents a successful match.</summary>
    public bool Success => Groups is { Length: > 0 } && Groups[0].Success;

    /// <summary>Gets group zero, or its default value after no match.</summary>
    public Utf8PythonGroupMatchData Value => Success ? Groups[0] : default;

    /// <summary>Gets all capture slots, including group zero.</summary>
    public Utf8PythonGroupMatchData[] Groups { get; init; }

    /// <summary>Gets the mappings from named groups to capture slots.</summary>
    public PythonReNameEntry[] NameEntries { get; init; }

    /// <summary>Gets the number of capture slots, including group zero.</summary>
    public int CaptureSlotCount => Groups?.Length ?? 0;

    /// <summary>Gets the number of named-group mappings.</summary>
    public int NameEntryCount => NameEntries?.Length ?? 0;

    /// <summary>Attempts to get a capture slot by number, including an unmatched slot.</summary>
    public bool TryGetGroup(int number, out Utf8PythonGroupMatchData group)
    {
        if (Groups is null || number < 0 || number >= Groups.Length)
        {
            group = default;
            return false;
        }

        group = Groups[number];
        return true;
    }

    /// <summary>Attempts to resolve a named capture slot.</summary>
    public bool TryGetFirstSetGroup(string name, out Utf8PythonGroupMatchData group)
    {
        if (NameEntries is null)
        {
            group = default;
            return false;
        }

        foreach (var entry in NameEntries)
        {
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            return TryGetGroup(entry.Number, out group);
        }

        group = default;
        return false;
    }
}

/// <summary>Represents one text or captured-group item in a CPython-compatible split result.</summary>
public readonly struct Utf8PythonSplitItem
{
    /// <summary>Gets the item text, or <see langword="null"/> for an unmatched captured group.</summary>
    public string? ValueText { get; init; }

    /// <summary>Gets whether this item was inserted for a capture group rather than a split segment.</summary>
    public bool IsCapture { get; init; }

    /// <summary>Gets the capture slot number, or zero for an ordinary split segment.</summary>
    public int CaptureGroupNumber { get; init; }
}

/// <summary>Represents a stateful callback that returns UTF-16 replacement text for one detailed match.</summary>
public delegate string Utf8PythonMatchEvaluator<in TState>(TState state, Utf8PythonDetailedMatchData match);

/// <summary>Represents a stateful callback that returns well-formed UTF-8 replacement bytes for one detailed match.</summary>
public delegate byte[] Utf8PythonUtf8MatchEvaluator<in TState>(TState state, Utf8PythonDetailedMatchData match);

/// <summary>Provides a stack-only view of one Python capture group over the original UTF-8 input.</summary>
/// <remarks>The context and returned spans must not outlive the input supplied to the matching operation.</remarks>
public readonly ref struct Utf8PythonGroupContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly PythonReGroupData _data;

    internal Utf8PythonGroupContext(ReadOnlySpan<byte> input, PythonReGroupData data)
    {
        _input = input;
        _data = data;
    }

    /// <summary>Gets the zero-based capture slot number.</summary>
    public int Number => _data.Number;

    /// <summary>Gets whether the group participated in the match.</summary>
    public bool Success => _data.Success;

    /// <summary>Gets the group's coordinate and byte-value view.</summary>
    public Utf8PythonValueMatch Value => Utf8PythonValueMatch.Create(_input, _data);

    /// <summary>Gets the captured UTF-16 text, or an empty string when the group did not participate.</summary>
    public string GetValueString() => Value.GetValueString();
}

/// <summary>Provides a stack-only detailed match view with numbered and named Python groups.</summary>
/// <remarks>The context and returned spans must not outlive the input supplied to the matching operation.</remarks>
public readonly ref struct Utf8PythonMatchContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly PythonReGroupData[]? _groups;
    private readonly PythonReNameEntry[]? _nameEntries;

    internal Utf8PythonMatchContext(ReadOnlySpan<byte> input, PythonReGroupData[]? groups, PythonReNameEntry[]? nameEntries)
    {
        _input = input;
        _groups = groups;
        _nameEntries = nameEntries;
    }

    /// <summary>Gets whether group zero represents a successful match.</summary>
    public bool Success => _groups is { Length: > 0 } && _groups[0].Success;

    /// <summary>Gets the group-zero match view, or its default value after no match.</summary>
    public Utf8PythonValueMatch Value =>
        _groups is { Length: > 0 } groups && groups[0].Success
            ? Utf8PythonValueMatch.Create(_input, groups[0])
            : default;

    /// <summary>Gets the matched UTF-16 text, or an empty string after no match.</summary>
    public string GetValueString() => Value.GetValueString();

    /// <summary>Gets the number of capture slots, including group zero.</summary>
    public int CaptureSlotCount => _groups?.Length ?? 0;

    /// <summary>Gets the number of named-group mappings.</summary>
    public int NameEntryCount => _nameEntries?.Length ?? 0;

    /// <summary>Gets a capture slot by number, including an unmatched slot.</summary>
    /// <exception cref="InvalidOperationException">The requested capture slot is unavailable.</exception>
    public Utf8PythonGroupContext GetGroup(int number)
    {
        if (!TryGetGroup(number, out var group))
        {
            throw new InvalidOperationException("No group is available for the requested number.");
        }

        return group;
    }

    /// <summary>Attempts to get a capture slot by number, including an unmatched slot.</summary>
    public bool TryGetGroup(int number, out Utf8PythonGroupContext group)
    {
        if (_groups is null || number < 0 || number >= _groups.Length)
        {
            group = default;
            return false;
        }

        group = new Utf8PythonGroupContext(_input, _groups[number]);
        return true;
    }

    /// <summary>Attempts to resolve a named capture slot.</summary>
    public bool TryGetFirstSetGroup(string name, out Utf8PythonGroupContext group)
    {
        if (_nameEntries is null)
        {
            group = default;
            return false;
        }

        foreach (var entry in _nameEntries)
        {
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            return TryGetGroup(entry.Number, out group);
        }

        group = default;
        return false;
    }

    /// <summary>Attempts to get the value of a participating numbered group.</summary>
    /// <remarks>A successful empty capture returns <see langword="true"/> with an empty value.</remarks>
    public bool TryGetGroupValueString(int number, out string value)
    {
        if (!TryGetGroup(number, out var group) || !group.Success)
        {
            value = string.Empty;
            return false;
        }

        value = group.GetValueString();
        return true;
    }

    /// <summary>Attempts to get the value of a participating named group.</summary>
    /// <remarks>A successful empty capture returns <see langword="true"/> with an empty value.</remarks>
    public bool TryGetFirstSetGroupValueString(string name, out string value)
    {
        if (!TryGetFirstSetGroup(name, out var group) || !group.Success)
        {
            value = string.Empty;
            return false;
        }

        value = group.GetValueString();
        return true;
    }

    /// <summary>Returns the mappings from named groups to capture slots.</summary>
    public PythonReNameEntry[] GetNameEntries() => _nameEntries ?? [];
}
