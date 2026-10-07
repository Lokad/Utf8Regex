namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Shares scalar-program capacity admission across synchronous nested
/// construction. Scalar tree conversion/lowering arrays share byte admission;
/// instruction arrays also share instruction-slot admission.
/// Parser/IR node objects, strings, descriptors, CLR/JIT metadata and lazy retained
/// caches remain outside this partial backstop.</summary>
internal sealed class Utf8ConstructionScope : IDisposable
{
    // Preserve a bounded backstop while replacing the former per-program silent
    // BCL decline. A final safe default needs the full resource/corpus qualification.
    internal const ulong DefaultInstructionCapacity = 1_048_576;
    internal const ulong DefaultBufferBytes = 64 * 1024 * 1024;
    [ThreadStatic] private static Utf8ConstructionScope? s_current;
    private readonly Utf8ConstructionScope? _previous;
    private readonly ulong _checkpoint;
    private readonly ulong _byteCheckpoint;
    private bool _disposed;
    private bool _completed;

    internal Utf8ConstructionScope(ulong maximum)
        : this(maximum, DefaultBufferBytes)
    {
    }

    internal Utf8ConstructionScope(ulong maximum, ulong maximumBytes)
    {
        _previous = s_current;
        Budget = _previous?.Budget ?? new Utf8ConstructionBudget(maximum, maximumBytes);
        _checkpoint = Budget.Reserved;
        _byteCheckpoint = Budget.ReservedBytes;
        s_current = this;
    }

    internal Utf8ConstructionBudget Budget { get; }
    internal static bool IsActive => s_current is not null;

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
        if (!_completed) Budget.Rollback(_checkpoint, _byteCheckpoint);
        s_current = _previous;
        _disposed = true;
    }
}
