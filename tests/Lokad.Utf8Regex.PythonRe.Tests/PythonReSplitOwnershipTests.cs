using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReSplitOwnershipTests
{
    public static IEnumerable<object?[]> OwnedCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { ":", none, ":a:b::c", 0, 0, new string?[] { "", "a", "b", "", "c" } };
        yield return new object?[] { ":", none, ":a:b::c", 2, 0, new string?[] { "", "a", "b::c" } };
        yield return new object?[] { ":", none, ":a:b::c", 1, 0, new string?[] { "", "a:b::c" } };
        yield return new object?[] { "cat|horse", none, "cat horse fish", 0, 0, new string?[] { "", " ", " fish" } };
        yield return new object?[] { "cat|horse", none, "cathorse", 0, 0, new string?[] { "", "", "" } };
        yield return new object?[] { "cat|horse", none, "no match", 0, 0, new string?[] { "no match" } };
        yield return new object?[] { "cat|horse", none, "", 0, 0, new string?[] { "" } };
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, 0, new string?[] { "", "cat", " horse ", "cat", "" } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, 0, new string?[] { "", "cat", null, " ", null, "horse", "" } };
        yield return new object?[] { "(cat)|(horse)", none, "horsecat", 0, 0, new string?[] { "", null, "horse", "", "cat", null, "" } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse fish horse", 2, 0, new string?[] { "", "cat", null, " ", null, "horse", " fish horse" } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 1, 0, new string?[] { "", "cat", null, " horse" } };
        yield return new object?[] { "(?P<w>cat)|(?P<v>horse)", none, "cat horse", 0, 0, new string?[] { "", "cat", null, " ", null, "horse", "" } };
        yield return new object?[] { "(cat)|horse", none, "horsecat", 0, 0, new string?[] { "", null, "", "cat", "" } };
        yield return new object?[] { "(a)|(ab)", none, "xab", 0, 0, new string?[] { "x", "a", null, "b" } };
        yield return new object?[] { "(ab)|(a)", none, "xab", 0, 0, new string?[] { "x", "ab", null, "" } };
        yield return new object?[] { "((cat)|(horse))", none, "cat horse", 0, 0, new string?[] { "", "cat", "cat", null, " ", "horse", null, "horse", "" } };
        yield return new object?[] { "(?:cat|horse)", none, "cat horse", 0, 0, new string?[] { "", " ", "" } };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "cat dog", 0, 0, new string?[] { "", "cat", null, " ", null, "dog", "" } };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "CAT DOG", 0, 0, new string?[] { "", "CAT", null, " ", null, "DOG", "" } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, 4, new string?[] { "", null, "horse", "" } };
        yield return new object?[] { "cat|horse", none, "cat horse", 0, 4, new string?[] { "", "" } };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 0, 0, new string?[] { "\u03B1", "caf\u00E9", null, "\u03B2", null, "ni\u00F1o", "\u03B3" } };
    }

    [Theory]
    [MemberData(nameof(OwnedCases))]
    public void OwnedSplitMatchesCpython(string pattern, PythonReCompileOptions options, string inputText, int maxSplit, int startOffsetInBytes, string?[] expected)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var actual = regex.SplitToStrings(input, maxSplit, startOffsetInBytes);
        Assert.Equal(expected, actual);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [MemberData(nameof(OwnedCases))]
    public void OwnedDetailedSplitMatchesCpython(string pattern, PythonReCompileOptions options, string inputText, int maxSplit, int startOffsetInBytes, string?[] expected)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var items = regex.SplitDetailed(input, maxSplit, startOffsetInBytes);
        Assert.Equal(expected, items.Select(static item => item.ValueText).ToArray());
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedSplitReportsCaptures()
    {
        var regex = new Utf8PythonRegex("(cat)|(horse)");
        var items = regex.SplitDetailed(Encoding.UTF8.GetBytes("cat horse"));
        var expected = new (string? Value, bool IsCapture, int Group)[]
        {
            ("", false, 0),
            ("cat", true, 1),
            (null, true, 2),
            (" ", false, 0),
            (null, true, 1),
            ("horse", true, 2),
            ("", false, 0),
        };
        Assert.Equal(expected.Length, items.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Value, items[i].ValueText);
            Assert.Equal(expected[i].IsCapture, items[i].IsCapture);
            Assert.Equal(expected[i].Group, items[i].CaptureGroupNumber);
        }

        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }

    public static IEnumerable<object?[]> ManagedCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(b)|(:+)", none, ":a:b::c", 0, 0, new string?[] { "", null, ":", "a", null, ":", "", "b", null, "", null, "::", "c" } };
        yield return new object?[] { "(:)+", none, ":a:b::c", 0, 0, new string?[] { "", ":", "a", ":", "b", ":", "c" } };
        yield return new object?[] { "(a)+", none, "aaabaaa", 0, 0, new string?[] { "", "a", "b", "a", "" } };
        yield return new object?[] { "a*", none, "ba", 0, 0, new string?[] { "", "b", "", "" } };
        yield return new object?[] { "(?:b)|(?::+)", none, ":a:b::c", 0, 0, new string?[] { "", "a", "", "", "c" } };
    }

    [Theory]
    [MemberData(nameof(ManagedCases))]
    public void FormerManagedSplitStaysOwned(string pattern, PythonReCompileOptions options, string inputText, int maxSplit, int startOffsetInBytes, string?[] expected)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var actual = regex.SplitToStrings(input, maxSplit, startOffsetInBytes);
        Assert.Equal(expected, actual);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}
