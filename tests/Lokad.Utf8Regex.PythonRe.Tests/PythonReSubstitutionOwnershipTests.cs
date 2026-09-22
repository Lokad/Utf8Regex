using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReSubstitutionOwnershipTests
{
    public static IEnumerable<object?[]> OwnedCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "cat|horse", none, "cat horse fish", "X", 0, 0, "X X fish", 2 };
        yield return new object?[] { "cat|horse", none, "cathorse", "X", 0, 0, "XX", 2 };
        yield return new object?[] { "cat|horse", none, "no match", "X", 0, 0, "no match", 0 };
        yield return new object?[] { "cat|horse", none, "", "X", 0, 0, "", 0 };
        yield return new object?[] { "(cat)", none, "cat horse cat", "<\\1>", 0, 0, "<cat> horse <cat>", 2 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", "<\\1|\\2>", 0, 0, "<cat|> <|horse>", 2 };
        yield return new object?[] { "(cat)|(horse)", none, "horsecat", "[\\1][\\2]", 0, 0, "[][horse][cat][]", 2 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse fish horse", "X", 2, 0, "X X fish horse", 2 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", "X", 1, 0, "X horse", 1 };
        yield return new object?[] { "(?P<w>cat)|(?P<v>horse)", none, "cat horse", "<\\g<w>|\\g<v>>", 0, 0, "<cat|> <|horse>", 2 };
        yield return new object?[] { "(cat)|horse", none, "horsecat", "[\\1]", 0, 0, "[][cat]", 2 };
        yield return new object?[] { "(a)|(ab)", none, "xab", "<\\1>", 0, 0, "x<a>b", 1 };
        yield return new object?[] { "(ab)|(a)", none, "xab", "<\\2>", 0, 0, "x<>", 1 };
        yield return new object?[] { "((cat)|(horse))", none, "cat horse", "<\\1:\\2:\\3>", 0, 0, "<cat:cat:> <horse::horse>", 2 };
        yield return new object?[] { "(?:cat|horse)", none, "cat horse", "X", 0, 0, "X X", 2 };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "cat dog", "X", 0, 0, "X X", 2 };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2", "<\\1>", 0, 0, "\u03B1<caf\u00E9>\u03B2", 1 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", "X", 0, 4, "cat X", 1 };
        yield return new object?[] { "(cat|horse)", none, "cat horse", "<\\1>", 0, 0, "<cat> <horse>", 2 };
        yield return new object?[] { "(cat|horse)", none, "no match", "<\\1>", 0, 0, "no match", 0 };
    }

    [Theory]
    [MemberData(nameof(OwnedCases))]
    public void OwnedSubstitutionMatchesCpython(string pattern, PythonReCompileOptions options, string inputText, string replacement, int count, int startOffsetInBytes, string expectedText, int expectedCount)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var subn = regex.SubnToString(input, replacement, count, startOffsetInBytes);
        Assert.Equal(expectedText, subn.ResultText);
        Assert.Equal(expectedCount, subn.ReplacementCount);
        var bytes = regex.Subn(input, replacement, count, startOffsetInBytes);
        Assert.Equal(expectedText, Encoding.UTF8.GetString(bytes.ResultBytes));
        Assert.Equal(expectedCount, bytes.ReplacementCount);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ManagedCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(b)|(:+)", none, ":a:b::c", "<\\1|\\2>", 0, 0, "<|:>a<|:><b|><|::>c", 4 };
        yield return new object?[] { "(a)+", none, "aaabaaa", "<\\1>", 0, 0, "<a>b<a>", 2 };
        yield return new object?[] { "a*", none, "ba", "X", 0, 0, "XbXX", 3 };
        yield return new object?[] { "\\b(cat)|(horse)\\b", none, "cat horse", "X", 0, 0, "X X", 2 };
    }

    [Theory]
    [MemberData(nameof(ManagedCases))]
    public void ManagedSubstitutionMatchesCpython(string pattern, PythonReCompileOptions options, string inputText, string replacement, int count, int startOffsetInBytes, string expectedText, int expectedCount)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var subn = regex.SubnToString(input, replacement, count, startOffsetInBytes);
        Assert.Equal(expectedText, subn.ResultText);
        Assert.Equal(expectedCount, subn.ReplacementCount);
        Assert.True(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}
