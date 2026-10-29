namespace Lokad.Utf8Regex.PythonRe;

// One conservative program charge, not a retained-object graph. Closing stops
// new uses; the final in-flight user refunds the charge without GC authority.
internal sealed class PythonReProgramMemory(IUtf8MemoryAllowance allowance)
{
    private readonly object _gate = new();
    private ulong _bytes;
    private int _users;
    private bool _closed;

    // Called only before the compiled expression is published.
    internal void Retain(ulong bytes) => _bytes = bytes;

    internal Use Enter()
    {
        var use = new Use(this);
        lock (_gate)
        {
            if (_closed) throw new ObjectDisposedException(nameof(Utf8PythonRegex));
            checked { _users++; }
        }
        return use;
    }

    internal void Close()
    {
        ulong release = 0;
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            if (_users == 0) { release = _bytes; _bytes = 0; }
        }
        if (release != 0) allowance.Release(release);
    }

    private void Leave()
    {
        ulong release = 0;
        lock (_gate)
        {
            if (_users <= 0) throw new InvalidOperationException("Program use was already released.");
            _users--;
            if (_closed && _users == 0) { release = _bytes; _bytes = 0; }
        }
        if (release != 0) allowance.Release(release);
    }

    internal sealed class Use(PythonReProgramMemory owner) : IDisposable
    {
        private PythonReProgramMemory? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Leave();
    }
}
