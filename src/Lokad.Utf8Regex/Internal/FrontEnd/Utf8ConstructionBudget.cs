using System.Runtime.CompilerServices;
using System.Text;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Aggregate admission for pattern decoding and scalar-lowering capacity. Charges
/// actual array strides, decoded string text plus its terminator and acquired
/// object field-state extents, with a conservative 32-byte header and 16-byte
/// payload rounding. Object tracking descriptors use the same byte owner.
/// Shared empties and
/// borrowed payloads are excluded. This is
/// defined live buffer capacity, not RSS or cumulative GC allocation traffic.</summary>
internal sealed class Utf8ConstructionBudget(ulong maximum, ulong maximumBytes, ulong maximumWorkSteps, ulong maximumDepth)
{
    internal Utf8ConstructionBudget(ulong maximum, ulong maximumBytes)
        : this(maximum, maximumBytes, Utf8ConstructionScope.DefaultWorkSteps, Utf8ConstructionScope.DefaultDepth) { }

    internal ulong Maximum { get; } = maximum;
    internal ulong MaximumBytes { get; } = maximumBytes;
    internal ulong Reserved { get; private set; }
    internal ulong Peak { get; private set; }
    internal ulong ReservedBytes { get; private set; }
    internal ulong PeakBytes { get; private set; }
    internal ulong MaximumWorkSteps { get; } = maximumWorkSteps;
    internal ulong MaximumDepth { get; } = maximumDepth;
    internal ulong WorkSteps { get; private set; }
    internal ulong Depth { get; private set; }
    internal ulong PeakDepth { get; private set; }
    internal ObjectAllocation? ObjectCheckpoint { get; private set; }

    // The owner keeps acquired objects alive until its scope closes. This makes
    // their conservative loans live construction capacity rather than a count
    // of allocations whose objects may already have become unreachable.
    internal sealed class ObjectAllocation(object value, ObjectAllocation? previous, ulong bytes)
    {
        internal readonly object Value = value;
        internal readonly ObjectAllocation? Previous = previous;
        internal readonly ulong Bytes = bytes;
    }

    // Actual admitted work stays spent even when a speculative child declines.
    // Batch admission happens before the bounded scan/copy it represents.
    internal void ChargeWork(ulong steps)
    {
        var requested = steps > ulong.MaxValue - WorkSteps ? ulong.MaxValue : WorkSteps + steps;
        if (steps > ulong.MaxValue - WorkSteps || requested > MaximumWorkSteps)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionWorkSteps, MaximumWorkSteps, requested);
        WorkSteps += steps;
    }

    internal void EnterDepth()
    {
        var requested = Depth == ulong.MaxValue ? ulong.MaxValue : Depth + 1;
        if (Depth == ulong.MaxValue || requested > MaximumDepth)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionDepth, MaximumDepth, requested);
        Depth++;
        PeakDepth = Math.Max(PeakDepth, Depth);
    }

    internal void LeaveDepth()
    {
        if (Depth == 0) throw new InvalidOperationException("Construction depth release exceeds admitted frames.");
        Depth--;
    }

    internal static ulong ArrayBytes<T>(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return count == 0 ? 0 : 32 + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15ul);
    }

    // Decoded pattern text is retained by the regex. Include its terminator;
    // shared empty text and caller-owned strings require no acquisition.
    internal static ulong StringBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        return length == 0 ? 0 : 32 + ((2 * ((ulong)length + 1) + 15) & ~15ul);
    }

    // State contains every acquired instance field, including initializer
    // fields; referenced strings/arrays have independent acquisition loans.
    internal static ulong ObjectBytes<TState>() => 32 + (((ulong)Unsafe.SizeOf<TState>() + 15) & ~15ul);

    internal T AllocateObject<TState, T>(TState state, Func<TState, T> factory) where T : class
    {
        var bytes = ObjectBytes<TState>() + ObjectBytes<(object, ObjectAllocation?, ulong)>();
        CheckBytes(bytes);
        ChargeWork(2); // Acquired object and its retaining descriptor.
        ReserveBytes(bytes);
        try
        {
            var result = factory(state);
            ObjectCheckpoint = new ObjectAllocation(result, ObjectCheckpoint, bytes);
            return result;
        }
        catch { ReleaseBytes(bytes); throw; }
    }

    internal void ClearObjectRoots() => ObjectCheckpoint = null;

    internal string CopyText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return string.Empty;
        var bytes = StringBytes(text.Length) + ObjectBytes<(object, ObjectAllocation?, ulong)>();
        CheckBytes(bytes);
        ChargeWork((ulong)text.Length + 1);
        ReserveBytes(bytes);
        try
        {
            var result = new string(text);
            ObjectCheckpoint = new ObjectAllocation(result, ObjectCheckpoint, bytes);
            return result;
        }
        catch { ReleaseBytes(bytes); throw; }
    }

    internal string CopyAsciiText(byte[] input)
    {
        // Retained literal-pattern metadata only; byte execution reuses input.
        // No subject decoding or temporary UTF-16 character buffer is needed.
        ChargeWork((ulong)input.Length);
        foreach (var value in input)
            if (value > 0x7f) throw new ArgumentException("Literal metadata must be ASCII.", nameof(input));
        if (input.Length == 0) return string.Empty;
        var bytes = StringBytes(input.Length) + ObjectBytes<(object, ObjectAllocation?, ulong)>();
        CheckBytes(bytes); ChargeWork((ulong)input.Length + 1); ReserveBytes(bytes);
        try
        {
            var result = string.Create(input.Length, input, static (target, source) =>
            { for (var index = 0; index < source.Length; index++) target[index] = (char)source[index]; });
            ObjectCheckpoint = new ObjectAllocation(result, ObjectCheckpoint, bytes);
            return result;
        }
        catch { ReleaseBytes(bytes); throw; }
    }

    internal byte[] EncodeUtf8(ReadOnlySpan<char> input)
    {
        // Pattern strings are interoperability input. Validate complete scalars
        // before acquiring bytes; an impossible surrogate atom must never become
        // a replacement-character literal in a native search plan.
        ChargeWork((ulong)input.Length);
        var byteCount = 0;
        for (var offset = 0; offset < input.Length;)
        {
            if (Rune.DecodeFromUtf16(input[offset..], out var scalar, out var width) != System.Buffers.OperationStatus.Done)
                throw new ArgumentException("A native literal must contain complete Unicode scalars.", nameof(input));
            byteCount = checked(byteCount + scalar.Utf8SequenceLength);
            offset += width;
        }
        var result = AllocateArray<byte>(byteCount, false);
        try
        {
            ChargeWork((ulong)input.Length + (ulong)byteCount);
            var position = 0;
            for (var offset = 0; offset < input.Length;)
            {
                _ = Rune.DecodeFromUtf16(input[offset..], out var scalar, out var width);
                position += scalar.EncodeToUtf8(result.AsSpan(position));
                offset += width;
            }
            return result;
        }
        catch { ReleaseArray(result, false); throw; }
    }

    internal string DecodeUtf8(ReadOnlySpan<byte> input, Encoding encoding)
    {
        // Count/validate before allocating the output. Parsing starts only after
        // the decoded string has been admitted into the enclosing owner.
        ChargeWork((ulong)input.Length);
        var bytes = StringBytes(encoding.GetCharCount(input));
        ReserveBytes(bytes);
        try
        {
            ChargeWork((ulong)input.Length);
            return encoding.GetString(input);
        }
        catch
        {
            ReleaseBytes(bytes);
            throw;
        }
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

    internal void ReserveBytes(ulong bytes)
    {
        CheckBytes(bytes);
        ReservedBytes += bytes;
        PeakBytes = Math.Max(PeakBytes, ReservedBytes);
    }

    internal T[] AllocateArray<T>(int count, bool instructions)
    {
        var bytes = ArrayBytes<T>(count);
        if (instructions) CheckSlots((ulong)count);
        CheckBytes(bytes);
        ChargeWork((ulong)count);
        if (instructions) Reserve((ulong)count);
        ReserveBytes(bytes);
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

    internal void Rollback(ulong checkpoint, ulong byteCheckpoint, ulong depthCheckpoint, ObjectAllocation? objectCheckpoint)
    {
        if (checkpoint > Reserved || byteCheckpoint > ReservedBytes || depthCheckpoint > Depth)
            throw new InvalidOperationException("Construction rollback exceeds admitted capacity.");
        Reserved = checkpoint;
        ReservedBytes = byteCheckpoint;
        Depth = depthCheckpoint;
        ObjectCheckpoint = objectCheckpoint;
    }
}
