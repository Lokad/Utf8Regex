namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8CaptureSlots
{
    private readonly int[] _starts;
    private readonly int[] _lengths;

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
        Array.Fill(_starts, -1);
        Array.Fill(_lengths, 0);
    }

    public void Set(int slot, int start, int length)
    {
        if ((uint)slot >= (uint)_starts.Length)
        {
            return;
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

    public Snapshot CaptureSnapshot()
    {
        return new Snapshot((int[])_starts.Clone(), (int[])_lengths.Clone());
    }

    public Utf8CaptureSlots Clone()
    {
        var clone = new Utf8CaptureSlots(_starts.Length);
        Array.Copy(_starts, clone._starts, _starts.Length);
        Array.Copy(_lengths, clone._lengths, _lengths.Length);
        return clone;
    }

    public void Restore(in Snapshot snapshot)
    {
        snapshot.Starts.CopyTo(_starts, 0);
        snapshot.Lengths.CopyTo(_lengths, 0);
    }

    internal readonly record struct Snapshot(int[] Starts, int[] Lengths);
}
