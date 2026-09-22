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
    public void ManagedOperationsConstructManagedEngine(string pattern, string inputText)
    {
        // FindIterDetailed always runs on the managed cursor; lookbehind Search keeps the
        // verified managed route. Both construct the managed engine on first use, never before.
        var regex = new Utf8PythonRegex(pattern);
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        Assert.False(regex.DebugIsManagedRegexValueCreated);
        Assert.NotEmpty(regex.FindIterDetailed(input));
        Assert.True(regex.DebugIsManagedRegexValueCreated, "pattern=" + pattern);
    }
}