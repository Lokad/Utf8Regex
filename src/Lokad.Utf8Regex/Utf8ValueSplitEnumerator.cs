using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

/// <summary>Enumerates regex split segments without allocating a string array.</summary>
/// <remarks>This stack-only enumerator and its current segment borrow the original unchanged UTF-8 input. Funded enumeration also borrows an open regex; close rejects later advances on all copies, while returned segments remain valid. Copies retain independent positions and shared numeric authority, without host working loans.</remarks>
public ref struct Utf8ValueSplitEnumerator
{
    private Utf8FundedStorage? _programMemory;

    private readonly ReadOnlySpan<byte> _input;
    private readonly bool _rightToLeft;
    private Utf8OperationMatchCursor _matchCursor;
    internal ulong DebugInvocationSteps => _matchCursor.DebugInvocationSteps;
    internal ulong DebugInvocationReservedBytes => _matchCursor.DebugInvocationReservedBytes;
    internal ulong DebugPeakWorkingBytes => _matchCursor.DebugPeakWorkingBytes;
    private int _segmentBoundary;
    private int _remainingCount;
    private bool _completed;
    private Utf8Regex? _timeoutRegex;

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input,
        PreparedSmallAsciiLiteralFamilySearch search, int count)
        : this(input, new Utf8OperationMatchCursor(input, search, Utf8ExecutionDeadline.Infinite), count)
    {
    }

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input,
        Utf8OperationMatchCursor matchCursor, int count, bool rightToLeft = false)
    {
        _input = input;
        _matchCursor = matchCursor;
        _rightToLeft = rightToLeft;
        _segmentBoundary = rightToLeft ? input.Length : 0;
        _remainingCount = count;
    }

    /// <summary>Gets the split segment at the current enumerator position.</summary>
    public Utf8ValueSplit Current { get; private set; }

    /// <summary>Returns this value as the enumerator for <see langword="foreach"/> pattern matching.</summary>
    public Utf8ValueSplitEnumerator GetEnumerator() => this;

    /// <summary>Advances to the next split segment.</summary>
    /// <returns><see langword="true"/> when another segment is available.</returns>
    /// <exception cref="RegexMatchTimeoutException">The configured matching timeout elapsed.</exception>
    /// <exception cref="Utf8ResourceLimitExceededException">The cumulative execution allowance was exhausted.</exception>
    public bool MoveNext()
    {
        if (_programMemory is null) return MoveNextCore();
        _matchCursor.CheckCancellationAtEntry();
        using var use = _programMemory.Enter();
        return MoveNextCore();
    }

    internal Utf8ValueSplitEnumerator WithProgramMemory(Utf8FundedStorage? memory)
    {
        _programMemory = memory;
        return this;
    }

    private bool MoveNextCore()
    {
        try
        {
            if (_remainingCount == 0) return false;
            // Terminal and count-one advances still observe the original authority.
            _matchCursor.CheckOwnedSliceCursorBoundary();
            if (_completed || _remainingCount <= 0) return false;
            if (_remainingCount == 1 || !_matchCursor.MoveNextSplit(out var match))
            {
                _matchCursor.CheckOwnedSliceCursorBoundary();
                Current = _rightToLeft
                    ? new Utf8ValueSplit(_input, 0, _segmentBoundary)
                    : new Utf8ValueSplit(_input, _segmentBoundary, _input.Length - _segmentBoundary);
                _matchCursor.AbortOwnedSplitMatches();
                _completed = true;
                return true;
            }

            _matchCursor.CheckOwnedSliceCursorBoundary();
            Current = _rightToLeft
                ? new Utf8ValueSplit(_input, match.EndOffsetInBytes, _segmentBoundary - match.EndOffsetInBytes)
                : new Utf8ValueSplit(_input, _segmentBoundary, match.StartOffsetInBytes - _segmentBoundary);
            _segmentBoundary = _rightToLeft ? match.StartOffsetInBytes : match.EndOffsetInBytes;
            _remainingCount--;
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (_timeoutRegex is not null)
        {
            _matchCursor.AbortOwnedSplitMatches();
            throw _matchCursor.CreateMatchTimeoutException(_input, _timeoutRegex.Pattern, _timeoutRegex.MatchTimeout);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _matchCursor.HasResourceLimitBreach)
        {
            _matchCursor.AbortOwnedSplitMatches();
            throw _matchCursor.CreateExecutionResourceFailure();
        }
        catch
        {
            _matchCursor.AbortOwnedSplitMatches();
            throw;
        }
    }

    internal Utf8ValueSplitEnumerator WithTimeoutMapping(Utf8Regex regex)
    {
        _timeoutRegex = regex;
        return this;
    }
}
