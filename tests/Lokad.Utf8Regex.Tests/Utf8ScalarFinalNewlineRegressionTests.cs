using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarFinalNewlineRegressionTests
{
    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { @"(?m)\Aab[0-9]{2}\z", @"(?m)\Aab[0-9]{2}\Z", @"\Aab[0-9]{260}x\z", @"^ab(?:cd|ef){1,3}$", @"\Gab[0-9]{2}" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicConformanceKeepsCompiledRtlOversizedAndFinalNewlineInputs(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "ab12", "ab12\n", "ab12\nrest", "\nab12", "xab12", "abcd", "abcdefef\n", "\U0001f600ab12",
             "ab" + new string('1', 260) + "x", "ab" + new string('1', 260) + "x\n"], options);
}
