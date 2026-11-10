using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAtomLoweringTests
{
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
