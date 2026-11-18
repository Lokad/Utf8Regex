using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2RegexLookaroundKTests
{
    [Fact]
    public void LookaroundKRequiresExplicitOptIn()
    {
        var exception = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex("(?=ab\\K)"));
        Assert.Equal(Pcre2ErrorKind.LookaroundBackslashKDisabled, exception.ErrorKind);
    }

    [Fact]
    public void LookaroundKMatchCanReportNonContiguousOffsets()
    {
        var regex = new Utf8Pcre2Regex("(?=ab\\K)", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });

        var match = regex.Match("ab"u8);

        Assert.True(match.Success);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(0, match.EndOffsetInBytes);
        Assert.False(match.HasContiguousByteRange);
        Assert.False(match.IsUtf8SliceWellFormed);
        Assert.ThrowsAny<InvalidOperationException>(static () => ThrowForNonContiguousValueMatch("ab"u8));
    }

    [Fact]
    public void LookaroundKGlobalIterationRemainsRejected()
    {
        var regex = new Utf8Pcre2Regex("(?=ab\\K)", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });

        Assert.Throws<NotSupportedException>(() => regex.Count("ab"u8));
        Assert.Throws<NotSupportedException>(() => regex.EnumerateMatches("ab"u8));
    }

    [Fact]
    public void LookaroundKReplacementRemainsRejectedForNonMonotoneIteration()
    {
        var regex = new Utf8Pcre2Regex("(?=ab\\K)", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });

        Assert.Throws<NotSupportedException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("ab"u8, "<$0>")));
    }

    [Fact]
    public void LookbehindKReplacementRemainsRejectedForNonMonotoneIteration()
    {
        var regex = new Utf8Pcre2Regex("(?<=\\Ka)", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });

        Assert.Throws<NotSupportedException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("a"u8, "<$0>")));
    }

    [Fact]
    public void SneakyLookbehindKEnumerationCanYieldThenFail()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\Ka))(?<=(?&sneaky))b");

        var enumerator = regex.EnumerateMatches("ab"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        var retained = enumerator.Current;

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
        Assert.Equal(Pcre2ErrorKind.DisallowedLookaroundBackslashK, exception.ErrorKind);
        Assert.Equal((0, 1), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
    }

    [Fact]
    public void SneakyLookaheadKReplacementIsExplicitlyRejected()
    {
        var regex = new Utf8Pcre2Regex("(?(DEFINE)(?<sneaky>b\\K))a(?=(?&sneaky))");

        Assert.Throws<NotSupportedException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("ab"u8, "<$0>")));
    }

    [Fact]
    public void SneakyGlobalLookbehindKReplacementIsExplicitlyRejected()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\K\\Ga))(?<=(?&sneaky))b");

        Assert.Throws<NotSupportedException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("ab"u8, "<$0>")));
    }

    [Fact]
    public void SneakyGlobalLookbehindKEnumerationCanStayMonotone()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\K\\Ga))(?<=(?&sneaky))b");

        var enumerator = regex.EnumerateMatches("ab"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.False(enumerator.MoveNext());
        Assert.Equal(1, regex.Count("ab"u8));
    }

    private static void ThrowForNonContiguousValueMatch(ReadOnlySpan<byte> input)
    {
        var regex = new Utf8Pcre2Regex("(?=ab\\K)", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        _ = regex.Match(input).GetValueBytes();
    }

}
