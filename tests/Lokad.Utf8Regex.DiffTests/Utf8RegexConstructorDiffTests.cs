using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexConstructorDiffTests
{
    [Theory]
    [InlineData("foo", RegexOptions.CultureInvariant, -1L)]
    [InlineData("foo", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, -1L)]
    [InlineData("foo", RegexOptions.Compiled | RegexOptions.CultureInvariant, -1L)]
    [InlineData("foo", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, 1L)]
    public void ConstructorMirrorsPatternOptionsAndTimeoutMetadata(string pattern, RegexOptions options, long timeoutMilliseconds)
    {
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.Ctor.Tests.cs
        var matchTimeout = timeoutMilliseconds < 0
            ? Regex.InfiniteMatchTimeout
            : TimeSpan.FromMilliseconds(timeoutMilliseconds);

        var utf8Regex = new Utf8Regex(pattern, options, matchTimeout);
        var runtimeRegex = new Regex(pattern, options, matchTimeout);

        Assert.Equal(runtimeRegex.Options, utf8Regex.Options);
        Assert.Equal(runtimeRegex.MatchTimeout, utf8Regex.MatchTimeout);
    }

    [Fact]
    public void DefaultConstructorUsesRepositoryCultureInvariantDefault()
    {
        var utf8Regex = new Utf8Regex("foo");
        var runtimeRegex = new Regex("foo", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        Assert.Equal(runtimeRegex.Options, utf8Regex.Options);
        Assert.Equal(runtimeRegex.MatchTimeout, utf8Regex.MatchTimeout);
    }

    [Fact]
    public void ConstructorRejectsNullPatternLikeRuntime()
    {
        var runtime = Assert.Throws<ArgumentNullException>(() => new Regex(null!));
        var utf8 = Assert.Throws<ArgumentNullException>(() => new Utf8Regex(null!));

        Assert.Equal(runtime.ParamName, utf8.ParamName);
    }

    [Theory]
    [InlineData((RegexOptions)(-1))]
    [InlineData((RegexOptions)0x1000)]
    public void ConstructorRejectsInvalidOptionsLikeRuntime(RegexOptions options)
    {
        // Mirrored and adapted from Regex.Ctor.Tests.cs invalid-options cases.
        var runtime = Assert.Throws<ArgumentOutOfRangeException>(() => new Regex("foo", options));
        var utf8 = Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("foo", options));

        Assert.Equal(runtime.ParamName, utf8.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2147483647)]
    public void ConstructorRejectsInvalidTimeoutLikeRuntime(long timeoutMilliseconds)
    {
        // Mirrored and adapted from Regex.Ctor.Tests.cs invalid-timeout cases.
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        var runtime = Assert.Throws<ArgumentOutOfRangeException>(() => new Regex("foo", RegexOptions.CultureInvariant, timeout));
        var utf8 = Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("foo", RegexOptions.CultureInvariant, timeout));

        Assert.Equal(runtime.ParamName, utf8.ParamName);
    }
}
