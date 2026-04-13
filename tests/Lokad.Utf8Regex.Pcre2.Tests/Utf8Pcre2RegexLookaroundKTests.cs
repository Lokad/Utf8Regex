using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2RegexLookaroundKTests
{
    [Fact]
    public void LookaroundKRequiresExplicitOptIn()
    {
        var exception = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex("(?=ab\\K)"));
        Assert.Equal(Pcre2ErrorKinds.LookaroundBackslashKDisabled, exception.ErrorKind);
    }

    [Fact]
    public void LookaroundKMatchCanReportNonContiguousOffsets()
    {
        var regex = new Utf8Pcre2Regex(
            "(?=ab\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true });

        var match = regex.Match("ab"u8);

        Assert.True(match.Success);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(0, match.EndOffsetInBytes);
        Assert.False(match.HasContiguousByteRange);
        Assert.False(match.IsUtf8SliceWellFormed);
        Assert.ThrowsAny<InvalidOperationException>(static () => ThrowForNonContiguousValueMatch("ab"u8));
    }

    [Fact]
    public void LookaroundKDetailedMatchCanReportNonContiguousOffsets()
    {
        var regex = new Utf8Pcre2Regex(
            "(?=ab\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true });

        var match = regex.MatchDetailed("ab"u8);

        Assert.True(match.Success);
        Assert.Equal(2, match.Value.StartOffsetInBytes);
        Assert.Equal(0, match.Value.EndOffsetInBytes);
        Assert.False(match.Value.HasContiguousByteRange);
        Assert.ThrowsAny<InvalidOperationException>(static () => ThrowForNonContiguousDetailedValue("ab"u8));
    }

    [Fact]
    public void LookaroundKGlobalIterationRemainsRejected()
    {
        var regex = new Utf8Pcre2Regex(
            "(?=ab\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true });

        Assert.Throws<NotSupportedException>(() => regex.Count("ab"u8));
        Assert.Throws<NotSupportedException>(() => regex.EnumerateMatches("ab"u8));
    }

    [Fact]
    public void LookaroundKReplacementRemainsRejectedForNonMonotoneIteration()
    {
        var regex = new Utf8Pcre2Regex(
            "(?=ab\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true });

        Assert.Throws<NotSupportedException>(() => regex.ReplaceToString("ab"u8, "<$0>"));
    }

    [Fact]
    public void LookbehindKReplacementRemainsRejectedForNonMonotoneIteration()
    {
        var regex = new Utf8Pcre2Regex(
            "(?<=\\Ka)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true });

        Assert.Throws<NotSupportedException>(() => regex.ReplaceToString("a"u8, "<$0>"));
    }

    [Fact]
    public void SneakyLookbehindKEnumerationCanYieldThenFail()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\Ka))(?<=(?&sneaky))b");

        var enumerator = regex.EnumerateMatches("ab"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current.GetValueString());

        Pcre2MatchException? exception = null;
        try
        {
            _ = enumerator.MoveNext();
        }
        catch (Pcre2MatchException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        Assert.Equal(Pcre2ErrorKinds.DisallowedLookaroundBackslashK, exception.ErrorKind);
    }

    [Fact]
    public void SneakyLookaheadKReplacementIsExplicitlyRejected()
    {
        var regex = new Utf8Pcre2Regex("(?(DEFINE)(?<sneaky>b\\K))a(?=(?&sneaky))");

        Assert.Throws<NotSupportedException>(() => regex.ReplaceToString("ab"u8, "<$0>"));
    }

    [Fact]
    public void SneakyGlobalLookbehindKReplacementIsExplicitlyRejected()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\K\\Ga))(?<=(?&sneaky))b");

        Assert.Throws<NotSupportedException>(() => regex.ReplaceToString("ab"u8, "<$0>"));
    }

    [Fact]
    public void SneakyGlobalLookbehindKEnumerationCanStayMonotone()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\K\\Ga))(?<=(?&sneaky))b");

        var enumerator = regex.EnumerateMatches("ab"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current.GetValueString());
        Assert.False(enumerator.MoveNext());
        Assert.Equal(1, regex.Count("ab"u8));
    }

    private static void ThrowForNonContiguousValueMatch(ReadOnlySpan<byte> input)
    {
        var regex = new Utf8Pcre2Regex(
            "(?=ab\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true });
        _ = regex.Match(input).GetValueBytes();
    }

    private static void ThrowForNonContiguousDetailedValue(ReadOnlySpan<byte> input)
    {
        var regex = new Utf8Pcre2Regex(
            "(?=ab\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true });
        _ = regex.MatchDetailed(input).Value.GetValueBytes();
    }

}
