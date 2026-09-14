using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D02: one managed global progression cursor owns empty-match advancement
// and the same-position nonempty retry; output consumers own shaping. Operation
// deadlines ride with the caller-supplied Regex instances (constructed with the
// operation timeout), so the cursor performs no manual polling and preserves the
// established timeout behavior. The empty-advance policy is scalar-first, matching
// CPython progression; PCRE2 keeps its distinct policy outside this type.
internal ref struct PythonReManagedMatchCursor
{
    private readonly string _subject;
    private readonly Regex _matcher;
    private readonly Lazy<Regex>? _nonEmptyMatcher;
    private int _searchIndex;
    private bool _retryPending;
    private int _retryPosition;

    internal PythonReManagedMatchCursor(string subject, Regex matcher, Lazy<Regex>? nonEmptyMatcher, int startOffsetInUtf16)
    {
        _subject = subject;
        _matcher = matcher;
        _nonEmptyMatcher = nonEmptyMatcher;
        _searchIndex = startOffsetInUtf16;
    }

    internal bool TryMoveNext(out Match match)
    {
        while (true)
        {
            if (_retryPending)
            {
                _retryPending = false;
                if (Utf8PythonRegex.TryMatchNonEmptyAtSamePosition(_nonEmptyMatcher, _subject, _retryPosition, out match))
                {
                    _searchIndex = match.Index + match.Length;
                    return true;
                }

                if (_retryPosition >= _subject.Length)
                {
                    match = Match.Empty;
                    return false;
                }

                _searchIndex = Utf8PythonRegex.AdvancePastScalar(_subject, _retryPosition);
                continue;
            }

            match = _matcher.Match(_subject, _searchIndex);
            if (!match.Success)
            {
                return false;
            }

            if (match.Length > 0)
            {
                _searchIndex = match.Index + match.Length;
                return true;
            }

            _retryPending = true;
            _retryPosition = match.Index;
            return true;
        }
    }
}
