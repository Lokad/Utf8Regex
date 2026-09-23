using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReCountOwnershipTests
{
    public static IEnumerable<object?[]> OwnedCountCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "cat|horse", none, "cat horse cat", 0, 3 };
        yield return new object?[] { "cat|horse", none, "cat horse cat", 4, 2 };
        yield return new object?[] { "cat|horse", none, "cat horse cat", 10, 1 };
        yield return new object?[] { "(?:cat|horse)", none, "cat horse cat", 4, 2 };
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, 2 };
        yield return new object?[] { "(cat)", none, "cat horse cat", 4, 1 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, 2 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 4, 1 };
        yield return new object?[] { "(?:CAT|DOG)", ignoreCase, "cat dog", 0, 2 };
        yield return new object?[] { "(?:CAT|DOG)", ignoreCase, "cat dog", 4, 1 };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 0, 2 };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 9, 1 };
        yield return new object?[] { "needle|needle-long", none, "needle-long needle", 0, 2 };
        yield return new object?[] { "needle|needle-long", none, "needle-long needle", 6, 1 };
        yield return new object?[] { "cat|horse", none, "no match", 0, 0 };
        yield return new object?[] { "cat|horse", none, "", 0, 0 };
    }

    [Theory]
    [MemberData(nameof(OwnedCountCases))]
    public void OwnedCountMatchesFindAll(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
        Assert.Equal(regex.FindAll(input, startOffsetInBytes).Length, regex.Count(input, startOffsetInBytes));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ManagedCountCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(?P<word>foo)-(?P=word)", none, "xx foo-foo yy foo-foo", 3, 2 };
        yield return new object?[] { "\\b|\\w+", none, "\u00E9 skip y", 8, 3 };
    }

    [Theory]
    [MemberData(nameof(ManagedCountCases))]
    public void ManagedCountMatchesExpected(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
        Assert.True(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}
