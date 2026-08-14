using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8CaptureSlots
{
    private readonly int[] _starts;
    private readonly int[] _lengths;
    private Utf8CaptureMutation[]? _journal;
    private int _journalCount;
    private bool _journaling;

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
        if (!_journaling)
        {
            return;
        }

        _journaling = false;
        _journalCount = 0;
        var journal = _journal;
        _journal = null;
        if (journal is not null)
        {
            ArrayPool<Utf8CaptureMutation>.Shared.Return(journal);
        }
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
        var journal = _journal;
        if (journal is not null && _journalCount < journal.Length)
        {
            return;
        }

        var requestedLength = journal is null ? Math.Max(16, _starts.Length) : checked(journal.Length * 2);
        var grown = ArrayPool<Utf8CaptureMutation>.Shared.Rent(requestedLength);
        if (journal is not null)
        {
            journal.AsSpan(0, _journalCount).CopyTo(grown);
            ArrayPool<Utf8CaptureMutation>.Shared.Return(journal);
        }

        _journal = grown;
    }
}

internal readonly record struct Utf8CaptureCheckpoint(int JournalCount);

internal readonly record struct Utf8CaptureMutation(int Slot, int PreviousStart, int PreviousLength);
