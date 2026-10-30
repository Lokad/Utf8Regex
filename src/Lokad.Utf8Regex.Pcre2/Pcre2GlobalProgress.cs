using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

// PCRE2's consumed range, rather than its possibly reset reported range, owns
// global progression. Shared by production replacement and memory-range cursors.
internal struct Pcre2GlobalProgress(Utf8BytePosition start)
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
        options = _retry == Pcre2GlobalRetryState.EmptyAtSamePosition
            ? matchOptions | Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart : matchOptions;
    }

    internal void Accept(int reportedStart, int reportedEnd, int consumedStart, int consumedEnd)
    {
        if (reportedStart > reportedEnd)
            throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
        _restart = new Utf8BytePosition(consumedEnd);
        _firstMatchingPosition = _restart;
        _retry = consumedStart == consumedEnd ? Pcre2GlobalRetryState.EmptyAtSamePosition : Pcre2GlobalRetryState.Search;
    }

    internal bool AfterMiss(ref Utf8ValidatedInput input, in Pcre2CompileRequest request, Pcre2MatchOptions options)
    {
        if (_retry != Pcre2GlobalRetryState.EmptyAtSamePosition ||
            !Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(ref input, request, options, _restart, out _restart))
        { _retry = Pcre2GlobalRetryState.Finished; return false; }
        _retry = Pcre2GlobalRetryState.Search;
        return true;
    }
}
