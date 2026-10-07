namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Construction-time class queries. Plain wires stay borrowed; chained
/// subtraction acquires complete admitted segments once and applies their
/// ordinary membership from inner to outer. Query work precedes each scan.</summary>
internal readonly struct Utf8ConstructionClass(
    Utf8ConstructionBudget budget, string wire, Utf8ConstructionBuffer<string>? segments) : IDisposable
{
    internal static Utf8ConstructionClass Create(Utf8ConstructionBudget owner, string set)
    {
        owner.ChargeWork((ulong)set.Length);
        if (!Utf8OwnedCharClass.IsSubtraction(set)) return new(owner, set, null);
        var parts = Utf8ConstructionBuffer<string>.Create(owner);
        try
        {
            for (var position = 0; position < set.Length;)
            {
                owner.ChargeWork(1);
                var length = 3 + set[position + 1] + set[position + 2];
                parts.Add(owner.CopyText(set.AsSpan(position, length))); position += length;
            }
            return new(owner, set, parts);
        }
        catch { parts.Dispose(); throw; }
    }

    internal bool Contains(char value)
    {
        if (segments is null)
        {
            budget.ChargeWork((ulong)wire.Length + 1);
            return Utf8OwnedCharClass.CharInClass(value, wire);
        }
        var matched = false;
        for (var index = segments.Count - 1; index >= 0; index--)
        {
            var segment = segments[index]; budget.ChargeWork((ulong)segment.Length + 1);
            matched = Utf8OwnedCharClass.CharInClass(value, segment) && !matched;
        }
        return matched;
    }

    public void Dispose() => segments?.Dispose();
}
