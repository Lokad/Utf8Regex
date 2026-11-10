using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAtomLoweringTests
{
    [Theory]
    [InlineData("([a-cb-d]+)", "abddxé😀", new[] { "abdd" })]
    [InlineData("([aa-a]+)", "baaa😀", new[] { "aaa" })]
    [InlineData("([^ab]+)", "aé😀bxy", new[] { "é😀", "xy" })]
    [InlineData(@"([\x00-\x7f]+)", "a\0\u007fé😀b", new[] { "a\0\u007f", "b" })]
    [InlineData(@"([\^\]\\]+)", "^]\\é", new[] { "^]\\" })]
    [InlineData("(?ai:([a-z]+))", "AzKİıſ", new[] { "Az" })]
    [InlineData("(?i:([a-z]+))", "AzKİıſ", new[] { "AzKİıſ" })]
    [InlineData("(?i:([^k]+))", "aKbKc", new[] { "a", "b", "c" })]
    [InlineData("(?i:([a-c]+))(?-i:([D-F]+))", "AbCDF", new[] { "AbCDF" })]
    [InlineData(@"([a\d]+)", "a١2😀", new[] { "a١2" })]
    public void ResolvedBracketOperandsPreserveFoldingComplementsAndCaptures(string pattern, string text, string[] expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var policy in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        {
            using var regex = new Utf8PythonRegex(pattern, resourcePolicy: new() { MaxWorkSteps = 1000000 }, inputValidation: policy);
            Assert.Equal(expected.Length, regex.Count(input));
            using var cursor = regex.CreateMatchCursor(input);
            var values = new List<string>();
            while (cursor.MoveNext()) values.Add(cursor.Current.Value.ValueText);
            Assert.Equal(expected, values);
        }
    }

    [Theory]
    [InlineData("^.$", "😀\n", new[] { "😀" })]
    [InlineData("(?s:.)", "😀\n", new[] { "😀", "\n" })]
    [InlineData("(?s:.)(?-s:.)", "\n😀", new[] { "\n😀" })]
    [InlineData("(?m:^.$)", "😀\nx\n", new[] { "😀", "x" })]
    [InlineData("(?m:^.)(?-m:$)", "a\nb\n", new[] { "b" })]
    [InlineData("(?m:^.(?-s:.)(?s:.)(?-m:$))", "a😀\n", new[] { "a😀\n" })]
    [InlineData(@"\^\.\$", "^.$", new[] { "^.$" })]
    [InlineData(@"\uD800", "😀", new string[] { })]
    public void ResolvedAtomsKeepScopedFlagsScalarsAndEscapedLiterals(string pattern, string text, string[] expected)
    {
        var input=Encoding.UTF8.GetBytes(text);
        foreach(var policy in new[]{Utf8InputValidation.Validate,Utf8InputValidation.AssumeValid})
        foreach(var finite in new[]{false,true})
        {
            var regex=new Utf8PythonRegex(pattern,resourcePolicy:finite
                ? new(){MaxWorkSteps=1000000,MaxWorkingBytes=1048576}:default,inputValidation:policy);
            Assert.Equal(expected.Length,regex.Count(input));
            Assert.Equal(expected,regex.FindAllToStrings(input).ScalarValues);
        }
    }
}
