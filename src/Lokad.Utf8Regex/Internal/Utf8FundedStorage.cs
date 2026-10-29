namespace Lokad.Utf8Regex.Internal;

// One conservative storage charge, not a retained-object graph. Closing stops
// new uses; the final in-flight user refunds the charge without GC authority.
internal sealed class Utf8FundedStorage(IUtf8MemoryAllowance allowance, string name)
{
    private readonly object _gate = new();
    private ulong _bytes;
    private int _users;
    private bool _closed;

    internal IUtf8MemoryAllowance Allowance => allowance;

    // Called once after construction transfers its reservations. A pending cache
    // entry may have been detached already; its preparer/waiters hold active uses.
    internal void Retain(ulong bytes)
    {
        ulong release = 0;
        lock (_gate)
        {
            _bytes = bytes;
            if (_closed && _users == 0) { release = _bytes; _bytes = 0; }
        }
        if (release != 0) allowance.Release(release);
    }

    internal Use Enter()
    {
        var use = new Use(this);
        lock (_gate)
        {
            if (_closed) throw new ObjectDisposedException(name);
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
            if (_users <= 0) throw new InvalidOperationException("Storage use was already released.");
            _users--;
            if (_closed && _users == 0) { release = _bytes; _bytes = 0; }
        }
        if (release != 0) allowance.Release(release);
    }

    internal sealed class Use(Utf8FundedStorage owner) : IDisposable
    {
        private Utf8FundedStorage? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Leave();
    }
}
