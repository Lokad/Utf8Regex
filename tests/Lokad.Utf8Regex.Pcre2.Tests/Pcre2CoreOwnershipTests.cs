namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CoreOwnershipTests
{
    [Theory]
    [InlineData("[a-z]+", "AB", Pcre2CompileOptions.Caseless, Pcre2NewlineConvention.Default)]
    [InlineData("a+b", "aaab", Pcre2CompileOptions.None, Pcre2NewlineConvention.Cr)]
    [InlineData(@"(a)\1", "aa", Pcre2CompileOptions.Anchored, Pcre2NewlineConvention.Default)]
    public void ProfileOnlyOperationsDoNotRetainUnusedCorePreparation(string pattern, string subject,
        Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        var regex = new Utf8Pcre2Regex(pattern, options, new() { Newline = newline }, default, Timeout.InfiniteTimeSpan);
        Assert.Null(regex.DebugCompiledProgram.PrimaryUtf8);
        var bytes = System.Text.Encoding.UTF8.GetBytes(subject);
        Assert.True(regex.IsMatch(bytes));
        Assert.Equal(1, regex.Count(bytes));
        Assert.Equal(subject, regex.MatchDetailed(bytes).Value.GetValueString());
        Assert.Equal(1, regex.Count(bytes, 0, Pcre2MatchOptions.NotEmpty));
    }

    [Fact]
    public void FiniteCapturedLanguageKeepsItsLiveDerivedCoreInsteadOfTheOriginal()
    {
        var regex = new Utf8Pcre2Regex("(?<left>a)(b)");
        var owner = Assert.IsType<Utf8Regex>(regex.DebugCompiledProgram.PrimaryUtf8);
        Assert.Equal("ab", owner.Pattern);
        Assert.Same(owner, Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(regex.DebugCompiledProgram.Operations.Count).Regex);
        Assert.Equal(2, regex.Count("ab ab"u8));
        var match = regex.MatchDetailed("ab"u8);
        Assert.Equal("a", match.GetGroup(1).GetValueString());
        Assert.Equal("b", match.GetGroup(2).GetValueString());
    }

    [Fact]
    public void SharedSliceOwnsItsPayloadAndFallbackWithoutTheUnusedCoreFacade()
    {
        var regex = new Utf8Pcre2Regex(@"([0-9]+)-\1");
        var program = regex.DebugCompiledProgram;
        Assert.Null(program.PrimaryUtf8);
        var canonical = Assert.IsType<Pcre2SliceDirectProgram>(program.Operations.Match);
        Assert.Same(canonical.SliceProgram, Assert.IsType<Pcre2SliceDirectProgram>(program.Operations.Count).SliceProgram);
        Assert.Equal(2, regex.Count("12-12 7-7"u8));
        var retained = regex.MatchDetailed("12-12"u8);
        Assert.Equal("12", retained.GetGroup(1).GetValueString());
        Assert.Equal(1, regex.Count("12-12"u8, 0, Pcre2MatchOptions.NotEmpty));
        Assert.Equal("12", retained.GetGroup(1).GetValueString());
    }
}
