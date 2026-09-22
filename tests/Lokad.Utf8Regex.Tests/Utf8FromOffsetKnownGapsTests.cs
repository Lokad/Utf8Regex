using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 gaps in shared from-offset enumeration: suffix slicing loses absolute-anchor
/// and left-context semantics. Each test asserts BCL-correct values and stays skipped
/// until the shared path is fixed. MatchFromUtf16Offset keeps BCL semantics on these
/// shapes meanwhile, and the R03 differential suite covers the shapes that agree.
/// </summary>
public sealed class Utf8FromOffsetKnownGapsTests
{
    [Fact(Skip = "R03 gap: absolute-start anchors are treated as satisfiable at the search start.")]
    public void AnchoredEnumerationFromOffsetMatchesRuntime()
    {
        const string text = "abcabc";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("^abc", RegexOptions.CultureInvariant);
        var runtime = new Regex("^abc", RegexOptions.CultureInvariant);

        foreach (var offset in new[] { 1, 3 })
        {
            var expected = runtime.Match(text, offset);
            Utf8ValueMatch? first = null;
            foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, offset))
            {
                first = match;
                break;
            }

            Assert.Equal(expected.Success, first?.Success ?? false);
        }
    }

    [Fact(Skip = "R03 gap: word boundaries lose the cut-off left context in suffix slicing.")]
    public void WordBoundaryEnumerationFromInsideWordMatchesRuntime()
    {
        const string text = "aword here";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("\\bword\\b", RegexOptions.CultureInvariant);
        var runtime = new Regex("\\bword\\b", RegexOptions.CultureInvariant);

        var expected = runtime.Match(text, 1);
        Utf8ValueMatch? first = null;
        foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, 1))
        {
            first = match;
            break;
        }

        Assert.Equal(expected.Success, first?.Success ?? false);
    }

    [Fact(Skip = "R03 gap: lookbehind loses the cut-off left context in suffix slicing.")]
    public void LookbehindEnumerationFromOffsetMatchesRuntime()
    {
        const string text = "zab";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("(?<=a)b", RegexOptions.CultureInvariant);
        var runtime = new Regex("(?<=a)b", RegexOptions.CultureInvariant);

        var expected = runtime.Match(text, 2);
        Utf8ValueMatch? first = null;
        foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, 2))
        {
            first = match;
            break;
        }

        Assert.True(expected.Success);
        Assert.True(first?.Success ?? false);
        Assert.Equal(expected.Index, first?.IndexInUtf16 ?? -1);
    }
}
