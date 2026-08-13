using System.Text.Json;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2OracleProfileTests
{
    [Fact]
    public void OracleProfilePinsTheNativeReferenceConfiguration()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Corpus", "oracle-profile.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var oracle = root.GetProperty("Oracle");
        var build = root.GetProperty("Build");
        var invocation = root.GetProperty("Invocation");
        var generator = root.GetProperty("CorpusGenerator");

        Assert.Equal(1, root.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("PCRE2", oracle.GetProperty("Project").GetString());
        Assert.Equal("10.47", oracle.GetProperty("Release").GetString());
        Assert.Equal("pcre2-10.47", oracle.GetProperty("Tag").GetString());
        Assert.Equal("f454e23", oracle.GetProperty("Commit").GetString());
        Assert.Equal(8, build.GetProperty("CodeUnitWidth").GetInt32());
        Assert.True(build.GetProperty("UnicodeSupport").GetBoolean());
        Assert.Equal("16.0.0", build.GetProperty("UnicodeVersion").GetString());
        Assert.Equal("LF", build.GetProperty("DefaultNewline").GetString());
        Assert.Equal("Unicode", build.GetProperty("DefaultBsr").GetString());
        Assert.False(build.GetProperty("Jit").GetBoolean());
        Assert.Equal(
            ["PCRE2_UTF", "PCRE2_UCP"],
            invocation.GetProperty("CompileOptions").EnumerateArray().Select(static value => value.GetString() ?? throw new InvalidOperationException("Compile option cannot be null.")).ToArray());
        Assert.Empty(invocation.GetProperty("MatchOptions").EnumerateArray());
        Assert.Equal("Lokad.Utf8Regex.Pcre2 corpus normalizer", generator.GetProperty("Name").GetString());
        Assert.Equal(1, generator.GetProperty("Version").GetInt32());
    }
}
