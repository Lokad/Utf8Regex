using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

internal sealed class PythonReParserStorage(
    Utf8ConstructionArena arena,
    Utf8ConstructionMap<string, int> namedGroups,
    Utf8ConstructionMap<int, PythonReNode> groupBodies,
    Utf8ConstructionBuffer<bool> openGroups) : IDisposable
{
    internal Utf8ConstructionArena Arena { get; } = arena;
    internal Utf8ConstructionMap<string, int> NamedGroups { get; } = namedGroups;
    internal Utf8ConstructionMap<int, PythonReNode> GroupBodies { get; } = groupBodies;
    internal Utf8ConstructionBuffer<bool> OpenGroups { get; } = openGroups;
    internal bool IsOpen(int number) => number > 0 && number <= OpenGroups.Count && OpenGroups[number - 1];

    internal static PythonReParserStorage Create(Utf8ConstructionBudget budget)
    {
        var ownedArena = Utf8ConstructionArena.Create(budget);
        var names = Utf8ConstructionMap<string, int>.Create(budget, StringComparer.Ordinal);
        var bodies = Utf8ConstructionMap<int, PythonReNode>.Create(budget, EqualityComparer<int>.Default);
        var open = Utf8ConstructionBuffer<bool>.Create(budget);
        return budget.AllocateObject((ownedArena, names, bodies, open), static state =>
            new PythonReParserStorage(state.ownedArena, state.names, state.bodies, state.open));
    }

    public void Dispose()
    {
        OpenGroups.Dispose();
        GroupBodies.Dispose();
        NamedGroups.Dispose();
        Arena.Dispose();
    }
}
