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
            var result = PreparedByteSearch.FromOwnedValues(owned);
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
            var result = PreparedByteSearch.FromOwnedValues(charClass.GetPositiveMatchBytes());
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
