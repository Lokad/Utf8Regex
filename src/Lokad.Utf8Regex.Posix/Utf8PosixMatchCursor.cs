using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix;

/// <summary>Exclusive incremental POSIX matching over borrowed unchanged memory.</summary>
/// <remarks>Use one reference at a time. Aliases share progress and disposal; returned coordinate values survive either. Dispose on early termination to refund covered workspace.</remarks>
public sealed class Utf8PosixMatchCursor : IDisposable
{
    private Utf8PosixRegex? _regex;
    private ReadOnlyMemory<byte> _input;
    private Utf8SliceInvocation _invocation;
    private PosixWorkspace _workspace;
    private int[] _state = [];
    private int _position;
    private bool _completed;

    internal Utf8PosixMatchCursor(Utf8PosixRegex regex, ReadOnlyMemory<byte> input, int start,
        CancellationToken token, IUtf8MemoryAllowance? allowance)
    {
        _invocation = regex.StartInvocation(token, allowance);
        _regex = regex; _input = input; _position = start;
        try
        {
            Utf8PosixRegex.CheckStart(input.Span, start, ref _invocation);
            _workspace = regex.CreateWorkspace(ref _invocation);
            if (_workspace.StateLength != 0) _state = new int[_workspace.StateLength];
        }
        catch (Utf8ExecutionDeadlineExpiredException) { Dispose(); throw regex.CreateTimeout(); }
        catch { Dispose(); throw; }
    }

    /// <summary>Advances under the original invocation and returns selected whole-match coordinates.</summary>
    public bool MoveNext(out Utf8ValueMatch match) => MoveNext(Span<Utf8ValueMatch>.Empty, out match);

    /// <summary>Advances and writes available capture slots zero onward, without changing selection for shorter output.</summary>
    /// <remarks>Excess slots stay untouched. No match clears the output window; failure clears partial publication and closes the cursor. Earlier retained values remain valid.</remarks>
    public bool MoveNext(Span<Utf8ValueMatch> groups, out Utf8ValueMatch match)
    {
        var regex = _regex ?? throw new ObjectDisposedException(nameof(Utf8PosixMatchCursor));
        var output = regex.OutputWindow(groups);
        match = default;
        try
        {
            PosixWorkspace.Finish(ref _invocation);
            if (_completed || !_workspace.Find(_input.Span, _state, _position, false, ref _invocation, out var start, out var end,
                captureHistory: output.Length > 1))
            {
                Utf8PosixRegex.ClearOutput(output, ref _invocation);
                Complete();
                return false;
            }
            match = regex.Project(_input.Span, start, end, in _workspace, _state, output, ref _invocation);
            PosixWorkspace.Charge(ref _invocation);
            if (start == end && end == _input.Length) Complete();
            else _position = start == end ? Utf8SliceRunner.AdvancePastScalar(_input.Span, end, false) : end;
            PosixWorkspace.Finish(ref _invocation);
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { match = default; output.Clear(); Dispose(); throw regex.CreateTimeout(); }
        catch { match = default; output.Clear(); Dispose(); throw; }
    }

    private void Complete()
    {
        _completed = true;
        _workspace.Release(ref _invocation);
        _state = [];
        _input = default;
    }

    /// <summary>Rejects further advances and synchronously releases covered storage; repeated disposal is harmless.</summary>
    public void Dispose()
    {
        Complete();
        _regex = null;
        _invocation = default;
    }
}
