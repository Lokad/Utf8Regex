using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Self-overlapping literal prefixes: a partial-prefix mismatch must resume at
// the overlap position, never skip a match or report a false one. These rows
// pin the behavior a future KMP overlap-table port (see the R08 cpython
// disposition in PLAN.md: _sre overlap tables) must preserve.
[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonRePrefixOverlapTests
{
    public static IEnumerable<object?[]> SearchCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "abcabd", none, "abcabcabd", 3 };
        yield return new object?[] { "abcabd", none, "abcabcab", -1 };
        yield return new object?[] { "aaab", none, "aaaaaab", 3 };
        yield return new object?[] { "aaab", none, "aaaaaa", -1 };
        yield return new object?[] { "abab", none, "xxabab", 2 };
        yield return new object?[] { "aabaa", none, "xaabaabaa", 1 };
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public void OverlapPrefixSearchFindsCorrectStart(string pattern, PythonReCompileOptions options, string inputText, int expectedStart)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var match = regex.Search(Encoding.UTF8.GetBytes(inputText));
        Assert.Equal(expectedStart >= 0, match.Success);
        if (expectedStart >= 0)
        {
            Assert.Equal(expectedStart, match.StartOffsetInUtf16);
        }
    }

    public static IEnumerable<object?[]> FindAllCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "abcabd", none, "abcabcabd", new[] { "abcabd" } };
        yield return new object?[] { "aaab", none, "aaaaaab", new[] { "aaab" } };
        yield return new object?[] { "abab", none, "ababab", new[] { "abab" } };
        yield return new object?[] { "abab", none, "abababab", new[] { "abab", "abab" } };
        yield return new object?[] { "aabaa", none, "xaabaabaa", new[] { "aabaa" } };
        yield return new object?[] { "abcabd", none, "abcabcab", new string[0] };
    }

    [Theory]
    [MemberData(nameof(FindAllCases))]
    public void OverlapPrefixFindAllListsExactMatches(string pattern, PythonReCompileOptions options, string inputText, string[] values)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var matches = regex.FindAll(Encoding.UTF8.GetBytes(inputText));
        Assert.Equal(values, matches.Select(static match => match.ValueText).ToArray());
    }
}
