namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NullPatternTests
{
    private static Utf8Regex Compile(Utf8ResourcePolicy policy = default,
        Utf8ConstructionPolicy construction = default, TimeSpan? constructionTimeout = null, CancellationToken token = default)
    {
        var timeout = constructionTimeout ?? Timeout.InfiniteTimeSpan;
        return Utf8Regex.Compile((string)null!, options: default, policy: new Utf8RegexPolicy
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
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => new Utf8Regex((string)null!)).ParamName);
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => Compile()).ParamName);

        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() =>
            Compile(construction: new Utf8ConstructionPolicy { MaxBytes = 1 })).ParamName);
        Assert.True(new Utf8Regex(string.Empty).IsMatch(""u8));

    }

    [Fact]
    public void ExistingArgumentAndPreCancellationPrecedenceIsPreserved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex((string)null!, options: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.Zero
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex((string)null!, options: (System.Text.RegularExpressions.RegexOptions)(1 << 30)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex((string)null!, options: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 0
            }
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
