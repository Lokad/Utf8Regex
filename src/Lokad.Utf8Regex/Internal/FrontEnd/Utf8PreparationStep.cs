namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Preparation walkers suspend child work on this explicit stack. A declined
// child aborts the speculative plan without resuming its parent. In particular,
// scratch capture projections use the same stack as their containing branch.
internal readonly record struct Utf8PreparationStep(IEnumerable<Utf8PreparationStep> Children, bool Failed)
{
    internal static Utf8PreparationStep Decline => new(Array.Empty<Utf8PreparationStep>(), true);

    internal static bool TryRun(IEnumerable<Utf8PreparationStep> body)
    {
        var frames = new Stack<IEnumerator<Utf8PreparationStep>>();
        frames.Push(body.GetEnumerator());
        try
        {
            while (frames.Count != 0)
            {
                var frame = frames.Peek();
                if (!frame.MoveNext()) frames.Pop().Dispose();
                else if (frame.Current.Failed) return false;
                else frames.Push(frame.Current.Children.GetEnumerator());
            }
            return true;
        }
        finally
        {
            while (frames.Count != 0) frames.Pop().Dispose();
        }
    }
}
