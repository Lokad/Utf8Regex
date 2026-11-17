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

    public static IEnumerable<object[]> EntryCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var mixed in new[] { false, true })
        foreach (var split in new[] { false, true }) yield return [pattern, compiled, mixed, split];
    }

    [Theory]
    [MemberData(nameof(EntryCases))]
    public void InitialMaterializationObservesTheExactTokenBeforeReturning(string pattern, bool compiled, bool mixed, bool split)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0), TimeSpan.FromSeconds(10));
        var input = Encoding.UTF8.GetBytes(new string(mixed ? 'é' : 'x', 200000));
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            var canceled = false;
            try
            {
                if (split) _ = regex.EnumerateSplits(input, source.Token);
                else _ = regex.EnumerateMatches(input, source.Token);
            }
            catch (OperationCanceledException exception)
            {
                Assert.Equal(source.Token, exception.CancellationToken);
                canceled = true;
            }
            Assert.True(canceled);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
        finally { session.Complete(); }
    }

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
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, options),
            ["", "abab aaab aabc", "é😀 abab 中aaab aabc 😀", "abab\naaab\naabc"]);
    }
}
