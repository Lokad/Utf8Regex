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
        yield return new object?[] { "([A-Za-z]{2,3})", none, "éabcdefg 😀 ab", 0, 3 };
        yield return new object?[] { "([A-Za-z]){2,3}", none, "éabcdefg 😀 ab", 5, 2 };
        yield return new object?[] { "(([A-Za-z]{2,3}))", none, "éabcdefg 😀 ab", 0, 3 };
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
        yield return new object?[] { "(needle)", none, "é😀needle 中needle", 6, 2 };
        yield return new object?[] { "café|😀", none, "é😀café😀café", 6, 3 };
    }

    [Theory]
    [MemberData(nameof(OwnedCountCases))]
    public void OwnedCountMatchesFindAll(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
        Assert.Equal(regex.FindAll(input, startOffsetInBytes).Length, regex.Count(input, startOffsetInBytes));
    }

    [Fact]
    public void SplitBackendCountAgreesWithFindAll()
    {
        // Count and FindAll share scalar discovery with Python empty-match
        // adjacency and agree with the pinned count.
        var regex = new Utf8PythonRegex("\\b|\\w+", PythonReCompileOptions.None);
        var input = Encoding.UTF8.GetBytes("\u00E9 skip y");
        Assert.Equal(3, regex.Count(input, 8));
        Assert.Equal(3, regex.FindAll(input, 8).Length);
    }

    [Theory]
    [InlineData("(?P<word>foo)-(?P=word)", PythonReCompileOptions.None, "xx foo-foo yy foo-foo", 3, 2)]
    public void NamedReferenceCountRunsOwned(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void CapturedRepeatCountNeedsNoScratchAndPreservesActualCaptureValues(Utf8InputValidation validation)
    {
        var policy = new Utf8ResourcePolicy
            { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };
        const string pattern = "([A-Za-z]){2,3}";
        var count = new Utf8PythonRegex(pattern, resourcePolicy: policy, inputValidation: validation);
        var input = "éabcdefg 😀 ab"u8.ToArray();
        using var token = new CancellationTokenSource();
        Assert.Equal(3, count.Count(input));
        Assert.Equal(2, count.Count(input, 5, token.Token));
        var values = new Utf8PythonRegex(pattern, inputValidation: validation).FindAllToStrings(input);
        Assert.Equal(new[] { "c", "f", "b" }, values.ScalarValues);
        if (validation == Utf8InputValidation.Validate)
            Assert.Throws<ArgumentException>(() => count.Count([.. input, 0xff]));
    }
}
