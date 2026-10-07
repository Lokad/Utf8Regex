namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Preparation walkers suspend child work on this explicit stack. A declined
// child aborts the speculative plan without resuming its parent. In particular,
// scratch capture projections use the same stack as their containing branch.
internal readonly record struct Utf8PreparationStep(IEnumerable<Utf8PreparationStep> Children, bool Failed)
{
    internal static Utf8PreparationStep Decline => new(Array.Empty<Utf8PreparationStep>(), true);

    internal static bool TryRun(IEnumerable<Utf8PreparationStep> body)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var completed = TryRun(body, construction.Budget);
        if (completed) construction.Complete();
        return completed;
    }

    internal static bool TryRun(IEnumerable<Utf8PreparationStep> body, Utf8ConstructionBudget budget)
    {
        using var frames = new Utf8ConstructionBuffer<IEnumerator<Utf8PreparationStep>>(budget);
        Push(frames, body);
        try
        {
            while (frames.Count != 0)
            {
                var frame = frames.Peek();
                if (!frame.MoveNext()) frames.Pop().Dispose();
                else if (frame.Current.Failed) return false;
                else Push(frames, frame.Current.Children);
            }
            return true;
        }
        finally
        {
            while (frames.Count != 0) frames.Pop().Dispose();
        }
    }

    private static void Push(Utf8ConstructionBuffer<IEnumerator<Utf8PreparationStep>> frames,
        IEnumerable<Utf8PreparationStep> body)
    {
        var iterator = body.GetEnumerator();
        try { frames.Push(iterator); }
        catch { iterator.Dispose(); throw; }
    }
}
