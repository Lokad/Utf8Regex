using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReFindAllOwnershipTests
{
    public static IEnumerable<object?[]> OwnedScalarCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "cat|horse", none, "cat horse fish", 0, new[] { "cat", "horse" } };
        yield return new object?[] { "cat|horse", none, "cathorse", 0, new[] { "cat", "horse" } };
        yield return new object?[] { "cat|horse", none, "no match", 0, new string[0] };
        yield return new object?[] { "cat|horse", none, "", 0, new string[0] };
        yield return new object?[] { "(?:cat|horse)", none, "cat horse", 0, new[] { "cat", "horse" } };
        yield return new object?[] { "cat|horse", none, "cat horse fish", 4, new[] { "horse" } };
        yield return new object?[] { "needle|needle-long", none, "\u00E9 skip haystack", 8, new string[0] };
        yield return new object?[] { "(?:CAT|DOG)", ignoreCase, "cat dog", 0, new[] { "cat", "dog" } };
    }

    [Theory]
    [MemberData(nameof(OwnedScalarCases))]
    public void OwnedFindAllMatchesManaged(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var matches = regex.FindAll(input, startOffsetInBytes);
        Assert.Equal(values, matches.Select(static match => match.ValueText).ToArray());
        var strings = regex.FindAllToStrings(input, startOffsetInBytes);
        Assert.Equal(values, strings.ScalarValues);
        var utf8 = regex.FindAllToUtf8(input, startOffsetInBytes);
        Assert.Equal(values, utf8.ScalarValues.Select(Encoding.UTF8.GetString).ToArray());
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> OwnedSingleGroupCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, new[] { "cat", "cat" }, new[] { "cat", "cat" } };
        yield return new object?[] { "(cat)|horse", none, "horsecat", 0, new[] { "horse", "cat" }, new string?[] { "", "cat" } };
    }

    [Theory]
    [MemberData(nameof(OwnedSingleGroupCases))]
    public void OwnedSingleGroupFindAllMatchesManaged(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values, string?[] groupValues)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var matches = regex.FindAll(input, startOffsetInBytes);
        Assert.Equal(values, matches.Select(static match => match.ValueText).ToArray());
        var strings = regex.FindAllToStrings(input, startOffsetInBytes);
        Assert.Equal(groupValues, strings.ScalarValues);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> OwnedTupleCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, new[] { new[] { "cat", "" }, new[] { "", "horse" } } };
        yield return new object?[] { "(cat)|(horse)", none, "horsecat", 0, new[] { new[] { "", "horse" }, new[] { "cat", "" } } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 4, new[] { new[] { "", "horse" } } };
        yield return new object?[] { "(?P<w>cat)|(?P<v>horse)", none, "cat horse", 0, new[] { new[] { "cat", "" }, new[] { "", "horse" } } };
        yield return new object?[] { "(a)|(ab)", none, "xab", 0, new[] { new[] { "a", "" } } };
        yield return new object?[] { "(ab)|(a)", none, "xab", 0, new[] { new[] { "ab", "" } } };
        yield return new object?[] { "((cat)|(horse))", none, "cat horse", 0, new[] { new[] { "cat", "cat", "" }, new[] { "horse", "", "horse" } } };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 0, new[] { new[] { "caf\u00E9", "" }, new[] { "", "ni\u00F1o" } } };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "cat dog", 0, new[] { new[] { "cat", "" }, new[] { "", "dog" } } };
    }

    [Theory]
    [MemberData(nameof(OwnedTupleCases))]
    public void OwnedTupleFindAllMatchesManaged(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[][] tuples)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var strings = regex.FindAllToStrings(input, startOffsetInBytes);
        Assert.Equal(tuples.Length, strings.TupleValues.Length);
        for (var i = 0; i < tuples.Length; i++)
        {
            Assert.Equal(tuples[i], strings.TupleValues[i]);
        }

        var utf8 = regex.FindAllToUtf8(input, startOffsetInBytes);
        Assert.Equal(tuples.Length, utf8.TupleValues.Length);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ManagedCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(b)|(:+)", none, ":a:b::c", 0, new[] { ":", ":", "b", "::" } };
        yield return new object?[] { "a*", none, "ba", 0, new[] { "", "a", "" } };
        yield return new object?[] { "\\b|\\w+", none, "\u00E9 skip y", 8, new[] { "", "y", "" } };
    }

    [Theory]
    [MemberData(nameof(ManagedCases))]
    public void FormerManagedFindAllStaysOwned(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var matches = regex.FindAll(input, startOffsetInBytes);
        Assert.Equal(values, matches.Select(static match => match.ValueText).ToArray());
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}
