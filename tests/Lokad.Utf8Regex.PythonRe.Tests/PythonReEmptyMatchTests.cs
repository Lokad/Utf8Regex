using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Empty-match progression in enumeration: FindAll and FindIter visit every
// scalar boundary exactly once, retaining original byte offsets.
[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReEmptyMatchTests
{
    public static IEnumerable<object?[]> EmptyRows()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "a*", none, "bbb", 4, "0,1,2,3" };
        yield return new object?[] { "a*", none, "", 1, "0" };
        yield return new object?[] { "x*", none, "é", 2, "0,2" };
        yield return new object?[] { "x*", none, "axb", 4, "0,1,2,3" };
        yield return new object?[] { "", none, "ab", 3, "0,1,2" };
    }

    [Theory]
    [MemberData(nameof(EmptyRows))]
    public void FindAllVisitsEveryBoundaryOnce(string pattern, PythonReCompileOptions options, string inputText, int expected, string expectedStarts)
    {
        var wholeMatches5 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor5 = new Utf8PythonRegex(pattern, options).CreateMatchCursor(Encoding.UTF8.GetBytes(inputText), 0, default))
        {
            while (rangeCursor5.MoveNext(out var rangeMatch5))
            {
                wholeMatches5.Add(rangeMatch5);
            }
        }
        var matches = wholeMatches5.ToArray();
        Assert.Equal(expected, matches.Length);
        Assert.Equal(expectedStarts, string.Join(",", matches.Select(item => item.Value.StartOffsetInBytes)));
    }

    [Theory]
    [MemberData(nameof(EmptyRows))]
    public void FindIterVisitsEveryBoundaryOnce(string pattern, PythonReCompileOptions options, string inputText, int expected, string expectedStarts)
    {
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = new Utf8PythonRegex(pattern, options).CreateMatchCursor(Encoding.UTF8.GetBytes(inputText), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var detailed = collectedRanges1.ToArray();
        var starts = detailed.Select(static item => item.Value.StartOffsetInBytes).ToList();

        Assert.Equal(expected, starts.Count);
        Assert.Equal(expectedStarts, string.Join(",", starts));
    }
}
