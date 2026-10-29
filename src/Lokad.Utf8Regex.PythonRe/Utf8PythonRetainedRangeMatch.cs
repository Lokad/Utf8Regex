namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Holds an explicitly funded immutable range result until the caller releases its reservation.</summary>
/// <remarks>Dispose after retained users finish, or take over admission for any copies kept afterward. Copies already obtained from Match remain valid; subject bytes remain borrowed and their memory manager must stay alive. This receipt covers range/name arrays, shared name text counted conservatively again, and its object charge. Borrowed input is excluded. There is no finalizer.</remarks>
public sealed class Utf8PythonRetainedRangeMatch : IDisposable
{
    private readonly Utf8PythonRangeMatch _match;
    private IUtf8MemoryAllowance? _allowance;
    private ulong _bytes;

    internal Utf8PythonRetainedRangeMatch(Utf8PythonRangeMatch match, IUtf8MemoryAllowance allowance)
    { _match = match; _allowance = allowance; }

    // Set only after the invocation transfers its loan, before publication.
    internal void SetTransferredBytes(ulong bytes) => _bytes = bytes;

    /// <summary>Gets the immutable result while this receipt holds its reservation.</summary>
    /// <exception cref="ObjectDisposedException">The reservation was released.</exception>
    public Utf8PythonRangeMatch Match => Volatile.Read(ref _allowance) is not null
        ? _match : throw new ObjectDisposedException(nameof(Utf8PythonRetainedRangeMatch));

    /// <summary>Gets the original modeled charge transferred to this receipt, including after disposal.</summary>
    public ulong ChargedBytes => _bytes;

    /// <summary>Ends this receipt's admission responsibility and refunds its loan exactly once.</summary>
    /// <remarks>Idempotent, including concurrent calls and aliases. Already copied range data remains valid; the caller owns admission for any continued retention after this explicit release.</remarks>
    public void Dispose()
    {
        var allowance = Interlocked.Exchange(ref _allowance, null);
        if (allowance is not null && _bytes != 0) allowance.Release(_bytes);
    }
}
