using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral aggregate admission ledger for invocation-owned storage: frames,
/// repeats, capture slots/history, decode/coordinate buffers, and output staging.
/// Reservation precedes acquisition, so a denied reservation must skip the
/// allocation it would have covered; callers map denial onto their own failure
/// vocabulary (see <see cref="Utf8ExecutionLimitKind"/>). Zero means unlimited.
/// Growth and copies must reserve the new extent while the old extent is still
/// held, then release the old extent, so overlap peaks are admitted, not assumed.
/// Hold by reference unless promoted into a shared invocation or collection
/// state. Collection-only releases are queued atomically; the synchronous owner
/// drains them before inspecting or changing the total. Finalizers never mutate
/// execution counters or compete with normal admission arithmetic.
/// </summary>
internal struct Utf8StorageLedger
{
    private ulong _reservedBytes;
    private CollectionState? _collectionState;

    internal Utf8StorageLedger(ulong maxLiveBytes)
    {
        MaxLiveBytes = maxLiveBytes;
    }

    internal ulong MaxLiveBytes { get; }

    internal ulong ReservedBytes => _collectionState?.ReservedBytes ?? _reservedBytes;

    internal bool IsUnlimited => MaxLiveBytes == 0;

    internal bool TryReserve(ulong bytes)
    {
        if (_collectionState is not null) return _collectionState.TryReserve(bytes);
        if (bytes > ulong.MaxValue - _reservedBytes)
        {
            return false;
        }

        var admitted = _reservedBytes + bytes;
        if (MaxLiveBytes != 0 && admitted > MaxLiveBytes)
        {
            return false;
        }

        _reservedBytes = admitted;
        return true;
    }

    internal void Release(ulong bytes)
    {
        if (_collectionState is not null)
        {
            _collectionState.Release(bytes);
            return;
        }
        if (bytes > _reservedBytes)
        {
            throw new InvalidOperationException("Storage release exceeds the outstanding reservation.");
        }

        _reservedBytes -= bytes;
    }

    // The payload was admitted before this lifetime descriptor is created.
    // Exact managed arrays are not returned to a pool by collection cleanup.
    internal Utf8StorageLease? CreateLease(ulong bytes)
    {
        if (bytes == 0) return null;
        _collectionState ??= new CollectionState(MaxLiveBytes, _reservedBytes);
        return new Utf8StorageLease(_collectionState, bytes);
    }

    internal sealed class CollectionState(ulong maximum, ulong reserved)
    {
        private ulong _reserved = reserved;
        private long _collectedRelease;

        internal ulong ReservedBytes
        {
            get { DrainCollectedReleases(); return _reserved; }
        }

        internal bool TryReserve(ulong bytes)
        {
            DrainCollectedReleases();
            if (bytes > ulong.MaxValue - _reserved) return false;
            var admitted = _reserved + bytes;
            if (maximum != 0 && admitted > maximum) return false;
            _reserved = admitted;
            return true;
        }

        internal void Release(ulong bytes)
        {
            DrainCollectedReleases();
            if (bytes > _reserved)
                throw new InvalidOperationException("Storage release exceeds the outstanding reservation.");
            _reserved -= bytes;
        }

        internal void DeferCollectedRelease(ulong bytes)
        {
            // Signed storage is only the Interlocked transport. Its unchecked
            // bit pattern retains the full ulong range. Queued loans cannot
            // sum beyond the admitted outstanding total.
            Interlocked.Add(ref _collectedRelease, unchecked((long)bytes));
        }

        private void DrainCollectedReleases()
        {
            var bytes = unchecked((ulong)Interlocked.Exchange(ref _collectedRelease, 0));
            if (bytes > _reserved)
                throw new InvalidOperationException("Collected storage release exceeds the outstanding reservation.");
            _reserved -= bytes;
        }
    }
}
