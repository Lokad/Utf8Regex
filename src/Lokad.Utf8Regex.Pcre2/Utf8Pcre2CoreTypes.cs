using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Maps a PCRE2 group name to one numeric capture slot.</summary>
public readonly struct Pcre2NameEntry
{
    /// <summary>Gets the capture-group name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the zero-based capture slot number.</summary>
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

    internal bool CoordinateFlagsSpecified { get; init; }

    internal bool Utf8SliceIsWellFormed { get; init; }

    internal bool Utf16ProjectionIsExact { get; init; }

    public bool HasContiguousByteRange => Success && StartOffsetInBytes <= EndOffsetInBytes;

    public bool IsUtf8SliceWellFormed => Success &&
        (CoordinateFlagsSpecified ? Utf8SliceIsWellFormed : HasContiguousByteRange);

    public bool HasUtf16Projection => Success &&
        (!CoordinateFlagsSpecified || Utf16ProjectionIsExact);

    public static Pcre2GroupData FromByteOffsets(ReadOnlySpan<byte> input, int number, int startOffsetInBytes, int endOffsetInBytes)
    {
        var startIsBoundary = IsScalarBoundary(input, startOffsetInBytes);
        var endIsBoundary = IsScalarBoundary(input, endOffsetInBytes);
        var hasUtf16Projection = startIsBoundary && endIsBoundary;
        var startOffsetInUtf16 = hasUtf16Projection
            ? Encoding.UTF8.GetCharCount(input[..startOffsetInBytes])
            : 0;
        var endOffsetInUtf16 = hasUtf16Projection
            ? Encoding.UTF8.GetCharCount(input[..endOffsetInBytes])
            : 0;
        return new Pcre2GroupData
        {
            Number = number,
            Success = true,
            StartOffsetInBytes = startOffsetInBytes,
            EndOffsetInBytes = endOffsetInBytes,
            StartOffsetInUtf16 = startOffsetInUtf16,
            EndOffsetInUtf16 = endOffsetInUtf16,
            CoordinateFlagsSpecified = true,
            Utf8SliceIsWellFormed = startOffsetInBytes <= endOffsetInBytes && hasUtf16Projection,
            Utf16ProjectionIsExact = hasUtf16Projection,
        };
    }

    private static bool IsScalarBoundary(ReadOnlySpan<byte> input, int offset) =>
        offset >= 0 &&
        offset <= input.Length &&
        (offset == 0 || offset == input.Length || (input[offset] & 0xC0) != 0x80);
}

/// <summary>Provides a stack-only PCRE2 match view over the original UTF-8 input.</summary>
/// <remarks>With byte-oriented constructs such as <c>\C</c> or reset-start constructs such as <c>\K</c>, a successful match may lack a well-formed, contiguous, or exactly projectable value.</remarks>
public readonly ref struct Utf8Pcre2ValueMatch
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly int _startOffsetInBytes;
    private readonly int _endOffsetInBytes;
    private readonly int _startOffsetInUtf16;
    private readonly int _endOffsetInUtf16;
    private readonly bool _isUtf8SliceWellFormed;
    private readonly bool _hasUtf16Projection;
    private readonly bool _success;

    private Utf8Pcre2ValueMatch(ReadOnlySpan<byte> input, Pcre2GroupData data)
    {
        _input = input;
        _startOffsetInBytes = data.Success ? data.StartOffsetInBytes : 0;
        _endOffsetInBytes = data.Success ? data.EndOffsetInBytes : 0;
        _startOffsetInUtf16 = data.Success ? data.StartOffsetInUtf16 : 0;
        _endOffsetInUtf16 = data.Success ? data.EndOffsetInUtf16 : 0;
        _isUtf8SliceWellFormed = data.IsUtf8SliceWellFormed;
        _hasUtf16Projection = data.HasUtf16Projection;
        _success = data.Success;
    }

    /// <summary>Gets whether the operation found a full or partial match.</summary>
    public bool Success
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _success;
    }

    /// <summary>Gets the reported match start as a zero-based byte offset, or zero after no match.</summary>
    public int StartOffsetInBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _startOffsetInBytes;
    }

    /// <summary>Gets the reported exclusive match end as a zero-based byte offset, or zero after no match.</summary>
    public int EndOffsetInBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _endOffsetInBytes;
    }

    /// <summary>Gets whether the successful match maps to one forward contiguous byte range.</summary>
    public bool HasContiguousByteRange => Success && StartOffsetInBytes <= EndOffsetInBytes;

    /// <summary>Gets whether the contiguous value bytes form well-formed UTF-8.</summary>
    public bool IsUtf8SliceWellFormed => Success && _isUtf8SliceWellFormed;

    /// <summary>Gets whether both reported byte endpoints have exact UTF-16 code-unit projections.</summary>
    public bool HasUtf16Projection => Success && _hasUtf16Projection;

    /// <summary>Gets the reported match start as a zero-based UTF-16 code-unit offset.</summary>
    /// <exception cref="InvalidOperationException">No exact UTF-16 projection is available.</exception>
    public int StartOffsetInUtf16
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!HasUtf16Projection)
            {
                throw new InvalidOperationException("This match has no exact UTF-16 projection.");
            }

            return _startOffsetInUtf16;
        }
    }

    /// <summary>Gets the reported exclusive match end as a zero-based UTF-16 code-unit offset.</summary>
    /// <exception cref="InvalidOperationException">No exact UTF-16 projection is available.</exception>
    public int EndOffsetInUtf16
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!HasUtf16Projection)
            {
                throw new InvalidOperationException("This match has no exact UTF-16 projection.");
            }

            return _endOffsetInUtf16;
        }
    }

    /// <summary>Gets the contiguous match bytes from the original input.</summary>
    /// <returns>An empty span after no match.</returns>
    /// <exception cref="InvalidOperationException">The reported match range is nonmonotone.</exception>
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

    /// <summary>Gets the match value decoded as UTF-16 text.</summary>
    /// <exception cref="InvalidOperationException">The match has no exact UTF-16 projection or contiguous byte range.</exception>
    public string GetValueString()
    {
        if (!HasUtf16Projection)
        {
            throw new InvalidOperationException("This match has no exact string projection.");
        }

        return Encoding.UTF8.GetString(GetValueBytes());
    }

    internal static Utf8Pcre2ValueMatch Create(ReadOnlySpan<byte> input, Pcre2GroupData data)
        => new(input, data);
}

/// <summary>Stores an owned coordinate and projection snapshot for one PCRE2 match.</summary>
public readonly struct Utf8Pcre2MatchData
{
    /// <summary>Gets whether the operation found a match.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the reported match start as a zero-based byte offset, or zero after no match.</summary>
    public int StartOffsetInBytes { get; init; }

    /// <summary>Gets the reported exclusive match end as a zero-based byte offset, or zero after no match.</summary>
    public int EndOffsetInBytes { get; init; }

    /// <summary>Gets whether the successful match maps to one forward contiguous byte range.</summary>
    public bool HasContiguousByteRange { get; init; }

    /// <summary>Gets whether the contiguous match value is well-formed UTF-8.</summary>
    public bool IsUtf8SliceWellFormed { get; init; }

    /// <summary>Gets whether the byte endpoints have exact UTF-16 code-unit projections.</summary>
    public bool HasUtf16Projection { get; init; }

    /// <summary>Gets the projected match start in UTF-16 code units; consult <see cref="HasUtf16Projection"/> first.</summary>
    public int StartOffsetInUtf16 { get; init; }

    /// <summary>Gets the projected exclusive match end in UTF-16 code units; consult <see cref="HasUtf16Projection"/> first.</summary>
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
            HasUtf16Projection = data.HasUtf16Projection,
            StartOffsetInUtf16 = data.Success ? data.StartOffsetInUtf16 : 0,
            EndOffsetInUtf16 = data.Success ? data.EndOffsetInUtf16 : 0,
        };
    }
}

/// <summary>Provides a stack-only detailed PCRE2 match view with numbered and duplicate-name-aware groups.</summary>
/// <remarks>The context and returned spans must not outlive the original UTF-8 input.</remarks>
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

    /// <summary>Gets whether group zero represents a successful full match.</summary>
    public bool Success => _groups is { Length: > 0 } && _groups[0].Success;

    /// <summary>Gets the group-zero match view, or its default value after no match.</summary>
    public Utf8Pcre2ValueMatch Value => _groups is { Length: > 0 } groups && groups[0].Success
        ? Utf8Pcre2ValueMatch.Create(_input, groups[0])
        : default;

    /// <summary>Gets the number of capture slots, including group zero.</summary>
    public int CaptureSlotCount => _groups?.Length ?? 0;

    /// <summary>Gets the number of name-to-slot entries, including duplicate names.</summary>
    public int NameEntryCount => _nameEntries?.Length ?? 0;

    /// <summary>Gets the last relevant PCRE2 <c>(*MARK)</c> value, if one was reported.</summary>
    public string? Mark => _mark;

    /// <summary>Gets a capture slot by number, including an unmatched slot.</summary>
    /// <exception cref="InvalidOperationException">The requested capture slot is unavailable.</exception>
    public Utf8Pcre2GroupContext GetGroup(int number)
    {
        if (!TryGetGroup(number, out var group))
        {
            throw new InvalidOperationException("No group is available for the requested number.");
        }

        return group;
    }

    /// <summary>Attempts to get a capture slot by number, including an unmatched slot.</summary>
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

    /// <summary>Copies name-to-slot entries into a caller-provided destination.</summary>
    /// <param name="destination">The destination receiving entries in pattern order.</param>
    /// <param name="isMore">Whether additional entries did not fit.</param>
    /// <returns>The number of entries copied.</returns>
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

    /// <summary>Copies every capture slot assigned to a possibly duplicated group name.</summary>
    /// <param name="name">The case-sensitive PCRE2 group name.</param>
    /// <param name="destination">The destination receiving slot numbers in pattern order.</param>
    /// <param name="isMore">Whether additional numbers did not fit.</param>
    /// <returns>The number of slot numbers copied.</returns>
    public int CopyNumbersForName(string name, Span<int> destination, out bool isMore)
    {
        if (_nameEntries is null)
        {
            isMore = false;
            return 0;
        }

        var count = 0;
        foreach (var entry in _nameEntries)
        {
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal)) continue;
            if (count < destination.Length) destination[count] = entry.Number;
            count++;
        }

        isMore = count > destination.Length;
        return Math.Min(count, destination.Length);
    }

    /// <summary>Attempts to get the first participating capture slot assigned to a group name.</summary>
    public bool TryGetFirstSetGroup(string name, out Utf8Pcre2GroupContext group)
    {
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

    /// <summary>Gets the complete match as UTF-16 text.</summary>
    /// <exception cref="InvalidOperationException">No successful, exactly projectable match is available.</exception>
    public string GetValueString() => Value.GetValueString();

    internal static Utf8Pcre2MatchContext Create(
        ReadOnlySpan<byte> input,
        Pcre2GroupData[] groups,
        Pcre2NameEntry[]? nameEntries,
        string? mark)
        => new(input, groups, nameEntries, mark);

    internal static Utf8Pcre2MatchContext Create(ReadOnlySpan<byte> input, Pcre2GroupData[] groups)
        => Create(input, groups, null, null);

    internal static Utf8Pcre2MatchContext Create(ReadOnlySpan<byte> input, Pcre2GroupData[] groups, Pcre2NameEntry[]? nameEntries)
        => Create(input, groups, nameEntries, null);

    // The probe borrows this already-owned immutable snapshot. Copying it
    // again would allocate outside discovery's publication authority and lose
    // the original name table/MARK.
    internal Utf8Pcre2ProbeResult AsProbeResult() => _groups is { Length: > 0 } groups && groups[0].Success
        ? Utf8Pcre2ProbeResult.CreateFullMatch(_input, groups, _nameEntries, _mark)
        : Utf8Pcre2ProbeResult.CreateNoMatch(_input, _mark);
}

/// <summary>Provides a stack-only view of one PCRE2 capture slot over the original UTF-8 input.</summary>
/// <remarks>Byte-oriented matching can produce a successful group without a well-formed UTF-8 value or exact UTF-16 projection.</remarks>
public readonly ref struct Utf8Pcre2GroupContext
{
    private readonly ReadOnlySpan<byte> _input;
    internal readonly Pcre2GroupData _data;

    private Utf8Pcre2GroupContext(ReadOnlySpan<byte> input, Pcre2GroupData data)
    {
        _input = input;
        _data = data;
    }

    /// <summary>Gets whether the group participated in the match.</summary>
    public bool Success => _data.Success;

    /// <summary>Gets the zero-based capture slot number.</summary>
    public int Number => _data.Number;

    /// <summary>Gets the reported group start as a zero-based byte offset, or zero when unmatched.</summary>
    public int StartOffsetInBytes => _data.Success ? _data.StartOffsetInBytes : 0;

    /// <summary>Gets the reported exclusive group end as a zero-based byte offset, or zero when unmatched.</summary>
    public int EndOffsetInBytes => _data.Success ? _data.EndOffsetInBytes : 0;

    /// <summary>Gets whether the successful group maps to one forward contiguous byte range.</summary>
    public bool HasContiguousByteRange => _data.HasContiguousByteRange;

    /// <summary>Gets whether the contiguous group value is well-formed UTF-8.</summary>
    public bool IsUtf8SliceWellFormed => _data.IsUtf8SliceWellFormed;

    /// <summary>Gets whether both group endpoints have exact UTF-16 code-unit projections.</summary>
    public bool HasUtf16Projection => _data.HasUtf16Projection;

    /// <summary>Gets the group start as a zero-based UTF-16 code-unit offset.</summary>
    /// <exception cref="InvalidOperationException">No exact UTF-16 projection is available.</exception>
    public int StartOffsetInUtf16 => HasUtf16Projection
        ? _data.StartOffsetInUtf16
        : throw new InvalidOperationException("This group has no exact UTF-16 projection.");

    /// <summary>Gets the exclusive group end as a zero-based UTF-16 code-unit offset.</summary>
    /// <exception cref="InvalidOperationException">No exact UTF-16 projection is available.</exception>
    public int EndOffsetInUtf16 => HasUtf16Projection
        ? _data.EndOffsetInUtf16
        : throw new InvalidOperationException("This group has no exact UTF-16 projection.");

    /// <summary>Gets the contiguous group bytes from the original UTF-8 input.</summary>
    /// <returns>An empty span when the group did not participate.</returns>
    /// <exception cref="InvalidOperationException">The reported group range is nonmonotone.</exception>
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

    /// <summary>Gets the group value decoded as UTF-16 text.</summary>
    /// <exception cref="InvalidOperationException">The group has no exact UTF-16 projection or contiguous byte range.</exception>
    public string GetValueString()
    {
        if (!HasUtf16Projection)
        {
            throw new InvalidOperationException("This group has no exact string projection.");
        }

        return Encoding.UTF8.GetString(GetValueBytes());
    }

    internal static Utf8Pcre2GroupContext Create(ReadOnlySpan<byte> input, Pcre2GroupData data)
        => new(input, data);
}

/// <summary>Identifies whether a PCRE2 probe found no match, a full match, or a subject-end partial match.</summary>
public enum Utf8Pcre2ProbeKind
{
    /// <summary>No viable match was found.</summary>
    NoMatch = 0,

    /// <summary>A complete match was found.</summary>
    FullMatch = 1,

    /// <summary>A viable match was truncated by the end of the supplied subject.</summary>
    PartialMatch = 2,
}

/// <summary>Provides a stack-only discriminated result for full and partial PCRE2 probing.</summary>
/// <remarks>The result and its projected contexts borrow the original UTF-8 input.</remarks>
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

    /// <summary>Gets the probe outcome kind.</summary>
    public Utf8Pcre2ProbeKind Kind => _kind;

    /// <summary>Gets the full or partial value coordinates, or a default unsuccessful view after no match.</summary>
    public Utf8Pcre2ValueMatch Value => Utf8Pcre2ValueMatch.Create(_input, _value);

    /// <summary>Gets the last relevant PCRE2 <c>(*MARK)</c> value, if one was reported.</summary>
    public string? Mark => _mark;

    /// <summary>Gets the detailed full-match context.</summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not <see cref="Utf8Pcre2ProbeKind.FullMatch"/>.</exception>
    public Utf8Pcre2MatchContext GetMatch()
        => _kind == Utf8Pcre2ProbeKind.FullMatch
            ? Utf8Pcre2MatchContext.Create(_input, _groups ?? [], _nameEntries, _mark)
            : throw new InvalidOperationException();

    /// <summary>Gets the partial-match context.</summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not <see cref="Utf8Pcre2ProbeKind.PartialMatch"/>.</exception>
    public Utf8Pcre2PartialMatchContext GetPartial()
        => _kind == Utf8Pcre2ProbeKind.PartialMatch
            ? Utf8Pcre2PartialMatchContext.Create(_input, _value, _mark)
            : throw new InvalidOperationException();

    internal static Utf8Pcre2ProbeResult CreateFullMatch(ReadOnlySpan<byte> input, Pcre2GroupData[] groups, Pcre2NameEntry[]? nameEntries, string? mark)
        => new(input, Utf8Pcre2ProbeKind.FullMatch, groups[0], groups, nameEntries, mark);

    internal static Utf8Pcre2ProbeResult CreateFullMatch(ReadOnlySpan<byte> input, Pcre2GroupData[] groups)
        => CreateFullMatch(input, groups, null, null);

    internal static Utf8Pcre2ProbeResult CreateFullMatch(ReadOnlySpan<byte> input, Pcre2GroupData[] groups, Pcre2NameEntry[]? nameEntries)
        => CreateFullMatch(input, groups, nameEntries, null);

    internal static Utf8Pcre2ProbeResult CreatePartial(ReadOnlySpan<byte> input, Pcre2GroupData value, string? mark)
        => new(input, Utf8Pcre2ProbeKind.PartialMatch, value, null, null, mark);

    internal static Utf8Pcre2ProbeResult CreatePartial(ReadOnlySpan<byte> input, Pcre2GroupData value)
        => CreatePartial(input, value, null);

    internal static Utf8Pcre2ProbeResult CreateNoMatch(ReadOnlySpan<byte> input, string? mark)
        => new(input, Utf8Pcre2ProbeKind.NoMatch, default, null, null, mark);

    internal static Utf8Pcre2ProbeResult CreateNoMatch(ReadOnlySpan<byte> input)
        => CreateNoMatch(input, null);
}

/// <summary>Provides a stack-only view of a PCRE2 match truncated by the subject end.</summary>
/// <remarks>The context and returned spans borrow the original UTF-8 input.</remarks>
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

    /// <summary>Gets the reported partial-match coordinates and projection flags.</summary>
    public Utf8Pcre2ValueMatch Value => Utf8Pcre2ValueMatch.Create(_input, _value);

    /// <summary>Gets the last relevant PCRE2 <c>(*MARK)</c> value, if one was reported.</summary>
    public string? Mark => _mark;

    /// <summary>Gets the partial value bytes from the original subject.</summary>
    public ReadOnlySpan<byte> GetValueBytes() => Value.GetValueBytes();

    /// <summary>Gets the partial value decoded as UTF-16 text.</summary>
    public string GetValueString() => Value.GetValueString();

    internal static Utf8Pcre2PartialMatchContext Create(ReadOnlySpan<byte> input, Pcre2GroupData value, string? mark)
        => new(input, value, mark);
}

/// <summary>Describes the compiled execution and result-shaping characteristics of a managed PCRE2 expression.</summary>
public readonly struct Utf8Pcre2Analysis
{
    /// <summary>Gets whether every supported operation avoids the semantic managed-regex fallback.</summary>
    public bool IsFullyNative { get; init; }

    /// <summary>Gets whether the expression is one exact literal.</summary>
    public bool IsExactLiteral { get; init; }

    /// <summary>Gets the minimum number of subject bytes required for a match.</summary>
    public int MinRequiredLengthInBytes { get; init; }

    /// <summary>Gets whether multiple named groups share a name.</summary>
    public bool HasDuplicateNames { get; init; }

    /// <summary>Gets whether the pattern uses branch-reset groups.</summary>
    public bool UsesBranchReset { get; init; }

    /// <summary>Gets whether the pattern uses PCRE2 backtracking-control verbs.</summary>
    public bool UsesBacktrackingControlVerbs { get; init; }

    /// <summary>Gets whether the pattern uses recursion or subroutine calls.</summary>
    public bool UsesRecursion { get; init; }

    /// <summary>Gets whether byte-oriented constructs may return a value that is not well-formed UTF-8.</summary>
    public bool MayProduceNonUtf8Slices { get; init; }

    /// <summary>Gets whether reset-start constructs may report a start offset after the end offset.</summary>
    public bool MayReportNonMonotoneMatchOffsets { get; init; }

    /// <summary>Gets whether global iteration rejects nonmonotone match ranges.</summary>
    public bool RejectsNonMonotoneIterativeMatches { get; init; }

    /// <summary>Gets whether a supported operation can still fail during execution for pattern-dependent reasons.</summary>
    public bool MayFailIterativeExecutionAtRuntime { get; init; }
}

/// <summary>Represents a stateful callback that appends one PCRE2 replacement directly as UTF-8.</summary>
public delegate void Pcre2MatchEvaluator<TState>(
    in Utf8Pcre2MatchContext match,
    ref Utf8ReplacementWriter writer,
    ref TState state);

/// <summary>Represents a stateful callback that returns UTF-16 replacement text for one PCRE2 match.</summary>
/// <remarks>Each return is encoded independently as UTF-8. Valid surrogate pairs within
/// that string denote one scalar; unpaired surrogates become U+FFFD.</remarks>
public delegate string Pcre2Utf16MatchEvaluator<TState>(
    in Utf8Pcre2MatchContext match,
    ref TState state);

/// <summary>Advances through non-overlapping PCRE2 matches without allocating a public match collection.</summary>
/// <remarks>The stack-only cursor and its current value borrow the original UTF-8 input.
/// Copies retain independent positions. Controlled native and owned-slice copies share their original cumulative
/// enforcement allowance and absolute deadline; copying a cursor does not create another finite budget.
/// Previously returned values remain valid within the input's lifetime after an advance or limit failure.</remarks>
public ref struct Utf8Pcre2ValueMatchEnumerator
{
    internal static int DebugSizeInBytes => Unsafe.SizeOf<Utf8Pcre2ValueMatchEnumerator>();

    internal static int DebugDirectCursorSizeInBytes => Unsafe.SizeOf<Pcre2GlobalMatchCursor>();
    internal static int DebugSliceCursorSizeInBytes => Unsafe.SizeOf<Pcre2SliceGlobalMatchCursor>();

    internal static int DebugValueDataSizeInBytes => Unsafe.SizeOf<Pcre2ValueData>();

    internal static int DebugGroupDataSizeInBytes => Unsafe.SizeOf<Pcre2GroupData>();

    private readonly Pcre2ValueMatchEnumeratorMode _mode;
    private readonly ReadOnlySpan<byte> _input;
    private Pcre2GlobalMatchCursor _directMatches;
    private Pcre2SliceGlobalMatchCursor _sliceMatches;
    private Pcre2ValueData _currentData;

    internal Utf8Pcre2ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Pcre2GlobalMatchCursor directMatches)
    {
        this = default;
        _mode = Pcre2ValueMatchEnumeratorMode.Pcre2DirectGlobal;
        _input = input;
        _directMatches = directMatches;
    }

    internal Utf8Pcre2ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Pcre2SliceGlobalMatchCursor sliceMatches)
    {
        this = default;
        _mode = Pcre2ValueMatchEnumeratorMode.Pcre2Slice;
        _input = input;
        _sliceMatches = sliceMatches;
    }

    /// <summary>Gets the match at the current cursor position.</summary>
    public Utf8Pcre2ValueMatch Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return Utf8Pcre2ValueMatch.Create(
                _input,
                new Pcre2GroupData
                {
                    Number = 0,
                    Success = _currentData.Success,
                    StartOffsetInBytes = _currentData.StartOffsetInBytes,
                    EndOffsetInBytes = _currentData.EndOffsetInBytes,
                    StartOffsetInUtf16 = _currentData.StartOffsetInUtf16,
                    EndOffsetInUtf16 = _currentData.EndOffsetInUtf16,
                    CoordinateFlagsSpecified = _currentData.CoordinateFlagsSpecified,
                    Utf8SliceIsWellFormed = _currentData.Utf8SliceIsWellFormed,
                    Utf16ProjectionIsExact = _currentData.Utf16ProjectionIsExact,
                });
        }
    }

    /// <summary>Advances to the next non-overlapping match.</summary>
    /// <returns><see langword="true"/> when another match is available.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_mode == Pcre2ValueMatchEnumeratorMode.Pcre2DirectGlobal)
        {
            if (!_directMatches.MoveNext())
            {
                _currentData = default;
                return false;
            }

            var match = _directMatches.Current;
            _currentData = new Pcre2ValueData
            {
                Success = true,
                StartOffsetInBytes = match.StartOffsetInBytes,
                EndOffsetInBytes = match.EndOffsetInBytes,
                StartOffsetInUtf16 = match.StartOffsetInUtf16,
                EndOffsetInUtf16 = match.EndOffsetInUtf16,
                CoordinateFlagsSpecified = true,
                Utf8SliceIsWellFormed = match.IsUtf8SliceWellFormed,
                Utf16ProjectionIsExact = match.HasUtf16Projection,
            };
            return true;
        }

        if (_mode == Pcre2ValueMatchEnumeratorMode.Pcre2Slice)
        {
            if (!_sliceMatches.MoveNext())
            {
                _currentData = default;
                return false;
            }

            var projected = _sliceMatches.ProjectCurrent();
            Pcre2SliceDriver.CheckInvocation(ref _sliceMatches.WorkingInvocation);
            _currentData = new Pcre2ValueData
            {
                Success = true,
                StartOffsetInBytes = projected.StartOffsetInBytes,
                EndOffsetInBytes = projected.EndOffsetInBytes,
                StartOffsetInUtf16 = projected.StartOffsetInUtf16,
                EndOffsetInUtf16 = projected.EndOffsetInUtf16,
                CoordinateFlagsSpecified = true,
                Utf8SliceIsWellFormed = projected.IsUtf8SliceWellFormed,
                Utf16ProjectionIsExact = projected.HasUtf16Projection,
            };
            return true;
        }

        _currentData = default;
        return false;
    }

    private readonly struct Pcre2ValueData
    {
        public bool Success { get; init; }

        public int StartOffsetInBytes { get; init; }

        public int EndOffsetInBytes { get; init; }

        public int StartOffsetInUtf16 { get; init; }

        public int EndOffsetInUtf16 { get; init; }

        public bool CoordinateFlagsSpecified { get; init; }

        public bool Utf8SliceIsWellFormed { get; init; }

        public bool Utf16ProjectionIsExact { get; init; }
    }

    private enum Pcre2ValueMatchEnumeratorMode : byte
    {
        None = 0,
        Pcre2DirectGlobal = 1,
        Pcre2Slice = 2,
    }
}
