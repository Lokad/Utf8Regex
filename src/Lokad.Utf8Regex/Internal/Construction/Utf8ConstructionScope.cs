namespace Lokad.Utf8Regex.Internal;

/// <summary>Shares scalar-program capacity admission across synchronous nested
/// construction. Scalar tree conversion/lowering arrays share byte admission;
/// instruction arrays also share instruction-slot admission. Adapter byte-pattern
/// decoding admits its retained text in the same owner before acquisition.
/// Covered scans, parser/conversion loops, vectors/maps/sorts and preparation
/// resumes share cumulative work admission; core/Python parser, preparation and
/// byte-safe DFA frames share live depth admission. DFA/NFA state vectors,
/// edges, closures, bucket maps and graph descriptors share capacity admission.
/// Owned dynamic emission also admits IL/fixup/local/token/stack buffers,
/// runtime managed copies, dispatch/lowering tables and complete wrapper state.
/// Serialized IL byte capacity conservatively counts as instruction capacity.
/// Speculative failure never refunds admitted work.
/// Core tree nodes/containers and Python parser AST objects, with their retaining
/// descriptors, share byte admission. Other IR node objects, lexical strings/child
/// storage, CLR/JIT metadata and lazy retained
/// cache key/value retention remains outside this partial backstop. Preparation
/// cache/component/entry metadata is admitted before acquisition.</summary>
internal sealed class Utf8ConstructionScope : IDisposable
{
    // Preserve a bounded backstop while replacing the former per-program silent
    // BCL decline. A final safe default needs the full resource/corpus qualification.
    internal const ulong DefaultInstructionCapacity = 1_048_576;
    internal const ulong DefaultBufferBytes = 64 * 1024 * 1024;
    internal const ulong DefaultWorkSteps = 64 * 1024 * 1024;
    internal const ulong DefaultDepth = 65536;
    [ThreadStatic] private static Utf8ConstructionScope? s_current;
    private readonly Utf8ConstructionScope? _previous;
    private readonly ulong _checkpoint;
    private readonly ulong _byteCheckpoint;
    private readonly ulong _depthCheckpoint;
    private readonly Utf8ConstructionBudget.ObjectAllocation? _objectCheckpoint;
    private bool _disposed;
    private bool _completed;

    internal Utf8ConstructionScope(ulong maximum)
        : this(maximum, DefaultBufferBytes)
    {
    }

    internal Utf8ConstructionScope(ulong maximum, ulong maximumBytes)
        : this(maximum, maximumBytes, DefaultWorkSteps, DefaultDepth)
    {
    }

    internal Utf8ConstructionScope(ulong maximum, ulong maximumBytes, ulong maximumWorkSteps, ulong maximumDepth)
    {
        _previous = s_current;
        Budget = _previous?.Budget ?? new Utf8ConstructionBudget(maximum, maximumBytes, maximumWorkSteps, maximumDepth);
        _checkpoint = Budget.Reserved;
        _byteCheckpoint = Budget.ReservedBytes;
        _depthCheckpoint = Budget.Depth;
        _objectCheckpoint = Budget.ObjectCheckpoint;
        s_current = this;
    }

    internal Utf8ConstructionBudget Budget { get; }
    internal static bool IsActive => s_current is not null;
    internal static void ChargeWork(ulong steps) => s_current?.Budget.ChargeWork(steps);
    internal static Utf8ConstructionBudget CurrentBudget => s_current?.Budget
        ?? throw new InvalidOperationException("No construction owner is active.");

    // Constructor and lowerer entry points are synchronous. Nested core/adaptor
    // construction inherits the owner instead of resetting its capacity counter.
    internal static Utf8ConstructionScope Enter() => new(DefaultInstructionCapacity);

    internal void Complete() => _completed = true;

    /// <summary>Restores the enclosing construction owner; scopes must close in lexical order.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (!ReferenceEquals(s_current, this))
            throw new InvalidOperationException("Construction scopes must close in lexical order.");
        if (!_completed) Budget.Rollback(_checkpoint, _byteCheckpoint, _depthCheckpoint, _objectCheckpoint);
        if (_previous is null) Budget.ClearObjectRoots();
        s_current = _previous;
        _disposed = true;
    }
}
