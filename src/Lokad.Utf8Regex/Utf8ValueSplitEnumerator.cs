using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

/// <summary>Enumerates regex split segments without allocating a string array.</summary>
/// <remarks>This stack-only enumerator and its current segment borrow the original UTF-8 input.</remarks>
public ref struct Utf8ValueSplitEnumerator
{
    private readonly SplitSourceKind _sourceKind;
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8BoundaryMap? _boundaryMap;
    private readonly int _totalUtf16Length;
    private Utf8OperationMatchCursor _matchCursor;
    internal ulong DebugInvocationSteps => _matchCursor.DebugInvocationSteps;
    internal ulong DebugInvocationReservedBytes => _matchCursor.DebugInvocationReservedBytes;
    internal ulong DebugPeakWorkingBytes => _matchCursor.DebugPeakWorkingBytes;
    private int _segmentStartBytes;
    private int _segmentStartUtf16;
    private int _remainingCount;
    private bool _completed;
    private Utf8Regex? _timeoutRegex;

    internal Utf8ValueSplitEnumerator(
        ReadOnlySpan<byte> input,
        PreparedSmallAsciiLiteralFamilySearch smallAsciiLiteralFamilySearch,
        int count)
    {
        _sourceKind = SplitSourceKind.SmallAsciiLiteralFamily;
        _input = input;
        _boundaryMap = null;
        _totalUtf16Length = input.Length;
        _matchCursor = new Utf8OperationMatchCursor(
            input,
            smallAsciiLiteralFamilySearch,
            Utf8ExecutionDeadline.Infinite);
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _remainingCount = count;
        _completed = false;
        _timeoutRegex = null;
        Current = default;
    }

    internal Utf8ValueSplitEnumerator(
        ReadOnlySpan<byte> input,
        Utf8OperationMatchCursor matchCursor,
        Utf8BoundaryMap map,
        int count,
        int totalUtf16Length,
        bool rightToLeft)
    {
        _sourceKind = rightToLeft ? SplitSourceKind.OwnedSliceRtl : SplitSourceKind.OwnedSlice;
        _input = input;
        _boundaryMap = map;
        _totalUtf16Length = totalUtf16Length;
        _matchCursor = matchCursor;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = rightToLeft ? totalUtf16Length : 0;
        _remainingCount = count;
        _completed = false;
        _timeoutRegex = null;
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
        _boundaryMap = null;
        _totalUtf16Length = totalUtf16Length;
        _matchCursor = matchCursor;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _remainingCount = count;
        _completed = false;
        _timeoutRegex = null;
        Current = default;
    }

    /// <summary>Gets the split segment at the current enumerator position.</summary>
    public Utf8ValueSplit Current { get; private set; }

    /// <summary>Returns this value as the enumerator for <see langword="foreach"/> pattern matching.</summary>
    public Utf8ValueSplitEnumerator GetEnumerator() => this;

    /// <summary>Advances to the next split segment.</summary>
    /// <returns><see langword="true"/> when another segment is available.</returns>
    /// <exception cref="RegexMatchTimeoutException">The configured matching timeout elapsed.</exception>
    /// <exception cref="Utf8ResourceLimitExceededException">The cumulative execution work or result-capacity allowance was exhausted.</exception>
    public bool MoveNext()
    {
        try
        {
            if (_sourceKind == SplitSourceKind.Exhausted) return false;
            _matchCursor.CheckOwnedSliceCursorBoundary();
            return _sourceKind switch
            {
                SplitSourceKind.NativeMatchCursor => MoveNextNative(),
                SplitSourceKind.OwnedSlice => MoveNextNative(),
                SplitSourceKind.OwnedSliceRtl => MoveNextNative(),
                SplitSourceKind.SmallAsciiLiteralFamily => MoveNextSmallAsciiLiteralFamily(),
                _ => MoveNextNative(),
            };
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (_timeoutRegex is not null)
        {
            if (_sourceKind is SplitSourceKind.OwnedSlice or SplitSourceKind.OwnedSliceRtl) _matchCursor.AbortOwnedSplitMatches();
            throw _matchCursor.CreateMatchTimeoutException(_input, _timeoutRegex.Pattern, _timeoutRegex.MatchTimeout);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _matchCursor.HasResourceLimitBreach)
        {
            if (_sourceKind is SplitSourceKind.OwnedSlice or SplitSourceKind.OwnedSliceRtl) _matchCursor.AbortOwnedSplitMatches();
            throw _matchCursor.CreateExecutionResourceFailure();
        }
        catch
        {
            if (_sourceKind is SplitSourceKind.OwnedSlice or SplitSourceKind.OwnedSliceRtl) _matchCursor.AbortOwnedSplitMatches();
            throw;
        }
    }

    internal Utf8ValueSplitEnumerator WithTimeoutMapping(Utf8Regex regex)
    {
        _timeoutRegex = regex;
        return this;
    }

    private bool MoveNextNative()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_sourceKind == SplitSourceKind.OwnedSlice)
        {
            // Scalar matches delimit byte-aligned segments. The map supplies
            // only the requested derived UTF-16 coordinates.
            if (_boundaryMap is not { } ownedMap)
            {
                throw new InvalidOperationException("Owned slice split enumeration requires prepared UTF-16 projection.");
            }

            if (_remainingCount == 1 || !_matchCursor.MoveNext())
            {
                _matchCursor.CheckOwnedSliceCursorBoundary();
                Current = new Utf8ValueSplit(
                    _input,
                    _segmentStartUtf16,
                    _totalUtf16Length - _segmentStartUtf16,
                    ownedMap);
                _matchCursor.AbortOwnedSplitMatches();
                _completed = true;
                return true;
            }

            var ownedMatch = _matchCursor.CurrentValueMatch;
            _matchCursor.CheckOwnedSliceCursorBoundary();
            Current = new Utf8ValueSplit(
                _input,
                _segmentStartUtf16,
                ownedMatch.IndexInUtf16 - _segmentStartUtf16,
                ownedMap);
            _segmentStartUtf16 = ownedMatch.IndexInUtf16 + ownedMatch.LengthInUtf16;
            _remainingCount--;
            return true;
        }

        if (_sourceKind == SplitSourceKind.OwnedSliceRtl)
        {
            // Scalar discovery runs from the tail and returns the tail piece
            // first. The map supplies the derived UTF-16 coordinates.
            if (_boundaryMap is not { } rtlMap)
            {
                throw new InvalidOperationException("Owned slice split enumeration requires prepared UTF-16 projection.");
            }

            if (_remainingCount == 1 || !_matchCursor.MoveNext())
            {
                _matchCursor.CheckOwnedSliceCursorBoundary();
                Current = new Utf8ValueSplit(
                    _input,
                    0,
                    _segmentStartUtf16,
                    rtlMap);
                _matchCursor.AbortOwnedSplitMatches();
                _completed = true;
                return true;
            }

            var rtlMatch = _matchCursor.CurrentValueMatch;
            _matchCursor.CheckOwnedSliceCursorBoundary();
            Current = new Utf8ValueSplit(
                _input,
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

        var match = _matchCursor.CurrentValueMatch;
        _matchCursor.CheckOwnedSliceCursorBoundary();
        Current = new Utf8ValueSplit(
            _input,
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

        _matchCursor.CheckOwnedSliceCursorBoundary();
        Current = new Utf8ValueSplit(
            _input,
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
        _matchCursor.CheckOwnedSliceCursorBoundary();
        Current = new Utf8ValueSplit(
            _input,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: _totalUtf16Length - _segmentStartUtf16,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: _input.Length - _segmentStartBytes);
        _completed = true;
        return true;
    }

    private enum SplitSourceKind : byte
    {
        Exhausted = 0,
        NativeMatchCursor = 1,
        SmallAsciiLiteralFamily = 2,
        OwnedSlice = 3,
        OwnedSliceRtl = 4,
    }
}
