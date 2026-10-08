using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8PreparedByteSearchFactory
{
    internal static PreparedByteSearch Create(byte[] values)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var owned = budget.AllocateArray<byte>(values.Length, false);
            budget.ChargeWork((ulong)values.Length);
            values.CopyTo(owned, 0);
            var result = PreparedByteSearch.FromOwnedValues(owned, owned.Length > 3 ? CreateSet(owned) : null);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    internal static PreparedByteSearch CreateFromPositiveClass(AsciiCharClass charClass)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            // The class producer owns this exact admitted array. Arbitrary
            // caller arrays use Create's independent copy instead.
            var values = charClass.GetPositiveMatchBytes();
            var result = PreparedByteSearch.FromOwnedValues(values, values.Length > 3 ? CreateSet(charClass) : null);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    internal static PreparedByteSet CreateSet(byte[] values)
    {
        if (values.Length == 0) return PreparedByteSet.Empty;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget; budget.EnterDepth();
        try
        {
            budget.ChargeWork((ulong)values.Length + 256);
            var state = PreparedByteSetState.FromValues(values);
            var result = budget.AllocateObject(state, static value => new PreparedByteSet(value));
            scope.Complete(); return result;
        }
        finally { budget.LeaveDepth(); }
    }

    internal static PreparedByteSet CreateSet(AsciiCharClass charClass)
    {
        if (charClass.Count == 0) return PreparedByteSet.Empty;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget; budget.EnterDepth();
        try
        {
            budget.ChargeWork(256);
            var state = PreparedByteSetState.FromMasks(charClass.ActualLowMask, charClass.ActualHighMask, 0, 0);
            var result = budget.AllocateObject(state, static value => new PreparedByteSet(value));
            scope.Complete(); return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
