using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReComposedTemplateTests
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
    public void ByteTemplateKeepsCpythonCompositionPriorityAndOffsets(string pattern, PythonReCompileOptions options,
        string inputText, int count, int startOffsetInBytes, string expectedText, int expectedCount)
    {
        using var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var result = regex.Subn(input, @"[\g<0>]", count, startOffsetInBytes);
        Assert.Equal(expectedText, Encoding.UTF8.GetString(result.ResultBytes));
        Assert.Equal(expectedCount, result.ReplacementCount);
    }
}
