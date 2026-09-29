using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

/// <summary>Enumerates regex split segments without allocating a string array.</summary>
/// <remarks>This stack-only enumerator and its current segment borrow the original UTF-8 input.</remarks>
public ref struct Utf8ValueSplitEnumerator
{
    private readonly SplitSourceKind _sourceKind;
    private readonly ReadOnlySpan<byte> _input;
    private readonly string? _decoded;
    private readonly Utf8BoundaryMap? _boundaryMap;
    private readonly int _totalUtf16Length;
    private Regex.ValueSplitEnumerator _fallbackEnumerator;
    private Utf8OperationMatchCursor _matchCursor;
    private int _segmentStartBytes;
    private int _segmentStartUtf16;
    private int _remainingCount;
    private bool _completed;
    private string? _timeoutPattern;
    private TimeSpan _timeout;

    internal Utf8ValueSplitEnumerator(
        ReadOnlySpan<byte> input,
        string decoded,
        Regex regex,
        int count,
        Utf8BoundaryMap boundaryMap)
    {
        var emitTailOnly = count == 1;
        _sourceKind = emitTailOnly ? SplitSourceKind.NativeMatchCursor : SplitSourceKind.FallbackRegex;
        _input = input;
        _decoded = decoded;
        _boundaryMap = boundaryMap;
        _totalUtf16Length = decoded.Length;
        _fallbackEnumerator = emitTailOnly ? default : regex.EnumerateSplits(decoded, count);
        _matchCursor = default;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _remainingCount = emitTailOnly ? 1 : 0;
        _completed = false;
        _timeoutPattern = null;
        _timeout = default;
        Current = default;
    }

    internal Utf8ValueSplitEnumerator(
        ReadOnlySpan<byte> input,
        PreparedSmallAsciiLiteralFamilySearch smallAsciiLiteralFamilySearch,
        int count)
    {
        _sourceKind = SplitSourceKind.SmallAsciiLiteralFamily;
        _input = input;
        _decoded = null;
        _boundaryMap = null;
        _totalUtf16Length = input.Length;
        _fallbackEnumerator = default;
        _matchCursor = new Utf8OperationMatchCursor(
            input,
            smallAsciiLiteralFamilySearch,
            Utf8ExecutionDeadline.Infinite);
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _remainingCount = count;
        _completed = false;
        _timeoutPattern = null;
        _timeout = default;
        Current = default;
    }

    internal Utf8ValueSplitEnumerator(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8OperationMatchCursor matchCursor,
        Utf8BoundaryMap map,
        int count,
        int totalUtf16Length,
        bool rightToLeft)
    {
        _sourceKind = rightToLeft ? SplitSourceKind.OwnedSliceRtl : SplitSourceKind.OwnedSlice;
        _input = input;
        _decoded = decoded;
        _boundaryMap = map;
        _totalUtf16Length = totalUtf16Length;
        _fallbackEnumerator = default;
        _matchCursor = matchCursor;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = rightToLeft ? totalUtf16Length : 0;
        _remainingCount = count;
        _completed = false;
        _timeoutPattern = null;
        _timeout = default;
        Current = default;
    }

    internal Utf8ValueSplitEnumerator(
        ReadOnlySpan<byte> input,
        Utf8OperationMatchCursor matchCursor,
        int count,
        int totalUtf16Length)
    {
        _sourceKind = SplitSourceKind.NativeMatchCursor;
        _input = input;
        _decoded = null;
        _boundaryMap = null;
        _totalUtf16Length = totalUtf16Length;
        _fallbackEnumerator = default;
        _matchCursor = matchCursor;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _remainingCount = count;
        _completed = false;
        _timeoutPattern = null;
        _timeout = default;
        Current = default;
    }

    /// <summary>Gets the split segment at the current enumerator position.</summary>
    public Utf8ValueSplit Current { get; private set; }

    /// <summary>Returns this value as the enumerator for <see langword="foreach"/> pattern matching.</summary>
    public Utf8ValueSplitEnumerator GetEnumerator() => this;

    /// <summary>Advances to the next split segment.</summary>
    /// <returns><see langword="true"/> when another segment is available.</returns>
    /// <exception cref="RegexMatchTimeoutException">The configured matching timeout elapsed.</exception>
    public bool MoveNext()
    {
        try
        {
            return _sourceKind switch
            {
                SplitSourceKind.NativeMatchCursor => MoveNextNative(),
                SplitSourceKind.OwnedSlice => MoveNextNative(),
                SplitSourceKind.OwnedSliceRtl => MoveNextNative(),
                SplitSourceKind.SmallAsciiLiteralFamily => MoveNextSmallAsciiLiteralFamily(),
                _ => MoveNextFallback(),
            };
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (_timeoutPattern is not null)
        {
            throw new RegexMatchTimeoutException(
                Encoding.UTF8.GetString(_input),
                _timeoutPattern,
                _timeout);
        }
    }

    internal Utf8ValueSplitEnumerator WithTimeoutMapping(string pattern, TimeSpan timeout)
    {
        _timeoutPattern = pattern;
        _timeout = timeout;
        return this;
    }

    private bool MoveNextFallback()
    {
        if (!_fallbackEnumerator.MoveNext())
        {
            return false;
        }

        var range = _fallbackEnumerator.Current;
        var startIndex = range.Start;
        var endIndex = range.End;
        int start;
        int length;
        if (!startIndex.IsFromEnd && !endIndex.IsFromEnd)
        {
            start = startIndex.Value;
            length = endIndex.Value - start;
        }
        else
        {
            (start, length) = range.GetOffsetAndLength(_totalUtf16Length);
        }

        if (_boundaryMap is not { } boundaryMap)
        {
            throw new InvalidOperationException("Fallback split enumeration requires prepared UTF-16 projection.");
        }

        Current = new Utf8ValueSplit(_input, _decoded, start, length, boundaryMap);
        return true;
    }

    private bool MoveNextNative()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_sourceKind == SplitSourceKind.OwnedSlice)
        {
            // Unit-space shaping: mid-scalar empty matches have no valid byte
            // range, so segments project lazily through the map with UTF-16
            // indexes exactly like the BCL oracle.
            if (_boundaryMap is not { } ownedMap)
            {
                throw new InvalidOperationException("Owned slice split enumeration requires prepared UTF-16 projection.");
            }

            if (_remainingCount == 1 || !_matchCursor.MoveNext())
            {
                Current = new Utf8ValueSplit(
                    _input,
                    _decoded,
                    _segmentStartUtf16,
                    _totalUtf16Length - _segmentStartUtf16,
                    ownedMap);
                _completed = true;
                return true;
            }

            var ownedMatch = _matchCursor.Current;
            Current = new Utf8ValueSplit(
                _input,
                _decoded,
                _segmentStartUtf16,
                ownedMatch.IndexInUtf16 - _segmentStartUtf16,
                ownedMap);
            _segmentStartUtf16 = ownedMatch.IndexInUtf16 + ownedMatch.LengthInUtf16;
            _remainingCount--;
            return true;
        }

        if (_sourceKind == SplitSourceKind.OwnedSliceRtl)
        {
            // Right-to-left discovery runs from the tail: segments project
            // descending from the input end with UTF-16 indexes exactly like
            // the BCL oracle (encounter order, tail piece first).
            if (_boundaryMap is not { } rtlMap)
            {
                throw new InvalidOperationException("Owned slice split enumeration requires prepared UTF-16 projection.");
            }

            if (_remainingCount == 1 || !_matchCursor.MoveNext())
            {
                Current = new Utf8ValueSplit(
                    _input,
                    _decoded,
                    0,
                    _segmentStartUtf16,
                    rtlMap);
                _completed = true;
                return true;
            }

            var rtlMatch = _matchCursor.Current;
            Current = new Utf8ValueSplit(
                _input,
                _decoded,
                rtlMatch.IndexInUtf16 + rtlMatch.LengthInUtf16,
                _segmentStartUtf16 - (rtlMatch.IndexInUtf16 + rtlMatch.LengthInUtf16),
                rtlMap);
            _segmentStartUtf16 = rtlMatch.IndexInUtf16;
            _remainingCount--;
            return true;
        }

        if (_remainingCount == 1 || !_matchCursor.MoveNext())
        {
            return EmitTail();
        }

        var match = _matchCursor.Current;
        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: match.IndexInUtf16 - _segmentStartUtf16,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: match.IndexInBytes - _segmentStartBytes);
        _segmentStartBytes = match.IndexInBytes + match.LengthInBytes;
        _segmentStartUtf16 = match.IndexInUtf16 + match.LengthInUtf16;
        _remainingCount--;
        return true;
    }

    private bool MoveNextSmallAsciiLiteralFamily()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_remainingCount == 1 ||
            !_matchCursor.TryMoveNextSmallAsciiLiteralFamilyCoordinates(out var matchIndex, out var matchLength))
        {
            return EmitTail();
        }

        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartBytes,
            lengthInUtf16: matchIndex - _segmentStartBytes,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: matchIndex - _segmentStartBytes);
        _segmentStartBytes = matchIndex + matchLength;
        _segmentStartUtf16 = _segmentStartBytes;
        _remainingCount--;
        return true;
    }

    private bool EmitTail()
    {
        Current = new Utf8ValueSplit(
            _input,
            _decoded,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: _totalUtf16Length - _segmentStartUtf16,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: _input.Length - _segmentStartBytes);
        _completed = true;
        return true;
    }

    private bool MoveNextOwnedSlice()
    {
        // Unit-space twin of MoveNextNative: mid-scalar empty matches have no
        // valid byte range, so segments project lazily through the map while
        // indexes stay in UTF-16 units exactly like the BCL oracle.
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_boundaryMap is not { } map)
        {
            throw new InvalidOperationException("Owned slice split enumeration requires prepared UTF-16 projection.");
        }

        if (_remainingCount == 1 || !_matchCursor.MoveNext())
        {
            Current = new Utf8ValueSplit(
                _input,
                _decoded,
                _segmentStartUtf16,
                _totalUtf16Length - _segmentStartUtf16,
                map);
            _completed = true;
            return true;
        }

        var match = _matchCursor.Current;
        Current = new Utf8ValueSplit(
            _input,
            _decoded,
            _segmentStartUtf16,
            match.IndexInUtf16 - _segmentStartUtf16,
            map);
        _segmentStartUtf16 = match.IndexInUtf16 + match.LengthInUtf16;
        _remainingCount--;
        return true;
    }

    private enum SplitSourceKind : byte
    {
        FallbackRegex = 0,
        NativeMatchCursor = 1,
        SmallAsciiLiteralFamily = 2,
        OwnedSlice = 3,
        OwnedSliceRtl = 4,
    }
}
