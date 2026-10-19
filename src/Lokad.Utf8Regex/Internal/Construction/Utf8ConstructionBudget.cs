using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal;

/// <summary>Aggregate admission for pattern decoding and scalar-lowering capacity. Charges
/// actual array strides, decoded string text plus its terminator and acquired
/// conservative helper charges with a 128-byte floor or larger rounded field
/// state. Arrays/text use a 32-byte header and 16-byte payload rounding.
/// Shared empties and borrowed payloads are excluded. Dead helpers are not rooted
/// merely for accounting; their charges persist until scope completion/rollback.
/// This is covered capacity, not RSS or arbitrary GC allocation traffic.</summary>
internal sealed class Utf8ConstructionBudget(ulong maximum, ulong maximumBytes, ulong maximumWorkSteps, ulong maximumDepth)
{
    internal Utf8ConstructionBudget(ulong maximum, ulong maximumBytes)
        : this(maximum, maximumBytes, Utf8ConstructionScope.DefaultWorkSteps, Utf8ConstructionScope.DefaultDepth) { }

    internal ulong Maximum { get; } = maximum;
    internal ulong MaximumBytes { get; private set; } = maximumBytes;
    internal ulong Reserved { get; private set; }
    internal ulong Peak { get; private set; }
    internal ulong ReservedBytes { get; private set; }
    internal ulong PeakBytes { get; private set; }
    internal ulong MaximumWorkSteps { get; } = maximumWorkSteps;
    internal ulong MaximumDepth { get; } = maximumDepth;
    internal ulong WorkSteps { get; private set; }
    internal ulong Depth { get; private set; }
    internal ulong PeakDepth { get; private set; }
    // Optional authority belongs to this shared owner, so nested optimizers
    // inherit it. Existing constructor entries use no token/infinite timeout;
    // complete public construction/invocation policy remains a separate gate.
    internal Utf8ExecutionDeadline Deadline { get; set; } = Utf8ExecutionDeadline.Infinite;
    internal System.Threading.CancellationToken CancellationToken { get; set; }
    internal Action<ulong>? CheckpointHook { get; set; }

    // Actual admitted work stays spent even when a speculative child declines.
    // Batch admission happens before the bounded scan/copy it represents.
    internal void ChargeWork(ulong steps)
    {
        CheckpointHook?.Invoke(WorkSteps);
        CancellationToken.ThrowIfCancellationRequested();
        Deadline.CheckNow();
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

    // Larger acquisitions describe their instance fields, including initializers.
    // Small utility factories use constructor arguments under the 128-byte floor;
    // referenced strings/arrays have independent acquisition loans.
    internal static ulong ObjectBytes<TState>() => 32 + (((ulong)Unsafe.SizeOf<TState>() + 15) & ~15ul);

    internal T AllocateObject<TState, T>(TState state, Func<TState, T> factory) where T : class
    {
        var bytes = Math.Max(128ul, ObjectBytes<TState>());
        CheckBytes(bytes);
        ChargeWork(2); // Conservative admission and factory-acquisition work.
        ReserveBytes(bytes);
        try
        {
            return factory(state);
        }
        catch { ReleaseBytes(bytes); throw; }
    }

    internal string CopyText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return string.Empty;
        var bytes = StringBytes(text.Length);
        CheckBytes(bytes);
        ChargeWork((ulong)text.Length + 1);
        ReserveBytes(bytes);
        try
        {
            var result = new string(text);
            return result;
        }
        catch { ReleaseBytes(bytes); throw; }
    }

    internal string CopyAsciiText(byte[] input)
    {
        // Retained literal-pattern metadata only; byte execution reuses input.
        // No subject decoding or temporary UTF-16 character buffer is needed.
        ChargeWork((ulong)input.Length);
        for (var offset = 0; offset < input.Length;)
        {
            if (offset != 0) ChargeWork(0);
            var end = offset + Math.Min(2048, input.Length - offset);
            for (; offset < end; offset++)
                if (input[offset] > 0x7f) throw new ArgumentException("Literal metadata must be ASCII.", nameof(input));
        }
        if (input.Length == 0) return string.Empty;
        var bytes = StringBytes(input.Length);
        CheckBytes(bytes); ChargeWork((ulong)input.Length + 1); ReserveBytes(bytes);
        try
        {
            var result = string.Create(input.Length, (Input: input, Budget: this), static (target, state) =>
            {
                for (var offset = 0; offset < state.Input.Length;)
                {
                    if (offset != 0) state.Budget.ChargeWork(0);
                    var end = offset + Math.Min(2048, state.Input.Length - offset);
                    for (; offset < end; offset++) target[offset] = (char)state.Input[offset];
                }
            });
            ChargeWork(0);
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
            var end = offset + Math.Min(2048, input.Length - offset);
            if (offset != 0) ChargeWork(0);
            // A pair may cross the chunk end: finish its scalar before polling.
            // The pass was admitted above; checkpoints add no numeric work.
            do
            {
                if (Rune.DecodeFromUtf16(input[offset..], out var scalar, out var width) != System.Buffers.OperationStatus.Done)
                    throw new ArgumentException("A native literal must contain complete Unicode scalars.", nameof(input));
                byteCount = checked(byteCount + scalar.Utf8SequenceLength);
                offset += width;
            } while (offset < end);
        }
        var result = AllocateArray<byte>(byteCount, false);
        try
        {
            ChargeWork((ulong)input.Length + (ulong)byteCount);
            var position = 0;
            for (var offset = 0; offset < input.Length;)
            {
                var end = offset + Math.Min(2048, input.Length - offset);
                if (offset != 0) ChargeWork(0);
                do
                {
                    _ = Rune.DecodeFromUtf16(input[offset..], out var scalar, out var width);
                    position += scalar.EncodeToUtf8(result.AsSpan(position));
                    offset += width;
                } while (offset < end);
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
        var charCount = 0;
        for (var offset = 0; offset < input.Length;)
        {
            if (offset != 0) ChargeWork(0);
            var count = Utf8SliceBufferWork.Utf8BlockLength(input[offset..]);
            try { charCount = checked(charCount + encoding.GetCharCount(input.Slice(offset, count))); }
            catch (DecoderFallbackException failure) when (offset != 0)
            {
                // Codec spans report chunk-relative indices. Keep the original
                // input coordinate and offending bytes for pattern diagnostics.
                var index = checked(offset + failure.Index);
                throw new DecoderFallbackException($"Invalid UTF-8 data at byte index {index}.", failure.BytesUnknown, index);
            }
            offset += count;
        }
        var bytes = StringBytes(charCount);
        ReserveBytes(bytes);
        try
        {
            ChargeWork((ulong)input.Length);
            return string.Create(charCount, new ConstructionDecodeState(input, this, encoding), static (destination, state) =>
            {
                var written = 0;
                for (var offset = 0; offset < state.Input.Length;)
                {
                    if (offset != 0) state.Budget.ChargeWork(0);
                    var count = Utf8SliceBufferWork.Utf8BlockLength(state.Input[offset..]);
                    written += state.Encoding.GetChars(state.Input.Slice(offset, count), destination[written..]);
                    offset += count;
                }
            });
        }
        catch
        {
            ReleaseBytes(bytes);
            throw;
        }
    }

    // Synchronous string.Create state borrows the existing owner and bytes;
    // no intermediate buffer, retained owner or independent allowance.
    private readonly ref struct ConstructionDecodeState(ReadOnlySpan<byte> input, Utf8ConstructionBudget budget, Encoding encoding)
    {
        internal ReadOnlySpan<byte> Input { get; } = input;
        internal Utf8ConstructionBudget Budget { get; } = budget;
        internal Encoding Encoding { get; } = encoding;
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

    // PCRE2-INTEGRATION-POINT: a synchronous preparation can borrow an
    // additional-capacity ceiling without resetting its parent's reservations.
    internal void RestrictAdditionalBytes(ulong additional)
    {
        var ceiling = additional > ulong.MaxValue - ReservedBytes ? ulong.MaxValue : ReservedBytes + additional;
        MaximumBytes = Math.Min(MaximumBytes, ceiling);
        PeakBytes = ReservedBytes;
    }

    internal void RestoreByteLimit(ulong maximum, ulong peak)
    {
        MaximumBytes = maximum;
        PeakBytes = Math.Max(PeakBytes, peak);
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

    internal void Rollback(ulong checkpoint, ulong byteCheckpoint, ulong depthCheckpoint)
    {
        if (checkpoint > Reserved || byteCheckpoint > ReservedBytes || depthCheckpoint > Depth)
            throw new InvalidOperationException("Construction rollback exceeds admitted capacity.");
        Reserved = checkpoint;
        ReservedBytes = byteCheckpoint;
        Depth = depthCheckpoint;
    }
}
