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

        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
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
}
