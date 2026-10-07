namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>In-place construction sort whose comparisons and swaps are admitted
/// individually. Resource failures propagate directly and require no scratch array.</summary>
internal static class Utf8ConstructionSort
{
    internal static void Sort<T>(Span<T> items, Comparison<T> comparison, Utf8ConstructionBudget budget)
    {
        for (var root = items.Length / 2 - 1; root >= 0; root--)
            SiftDown(items, root, items.Length, comparison, budget);
        for (var end = items.Length - 1; end > 0; end--)
        {
            budget.ChargeWork(1);
            (items[0], items[end]) = (items[end], items[0]);
            SiftDown(items, 0, end, comparison, budget);
        }
    }

    private static void SiftDown<T>(Span<T> items, int root, int count, Comparison<T> comparison, Utf8ConstructionBudget budget)
    {
        while (root < count / 2)
        {
            var child = root * 2 + 1;
            if (child + 1 < count)
            {
                budget.ChargeWork(1);
                if (comparison(items[child], items[child + 1]) < 0) child++;
            }
            budget.ChargeWork(1);
            if (comparison(items[root], items[child]) >= 0) return;
            budget.ChargeWork(1);
            (items[root], items[child]) = (items[child], items[root]);
            root = child;
        }
    }
}
