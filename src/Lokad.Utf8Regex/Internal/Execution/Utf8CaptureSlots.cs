using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8CaptureSlots
{
    private readonly int[] _starts;
    private readonly int[] _lengths;
    private Utf8CaptureMutation[]? _journal;
    private int _journalCount;
    private bool _journaling;
    private bool _journalExact;
    private ulong _journalReservedBytes;

    public Utf8CaptureSlots(int slotCount)
    {
        if (slotCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        _starts = new int[slotCount];
        _lengths = new int[slotCount];
        Clear();
    }

    public void Clear()
    {
        if (_journaling)
        {
            throw new InvalidOperationException("Capture slots cannot be cleared while an invocation is active.");
        }

        Array.Fill(_starts, -1);
        Array.Fill(_lengths, 0);
    }

    internal Utf8ExecutionLimitKind TryClear(ref Utf8SliceInvocation invocation)
    {
        if (_journaling)
            throw new InvalidOperationException("Capture slots cannot be cleared while an invocation is active.");
        for (var offset = 0; offset < _starts.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var count = Math.Min(2048, _starts.Length - offset);
            _starts.AsSpan(offset, count).Fill(-1);
            _lengths.AsSpan(offset, count).Clear();
            offset += count;
        }
        return Utf8ExecutionLimitKind.None;
    }

    public void Set(int slot, int start, int length)
    {
        if ((uint)slot >= (uint)_starts.Length)
        {
            return;
        }

        if (_journaling)
        {
            EnsureJournalCapacity();
            var journal = _journal ?? throw new InvalidOperationException("The capture journal could not be allocated.");
            journal[_journalCount++] = new Utf8CaptureMutation(slot, _starts[slot], _lengths[slot]);
        }

        _starts[slot] = start;
        _lengths[slot] = length;
    }

    public bool TrySet(int slot, int start, int length, ref Utf8ExecutionBudget budget)
    {
        if ((uint)slot >= (uint)_starts.Length)
        {
            return true;
        }

        if (_journaling)
        {
            if (!TryEnsureJournalCapacity(ref budget))
            {
                return false;
            }

            var journal = _journal ?? throw new InvalidOperationException("The capture journal could not be allocated.");
            journal[_journalCount++] = new Utf8CaptureMutation(slot, _starts[slot], _lengths[slot]);
        }

        _starts[slot] = start;
        _lengths[slot] = length;
        return true;
    }

    public bool TryGet(int slot, out int start, out int length)
    {
        if ((uint)slot < (uint)_starts.Length && _starts[slot] >= 0)
        {
            start = _starts[slot];
            length = _lengths[slot];
            return true;
        }

        start = 0;
        length = 0;
        return false;
    }

    internal Utf8ExecutionLimitKind TrySet(int slot, int start, int length, ref Utf8SliceInvocation invocation)
    {
        if ((uint)slot >= (uint)_starts.Length) return Utf8ExecutionLimitKind.None;
        if (!_journaling) throw new InvalidOperationException("Capture journaling is not active.");
        var breach = TryEnsureInvocationJournalCapacity(ref invocation);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var journal = _journal ?? throw new InvalidOperationException("The capture journal could not be allocated.");
        journal[_journalCount++] = new Utf8CaptureMutation(slot, _starts[slot], _lengths[slot]);
        _starts[slot] = start;
        _lengths[slot] = length;
        return Utf8ExecutionLimitKind.None;
    }

    internal Utf8ExecutionLimitKind TryRollback(Utf8CaptureCheckpoint checkpoint, ref Utf8SliceInvocation invocation)
    {
        if (!_journaling || (uint)checkpoint.JournalCount > (uint)_journalCount)
            throw new InvalidOperationException("The capture checkpoint does not belong to the active invocation.");
        while (_journalCount > checkpoint.JournalCount)
        {
            var breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var journal = _journal ?? throw new InvalidOperationException("The active capture journal is unavailable.");
            var mutation = journal[--_journalCount];
            _starts[mutation.Slot] = mutation.PreviousStart;
            _lengths[mutation.Slot] = mutation.PreviousLength;
        }
        return Utf8ExecutionLimitKind.None;
    }

    private Utf8ExecutionLimitKind TryEnsureInvocationJournalCapacity(ref Utf8SliceInvocation invocation)
    {
        var old = _journal;
        if (old is not null && !_journalExact)
            throw new InvalidOperationException("An existing capture journal belongs to another invocation owner.");
        if (old is not null && _journalCount < old.Length) return Utf8ExecutionLimitKind.None;
        var capacity = old is null ? Math.Max(16, _starts.Length)
            : old.Length <= Array.MaxLength / 2 ? old.Length * 2 : Array.MaxLength;
        if (capacity <= _journalCount) return Utf8ExecutionLimitKind.WorkingBytes;
        var bytes = (ulong)(uint)capacity * (ulong)(uint)Unsafe.SizeOf<Utf8CaptureMutation>();
        if (!invocation.Ledger.TryReserve(bytes)) return Utf8ExecutionLimitKind.WorkingBytes;
        var adopted = false;
        try
        {
            var breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var grown = Utf8ExactStatePool<Utf8CaptureMutation>.Rent(capacity, invocation.Ledger.IsUnlimited);
            try
            {
                for (var offset = 0; offset < _journalCount;)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var count = Math.Min(2048, _journalCount - offset);
                    if (old is not null) old.AsSpan(offset, count).CopyTo(grown.AsSpan(offset, count));
                    offset += count;
                }
                _journal = grown;
                _journalExact = true;
                invocation.Ledger.Release(_journalReservedBytes);
                _journalReservedBytes = bytes;
                adopted = true;
                if (old is not null) Utf8ExactStatePool<Utf8CaptureMutation>.Return(old, invocation.Ledger.IsUnlimited);
            }
            finally { if (!adopted) Utf8ExactStatePool<Utf8CaptureMutation>.Return(grown, invocation.Ledger.IsUnlimited); }
            return Utf8ExecutionLimitKind.None;
        }
        finally { if (!adopted) invocation.Ledger.Release(bytes); }
    }

    public void BeginInvocation()
    {
        if (_journaling)
        {
            throw new InvalidOperationException("Capture journaling is already active.");
        }

        _journalCount = 0;
        _journaling = true;
    }

    public Utf8CaptureCheckpoint CreateCheckpoint()
    {
        if (!_journaling)
        {
            throw new InvalidOperationException("Capture journaling is not active.");
        }

        return new Utf8CaptureCheckpoint(_journalCount);
    }

    public void Rollback(Utf8CaptureCheckpoint checkpoint)
    {
        if (!_journaling || (uint)checkpoint.JournalCount > (uint)_journalCount)
        {
            throw new InvalidOperationException("The capture checkpoint does not belong to the active invocation.");
        }

        var journal = _journal;

        while (_journalCount > checkpoint.JournalCount)
        {
            if (journal is null)
            {
                throw new InvalidOperationException("The active capture journal is unavailable.");
            }

            var mutation = journal[--_journalCount];
            _starts[mutation.Slot] = mutation.PreviousStart;
            _lengths[mutation.Slot] = mutation.PreviousLength;
        }
    }

    public void EndInvocation()
    {
        if (_journalReservedBytes != 0)
            throw new InvalidOperationException("A metered capture journal must return through its invocation.");
        if (!_journaling)
        {
            return;
        }

        _journaling = false;
        _journalCount = 0;
        var journal = _journal;
        _journal = null;
        if (journal is not null && !_journalExact)
        {
            ArrayPool<Utf8CaptureMutation>.Shared.Return(journal);
        }
        _journalExact = false;
    }

    internal void EndInvocation(ref Utf8SliceInvocation invocation)
    {
        var journal = _journal;
        if (journal is not null && _journalExact)
            Utf8ExactStatePool<Utf8CaptureMutation>.Return(journal, invocation.Ledger.IsUnlimited);
        invocation.Ledger.Release(_journalReservedBytes);
        _journalReservedBytes = 0;
        EndInvocation();
    }

    public Utf8CaptureSlots Clone()
    {
        var clone = new Utf8CaptureSlots(_starts.Length);
        Array.Copy(_starts, clone._starts, _starts.Length);
        Array.Copy(_lengths, clone._lengths, _lengths.Length);
        return clone;
    }

    private void EnsureJournalCapacity()
    {
        var unlimited = new Utf8ExecutionBudget(default, Regex.InfiniteMatchTimeout, CancellationToken.None);
        if (!TryEnsureJournalCapacity(ref unlimited))
        {
            throw new InvalidOperationException("The capture journal could not be reserved.");
        }
    }

    private bool TryEnsureJournalCapacity(ref Utf8ExecutionBudget budget)
    {
        if (_journalExact) throw new InvalidOperationException("A metered capture journal requires its invocation budget.");
        var journal = _journal;
        if (journal is not null && _journalCount < journal.Length)
        {
            return true;
        }

        var requestedLength = journal is null ? Math.Max(16, _starts.Length) : checked(journal.Length * 2);
        var elementSize = Unsafe.SizeOf<Utf8CaptureMutation>();
        var liveBytes =
            checked((ulong)(uint)requestedLength * (ulong)(uint)elementSize) +
            (journal is null ? 0UL : checked((ulong)(uint)journal.Length * (ulong)(uint)elementSize));
        if (budget.TryNoteWorkingBytes(liveBytes) != Utf8ExecutionLimitKind.None)
        {
            return false;
        }

        var grown = ArrayPool<Utf8CaptureMutation>.Shared.Rent(requestedLength);
        var actualBytes =
            checked((ulong)(uint)grown.Length * (ulong)(uint)elementSize) +
            (journal is null ? 0UL : checked((ulong)(uint)journal.Length * (ulong)(uint)elementSize));
        if (budget.TryNoteWorkingBytes(actualBytes) != Utf8ExecutionLimitKind.None)
        {
            ArrayPool<Utf8CaptureMutation>.Shared.Return(grown);
            return false;
        }

        if (journal is not null)
        {
            journal.AsSpan(0, _journalCount).CopyTo(grown);
            ArrayPool<Utf8CaptureMutation>.Shared.Return(journal);
        }

        _journal = grown;
        return true;
    }
}

internal readonly record struct Utf8CaptureCheckpoint(int JournalCount);

internal readonly record struct Utf8CaptureMutation(int Slot, int PreviousStart, int PreviousLength);
