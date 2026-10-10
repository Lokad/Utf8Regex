using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

// Projection factories share the enclosing construction authority. Scratch
// vectors/text release on every exit; completed arrays and map descriptors keep
// their loans. The scope, rather than this session, owns speculative rollback.
internal sealed class Utf8CaptureMapConstruction(
    Utf8ConstructionBudget budget,
    Utf8ConstructionArena arrays,
    Utf8ConstructionBuffer<IDisposable> scratch) : IDisposable
{
    internal Utf8ConstructionBudget Budget => budget;

    internal static Utf8CaptureMapConstruction Create(Utf8ConstructionBudget owner)
    {
        owner.EnterDepth();
        Utf8ConstructionArena? arrays = null;
        Utf8ConstructionBuffer<IDisposable>? scratch = null;
        try
        {
            arrays = Utf8ConstructionArena.Create(owner);
            scratch = Utf8ConstructionBuffer<IDisposable>.Create(owner);
            return owner.AllocateObject((owner, arrays, scratch), static state =>
                new Utf8CaptureMapConstruction(state.owner, state.arrays, state.scratch));
        }
        catch
        {
            scratch?.Dispose();
            arrays?.Dispose();
            owner.LeaveDepth();
            throw;
        }
    }

    internal Utf8ConstructionBuffer<T> Buffer<T>() => Track(Utf8ConstructionBuffer<T>.Create(budget));
    internal Utf8ConstructionText Text() => Track(Utf8ConstructionText.Create());

    private T Track<T>(T value) where T : IDisposable
    {
        try { scratch.Add(value); return value; }
        catch { value.Dispose(); throw; }
    }

    internal T[] Allocate<T>(int count, bool retained = true) => arrays.Allocate<T>(count, retained);

    internal T[] Copy<T>(Utf8ConstructionBuffer<T> values)
    {
        var result = Allocate<T>(values.Count);
        budget.ChargeWork((ulong)values.Count);
        values.Span.CopyTo(result);
        return result;
    }

    internal string Concat(string left, string right)
    {
        if (left.Length == 0) return right;
        if (right.Length == 0) return left;
        using var text = Utf8ConstructionText.Create();
        return text.Append(left).Append(right).ToText();
    }

    internal void Complete() => arrays.Complete();

    public void Dispose()
    {
        // Cleanup must not allocate, charge work, or observe cancellation.
        for (var index = 0; index < scratch.Count; index++) scratch[index].Dispose();
        scratch.Dispose();
        arrays.Dispose();
        budget.LeaveDepth();
    }
}
