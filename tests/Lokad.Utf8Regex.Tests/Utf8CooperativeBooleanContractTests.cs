using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CooperativeBooleanContractTests
{
    private const string Pattern = "^[A-Z]{3}[0-9]{4}$";

    [Theory]
    [InlineData("ABC0123", true)]
    [InlineData("abc0123", false)]
    [InlineData("ABC0123\n", true)]
    [InlineData("ABC0123\r\n", false)]
    [InlineData("AB𐐀0123", false)]
    public void LivePredicateKeepsResultsAndNeedsNoMajorAsciiWorkspace(string text, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        var ordinary = new Utf8Regex(Pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, ordinary.IsMatch(input, CancellationToken.None));
        Assert.Equal(expected, ordinary.IsMatch(input, source.Token));
        // Tight major-buffer admission proves the ASCII route does not acquire
        // discarded match/capture arrays. Invocation metadata has separate scope.
        if (!input.AsSpan().ContainsAnyInRange((byte)128, byte.MaxValue))
        {
            var limited = new Utf8Regex(Pattern, RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan,
                new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 4096,
                    MaxWorkingBytes = 1, MaxResultBytes = 1 });
            Assert.Equal(expected, limited.IsMatch(input, CancellationToken.None));
            Assert.Equal(expected, limited.IsMatch(input, source.Token));
        }
    }

    [Theory]
    [InlineData("ABC0123")]
    [InlineData("abc0123")]
    public void WholeSubjectValidationAndOriginalEntryCancellationPrecedeTheAnswer(string text)
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant);
        byte[] malformed = [.. Encoding.UTF8.GetBytes(text), 0xff];
        using var source = new CancellationTokenSource();
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed, source.Token));
        source.Cancel();
        Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(malformed, source.Token)).CancellationToken);
    }
}
