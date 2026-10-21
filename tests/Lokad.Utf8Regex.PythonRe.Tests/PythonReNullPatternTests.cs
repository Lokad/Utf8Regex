namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNullPatternTests
{
    private static Utf8PythonRegex Compile(Utf8ResourcePolicy policy = default,
        Utf8ConstructionPolicy construction = default, TimeSpan? constructionTimeout = null, CancellationToken token = default)
    {
        var timeout = constructionTimeout ?? Timeout.InfiniteTimeSpan;
        return Utf8PythonRegex.Compile((string)null!, default, Timeout.InfiniteTimeSpan, policy, construction, timeout, token);
    }

    [Fact]
    public void NullTextHasAnArgumentErrorAtPublicBoundaries()
    {
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => new Utf8PythonRegex((string)null!)).ParamName);
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => Compile()).ParamName);

        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() =>
            Compile(construction: new Utf8ConstructionPolicy { MaxBytes = 1 })).ParamName);
        Assert.True(new Utf8PythonRegex(string.Empty).IsMatch(""u8));
        Assert.True(new Utf8PythonRegex(default(ReadOnlySpan<byte>)).IsMatch(""u8));
    }

    [Fact]
    public void ExistingArgumentAndPreCancellationPrecedenceIsPreserved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex((string)null!, default, TimeSpan.Zero));
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex((string)null!, PythonReCompileOptions.Locale));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex((string)null!, default, Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy { MaxWorkSteps = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Compile(construction: new Utf8ConstructionPolicy { MaxBytes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Compile(constructionTimeout: TimeSpan.Zero));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => Compile(
            construction: new Utf8ConstructionPolicy { MaxBytes = 0 }, token: cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }
}
