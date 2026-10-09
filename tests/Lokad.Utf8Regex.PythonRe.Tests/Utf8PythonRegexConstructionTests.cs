using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Full match wrappers are lazy and reuse admitted core scalar programs.
// Constructing or using them must not create the managed full-match engine.
public sealed class Utf8PythonRegexConstructionTests
{
    [Fact]
    public void ScalarFullProgramIsCreatedLazily()
    {
        var regex = new Utf8PythonRegex("(?<=ab)c");

        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Assert.False(regex.FullMatch("abc"u8).Success);
        Assert.True(regex.DebugUsesOwnedSlice);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
    }

    [Fact]
    public void ScalarFullMatchResultAgreesWithCpython()
    {
        var regex = new Utf8PythonRegex("(?<=a)a");

        Assert.True(regex.FullMatch("aa"u8, 1).Success);
        Assert.False(regex.FullMatch("ba"u8, 1).Success);
    }

    [Fact]
    public void Utf8BackedFullMatchUsesTheScalarFullProgram()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.FullMatch("foo"u8).Success);
        Assert.True(regex.DebugUsesOwnedSlice);
    }

    [Fact]
    public void NonFullMatchOperationsDoNotCreateTheScalarFullProgram()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.IsMatch("xxfooyy"u8));
        Assert.Equal(2, regex.Count("foo xx foo"u8));
        Assert.True(regex.Search("xxfoo"u8).Success);
        Assert.True(regex.Match("foo xx"u8).Success);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
    }

    [Fact]
    public void InvalidPatternStillFailsAtConstruction()
    {
        var exception = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("(a+)(?<=\\1)b"));
        Assert.Contains("look-behind requires fixed-width pattern", exception.Message);
    }
}