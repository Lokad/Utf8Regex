using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

internal sealed class PythonReParserStorage(
    Utf8ConstructionArena arena,
    Utf8ConstructionMap<string, int> namedGroups,
    Utf8ConstructionBuffer<PythonReNode?> groupBodies) : IDisposable
{
    internal Utf8ConstructionArena Arena { get; } = arena;
    internal Utf8ConstructionMap<string, int> NamedGroups { get; } = namedGroups;
    internal Utf8ConstructionBuffer<PythonReNode?> GroupBodies { get; } = groupBodies;
    // Slots are reserved in opening order; null marks a still-open capture.
    internal bool IsOpen(int number) => number > 0 && number <= GroupBodies.Count && GroupBodies[number - 1] is null;

    internal static PythonReParserStorage Create(Utf8ConstructionBudget budget)
    {
        var ownedArena = Utf8ConstructionArena.Create(budget);
        var names = Utf8ConstructionMap<string, int>.Create(budget, StringComparer.Ordinal);
        var bodies = Utf8ConstructionBuffer<PythonReNode?>.Create(budget);
        return budget.AllocateObject((ownedArena, names, bodies), static state =>
            new PythonReParserStorage(state.ownedArena, state.names, state.bodies));
    }

    public void Dispose()
    {
        GroupBodies.Dispose();
        NamedGroups.Dispose();
        Arena.Dispose();
    }
}
