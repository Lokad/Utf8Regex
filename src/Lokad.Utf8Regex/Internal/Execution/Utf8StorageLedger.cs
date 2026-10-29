namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral aggregate admission ledger for invocation-owned storage: frames,
/// repeats, capture slots/history, decode/coordinate buffers, and output staging.
/// Reservation precedes acquisition, so a denied reservation must skip the
/// allocation it would have covered; callers map denial onto their own failure
/// vocabulary (see <see cref="Utf8ExecutionLimitKind"/>). Zero means unlimited.
/// Growth and copies must reserve the new extent while the old extent is still
/// held, then release the old extent, so overlap peaks are admitted, not assumed.
/// Hold by reference, including through the shared invocation of copied cursors.
/// Published cursor maps/tables stay charged for that invocation: collection and
/// another copy's EOF never refund their reservation. The ledger holds numbers,
/// not payload references. Exclusive scratch and unpublished setup still release
/// synchronously, so this conservative upper bound needs no GC release protocol.
/// </summary>
internal struct Utf8StorageLedger
{
    private ulong _reservedBytes;
    private readonly IUtf8MemoryAllowance? _memoryAllowance;

    internal Utf8StorageLedger(ulong maxLiveBytes) : this(maxLiveBytes, null) { }

    internal Utf8StorageLedger(ulong maxLiveBytes, IUtf8MemoryAllowance? memoryAllowance)
    {
        MaxLiveBytes = maxLiveBytes;
        _memoryAllowance = memoryAllowance;
    }

    internal ulong MaxLiveBytes { get; }

    internal ulong ReservedBytes => _reservedBytes;

    internal bool HasMemoryAllowance => _memoryAllowance is not null;

    internal bool IsUnlimited => MaxLiveBytes == 0 && _memoryAllowance is null;

    internal bool TryReserve(ulong bytes)
    {
        if (bytes > ulong.MaxValue - _reservedBytes)
        {
            return false;
        }

        var admitted = _reservedBytes + bytes;
        if (MaxLiveBytes != 0 && admitted > MaxLiveBytes)
        {
            return false;
        }

        if (bytes != 0 && _memoryAllowance is not null && !_memoryAllowance.TryReserve(bytes))
            throw new Utf8MemoryAdmissionException(bytes);
        _reservedBytes = admitted;
        return true;
    }

    internal void Release(ulong bytes)
    {
        if (bytes > _reservedBytes)
        {
            throw new InvalidOperationException("Storage release exceeds the outstanding reservation.");
        }

        _reservedBytes -= bytes;
        if (bytes != 0) _memoryAllowance?.Release(bytes);
    }

}
