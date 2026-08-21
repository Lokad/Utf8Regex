using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

public readonly struct PythonReNameEntry
{
    public string Name { get; init; }

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

public readonly ref struct Utf8PythonValueMatch
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly PythonReGroupData _data;

    internal Utf8PythonValueMatch(ReadOnlySpan<byte> input, PythonReGroupData data)
    {
        _input = input;
        _data = data;
    }

    public bool Success => _data.Success;

    public int StartOffsetInBytes => _data.StartOffsetInBytes;

    public int EndOffsetInBytes => _data.EndOffsetInBytes;

    public int StartOffsetInUtf16 => _data.StartOffsetInUtf16;

    public int EndOffsetInUtf16 => _data.EndOffsetInUtf16;

    public bool HasContiguousByteRange => _data.HasContiguousByteRange;

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

    public string GetValueString() => PythonReValueTextExtractor.GetValueString(_input, _data);

    internal static Utf8PythonValueMatch Create(ReadOnlySpan<byte> input, PythonReGroupData data) => new(input, data);
}

public readonly struct Utf8PythonMatchData
{
    public bool Success { get; init; }

    public int StartOffsetInBytes { get; init; }

    public int EndOffsetInBytes { get; init; }

    public int StartOffsetInUtf16 { get; init; }

    public int EndOffsetInUtf16 { get; init; }

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

public enum Utf8PythonFindAllShape
{
    FullMatch,
    SingleGroup,
    GroupTuple,
}

public readonly struct Utf8PythonFindAllResult
{
    public Utf8PythonFindAllShape Shape { get; init; }

    public string[] ScalarValues { get; init; }

    public string[][] TupleValues { get; init; }

    public int Count => Shape == Utf8PythonFindAllShape.GroupTuple
        ? TupleValues?.Length ?? 0
        : ScalarValues?.Length ?? 0;
}

public readonly struct Utf8PythonFindAllUtf8Result
{
    public Utf8PythonFindAllShape Shape { get; init; }

    public byte[][] ScalarValues { get; init; }

    public byte[][][] TupleValues { get; init; }

    public int Count => Shape == Utf8PythonFindAllShape.GroupTuple
        ? TupleValues?.Length ?? 0
        : ScalarValues?.Length ?? 0;
}

public readonly struct Utf8PythonSubnResult
{
    public string ResultText { get; init; }

    public int ReplacementCount { get; init; }
}

public readonly struct Utf8PythonSubnUtf8Result
{
    public byte[] ResultBytes { get; init; }

    public int ReplacementCount { get; init; }
}

public readonly struct Utf8PythonGroupMatchData
{
    public int Number { get; init; }

    public bool Success { get; init; }

    public int StartOffsetInBytes { get; init; }

    public int EndOffsetInBytes { get; init; }

    public int StartOffsetInUtf16 { get; init; }

    public int EndOffsetInUtf16 { get; init; }

    public bool HasContiguousByteRange { get; init; }

    public string ValueText { get; init; }
}

public readonly struct Utf8PythonDetailedMatchData
{
    public bool Success => Groups is { Length: > 0 } && Groups[0].Success;

    public Utf8PythonGroupMatchData Value => Success ? Groups[0] : default;

    public Utf8PythonGroupMatchData[] Groups { get; init; }

    public PythonReNameEntry[] NameEntries { get; init; }

    public int CaptureSlotCount => Groups?.Length ?? 0;

    public int NameEntryCount => NameEntries?.Length ?? 0;

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

public readonly struct Utf8PythonSplitItem
{
    public string? ValueText { get; init; }

    public bool IsCapture { get; init; }

    public int CaptureGroupNumber { get; init; }
}

public delegate string Utf8PythonMatchEvaluator<in TState>(TState state, Utf8PythonDetailedMatchData match);

public delegate byte[] Utf8PythonUtf8MatchEvaluator<in TState>(TState state, Utf8PythonDetailedMatchData match);

public readonly ref struct Utf8PythonGroupContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly PythonReGroupData _data;

    internal Utf8PythonGroupContext(ReadOnlySpan<byte> input, PythonReGroupData data)
    {
        _input = input;
        _data = data;
    }

    public int Number => _data.Number;

    public bool Success => _data.Success;

    public Utf8PythonValueMatch Value => Utf8PythonValueMatch.Create(_input, _data);

    public string GetValueString() => Value.GetValueString();
}

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

    public bool Success => _groups is { Length: > 0 } && _groups[0].Success;

    public Utf8PythonValueMatch Value =>
        _groups is { Length: > 0 } groups && groups[0].Success
            ? Utf8PythonValueMatch.Create(_input, groups[0])
            : default;

    public string GetValueString() => Value.GetValueString();

    public int CaptureSlotCount => _groups?.Length ?? 0;

    public int NameEntryCount => _nameEntries?.Length ?? 0;

    public Utf8PythonGroupContext GetGroup(int number)
    {
        if (!TryGetGroup(number, out var group))
        {
            throw new InvalidOperationException("No group is available for the requested number.");
        }

        return group;
    }

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

    public PythonReNameEntry[] GetNameEntries() => _nameEntries ?? [];
}
