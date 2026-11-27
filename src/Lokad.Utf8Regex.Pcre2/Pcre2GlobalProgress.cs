using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

// Restart uses the consumed end. The malformed-subject mode uses native
// reported-range emptiness and can search later candidates in an empty retry.
internal struct Pcre2GlobalProgress(Utf8BytePosition start, bool matchInvalidUtf8 = false,
    bool preserveOrigin = false)
{
    private Utf8BytePosition _restart = start;
    private Utf8BytePosition _firstMatchingPosition = start;
    private Pcre2GlobalRetryState _retry;

    internal bool Finished => _retry == Pcre2GlobalRetryState.Finished;

    internal void GetAttempt(Pcre2MatchOptions matchOptions, out Utf8BytePosition restart,
        out Utf8BytePosition firstMatchingPosition, out Pcre2MatchOptions options)
    {
        restart = _restart;
        firstMatchingPosition = _firstMatchingPosition;
        options = matchOptions;
        if (_retry == Pcre2GlobalRetryState.EmptyAtSamePosition)
            options |= Pcre2MatchOptions.NotEmptyAtStart |
                (matchInvalidUtf8 ? Pcre2MatchOptions.None : Pcre2MatchOptions.Anchored);
    }

    internal void Accept(int reportedStart, int reportedEnd, int consumedStart, int consumedEnd, int subjectLength)
    {
        if (reportedStart > reportedEnd)
            throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
        _restart = new Utf8BytePosition(consumedEnd);
        if (!preserveOrigin) _firstMatchingPosition = _restart;
        var empty = matchInvalidUtf8 ? reportedStart == reportedEnd : consumedStart == consumedEnd;
        _retry = !empty ? Pcre2GlobalRetryState.Search : matchInvalidUtf8 && consumedEnd == subjectLength
            ? Pcre2GlobalRetryState.Finished : Pcre2GlobalRetryState.EmptyAtSamePosition;
    }

    internal bool AfterMiss(ReadOnlySpan<byte> input, in Pcre2CompileRequest request, Pcre2MatchOptions options)
    {
        if (_retry == Pcre2GlobalRetryState.EmptyAtSamePosition && !matchInvalidUtf8)
            return AdvanceAfterEmpty(input, request, options, _restart);
        _retry = Pcre2GlobalRetryState.Finished;
        return false;
    }

    internal bool AdvanceAfterEmpty(ReadOnlySpan<byte> input, in Pcre2CompileRequest request,
        Pcre2MatchOptions options, Utf8BytePosition position)
    {
        if (!Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(input, request, options, position, out _restart))
        { _retry = Pcre2GlobalRetryState.Finished; return false; }
        _retry = Pcre2GlobalRetryState.Search;
        return true;
    }
}
