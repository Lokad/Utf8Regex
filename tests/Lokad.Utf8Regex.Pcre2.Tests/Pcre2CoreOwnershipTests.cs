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
        var regex = new Utf8Pcre2Regex(pattern, options: options, compileSettings: new() { Newline = newline }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.False(regex.DebugCompiledProgram.Operations.Count is Pcre2LiteralFamilyDirectProgram);
        var bytes = System.Text.Encoding.UTF8.GetBytes(subject);
        Assert.True(regex.IsMatch(bytes));
        Assert.Equal(1, regex.Count(bytes));
        Assert.Equal(subject, System.Text.Encoding.UTF8.GetString(regex.Match(bytes).GetValueBytes()));
        Assert.Equal(1, regex.Count(bytes, 0, Pcre2MatchOptions.NotEmpty));
    }

    [Fact]
    public void FiniteCapturedLanguageKeepsItsLiveDerivedCoreInsteadOfTheOriginal()
    {
        var regex = new Utf8Pcre2Regex("(?<left>a)(b)");
        var operations = regex.DebugCompiledProgram.Operations;
        var family = Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(operations.Count);
        Assert.Equal("ab", family.Regex.Pattern);
        Assert.Same(family, operations.IsMatch);
        Assert.Same(family, operations.Enumerate);
        Assert.Equal(2, regex.Count("ab ab"u8));
        ReadOnlySpan<byte> match2Input = "ab"u8;
        var match2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match2Input, captures: match2Captures);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((match2Captures[1].Success ? match2Input.Slice(match2Captures[1].StartOffsetInBytes, match2Captures[1].EndOffsetInBytes - match2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((match2Captures[2].Success ? match2Input.Slice(match2Captures[2].StartOffsetInBytes, match2Captures[2].EndOffsetInBytes - match2Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void SharedSliceOwnsItsPayloadAndFallbackWithoutTheUnusedCoreFacade()
    {
        var regex = new Utf8Pcre2Regex(@"([0-9]+)-\1");
        var operations = regex.DebugCompiledProgram.Operations;
        var canonical = Assert.IsType<Pcre2SliceDirectProgram>(operations.Match);
        var count = Assert.IsType<Pcre2SliceDirectProgram>(operations.Count);
        Assert.Same(canonical.SliceProgram, count.SliceProgram);
        Assert.Same(canonical.Fallback, count.Fallback);
        Assert.Equal(2, regex.Count("12-12 7-7"u8));
        ReadOnlySpan<byte> retained3Input = "12-12"u8;
        var retained3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained3Input, captures: retained3Captures);
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString((retained3Captures[1].Success ? retained3Input.Slice(retained3Captures[1].StartOffsetInBytes, retained3Captures[1].EndOffsetInBytes - retained3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(1, regex.Count("12-12"u8, 0, Pcre2MatchOptions.NotEmpty));
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString((retained3Captures[1].Success ? retained3Input.Slice(retained3Captures[1].StartOffsetInBytes, retained3Captures[1].EndOffsetInBytes - retained3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }
}
