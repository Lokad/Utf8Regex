namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2UnusedTreeRetentionTests
{
    [Fact]
    public void UnsupportedPartialShapeDropsItsTreeAndKeepsLoweredOperations()
    {
        var regex = new Utf8Pcre2Regex(string.Concat(Enumerable.Repeat("a?", 4096)) + "x");
        Assert.Equal(Pcre2PartialProbeProgramKind.None, regex.DebugCompiledProgram.PartialProbe.Kind);
        Assert.Null(regex.DebugCompiledProgram.SyntaxTree);
        Assert.True(regex.IsMatch("x"u8));
        Assert.Equal(1, regex.Count("x"u8));
        Assert.Equal(1, regex.Count("x"u8, 0, Pcre2MatchOptions.NotEmpty));
        Assert.Equal("x", regex.MatchDetailed("x"u8).GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("x"u8, Pcre2PartialMode.None).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("x"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("x"u8, Pcre2PartialMode.Hard).Kind);
    }
}
