using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.PythonRe;

internal readonly struct PythonReReplacementRetainedStorage : IUtf8RetainedStorage<object, PythonReReplacementPlan>
{
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
    public ulong GetKeyCharge(object key) => key is byte[] bytes
        ? Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length)
        : Utf8ConstructionBudget.StringBytes(((string)key).Length);
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
            control.Check(); budget.ChargeWork(1);
            if (bytes > maximum - total) return false;
            total += bytes; return true;
        }
    }
}

// One bounded cache, with the original text keys for text conveniences and owned
// bytes for native templates. Representation sharing earns no admission discount.
internal sealed class PythonReReplacementKeyComparer : IEqualityComparer<object>
{
    internal static readonly PythonReReplacementKeyComparer Instance = new();
    public new bool Equals(object? left, object? right) => ReferenceEquals(left, right) ||
        (left is string text && right is string other && StringComparer.Ordinal.Equals(text, other)) ||
        (left is byte[] bytes && right is byte[] otherBytes && bytes.AsSpan().SequenceEqual(otherBytes));

    public int GetHashCode(object key)
    {
        if (key is string text) return StringComparer.Ordinal.GetHashCode(text);
        var hash = new HashCode();
        hash.AddBytes((byte[])key);
        return hash.ToHashCode();
    }
}
