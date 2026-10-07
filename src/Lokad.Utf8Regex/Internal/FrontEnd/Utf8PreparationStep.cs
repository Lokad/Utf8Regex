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
        Push(frames, body, budget);
        try
        {
            while (frames.Count != 0)
            {
                budget.ChargeWork(1);
                var frame = frames.Peek();
                if (!frame.MoveNext())
                {
                    try { frames.Pop().Dispose(); }
                    finally { budget.LeaveDepth(); }
                }
                else if (frame.Current.Failed) return false;
                else Push(frames, frame.Current.Children, budget);
            }
            return true;
        }
        finally
        {
            while (frames.Count != 0)
            {
                try { frames.Pop().Dispose(); }
                finally { budget.LeaveDepth(); }
            }
        }
    }

    private static void Push(Utf8ConstructionBuffer<IEnumerator<Utf8PreparationStep>> frames,
        IEnumerable<Utf8PreparationStep> body, Utf8ConstructionBudget budget)
    {
        var iterator = body.GetEnumerator();
        try
        {
            budget.EnterDepth();
            try { frames.Push(iterator); }
            catch { budget.LeaveDepth(); throw; }
        }
        catch { iterator.Dispose(); throw; }
    }
}
