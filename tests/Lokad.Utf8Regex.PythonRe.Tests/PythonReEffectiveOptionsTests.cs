using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReEffectiveOptionsTests
{
    // Expected global flags follow CPython's compiled-pattern metadata, not a
    // second header parser in the host. Enum bit values are profile-specific.
    [Theory]
    [InlineData("a", PythonReCompileOptions.None, PythonReCompileOptions.Unicode)]
    [InlineData("(?i)a", PythonReCompileOptions.None, PythonReCompileOptions.IgnoreCase | PythonReCompileOptions.Unicode)]
    [InlineData("(?#prefix)(?m)a", PythonReCompileOptions.None, PythonReCompileOptions.Multiline | PythonReCompileOptions.Unicode)]
    [InlineData("(?x) # prefix\n(?#group)(?im) a", PythonReCompileOptions.None, PythonReCompileOptions.Verbose | PythonReCompileOptions.IgnoreCase | PythonReCompileOptions.Multiline | PythonReCompileOptions.Unicode)]
    [InlineData(" # prefix\n(?#group)(?ai) a", PythonReCompileOptions.Verbose, PythonReCompileOptions.Verbose | PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase)]
    [InlineData("(?i:(?a:a))", PythonReCompileOptions.None, PythonReCompileOptions.Unicode)]
    [InlineData("(?a:(?u:é))", PythonReCompileOptions.None, PythonReCompileOptions.Unicode)]
    [InlineData("(?a)a", PythonReCompileOptions.None, PythonReCompileOptions.Ascii)]
    [InlineData("(?u)a", PythonReCompileOptions.None, PythonReCompileOptions.Unicode)]
    [InlineData("(?#prefix)(?a)(?i)a", PythonReCompileOptions.None, PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase)]
    [InlineData("(?s)a", PythonReCompileOptions.Multiline, PythonReCompileOptions.DotAll | PythonReCompileOptions.Multiline | PythonReCompileOptions.Unicode)]
    public void EffectiveGlobalFlagsComeFromValidatedPattern(string pattern, PythonReCompileOptions requested, PythonReCompileOptions expected)
    {
        var bytes = Encoding.UTF8.GetBytes(pattern);
        Utf8PythonRegex[] expressions = [
            new(pattern, requested), new(bytes, requested),
            Utf8PythonRegex.Compile(pattern, requested, Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default),
            Utf8PythonRegex.Compile(bytes, requested, Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default),
        ];
        foreach (var expression in expressions)
        {
            Assert.Equal(expected, expression.EffectiveOptions);
            Assert.Equal(requested, expression.Options);
            Assert.Equal(pattern, expression.Pattern);
        }
    }

    [Fact]
    public void MetadataAgreesWithGlobalAndScopedExecution()
    {
        var ascii = new Utf8PythonRegex(@"(?ai)\w");
        Assert.Equal(PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase, ascii.EffectiveOptions);
        Assert.True(ascii.FullMatch("A"u8).Success);
        Assert.False(ascii.FullMatch("é"u8).Success);

        var scoped = new Utf8PythonRegex(@"(?a:\w+)\s+(?u:\w+)");
        Assert.Equal(PythonReCompileOptions.Unicode, scoped.EffectiveOptions);
        Assert.True(scoped.FullMatch("a é"u8).Success);
        Assert.False(scoped.FullMatch("é é"u8).Success);

        var lines = new Utf8PythonRegex("(?x) # prefix\n(?#group)(?im) ^a$");
        Assert.True(lines.IsMatch("first\nA\nlast"u8));
        Assert.Equal(PythonReCompileOptions.None, lines.Options);
        Assert.Equal(PythonReCompileOptions.Verbose | PythonReCompileOptions.IgnoreCase |
            PythonReCompileOptions.Multiline | PythonReCompileOptions.Unicode, lines.EffectiveOptions);
    }
}
