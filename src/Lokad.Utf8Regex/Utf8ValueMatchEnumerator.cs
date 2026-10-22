using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

/// <summary>Enumerates non-overlapping regex matches without allocating a match collection.</summary>
/// <remarks>The enumerator borrows the original UTF-8 input for traversal; keep it unchanged while enumerating.
/// Its current coordinate-only value does not retain the input.</remarks>
public ref struct Utf8ValueMatchEnumerator
{
    private Utf8OperationMatchCursor _cursor;
    internal Utf8SliceInvocation ExportWorkingInvocation() => _cursor.ExportWorkingInvocation();
    internal bool DebugIsOwnedSlice => _cursor.DebugIsOwnedSlice;
    internal ulong DebugInvocationSteps => _cursor.DebugInvocationSteps;
    private ReadOnlySpan<byte> _timeoutInput;
    private string? _timeoutPattern;
    private TimeSpan _timeout;

    internal Utf8ValueMatchEnumerator(Utf8OperationMatchCursor cursor)
    {
        _cursor = cursor;
        _timeoutInput = default;
        _timeoutPattern = null;
        _timeout = default;
    }

    internal Utf8ValueMatchEnumerator(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        TimeSpan timeout,
        CancellationToken token,
        int startUnit)
        : this(new Utf8OperationMatchCursor(program, input, map, timeout, token, startUnit))
    {
    }

    /// <summary>Gets the match at the current enumerator position.</summary>
    public Utf8ValueMatch Current => _cursor.CurrentValueMatch;

    /// <summary>Returns this value as the enumerator for <see langword="foreach"/> pattern matching.</summary>
    public Utf8ValueMatchEnumerator GetEnumerator() => this;

    /// <summary>Advances to the next non-overlapping match.</summary>
    /// <returns><see langword="true"/> when another match is available.</returns>
    /// <exception cref="RegexMatchTimeoutException">The configured matching timeout elapsed.</exception>
    /// <exception cref="Utf8ResourceLimitExceededException">The cumulative execution work or result-capacity allowance was exhausted.</exception>
    public bool MoveNext()
    {
        try
        {
            return _cursor.MoveNext();
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _cursor.HasResourceLimitBreach)
        {
            throw _cursor.CreateExecutionResourceFailure();
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (_timeoutPattern is not null)
        {
            throw _cursor.CreateMatchTimeoutException(_timeoutInput, _timeoutPattern, _timeout);
        }
    }

    internal Utf8ValueMatchEnumerator WithBaseOffsets(int byteOffset, int utf16Offset)
    {
        _cursor = _cursor.WithBaseOffsets(byteOffset, utf16Offset);
        return this;
    }

    internal Utf8ValueMatchEnumerator WithTimeoutMapping(
        ReadOnlySpan<byte> input,
        string pattern,
        TimeSpan timeout)
    {
        if (timeout == Regex.InfiniteMatchTimeout)
        {
            return this;
        }

        _timeoutInput = input;
        _timeoutPattern = pattern;
        _timeout = timeout;
        return this;
    }
}
