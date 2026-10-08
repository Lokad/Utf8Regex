using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8PreparedByteSearchFactory
{
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

    internal static PreparedByteSet CreateSet(AsciiCharClass charClass)
        => PreparedByteSet.CreateOwned(charClass.ActualLowMask, charClass.ActualHighMask, 0, 0);
}
