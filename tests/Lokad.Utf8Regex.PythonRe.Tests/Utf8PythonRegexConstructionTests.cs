using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class Utf8PythonRegexConstructionTests
{
    [Fact]
    public void ScalarFullMatchResultAgreesWithCpython()
    {
        var regex = new Utf8PythonRegex("(?<=a)a");

        Assert.True(regex.Match("aa"u8,1,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("ba"u8,1,mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void LiteralFullMatchConsumesTheCompleteSubject()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.Match("foo"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void LiteralConstructionServesEveryMatchMode()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.IsMatch("xxfooyy"u8));
        Assert.Equal(2, regex.Count("foo xx foo"u8));
        Assert.True(regex.Match("xxfoo"u8,mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(regex.Match("foo xx"u8).Success);
    }

    [Fact]
    public void InvalidPatternStillFailsAtConstruction()
    {
        var exception = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("(a+)(?<=\\1)b"));
        Assert.Contains("look-behind requires fixed-width pattern", exception.Message);
    }
}