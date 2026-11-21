using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

/// <summary>Enumerates non-overlapping regex matches without allocating a match collection.</summary>
/// <remarks>The enumerator borrows the original UTF-8 input for traversal; keep it unchanged while enumerating.
/// Its current coordinate-only value does not retain the input. Funded enumeration borrows an open regex; close rejects later advances on all copies. Copies retain independent positions and shared numeric authority, without host working loans.</remarks>
public ref struct Utf8ValueMatchEnumerator
{
    private Utf8FundedStorage? _programMemory;

    private Utf8OperationMatchCursor _cursor;
    internal bool DebugIsOwnedSlice => _cursor.DebugIsOwnedSlice;
    internal ulong DebugInvocationSteps => _cursor.DebugInvocationSteps;
    private ReadOnlySpan<byte> _timeoutInput;
    private Utf8Regex? _timeoutRegex;

    internal Utf8ValueMatchEnumerator(Utf8OperationMatchCursor cursor)
    {
        _cursor = cursor;
        _programMemory = null;
        _timeoutInput = default;
        _timeoutRegex = null;
    }

    /// <summary>Gets the match at the current enumerator position.</summary>
    public Utf8CaptureRange Current => _cursor.CurrentValueMatch;

    /// <summary>Returns this value as the enumerator for <see langword="foreach"/> pattern matching.</summary>
    public Utf8ValueMatchEnumerator GetEnumerator() => this;

    /// <summary>Advances to the next non-overlapping match.</summary>
    /// <returns><see langword="true"/> when another match is available.</returns>
    /// <exception cref="RegexMatchTimeoutException">The configured matching timeout elapsed.</exception>
    /// <exception cref="Utf8ResourceLimitExceededException">The cumulative execution work or result-capacity allowance was exhausted.</exception>
    public bool MoveNext()
    {
        if (_programMemory is null) return MoveNextCore();
        _cursor.CheckCancellationAtEntry();
        using var use = _programMemory.Enter();
        return MoveNextCore();
    }

    internal Utf8ValueMatchEnumerator WithProgramMemory(Utf8FundedStorage? memory)
    {
        _programMemory = memory;
        return this;
    }

    private bool MoveNextCore()
    {
        try
        {
            return _cursor.MoveNext();
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _cursor.HasResourceLimitBreach)
        {
            throw _cursor.CreateExecutionResourceFailure();
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (_timeoutRegex is not null)
        {
            throw _cursor.CreateMatchTimeoutException(_timeoutInput, _timeoutRegex.Pattern, _timeoutRegex.MatchTimeout);
        }
    }

    internal Utf8ValueMatchEnumerator WithTimeoutMapping(
        ReadOnlySpan<byte> input, Utf8Regex regex)
    {
        if (regex.MatchTimeout == Regex.InfiniteMatchTimeout)
        {
            return this;
        }

        _timeoutInput = input;
        _timeoutRegex = regex;
        return this;
    }
}
