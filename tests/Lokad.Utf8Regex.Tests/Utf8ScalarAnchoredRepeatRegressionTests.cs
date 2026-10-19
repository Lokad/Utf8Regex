using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarAnchoredRepeatRegressionTests
{
    public static IEnumerable<object[]> AnchoredCases()
    {
        foreach (var pattern in new[] { @"^abx?xy$", @"\Aabx{0,2}xy\z", @"^(?<head>ab)x?xy$", @"\A(?<head>ab)x{0,2}xy\z" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, compiled, rtl];
    }

    [Theory]
    [MemberData(nameof(AnchoredCases))]
    public void AnchoredLoopOverlapRetainsOwnedCompletePublicOperations(string pattern, bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "abxy", "abxxy", "abxxxy", "abxy\n", "abxxy\n", "abxy\r\n", "abxy\nabxxy", "\U0001f600abxy", "abxy\U0001f600"], options);
    }
}
