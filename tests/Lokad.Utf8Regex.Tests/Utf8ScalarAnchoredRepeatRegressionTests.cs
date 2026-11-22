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

    [Theory]
    [InlineData("$", "100", true)]
    [InlineData("$", "100-😀\n", true)]
    [InlineData("$", "100-\n\n", false)]
    [InlineData("$", "100:\n", false)]
    [InlineData(@"\z", "100-😀", true)]
    [InlineData(@"\z", "100-😀\n", false)]
    public void ShortAnchoredRepeatProvesRangeWithUnobservedCaptures(string end, string text, bool expected)
    {
        var pattern = $@"^([0-9]+)(\-| |{end})(.*){end}";
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 3, MaxWorkingBytes = 1, MaxDepth = 1 } });
        using var token = new CancellationTokenSource();
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(bytes, token.Token));

        using var ranges = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 4, MaxWorkingBytes = 1, MaxDepth = 1 } });
        var actual = ranges.Match(bytes, cancellationToken: token.Token);
        Assert.Equal(expected, actual.Success);
        if (expected)
        {
            var oracle = Regex.Match(text, pattern);
            Utf8MatchSequence.AssertRange(actual, text, oracle.Index, oracle.Length);
        }
    }
}
