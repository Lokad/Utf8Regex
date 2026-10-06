using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReManagedConstructionTests
{
    public static IEnumerable<object?[]> FreshCases()
    {
        string[] patterns =
        [
            "abc",
            "cat|horse",
            "(cat|horse)",
            "(?P<w>cat)|(?P<v>horse)",
            "(cat)|(horse)",
            "[a-z]+",
            "(a)(b)",
            "\\d+",
        ];
        foreach (var pattern in patterns)
        {
            yield return new object?[] { pattern };
        }
    }

    [Theory]
    [MemberData(nameof(FreshCases))]
    public void FreshInstanceConstructsNoManagedEngine(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.False(regex.DebugIsManagedRegexValueCreated);
        Assert.False(regex.DebugIsManagedFullRegexValueCreated);
    }

    [Theory]
    [InlineData("cat|horse", "cat horse")]
    [InlineData("(cat|horse)", "cat horse")]
    public void Utf8BackedOperationsConstructNoManagedEngine(string pattern, string inputText)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        Assert.True(regex.IsMatch(input));
        Assert.True(regex.Search(input).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[a-z]+", "xxabcxx")]
    [InlineData("(a)(b)", "xxabxx")]
    [InlineData("(?<=a)b", "xab")]
    public void ScalarGlobalOperationsConstructNoManagedEngine(string pattern, string inputText)
    {
        // Scalar global discovery retains captures and full lookbehind context
        // without constructing the former managed engine.
        var regex = new Utf8PythonRegex(pattern);
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        Assert.False(regex.DebugIsManagedRegexValueCreated);
        Assert.NotEmpty(regex.FindIterDetailed(input));
        Assert.False(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}