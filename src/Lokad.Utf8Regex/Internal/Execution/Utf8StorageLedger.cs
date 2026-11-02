namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral aggregate admission ledger for invocation-owned storage: frames,
/// repeats, capture slots/history, decode/coordinate buffers, and output staging.
/// The invocation checks its numeric ceiling before reserving here. Reservation
/// precedes acquisition; overflow or refused caller funding must skip the
/// allocation it would have covered. Numeric limits and sticky denials belong
/// to the invocation budget, not a second copy in this ledger.
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

    internal Utf8StorageLedger(IUtf8MemoryAllowance? memoryAllowance)
    {
        _memoryAllowance = memoryAllowance;
    }

    internal ulong ReservedBytes => _reservedBytes;

    internal bool HasMemoryAllowance => _memoryAllowance is not null;
    internal IUtf8MemoryAllowance? MemoryAllowance => _memoryAllowance;

    internal bool TryReserve(ulong bytes)
    {
        if (bytes > ulong.MaxValue - _reservedBytes)
        {
            return false;
        }

        var admitted = _reservedBytes + bytes;
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

    // A published receipt takes responsibility for this already admitted loan.
    // The external account stays charged; only invocation ownership changes.
    internal void TransferToCaller(ulong bytes)
    {
        if (_memoryAllowance is null || bytes > _reservedBytes)
            throw new InvalidOperationException("Transfer requires live caller-funded storage.");
        _reservedBytes -= bytes;
    }

}
