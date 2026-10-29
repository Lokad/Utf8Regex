namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Holds a caller-funded UTF-8 substitution result until its reservation is released.</summary>
/// <remarks>Dispose after retained users finish, or take over admission for byte-array aliases kept afterward. Result bytes remain valid when copied before disposal. There is no finalizer.</remarks>
public sealed class Utf8PythonRetainedSubnResult : IDisposable
{
    private Utf8PythonSubnUtf8Result _result;
    private IUtf8MemoryAllowance? _allowance;
    private ulong _bytes;

    internal Utf8PythonRetainedSubnResult(Utf8PythonSubnUtf8Result result, IUtf8MemoryAllowance allowance)
    { _result = result; _allowance = allowance; }

    internal void SetTransferredBytes(ulong bytes) => _bytes = bytes;

    /// <summary>Gets the owned UTF-8 result and replacement count while this receipt holds its reservation.</summary>
    /// <remarks>Finish property accesses before disposal. Previously copied byte-array aliases remain independent of this receipt.</remarks>
    /// <exception cref="ObjectDisposedException">The reservation was released.</exception>
    public Utf8PythonSubnUtf8Result Result => Volatile.Read(ref _allowance) is not null
        ? _result : throw new ObjectDisposedException(nameof(Utf8PythonRetainedSubnResult));

    /// <summary>Gets the original modeled charge transferred to this receipt, including after disposal.</summary>
    public ulong ChargedBytes => _bytes;

    /// <summary>Clears this receipt's payload references and refunds its loan exactly once.</summary>
    /// <remarks>Idempotent, including concurrent disposal/aliases. The caller owns admission for continued retention of copied results after this explicit release.</remarks>
    public void Dispose()
    {
        var allowance = Interlocked.Exchange(ref _allowance, null);
        if (allowance is null) return;
        _result = default;
        if (_bytes != 0) allowance.Release(_bytes);
    }
}
