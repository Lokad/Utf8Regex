using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexTimeoutTests
{
    [Fact]
    public void NativeSimpleExecutionRespectsMatchTimeout()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';

        var exception = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.Equal(input.Length, exception.Input.Length);
    }

    [Fact]
    public void NativeMatchEnumerationRespectsMatchTimeout()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';

        Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            var enumerator = regex.EnumerateMatches(input);
            _ = enumerator.MoveNext();
        });
    }

    [Fact]
    public void NativeReplaceRespectsMatchTimeout()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';

        Assert.Throws<RegexMatchTimeoutException>(() => regex.Replace(input, "z"));
    }

    [Fact]
    public void NativeSplitEnumerationMapsDeadlineExpirationAtTheAdapter()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = CreateCatastrophicInput();

        Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            var enumerator = regex.EnumerateSplits(input);
            _ = enumerator.MoveNext();
        });
    }

    [Fact]
    public void NativeCountAndMatchMapDeadlineExpirationAtTheFacade()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));

        Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(CreateCatastrophicInput()));
        Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(CreateCatastrophicInput()));
    }

    private static byte[] CreateCatastrophicInput()
    {
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';
        return input;
    }
}
