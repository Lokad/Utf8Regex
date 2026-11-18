using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MappedEntryInvocationTests
{

    private static readonly string[] Patterns = [@"(ab)\1", @"(?<=a+)b", @"(?<x>a)+\k<x>",
        @"(?:(?<x>a)|b)+", @"(a|b)*c", @"(?i:a+)"];

    public static IEnumerable<object[]> SemanticCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, compiled, rtl];
    }

    [Theory]
    [MemberData(nameof(SemanticCases))]
    public void CompleteOperationsPreserveUnitsCapturesAnchorsAndSplitTail(string pattern, bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(10)
        });
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, options),
            ["", "abab aaab aabc", "é😀 abab 中aaab aabc 😀", "abab\naaab\naabc"]);
    }
}
