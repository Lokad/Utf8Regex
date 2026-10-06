namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>One payload reservation shared by every holder of an exact managed
/// buffer. The descriptor does not itself root the buffer: each buffer view must
/// retain this descriptor through its last read. Collection queues a release;
/// only the synchronous ledger owner applies it. No pooled/native resource is
/// returned by this finalizer.</summary>
internal sealed class Utf8StorageLease(Utf8StorageLedger.CollectionState ledger, ulong bytes)
{
    private ulong _bytes = bytes;

    // Only setup rollback, before any cursor/result is published, may use this
    // eager path. Normal EOF drops a holder; another value copy can still read.
    internal void AbortBeforePublication()
    {
        var bytes = _bytes;
        _bytes = 0;
        ledger.Release(bytes);
        GC.SuppressFinalize(this);
    }

    ~Utf8StorageLease()
    {
        ledger.DeferCollectedRelease(_bytes);
    }
}
