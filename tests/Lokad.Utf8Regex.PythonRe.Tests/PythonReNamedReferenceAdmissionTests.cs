using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNamedReferenceAdmissionTests
{
    [Theory]
    [InlineData("(?P=x)(?P<x>a)", "x", 4)]
    [InlineData("(?<=(?P=x))(?P<x>a)", "x", 8)]
    [InlineData("(?P=é)(?P<é>a)", "é", 4)]
    [InlineData("(?P=x)", "x", 4)]
    public void NamedBackreferencesCannotResolveLaterDeclarations(string pattern, string name, int position)
    {
        foreach (var bytes in new[] { false, true })
        {
            var error = Assert.Throws<PythonRePatternException>(() =>
            {
                if (bytes) _ = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern));
                else _ = new Utf8PythonRegex(pattern);
            });
            Assert.Contains("unknown group name '" + name + "'", error.Message);
            Assert.Equal(position, error.Position);
            Assert.False(Utf8ConstructionScope.IsActive);
        }
        Assert.True(new Utf8PythonRegex("(?P<x>a)(?P=x)").FullMatch("aa"u8).Success);
    }

    [Fact]
    public void UnterminatedNameKeepsItsSyntaxFailure()
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("(?P=x"));
        Assert.Contains("missing ), unterminated name", error.Message);
        Assert.Equal(4, error.Position);
    }
}
