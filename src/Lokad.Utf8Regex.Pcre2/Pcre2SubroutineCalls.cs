namespace Lokad.Utf8Regex.Pcre2;

// One owner stores both active calls and earlier calls referenced by retries.
// Parents precede their children. A retry retains the prefix it observed,
// so discarding newer entries never requires replaying pushes and pops.
internal ref struct Pcre2SubroutineCalls(int frameLimit, bool trackRents)
{
    // Call entries hold links as well as the return state. Start with a small
    // admitted buffer so a single deterministic call still fits a tight cap.
    private Pcre2InvocationStateStack<Pcre2SubroutineRecord> _entries = new(int.MaxValue, trackRents,
        initialCapacity: Math.Min(8, Math.Max(1, frameLimit)));
    private int _head; // Zero is the root. Other values are one-based entry indices.
    private int _depth;

    private readonly record struct Pcre2SubroutineRecord(Pcre2SubroutineCallFrame Call, int Parent, int Depth);

    internal readonly int Count => _depth;
    internal readonly int RentCount => _entries.RentCount;
    internal readonly Pcre2SubroutineCallFrame Current => _entries[_head - 1].Call;
    internal readonly (int Head, int Extent) Checkpoint => (_head, _entries.Count);

    internal void Push(Pcre2SubroutineCallFrame call, int depthBase, ref Pcre2ResourceBudget budget)
    {
        var depth = checked(_depth + 1);
        budget.ChargeFrame((uint)checked(depthBase + depth));
        if (!_entries.TryPush(new Pcre2SubroutineRecord(call, _head, depth), ref budget)) budget.ThrowWorkingLimit();
        _head = _entries.Count;
        _depth = depth;
    }

    internal void Return(int retainedExtent)
    {
        _head = _entries[_head - 1].Parent;
        _depth--;
        Trim(retainedExtent);
    }

    internal void Trim(int retainedExtent) => _entries.Truncate(Math.Max(retainedExtent, _head));

    internal void Restore((int Head, int Extent) checkpoint)
    {
        _entries.Truncate(checkpoint.Extent);
        _head = checkpoint.Head;
        _depth = _head == 0 ? 0 : _entries[_head - 1].Depth;
    }

    internal readonly bool Contains(int slot, ref Pcre2ResourceBudget budget)
    {
        var visited = 0;
        for (var head = _head; head != 0;)
        {
            if ((++visited & 63) == 0) budget.ChargeWorkspaceStep();
            var entry = _entries[head - 1];
            if (entry.Call.SubroutineSlot == slot) return true;
            head = entry.Parent;
        }
        return false;
    }

    internal void Dispose(ref Pcre2ResourceBudget budget) => _entries.Dispose(ref budget);
}
