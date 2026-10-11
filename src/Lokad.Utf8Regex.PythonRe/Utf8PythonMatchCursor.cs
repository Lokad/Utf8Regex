using System.Threading;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Discovers Python-compatible scalar matches incrementally over one UTF-8 memory subject.</summary>
/// <remarks>
/// Keep the subject unchanged and its memory manager alive until exhaustion or disposal.
/// Dispose the cursor when abandoning discovery early. There is no finalizer.
/// Sequential advances may cross host calls and asynchronous waits; the original deadline includes those waits.
/// Concurrent or reentrant access is rejected. Aliases reference the same position and lifetime.
/// Returned detailed snapshots own their values and remain usable after later advances or disposal.
/// This development surface does not enable finite public resource policies or retained-result limits.
/// </remarks>
public sealed class Utf8PythonMatchCursor : IDisposable
{
    private ReadOnlyMemory<byte> _input;
    private PythonReNameEntry[] _names;
    private PythonReScalarMatchState _discovery;
    private Utf8PythonDetailedMatchData _current;
    private int _active;
    private Status _status;

    private enum Status : byte { Initial, Positioned, Ended, Faulted, Disposed }

    internal Utf8PythonMatchCursor(ReadOnlyMemory<byte> input, Utf8SliceProgram program,
        int[] numbersByPython, PythonReNameEntry[] names, int startOffsetInBytes, TimeSpan timeout,
        string pattern, Utf8SliceInvocation invocation)
    {
        _input = input;
        _names = names;
        _discovery = new PythonReScalarMatchState(input.Span, program, numbersByPython,
            startOffsetInBytes, timeout, pattern, invocation);
        try { _discovery.Checkpoint(input.Span); }
        catch { Close(Status.Faulted); throw; }
    }

    /// <summary>Gets the owned snapshot from the last successful advance.</summary>
    /// <exception cref="InvalidOperationException">The cursor is not positioned on a match or is being accessed concurrently.</exception>
    /// <exception cref="ObjectDisposedException">The cursor has been disposed.</exception>
    public Utf8PythonDetailedMatchData Current
    {
        get
        {
            Enter();
            try
            {
                ThrowIfDisposed();
                if (_status != Status.Positioned)
                    throw new InvalidOperationException("The cursor is not positioned on a match.");
                return _current;
            }
            finally { Exit(); }
        }
    }

    /// <summary>Discovers and projects the next match without collecting later matches.</summary>
    /// <returns>True for a new match; false after exhaustion.</returns>
    /// <exception cref="OperationCanceledException">The original invocation token was canceled.</exception>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">The original invocation deadline expired.</exception>
    /// <exception cref="InvalidOperationException">The cursor previously failed or is being accessed concurrently.</exception>
    /// <exception cref="ObjectDisposedException">The cursor has been disposed.</exception>
    public bool MoveNext()
    {
        Enter();
        try
        {
            ThrowIfDisposed();
            if (_status == Status.Ended) return false;
            if (_status == Status.Faulted)
                throw new InvalidOperationException("The cursor cannot advance after a failed operation.");
            _current = default;
            try
            {
                var input = _input.Span;
                if (!_discovery.MoveNext(input))
                {
                    Close(Status.Ended);
                    return false;
                }
                var marker = _discovery.ResultReservation;
                var completed = _discovery.SnapshotDetailed(input, _names);
                _discovery.Checkpoint(input);
                // Publication transfers independent managed values to the caller.
                // A retained-result policy is not provided by this working loan.
                _discovery.ReleaseResultsSince(marker);
                _current = completed;
                _status = Status.Positioned;
                return true;
            }
            catch { Close(Status.Faulted); throw; }
        }
        finally { Exit(); }
    }

    /// <summary>Releases cursor scratch and subject references; previously returned snapshots remain valid.</summary>
    /// <remarks>Idempotent. Every alias observes the disposed state.</remarks>
    /// <exception cref="InvalidOperationException">The cursor is being accessed concurrently.</exception>
    public void Dispose()
    {
        Enter();
        try { Close(Status.Disposed); }
        finally { Exit(); }
    }

    private void Enter()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            throw new InvalidOperationException("Concurrent or reentrant cursor access is unsupported.");
    }

    private void Exit() => Volatile.Write(ref _active, 0);

    private void ThrowIfDisposed()
    {
        if (_status == Status.Disposed) throw new ObjectDisposedException(nameof(Utf8PythonMatchCursor));
    }

    private void Close(Status status)
    {
        if (_status is Status.Initial or Status.Positioned) _discovery.Dispose();
        _discovery = default;
        _input = default;
        _names = [];
        _current = default;
        _status = status;
    }

    internal ref Utf8SliceInvocation WorkingInvocation => ref _discovery.WorkingInvocation;
    internal bool HasCoordinateMap => _discovery.HasCoordinateMap;
}
