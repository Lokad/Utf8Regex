using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReEvaluatorOwnershipTests
{
    public static IEnumerable<object?[]> OwnedCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "cat|horse", none, "cat horse fish", 0, 0, "[cat] [horse] fish", 2 };
        yield return new object?[] { "cat|horse", none, "cathorse", 0, 0, "[cat][horse]", 2 };
        yield return new object?[] { "cat|horse", none, "no match", 0, 0, "no match", 0 };
        yield return new object?[] { "cat|horse", none, "", 0, 0, "", 0 };
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, 0, "[cat] horse [cat]", 2 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, 0, "[cat] [horse]", 2 };
        yield return new object?[] { "(cat)|(horse)", none, "horsecat", 0, 0, "[horse][cat]", 2 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse fish horse", 2, 0, "[cat] [horse] fish horse", 2 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 1, 0, "[cat] horse", 1 };
        yield return new object?[] { "(?P<w>cat)|(?P<v>horse)", none, "cat horse", 0, 0, "[cat] [horse]", 2 };
        yield return new object?[] { "(cat)|horse", none, "horsecat", 0, 0, "[horse][cat]", 2 };
        yield return new object?[] { "(a)|(ab)", none, "xab", 0, 0, "x[a]b", 1 };
        yield return new object?[] { "((cat)|(horse))", none, "cat horse", 0, 0, "[cat] [horse]", 2 };
        yield return new object?[] { "(?:cat|horse)", none, "cat horse", 0, 0, "[cat] [horse]", 2 };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "cat dog", 0, 0, "[cat] [dog]", 2 };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2", 0, 0, "\u03B1[caf\u00E9]\u03B2", 1 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, 4, "cat [horse]", 1 };
    }

    [Theory]
    [MemberData(nameof(OwnedCases))]
    public void OwnedStringEvaluatorMatchesManaged(string pattern, PythonReCompileOptions options, string inputText, int count, int startOffsetInBytes, string expectedText, int expectedCount)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var subn = regex.SubnToString(input, 0, static (int state, Utf8PythonDetailedMatchData match) => "[" + match.Value.ValueText + "]", count, startOffsetInBytes);
        Assert.Equal(expectedText, subn.ResultText);
        Assert.Equal(expectedCount, subn.ReplacementCount);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [MemberData(nameof(OwnedCases))]
    public void OwnedBytesEvaluatorMatchesManaged(string pattern, PythonReCompileOptions options, string inputText, int count, int startOffsetInBytes, string expectedText, int expectedCount)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var subn = regex.Subn(input, 0, static (int state, Utf8PythonDetailedMatchData match) => Encoding.UTF8.GetBytes("[" + match.Value.ValueText + "]"), count, startOffsetInBytes);
        Assert.Equal(expectedText, Encoding.UTF8.GetString(subn.ResultBytes));
        Assert.Equal(expectedCount, subn.ReplacementCount);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [MemberData(nameof(OwnedCases))]
    public void OwnedGroupEvaluatorMatchesManaged(string pattern, PythonReCompileOptions options, string inputText, int count, int startOffsetInBytes, string expectedText, int expectedCount)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var subn = regex.SubnToString(
            input,
            0,
            static (int state, Utf8PythonDetailedMatchData match) => "<" + string.Join("|", match.Groups.Select(static group => group.Success ? group.ValueText : "None")) + ">",
            count,
            startOffsetInBytes);
        Assert.Equal(expectedCount, subn.ReplacementCount);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ManagedCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(a)+", none, "aaabaaa", 0, 0 };
        yield return new object?[] { "(b)|(:+)", none, ":a:b::c", 0, 0 };
        yield return new object?[] { "a*", none, "ba", 0, 0 };
        yield return new object?[] { "\\b(cat)|(horse)\\b", none, "cat horse", 0, 0 };
    }

    [Theory]
    [MemberData(nameof(ManagedCases))]
    public void FormerManagedEvaluatorStaysOwned(string pattern, PythonReCompileOptions options, string inputText, int count, int startOffsetInBytes)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var subn = regex.SubnToString(input, 0, static (int state, Utf8PythonDetailedMatchData match) => "[" + match.Value.ValueText + "]", count, startOffsetInBytes);
        Assert.Equal(regex.Count(input), subn.ReplacementCount);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}