using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// FullMatch shares scalar preparation with other operations.
public sealed class Utf8PythonRegexConstructionTests
{
    [Fact]
    public void FullMatchKeepsWholeSubjectLookbehindContext()
    {
        var regex = new Utf8PythonRegex("(?<=ab)c");

        Assert.False(regex.FullMatch("abc"u8).Success);
    }

    [Fact]
    public void ScalarFullMatchResultAgreesWithCpython()
    {
        var regex = new Utf8PythonRegex("(?<=a)a");

        Assert.True(regex.FullMatch("aa"u8, 1).Success);
        Assert.False(regex.FullMatch("ba"u8, 1).Success);
    }

    [Fact]
    public void LiteralFullMatchKeepsItsNativeRoute()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.FullMatch("foo"u8).Success);
    }

    [Fact]
    public void SearchMatchCountAndFullMatchShareThePreparedExpression()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.IsMatch("xxfooyy"u8));
        Assert.Equal(2, regex.Count("foo xx foo"u8));
        Assert.True(regex.Search("xxfoo"u8).Success);
        Assert.True(regex.Match("foo xx"u8).Success);
        Assert.True(regex.FullMatch("foo"u8).Success);
    }

    [Fact]
    public void InvalidPatternStillFailsAtConstruction()
    {
        var exception = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("(a+)(?<=\\1)b"));
        Assert.Contains("look-behind requires fixed-width pattern", exception.Message);
    }
}