namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Shares scalar-program capacity admission across synchronous nested
/// construction. This is an instruction-capacity backstop, not a byte policy:
/// ASTs, tables, dictionaries, CLR/JIT metadata and lazy retained caches are not
/// accounted here. The complete common construction policy remains separate work.</summary>
internal sealed class Utf8ConstructionScope : IDisposable
{
    // Preserve a bounded backstop while replacing the former per-program silent
    // BCL decline. A final safe default needs the full resource/corpus qualification.
    internal const ulong DefaultInstructionCapacity = 1_048_576;
    [ThreadStatic] private static Utf8ConstructionScope? s_current;
    private readonly Utf8ConstructionScope? _previous;
    private readonly ulong _checkpoint;
    private bool _disposed;
    private bool _completed;

    internal Utf8ConstructionScope(ulong maximum)
    {
        _previous = s_current;
        Budget = _previous?.Budget ?? new Utf8ConstructionInstructionBudget(maximum);
        _checkpoint = Budget.Reserved;
        s_current = this;
    }

    internal Utf8ConstructionInstructionBudget Budget { get; }
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
        if (!_completed) Budget.Rollback(_checkpoint);
        s_current = _previous;
        _disposed = true;
    }
}

/// <summary>Counts aggregate live instruction-array slots before acquisition.
/// Exact old/new copying overlap and completed assertion graphs stay charged.
/// A failed lowerer rolls back only its own construction, never a parent's graphs.</summary>
internal sealed class Utf8ConstructionInstructionBudget(ulong maximum)
{
    internal ulong Maximum { get; } = maximum;
    internal ulong Reserved { get; private set; }
    internal ulong Peak { get; private set; }

    internal void Reserve(ulong slots)
    {
        var requested = slots > ulong.MaxValue - Reserved ? ulong.MaxValue : Reserved + slots;
        if (slots > ulong.MaxValue - Reserved || requested > Maximum)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionInstructionCapacity, Maximum, requested);
        Reserved = requested;
        Peak = Math.Max(Peak, requested);
    }

    internal void Release(ulong slots)
    {
        if (slots > Reserved) throw new InvalidOperationException("Construction release exceeds admitted instruction capacity.");
        Reserved -= slots;
    }

    internal void CheckReplacementMinimum(ulong temporarySlots, ulong finalMinimum)
    {
        var retained = Reserved - temporarySlots;
        var requested = finalMinimum > ulong.MaxValue - retained ? ulong.MaxValue : retained + finalMinimum;
        if (finalMinimum > ulong.MaxValue - retained || requested > Maximum)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionInstructionCapacity, Maximum, requested);
    }

    internal void Rollback(ulong checkpoint)
    {
        if (checkpoint > Reserved) throw new InvalidOperationException("Construction rollback exceeds admitted instruction capacity.");
        Reserved = checkpoint;
    }
}
