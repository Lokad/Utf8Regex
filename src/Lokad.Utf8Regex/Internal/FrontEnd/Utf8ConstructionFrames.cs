namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Construction-owned value frames. Depth admission precedes descriptor
/// or array acquisition; disposal unwinds live depth without spending more work.
/// Frames borrow referenced payloads, whose consumers retain independent loans.</summary>
internal readonly struct Utf8ConstructionFrames<T>(Utf8ConstructionBuffer<T> frames, Utf8ConstructionBudget budget) : IDisposable
{
    internal static Utf8ConstructionFrames<T> Create(Utf8ConstructionBudget owner, T root)
    {
        var slots = owner.Reserved;
        var bytes = owner.ReservedBytes;
        var depth = owner.Depth;
        owner.EnterDepth();
        Utf8ConstructionBuffer<T>? storage = null;
        try
        {
            storage = Utf8ConstructionBuffer<T>.Create(owner);
            storage.Push(root);
            return new(storage, owner);
        }
        catch
        {
            storage?.Dispose();
            owner.LeaveDepth();
            owner.Rollback(slots, bytes, depth);
            throw;
        }
    }

    internal int Count => frames.Count;
    internal T this[int index] { get => frames[index]; set => frames[index] = value; }

    internal void Push(T child)
    {
        budget.EnterDepth();
        try { frames.Push(child); }
        catch { budget.LeaveDepth(); throw; }
    }

    internal void Pop()
    {
        frames.Pop();
        budget.LeaveDepth();
    }

    public void Dispose()
    {
        while (frames.Count != 0) Pop();
        frames.Dispose();
    }
}
