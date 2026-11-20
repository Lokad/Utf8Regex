using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Planning;

internal readonly struct Utf8PrefilterPlan
{
    public Utf8PrefilterPlan(
        PreparedSearcher primarySearcher,
        PreparedSearcher secondarySearcher,
        Utf8StructuralSearchPlan[]? windowPlans)
    {
        _primarySearcher = primarySearcher;
        SecondarySearcher = secondarySearcher;
        WindowPlans = windowPlans;
    }

    private readonly PreparedSearcher _primarySearcher;

    [UnscopedRef]
    public ref readonly PreparedSearcher PrimarySearcher => ref _primarySearcher;

    public PreparedSearcher SecondarySearcher { get; }

    public Utf8StructuralSearchPlan[]? WindowPlans { get; }

    public bool HasValue =>
        PrimarySearcher.HasValue ||
        SecondarySearcher.HasValue ||
        WindowPlans is { Length: > 0 };

}
