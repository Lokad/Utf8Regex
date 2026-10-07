using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Aggregate admission for scalar-lowering array capacity. Charges
/// actual element strides plus a conservative 32-byte header and 16-byte payload
/// rounding. Shared empty arrays and borrowed payloads are excluded. This is
/// defined live buffer capacity, not RSS or cumulative GC allocation traffic.</summary>
internal sealed class Utf8ConstructionBudget(ulong maximum, ulong maximumBytes)
{
    internal ulong Maximum { get; } = maximum;
    internal ulong MaximumBytes { get; } = maximumBytes;
    internal ulong Reserved { get; private set; }
    internal ulong Peak { get; private set; }
    internal ulong ReservedBytes { get; private set; }
    internal ulong PeakBytes { get; private set; }

    internal static ulong ArrayBytes<T>(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return count == 0 ? 0 : 32 + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15ul);
    }

    internal int AvailableArrayCapacity<T>()
    {
        var available = MaximumBytes - ReservedBytes;
        if (available < 32) return 0;
        var payload = (available - 32) & ~15ul;
        return (int)Math.Min((ulong)Array.MaxLength, payload / (ulong)Unsafe.SizeOf<T>());
    }

    internal void CheckSlots(ulong slots)
    {
        var requested = slots > ulong.MaxValue - Reserved ? ulong.MaxValue : Reserved + slots;
        if (slots > ulong.MaxValue - Reserved || requested > Maximum)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionInstructionCapacity, Maximum, requested);
    }

    internal void CheckBytes(ulong bytes)
    {
        var requested = bytes > ulong.MaxValue - ReservedBytes ? ulong.MaxValue : ReservedBytes + bytes;
        if (bytes > ulong.MaxValue - ReservedBytes || requested > MaximumBytes)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionBytes, MaximumBytes, requested);
    }

    internal void Reserve(ulong slots)
    {
        CheckSlots(slots);
        Reserved += slots;
        Peak = Math.Max(Peak, Reserved);
    }

    internal T[] AllocateArray<T>(int count, bool instructions)
    {
        var bytes = ArrayBytes<T>(count);
        if (instructions) CheckSlots((ulong)count);
        CheckBytes(bytes);
        if (instructions) Reserve((ulong)count);
        ReservedBytes += bytes;
        PeakBytes = Math.Max(PeakBytes, ReservedBytes);
        try { return count == 0 ? [] : new T[count]; }
        catch
        {
            if (instructions) Release((ulong)count);
            ReleaseBytes(bytes);
            throw;
        }
    }

    internal void Release(ulong slots)
    {
        if (slots > Reserved) throw new InvalidOperationException("Construction release exceeds admitted instruction capacity.");
        Reserved -= slots;
    }

    internal void ReleaseBytes(ulong bytes)
    {
        if (bytes > ReservedBytes) throw new InvalidOperationException("Construction release exceeds admitted buffer capacity.");
        ReservedBytes -= bytes;
    }

    internal void ReleaseArray<T>(T[] array, bool instructions)
    {
        if (instructions) Release((ulong)array.Length);
        ReleaseBytes(ArrayBytes<T>(array.Length));
    }

    internal void CheckReplacementMinimum(ulong temporarySlots, ulong finalMinimum)
    {
        var retained = Reserved - temporarySlots;
        var requested = finalMinimum > ulong.MaxValue - retained ? ulong.MaxValue : retained + finalMinimum;
        if (finalMinimum > ulong.MaxValue - retained || requested > Maximum)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionInstructionCapacity, Maximum, requested);
    }

    internal void Rollback(ulong checkpoint, ulong byteCheckpoint)
    {
        if (checkpoint > Reserved || byteCheckpoint > ReservedBytes)
            throw new InvalidOperationException("Construction rollback exceeds admitted capacity.");
        Reserved = checkpoint;
        ReservedBytes = byteCheckpoint;
    }
}
