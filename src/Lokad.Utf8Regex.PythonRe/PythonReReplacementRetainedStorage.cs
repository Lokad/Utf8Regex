using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.PythonRe;

internal readonly struct PythonReReplacementRetainedStorage : IUtf8RetainedStorage<byte[], PythonReReplacementPlan>
{
    public ulong GetKeyCharge(byte[] key) => Utf8ConstructionBudget.ArrayBytes<byte>(key.Length);

    public bool TryGetValueCharge<TControl>(PythonReReplacementPlan value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        ulong total = 0;
        charge = 0;
        if (!Add(Utf8ConstructionBudget.ArrayBytes<PythonReReplacementToken>(value.Tokens.Length)
            + Utf8ConstructionBudget.ArrayBytes<byte>(value.LiteralUtf8?.Length ?? 0))) return false;
        foreach (var token in value.Tokens)
            if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(token.Utf8?.Length ?? 0))) return false;
        charge = total;
        return true;

        bool Add(ulong bytes)
        {
            control.Check(); budget.Checkpoint();
            if (bytes > maximum - total) return false;
            total += bytes; return true;
        }
    }
}

internal sealed class PythonReReplacementKeyComparer : IEqualityComparer<byte[]>
{
    internal static readonly PythonReReplacementKeyComparer Instance = new();

    public bool Equals(byte[]? left, byte[]? right) => ReferenceEquals(left, right) ||
        (left is not null && right is not null && left.AsSpan().SequenceEqual(right));

    public int GetHashCode(byte[] key)
    {
        var hash = new HashCode();
        hash.AddBytes(key);
        return hash.ToHashCode();
    }
}
