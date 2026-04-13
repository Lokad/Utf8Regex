using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Pcre2;

public readonly struct Pcre2NameEntry
{
    public string Name { get; init; }

    public int Number { get; init; }
}

internal readonly struct Pcre2GroupData
{
    public int Number { get; init; }

    public bool Success { get; init; }

    public int StartOffsetInBytes { get; init; }

    public int EndOffsetInBytes { get; init; }

    public int StartOffsetInUtf16 { get; init; }

    public int EndOffsetInUtf16 { get; init; }

    public bool HasContiguousByteRange => Success && StartOffsetInBytes <= EndOffsetInBytes;

    public bool IsUtf8SliceWellFormed => HasContiguousByteRange;

    public static Pcre2GroupData FromUtf16(ReadOnlySpan<byte> input, int number, Group group)
    {
        if (!group.Success)
        {
            return new Pcre2GroupData
            {
                Number = number,
                Success = false,
            };
        }

        var decoded = Encoding.UTF8.GetString(input);
        var startOffsetInBytes = Encoding.UTF8.GetByteCount(decoded.AsSpan(0, group.Index));
        var byteLength = Encoding.UTF8.GetByteCount(decoded.AsSpan(group.Index, group.Length));
        return new Pcre2GroupData
        {
            Number = number,
            Success = true,
            StartOffsetInBytes = startOffsetInBytes,
            EndOffsetInBytes = startOffsetInBytes + byteLength,
            StartOffsetInUtf16 = group.Index,
            EndOffsetInUtf16 = group.Index + group.Length,
        };
    }

    public static Pcre2GroupData FromByteOffsets(ReadOnlySpan<byte> input, int number, int startOffsetInBytes, int endOffsetInBytes)
    {
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var endOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..endOffsetInBytes]);
        return new Pcre2GroupData
        {
            Number = number,
            Success = true,
            StartOffsetInBytes = startOffsetInBytes,
            EndOffsetInBytes = endOffsetInBytes,
            StartOffsetInUtf16 = startOffsetInUtf16,
            EndOffsetInUtf16 = endOffsetInUtf16,
        };
    }
}

public readonly ref struct Utf8Pcre2ValueMatch
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly int _startOffsetInBytes;
    private readonly int _endOffsetInBytes;
    private readonly int _startOffsetInUtf16;
    private readonly int _endOffsetInUtf16;
    private readonly bool _success;

    private Utf8Pcre2ValueMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, int endOffsetInBytes, int startOffsetInUtf16, int endOffsetInUtf16)
    {
        _input = input;
        _startOffsetInBytes = startOffsetInBytes;
        _endOffsetInBytes = endOffsetInBytes;
        _startOffsetInUtf16 = startOffsetInUtf16;
        _endOffsetInUtf16 = endOffsetInUtf16;
        _success = true;
    }

    private Utf8Pcre2ValueMatch(ReadOnlySpan<byte> input, Pcre2GroupData data)
    {
        _input = input;
        _startOffsetInBytes = data.Success ? data.StartOffsetInBytes : 0;
        _endOffsetInBytes = data.Success ? data.EndOffsetInBytes : 0;
        _startOffsetInUtf16 = data.Success ? data.StartOffsetInUtf16 : 0;
        _endOffsetInUtf16 = data.Success ? data.EndOffsetInUtf16 : 0;
        _success = data.Success;
    }

    public bool Success
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _success;
    }

    public int StartOffsetInBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _startOffsetInBytes;
    }

    public int EndOffsetInBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _endOffsetInBytes;
    }

    public bool HasContiguousByteRange => Success && StartOffsetInBytes <= EndOffsetInBytes;

    public bool IsUtf8SliceWellFormed => HasContiguousByteRange;

    public bool HasUtf16Projection => true;

    public int StartOffsetInUtf16
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _startOffsetInUtf16;
    }

    public int EndOffsetInUtf16
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _endOffsetInUtf16;
    }

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

    public string GetValueString() => Encoding.UTF8.GetString(GetValueBytes());

    internal static Utf8Pcre2ValueMatch Create(ReadOnlySpan<byte> input, Match match)
    {
        if (!match.Success)
        {
            return default;
        }

        return new Utf8Pcre2ValueMatch(input, Pcre2GroupData.FromUtf16(input, 0, match.Groups[0]));
    }

    internal static Utf8Pcre2ValueMatch Create(ReadOnlySpan<byte> input, Pcre2GroupData data)
        => new(input, data);

    internal static Utf8Pcre2ValueMatch Create(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        int endOffsetInBytes,
        int startOffsetInUtf16,
        int endOffsetInUtf16)
        => new(input, startOffsetInBytes, endOffsetInBytes, startOffsetInUtf16, endOffsetInUtf16);
}

public readonly struct Utf8Pcre2MatchData
{
    public bool Success { get; init; }

    public int StartOffsetInBytes { get; init; }

    public int EndOffsetInBytes { get; init; }

    public bool HasContiguousByteRange { get; init; }

    public bool IsUtf8SliceWellFormed { get; init; }

    public bool HasUtf16Projection { get; init; }

    public int StartOffsetInUtf16 { get; init; }

    public int EndOffsetInUtf16 { get; init; }

    internal static Utf8Pcre2MatchData Create(Pcre2GroupData data)
    {
        return new Utf8Pcre2MatchData
        {
            Success = data.Success,
            StartOffsetInBytes = data.Success ? data.StartOffsetInBytes : 0,
            EndOffsetInBytes = data.Success ? data.EndOffsetInBytes : 0,
            HasContiguousByteRange = data.HasContiguousByteRange,
            IsUtf8SliceWellFormed = data.IsUtf8SliceWellFormed,
            HasUtf16Projection = true,
            StartOffsetInUtf16 = data.Success ? data.StartOffsetInUtf16 : 0,
            EndOffsetInUtf16 = data.Success ? data.EndOffsetInUtf16 : 0,
        };
    }
}

public readonly ref struct Utf8Pcre2MatchContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Pcre2GroupData[]? _groups;
    private readonly Pcre2NameEntry[]? _nameEntries;
    private readonly string? _mark;

    private Utf8Pcre2MatchContext(
        ReadOnlySpan<byte> input,
        Pcre2GroupData[]? groups,
        Pcre2NameEntry[]? nameEntries,
        string? mark)
    {
        _input = input;
        _groups = groups;
        _nameEntries = nameEntries;
        _mark = mark;
    }

    public bool Success => _groups is { Length: > 0 } && _groups[0].Success;

    public Utf8Pcre2ValueMatch Value => Success ? Utf8Pcre2ValueMatch.Create(_input, _groups![0]) : default;

    public int CaptureSlotCount => _groups?.Length ?? 0;

    public int NameEntryCount => _nameEntries?.Length ?? 0;

    public string? Mark => _mark;

    public Utf8Pcre2GroupContext GetGroup(int number)
    {
        if (!TryGetGroup(number, out var group))
        {
            throw new InvalidOperationException("No group is available for the requested number.");
        }

        return group;
    }

    public bool TryGetGroup(int number, out Utf8Pcre2GroupContext group)
    {
        if (_groups is null || number < 0 || number >= _groups.Length)
        {
            group = default;
            return false;
        }

        group = Utf8Pcre2GroupContext.Create(_input, _groups[number]);
        return true;
    }

    public int CopyNameEntries(Span<Pcre2NameEntry> destination, out bool isMore)
    {
        if (_nameEntries is null)
        {
            isMore = false;
            return 0;
        }

        var written = Math.Min(destination.Length, _nameEntries.Length);
        _nameEntries.AsSpan(0, written).CopyTo(destination);
        isMore = _nameEntries.Length > written;
        return written;
    }

    public int CopyNumbersForName(string name, Span<int> destination, out bool isMore)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_nameEntries is null)
        {
            isMore = false;
            return 0;
        }

        var matches = _nameEntries.Where(e => string.Equals(e.Name, name, StringComparison.Ordinal)).ToArray();
        var written = Math.Min(destination.Length, matches.Length);
        for (var i = 0; i < written; i++)
        {
            destination[i] = matches[i].Number;
        }

        isMore = matches.Length > written;
        return written;
    }

    public bool TryGetFirstSetGroup(string name, out Utf8Pcre2GroupContext group)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_nameEntries is not null && _groups is not null)
        {
            foreach (var entry in _nameEntries)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal) &&
                    entry.Number >= 0 &&
                    entry.Number < _groups.Length &&
                    _groups[entry.Number].Success)
                {
                    group = Utf8Pcre2GroupContext.Create(_input, _groups[entry.Number]);
                    return true;
                }
            }
        }

        group = default;
        return false;
    }

    public string GetValueString() => Value.GetValueString();

    internal static Utf8Pcre2MatchContext Create(ReadOnlySpan<byte> input, Match? match, string[]? groupNames)
    {
        if (match is null)
        {
            return default;
        }

        var groups = new Pcre2GroupData[match.Groups.Count];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = Pcre2GroupData.FromUtf16(input, i, match.Groups[i]);
        }

        Pcre2NameEntry[]? nameEntries = null;
        if (groupNames is { Length: > 0 })
        {
            nameEntries = new Pcre2NameEntry[groupNames.Length];
            for (var i = 0; i < groupNames.Length; i++)
            {
                nameEntries[i] = new Pcre2NameEntry { Name = groupNames[i], Number = i };
            }
        }

        return new Utf8Pcre2MatchContext(input, groups, nameEntries, mark: null);
    }

    internal static Utf8Pcre2MatchContext Create(
        ReadOnlySpan<byte> input,
        Pcre2GroupData[] groups,
        Pcre2NameEntry[]? nameEntries = null,
        string? mark = null)
        => new(input, groups, nameEntries, mark);
}

public readonly ref struct Utf8Pcre2GroupContext
{
    private readonly ReadOnlySpan<byte> _input;
    internal readonly Pcre2GroupData _data;

    private Utf8Pcre2GroupContext(ReadOnlySpan<byte> input, Pcre2GroupData data)
    {
        _input = input;
        _data = data;
    }

    public bool Success => _data.Success;

    public int Number => _data.Number;

    public int StartOffsetInBytes => _data.Success ? _data.StartOffsetInBytes : 0;

    public int EndOffsetInBytes => _data.Success ? _data.EndOffsetInBytes : 0;

    public bool HasContiguousByteRange => _data.HasContiguousByteRange;

    public bool IsUtf8SliceWellFormed => _data.IsUtf8SliceWellFormed;

    public bool HasUtf16Projection => true;

    public int StartOffsetInUtf16 => _data.Success ? _data.StartOffsetInUtf16 : 0;

    public int EndOffsetInUtf16 => _data.Success ? _data.EndOffsetInUtf16 : 0;

    public ReadOnlySpan<byte> GetValueBytes()
    {
        if (!_data.Success)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        if (!HasContiguousByteRange)
        {
            throw new InvalidOperationException("This group does not map to a contiguous byte range.");
        }

        return _input[StartOffsetInBytes..EndOffsetInBytes];
    }

    public string GetValueString() => Encoding.UTF8.GetString(GetValueBytes());

    internal static Utf8Pcre2GroupContext Create(ReadOnlySpan<byte> input, Pcre2GroupData data)
        => new(input, data);
}

public enum Utf8Pcre2ProbeKind
{
    NoMatch = 0,
    FullMatch = 1,
    PartialMatch = 2,
}

public readonly ref struct Utf8Pcre2ProbeResult
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8Pcre2ProbeKind _kind;
    private readonly Pcre2GroupData _value;
    private readonly Pcre2GroupData[]? _groups;
    private readonly Pcre2NameEntry[]? _nameEntries;
    private readonly string? _mark;

    private Utf8Pcre2ProbeResult(
        ReadOnlySpan<byte> input,
        Utf8Pcre2ProbeKind kind,
        Pcre2GroupData value,
        Pcre2GroupData[]? groups,
        Pcre2NameEntry[]? nameEntries,
        string? mark)
    {
        _input = input;
        _kind = kind;
        _value = value;
        _groups = groups;
        _nameEntries = nameEntries;
        _mark = mark;
    }

    public Utf8Pcre2ProbeKind Kind => _kind;

    public Utf8Pcre2ValueMatch Value => Utf8Pcre2ValueMatch.Create(_input, _value);

    public string? Mark => _mark;

    public Utf8Pcre2MatchContext GetMatch()
        => _kind == Utf8Pcre2ProbeKind.FullMatch
            ? Utf8Pcre2MatchContext.Create(_input, _groups ?? [], _nameEntries, _mark)
            : throw new InvalidOperationException();

    public Utf8Pcre2PartialMatchContext GetPartial()
        => _kind == Utf8Pcre2ProbeKind.PartialMatch
            ? Utf8Pcre2PartialMatchContext.Create(_input, _value, _mark)
            : throw new InvalidOperationException();

    internal static Utf8Pcre2ProbeResult CreateFullMatch(ReadOnlySpan<byte> input, Pcre2GroupData[] groups, Pcre2NameEntry[]? nameEntries = null, string? mark = null)
        => new(input, Utf8Pcre2ProbeKind.FullMatch, groups[0], groups, nameEntries, mark);

    internal static Utf8Pcre2ProbeResult CreatePartial(ReadOnlySpan<byte> input, Pcre2GroupData value, string? mark = null)
        => new(input, Utf8Pcre2ProbeKind.PartialMatch, value, null, null, mark);

    internal static Utf8Pcre2ProbeResult CreateNoMatch(ReadOnlySpan<byte> input, string? mark = null)
        => new(input, Utf8Pcre2ProbeKind.NoMatch, default, null, null, mark);
}

public readonly ref struct Utf8Pcre2PartialMatchContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Pcre2GroupData _value;
    private readonly string? _mark;

    private Utf8Pcre2PartialMatchContext(ReadOnlySpan<byte> input, Pcre2GroupData value, string? mark)
    {
        _input = input;
        _value = value;
        _mark = mark;
    }

    public Utf8Pcre2ValueMatch Value => Utf8Pcre2ValueMatch.Create(_input, _value);

    public string? Mark => _mark;

    public ReadOnlySpan<byte> GetValueBytes() => Value.GetValueBytes();

    public string GetValueString() => Value.GetValueString();

    internal static Utf8Pcre2PartialMatchContext Create(ReadOnlySpan<byte> input, Pcre2GroupData value, string? mark)
        => new(input, value, mark);
}

public readonly struct Utf8Pcre2Analysis
{
    public bool IsFullyNative { get; init; }

    public bool IsExactLiteral { get; init; }

    public int MinRequiredLengthInBytes { get; init; }

    public bool HasDuplicateNames { get; init; }

    public bool UsesBranchReset { get; init; }

    public bool UsesBacktrackingControlVerbs { get; init; }

    public bool UsesRecursion { get; init; }

    public bool MayProduceNonUtf8Slices { get; init; }

    public bool MayReportNonMonotoneMatchOffsets { get; init; }

    public bool RejectsNonMonotoneIterativeMatches { get; init; }

    public bool MayFailIterativeExecutionAtRuntime { get; init; }
}

public delegate void Pcre2MatchEvaluator<TState>(
    in Utf8Pcre2MatchContext match,
    ref Utf8ReplacementWriter writer,
    ref TState state);

public delegate string Pcre2Utf16MatchEvaluator<TState>(
    in Utf8Pcre2MatchContext match,
    ref TState state);

public ref struct Utf8Pcre2ValueMatchEnumerator
{
    private readonly Pcre2ValueMatchEnumeratorMode _mode;
    private readonly ReadOnlySpan<byte> _input;
    private readonly Pcre2GroupData[]? _matches;
    private readonly int _matchCount;
    private readonly Pcre2NativeValueEnumeratorKind _generatorExecutionKind;
    private Utf8PreparedValueMatchEnumerator _utf8PreparedMatches;
    private Utf8ValueMatchEnumerator _utf8Matches;
    private Regex.ValueMatchEnumerator _managedMatches;
    private readonly Utf8BoundaryMap? _managedBoundaryMap;
    private readonly bool _managedMatchesAreAscii;
    private readonly int _utf8RegexByteOffsetBase;
    private readonly int _utf8RegexUtf16OffsetBase;
    private readonly Exception? _pendingException;
    private readonly int _exceptionIndex;
    private int _pendingNextCursor;
    private Pcre2ValueData _currentData;
    private Pcre2ValueData _pendingData;
    private bool _hasPendingData;
    private int _cursor;
    private int _index;

    internal Utf8Pcre2ValueMatchEnumerator(ReadOnlySpan<byte> input, Pcre2GroupData[]? matches)
    {
        _mode = Pcre2ValueMatchEnumeratorMode.Pcre2GroupDataArray;
        _input = input;
        _matches = matches;
        _matchCount = matches?.Length ?? 0;
        _generatorExecutionKind = default;
        _utf8PreparedMatches = default;
        _utf8Matches = default;
        _managedMatches = default;
        _managedBoundaryMap = null;
        _managedMatchesAreAscii = false;
        _utf8RegexByteOffsetBase = 0;
        _utf8RegexUtf16OffsetBase = 0;
        _pendingException = null;
        _exceptionIndex = int.MaxValue;
        _pendingNextCursor = 0;
        _currentData = default;
        _pendingData = default;
        _hasPendingData = false;
        _cursor = 0;
        _index = -1;
    }

    internal Utf8Pcre2ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValueMatchEnumerator utf8Matches, int byteOffsetBase = 0, int utf16OffsetBase = 0)
    {
        this = default;
        _mode = Pcre2ValueMatchEnumeratorMode.Utf8RegexEnumerator;
        _input = input;
        _utf8Matches = utf8Matches;
        _utf8RegexByteOffsetBase = byteOffsetBase;
        _utf8RegexUtf16OffsetBase = utf16OffsetBase;
        _index = -1;
        _exceptionIndex = int.MaxValue;
    }

    internal Utf8Pcre2ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Regex.ValueMatchEnumerator managedMatches,
        Utf8BoundaryMap? boundaryMap,
        bool managedMatchesAreAscii)
    {
        this = default;
        _mode = managedMatchesAreAscii
            ? Pcre2ValueMatchEnumeratorMode.ManagedRegexAsciiEnumerator
            : Pcre2ValueMatchEnumeratorMode.ManagedRegexBoundaryEnumerator;
        _input = input;
        _managedMatches = managedMatches;
        _managedBoundaryMap = boundaryMap;
        _managedMatchesAreAscii = managedMatchesAreAscii;
        _exceptionIndex = int.MaxValue;
        _index = -1;
    }

    internal Utf8Pcre2ValueMatchEnumerator(ReadOnlySpan<byte> input, Pcre2GroupData[]? matches, Exception pendingException, int exceptionIndex)
    {
        _mode = Pcre2ValueMatchEnumeratorMode.Pcre2GroupDataArrayWithDeferredException;
        _input = input;
        _matches = matches;
        _matchCount = matches?.Length ?? 0;
        _generatorExecutionKind = default;
        _utf8PreparedMatches = default;
        _utf8Matches = default;
        _managedMatches = default;
        _managedBoundaryMap = null;
        _managedMatchesAreAscii = false;
        _utf8RegexByteOffsetBase = 0;
        _utf8RegexUtf16OffsetBase = 0;
        _pendingException = pendingException;
        _exceptionIndex = exceptionIndex;
        _pendingNextCursor = 0;
        _currentData = default;
        _pendingData = default;
        _hasPendingData = false;
        _cursor = 0;
        _index = -1;
    }

    internal Utf8Pcre2ValueMatchEnumerator(ReadOnlySpan<byte> input, Pcre2NativeValueEnumeratorKind generatorExecutionKind, int startOffsetInBytes)
    {
        _mode = Pcre2ValueMatchEnumeratorMode.NativeValueGenerator;
        _input = input;
        _matches = null;
        _matchCount = 0;
        _generatorExecutionKind = generatorExecutionKind;
        _utf8PreparedMatches = default;
        _utf8Matches = default;
        _managedMatches = default;
        _managedBoundaryMap = null;
        _managedMatchesAreAscii = false;
        _utf8RegexByteOffsetBase = 0;
        _utf8RegexUtf16OffsetBase = 0;
        _pendingException = null;
        _exceptionIndex = int.MaxValue;
        _pendingNextCursor = 0;
        _currentData = default;
        _pendingData = default;
        _hasPendingData = false;
        _cursor = startOffsetInBytes;
        _index = -1;
    }

    internal Utf8Pcre2ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8PreparedValueMatchEnumerator utf8PreparedMatches, int startOffsetInBytes)
    {
        this = default;
        _mode = Pcre2ValueMatchEnumeratorMode.Utf8PreparedAsciiEnumerator;
        _input = input;
        _utf8PreparedMatches = utf8PreparedMatches;
        _managedMatchesAreAscii = true;
        _exceptionIndex = int.MaxValue;
        _index = -1;
    }

    public Utf8Pcre2ValueMatch Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((_mode == Pcre2ValueMatchEnumeratorMode.Pcre2GroupDataArray ||
                 _mode == Pcre2ValueMatchEnumeratorMode.Pcre2GroupDataArrayWithDeferredException) &&
                (uint)_index < (uint)_matchCount)
            {
                return Utf8Pcre2ValueMatch.Create(_input, _matches![_index]);
            }

            return Utf8Pcre2ValueMatch.Create(
                _input,
                _currentData.StartOffsetInBytes,
                _currentData.EndOffsetInBytes,
                _currentData.StartOffsetInUtf16,
                _currentData.EndOffsetInUtf16);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_mode == Pcre2ValueMatchEnumeratorMode.Pcre2GroupDataArray)
        {
            var nextIndex = _index + 1;
            if ((uint)nextIndex >= (uint)_matchCount)
            {
                return false;
            }

            _index = nextIndex;
            return true;
        }

        if (_mode == Pcre2ValueMatchEnumeratorMode.Pcre2GroupDataArrayWithDeferredException)
        {
            var nextIndex = _index + 1;
            if (_pendingException is not null && nextIndex >= _exceptionIndex)
            {
                throw _pendingException;
            }

            if ((uint)nextIndex >= (uint)_matchCount)
            {
                return false;
            }

            _index = nextIndex;
            return true;
        }

        if (_mode == Pcre2ValueMatchEnumeratorMode.NativeValueGenerator)
        {
            return TryMoveNextNativeValueGenerator();
        }

        if (_mode == Pcre2ValueMatchEnumeratorMode.Utf8RegexEnumerator)
        {
            if (!_utf8Matches.MoveNext())
            {
                _currentData = default;
                return false;
            }

            _currentData = CreateManagedProfileGroupData(_utf8Matches.Current, _utf8RegexByteOffsetBase, _utf8RegexUtf16OffsetBase);
            return true;
        }

        if (_mode == Pcre2ValueMatchEnumeratorMode.Utf8PreparedAsciiEnumerator)
        {
            if (!_utf8PreparedMatches.MoveNext())
            {
                _currentData = default;
                return false;
            }

            var start = _utf8PreparedMatches.StartOffsetInBytes;
            var end = _utf8PreparedMatches.EndOffsetInBytes;
            _currentData = new Pcre2ValueData
            {
                Success = true,
                StartOffsetInBytes = start,
                EndOffsetInBytes = end,
                StartOffsetInUtf16 = start,
                EndOffsetInUtf16 = end,
            };
            return true;
        }

        if (_mode == Pcre2ValueMatchEnumeratorMode.ManagedRegexAsciiEnumerator)
        {
            if (!_managedMatches.MoveNext())
            {
                _currentData = default;
                return false;
            }

            var match = _managedMatches.Current;
            _currentData = new Pcre2ValueData
            {
                Success = true,
                StartOffsetInBytes = match.Index,
                EndOffsetInBytes = match.Index + match.Length,
                StartOffsetInUtf16 = match.Index,
                EndOffsetInUtf16 = match.Index + match.Length,
            };
            return true;
        }

        if (_mode == Pcre2ValueMatchEnumeratorMode.ManagedRegexBoundaryEnumerator)
        {
            if (!_managedMatches.MoveNext())
            {
                _currentData = default;
                return false;
            }

            _currentData = CreateManagedRegexBoundaryGroupData(_managedMatches.Current);
            return true;
        }

        _currentData = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryMoveNextNativeValueGenerator()
    {
        if (_hasPendingData)
        {
            _currentData = _pendingData;
            _pendingData = default;
            _hasPendingData = false;
            _cursor = _pendingNextCursor;
            return true;
        }

        return _generatorExecutionKind switch
        {
            Pcre2NativeValueEnumeratorKind.BranchResetBasic => TryMoveNextLiteralAlternation(_input, ref _cursor, "abc"u8, "xyz"u8, out _currentData),
            Pcre2NativeValueEnumeratorKind.BranchResetBackref => TryMoveNextLiteralAlternation(_input, ref _cursor, "abcabc"u8, "xyzxyz"u8, out _currentData),
            Pcre2NativeValueEnumeratorKind.BranchResetNested => TryMoveNextLiteralAlternation(_input, ref _cursor, "xabcx"u8, "xxyzx"u8, out _currentData),
            Pcre2NativeValueEnumeratorKind.BranchResetSameNameFollowup => TryMoveNextLiteralAlternation(_input, ref _cursor, "aaaccccaaa"u8, "bccccb"u8, out _currentData),
            Pcre2NativeValueEnumeratorKind.DuplicateNamesFooBar => TryMoveNextLiteralAlternation(_input, ref _cursor, "foofoo"u8, "barbar"u8, out _currentData),
            Pcre2NativeValueEnumeratorKind.KResetAbc123 => TryMoveNextKResetLiteral(_input, ref _cursor, "abc123"u8, 3, 3, out _currentData),
            Pcre2NativeValueEnumeratorKind.KResetBarOrBaz => TryMoveNextKResetLiteralAlternation(_input, ref _cursor, "foobar"u8, 3, 3, "foobaz"u8, 3, 3, out _currentData),
            Pcre2NativeValueEnumeratorKind.KResetRepeatAb => TryMoveNextRepeatedKResetAb(_input, ref _cursor, out _currentData),
            Pcre2NativeValueEnumeratorKind.KResetAtomicAltAb => TryMoveNextKResetAtomicAltAb(_input, ref _cursor, out _currentData),
            Pcre2NativeValueEnumeratorKind.EmptyOrDefAfterAbc => TryMoveNextEmptyOrLiteralAfterAbc(_input, ref _cursor, "def"u8, out _currentData, out _pendingData, out _hasPendingData, out _pendingNextCursor),
            _ => false,
        };
    }

    private static bool TryMoveNextLiteralAlternation(ReadOnlySpan<byte> input, ref int cursor, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, out Pcre2ValueData data)
    {
        data = default;
        var remaining = input[cursor..];
        var firstIndex = remaining.IndexOf(first);
        var secondIndex = remaining.IndexOf(second);
        if (firstIndex < 0 && secondIndex < 0)
        {
            return false;
        }

        var firstWins = firstIndex >= 0 && (secondIndex < 0 || firstIndex <= secondIndex);
        var relativeIndex = firstWins ? firstIndex : secondIndex;
        var length = firstWins ? first.Length : second.Length;
        var start = cursor + relativeIndex;
        var end = start + length;
        data = Pcre2ValueData.FromByteOffsets(input, start, end);
        cursor = end;
        return true;
    }

    private static bool TryMoveNextKResetLiteral(ReadOnlySpan<byte> input, ref int cursor, ReadOnlySpan<byte> wholeLiteral, int valueOffset, int valueLength, out Pcre2ValueData data)
    {
        data = default;
        var relativeIndex = input[cursor..].IndexOf(wholeLiteral);
        if (relativeIndex < 0)
        {
            return false;
        }

        var wholeStart = cursor + relativeIndex;
        data = Pcre2ValueData.FromByteOffsets(input, wholeStart + valueOffset, wholeStart + valueOffset + valueLength);
        cursor = wholeStart + wholeLiteral.Length;
        return true;
    }

    private static bool TryMoveNextKResetLiteralAlternation(
        ReadOnlySpan<byte> input,
        ref int cursor,
        ReadOnlySpan<byte> firstWholeLiteral,
        int firstValueOffset,
        int firstValueLength,
        ReadOnlySpan<byte> secondWholeLiteral,
        int secondValueOffset,
        int secondValueLength,
        out Pcre2ValueData data)
    {
        data = default;
        var remaining = input[cursor..];
        var firstIndex = remaining.IndexOf(firstWholeLiteral);
        var secondIndex = remaining.IndexOf(secondWholeLiteral);
        if (firstIndex < 0 && secondIndex < 0)
        {
            return false;
        }

        var firstWins = firstIndex >= 0 && (secondIndex < 0 || firstIndex <= secondIndex);
        var relativeIndex = firstWins ? firstIndex : secondIndex;
        var wholeStart = cursor + relativeIndex;
        var wholeLength = firstWins ? firstWholeLiteral.Length : secondWholeLiteral.Length;
        var valueOffset = firstWins ? firstValueOffset : secondValueOffset;
        var valueLength = firstWins ? firstValueLength : secondValueLength;
        data = Pcre2ValueData.FromByteOffsets(input, wholeStart + valueOffset, wholeStart + valueOffset + valueLength);
        cursor = wholeStart + wholeLength;
        return true;
    }

    private static bool TryMoveNextRepeatedKResetAb(ReadOnlySpan<byte> input, ref int cursor, out Pcre2ValueData data)
    {
        data = default;
        var relativeIndex = input[cursor..].IndexOf("ab"u8);
        if (relativeIndex < 0)
        {
            return false;
        }

        var runCursor = cursor + relativeIndex;
        var lastAbStart = runCursor;
        do
        {
            lastAbStart = runCursor;
            runCursor += 2;
        }
        while (runCursor <= input.Length - 2 &&
               input[runCursor] == (byte)'a' &&
               input[runCursor + 1] == (byte)'b');

        data = Pcre2ValueData.FromByteOffsets(input, lastAbStart + 1, lastAbStart + 2);
        cursor = lastAbStart + 2;
        return true;
    }

    private static bool TryMoveNextKResetAtomicAltAb(ReadOnlySpan<byte> input, ref int cursor, out Pcre2ValueData data)
    {
        data = default;
        var relativeIndex = input[cursor..].IndexOf("ab"u8);
        if (relativeIndex < 0)
        {
            return false;
        }

        var matchStart = cursor + relativeIndex;
        if (matchStart <= input.Length - 3 && input[matchStart + 2] == (byte)'z')
        {
            data = Pcre2ValueData.FromByteOffsets(input, matchStart + 1, matchStart + 3);
            cursor = matchStart + 3;
            return true;
        }

        data = Pcre2ValueData.FromByteOffsets(input, matchStart, matchStart + 2);
        cursor = matchStart + 2;
        return true;
    }

    private static bool TryMoveNextEmptyOrLiteralAfterAbc(
        ReadOnlySpan<byte> input,
        ref int cursor,
        ReadOnlySpan<byte> literal,
        out Pcre2ValueData data,
        out Pcre2ValueData pendingData,
        out bool hasPendingData,
        out int pendingNextCursor)
    {
        data = default;
        pendingData = default;
        hasPendingData = false;
        pendingNextCursor = 0;
        if ((uint)cursor > (uint)(input.Length - 3))
        {
            return false;
        }

        var relativeIndex = input[cursor..].IndexOf("abc"u8);
        if (relativeIndex < 0)
        {
            return false;
        }

        var boundary = cursor + relativeIndex + 3;
        data = Pcre2ValueData.FromByteOffsets(input, boundary, boundary);
        if (boundary <= input.Length - literal.Length && input[boundary..].StartsWith(literal))
        {
            pendingData = Pcre2ValueData.FromByteOffsets(input, boundary, boundary + literal.Length);
            hasPendingData = true;
            pendingNextCursor = boundary + literal.Length;
        }

        cursor = hasPendingData ? boundary + literal.Length : boundary + 1;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Pcre2ValueData CreateManagedProfileGroupData(Utf8ValueMatch utf8Match, int byteOffsetBase, int utf16OffsetBase)
    {
        if (!utf8Match.Success)
        {
            return default;
        }

        if (!utf8Match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
        {
            throw new InvalidOperationException("Managed Utf8Regex fallback returned a match that is not aligned to byte boundaries.");
        }

        return new Pcre2ValueData
        {
            Success = true,
            StartOffsetInBytes = byteOffsetBase + indexInBytes,
            EndOffsetInBytes = byteOffsetBase + indexInBytes + lengthInBytes,
            StartOffsetInUtf16 = utf16OffsetBase + utf8Match.IndexInUtf16,
            EndOffsetInUtf16 = utf16OffsetBase + utf8Match.IndexInUtf16 + utf8Match.LengthInUtf16,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Pcre2ValueData CreateManagedRegexBoundaryGroupData(ValueMatch match)
    {
        if (_managedBoundaryMap is not null && _managedBoundaryMap.TryGetByteRange(match.Index, match.Length, out var indexInBytes, out var lengthInBytes))
        {
        }
        else
        {
            throw new InvalidOperationException("Managed Regex fallback produced a match that is not aligned to UTF-8 scalar boundaries.");
        }

        return new Pcre2ValueData
        {
            Success = true,
            StartOffsetInBytes = indexInBytes,
            EndOffsetInBytes = indexInBytes + lengthInBytes,
            StartOffsetInUtf16 = match.Index,
            EndOffsetInUtf16 = match.Index + match.Length,
        };
    }

    private readonly struct Pcre2ValueData
    {
        public bool Success { get; init; }

        public int StartOffsetInBytes { get; init; }

        public int EndOffsetInBytes { get; init; }

        public int StartOffsetInUtf16 { get; init; }

        public int EndOffsetInUtf16 { get; init; }

        public static Pcre2ValueData FromByteOffsets(ReadOnlySpan<byte> input, int startOffsetInBytes, int endOffsetInBytes)
        {
            var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
            var endOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..endOffsetInBytes]);
            return new Pcre2ValueData
            {
                Success = true,
                StartOffsetInBytes = startOffsetInBytes,
                EndOffsetInBytes = endOffsetInBytes,
                StartOffsetInUtf16 = startOffsetInUtf16,
                EndOffsetInUtf16 = endOffsetInUtf16,
            };
        }
    }

    private enum Pcre2ValueMatchEnumeratorMode : byte
    {
        Pcre2GroupDataArray = 0,
        Pcre2GroupDataArrayWithDeferredException = 1,
        Utf8RegexEnumerator = 2,
        Utf8PreparedAsciiEnumerator = 3,
        ManagedRegexAsciiEnumerator = 4,
        ManagedRegexBoundaryEnumerator = 5,
        NativeValueGenerator = 6,
    }

    internal enum Pcre2NativeValueEnumeratorKind : byte
    {
        None = 0,
        BranchResetBasic = 1,
        BranchResetBackref = 2,
        BranchResetNested = 3,
        BranchResetSameNameFollowup = 4,
        DuplicateNamesFooBar = 5,
        KResetAbc123 = 6,
        KResetBarOrBaz = 7,
        KResetRepeatAb = 8,
        KResetAtomicAltAb = 9,
        EmptyOrDefAfterAbc = 10,
    }
}
