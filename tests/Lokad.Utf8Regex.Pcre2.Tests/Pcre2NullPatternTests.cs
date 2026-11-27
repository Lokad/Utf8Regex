namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2NullPatternTests
{
    private static Utf8Pcre2Regex Compile(Utf8ResourcePolicy policy = default,
        Utf8ConstructionPolicy construction = default, TimeSpan? constructionTimeout = null, CancellationToken token = default)
    {
        var timeout = constructionTimeout ?? Timeout.InfiniteTimeSpan;
        return Utf8Pcre2Regex.Compile((string)null!, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy,
            ConstructionPolicy = construction,
            ConstructionTimeout = timeout
        }, cancellationToken: token);
    }

    [Fact]
    public void NullTextHasAnArgumentErrorAtPublicBoundaries()
    {
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => new Utf8Pcre2Regex((string)null!)).ParamName);
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => Compile()).ParamName);
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() =>
            new Utf8Pcre2Regex((string)null!, options: default, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = default,
                MatchTimeout = Timeout.InfiniteTimeSpan
            })).ParamName);
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() =>
            Compile(construction: new Utf8ConstructionPolicy { MaxBytes = 1 })).ParamName);
        Assert.True(new Utf8Pcre2Regex(string.Empty).IsMatch(""u8));
        Assert.True(new Utf8Pcre2Regex(default(ReadOnlySpan<byte>)).IsMatch(""u8));
    }

    [Fact]
    public void ExistingArgumentAndPreCancellationPrecedenceIsPreserved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex((string)null!, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.Zero
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex((string)null!, options: (Pcre2CompileOptions)(1 << 30)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex((string)null!, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 0
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Compile(construction: new Utf8ConstructionPolicy { MaxBytes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Compile(constructionTimeout: TimeSpan.Zero));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => Compile(
            construction: new Utf8ConstructionPolicy { MaxBytes = 0 }, token: cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }
}
