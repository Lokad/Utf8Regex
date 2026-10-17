using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Empty-match progression in enumeration: FindAll and FindIter visit every
// scalar boundary exactly once. UTF-16 starts below are derived coordinates,
// including for non-ASCII input.
[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReEmptyMatchTests
{
    public static IEnumerable<object?[]> EmptyRows()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "a*", none, "bbb", 4, "0,1,2,3" };
        yield return new object?[] { "a*", none, "", 1, "0" };
        yield return new object?[] { "x*", none, "é", 2, "0,1" };
        yield return new object?[] { "x*", none, "axb", 4, "0,1,2,3" };
        yield return new object?[] { "", none, "ab", 3, "0,1,2" };
    }

    [Theory]
    [MemberData(nameof(EmptyRows))]
    public void FindAllVisitsEveryBoundaryOnce(string pattern, PythonReCompileOptions options, string inputText, int expected, string expectedStarts)
    {
        var matches = new Utf8PythonRegex(pattern, options).FindAll(Encoding.UTF8.GetBytes(inputText));
        Assert.Equal(expected, matches.Length);
        Assert.Equal(expectedStarts, string.Join(",", matches.Select(static match => match.StartOffsetInUtf16)));
    }

    [Theory]
    [MemberData(nameof(EmptyRows))]
    public void FindIterVisitsEveryBoundaryOnce(string pattern, PythonReCompileOptions options, string inputText, int expected, string expectedStarts)
    {
        var detailed = new Utf8PythonRegex(pattern, options).FindIterDetailed(Encoding.UTF8.GetBytes(inputText));
        var starts = detailed.Select(static item => item.Value.StartOffsetInUtf16).ToList();

        Assert.Equal(expected, starts.Count);
        Assert.Equal(expectedStarts, string.Join(",", starts));
    }
}
