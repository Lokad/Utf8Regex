using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteUriCountTests
{
    private const string Pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
    private static Utf8Regex Create(RegexOptions options, ulong work)
        => new(Pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = work,
                MaxDepth = 8192,
                MaxWorkingBytes = 8 << 20,
                MaxResultBytes = 1
            }
        });

    [Theory]
    [InlineData("", 0)]
    [InlineData("x://a", 0)]
    [InlineData("x://ab", 1)]
    [InlineData("x://a/", 1)]
    [InlineData("x:///ab", 0)]
    [InlineData("x://ab?", 1)]
    [InlineData("x://ab#", 1)]
    [InlineData("x://ab?#", 1)]
    [InlineData("x://ab?q=é#片", 1)]
    [InlineData("x://ab#fragment?tail", 1)]
    [InlineData("x://ab?q#x://cd", 1)]
    [InlineData("x://ab y://cd", 2)]
    [InlineData("é://ab", 1)]
    [InlineData("𐐀://ab", 1)]
    [InlineData("😀://ab", 0)]
    [InlineData("x://😀", 0)]
    [InlineData("x://😀a", 1)]
    [InlineData("x://a😀", 1)]
    [InlineData("x://ab\u00a0y://cd", 2)]
    [InlineData("x://a\u2003b", 0)]
    [InlineData("x://a\u2003 y://é😀", 1)]
    [InlineData("x://😀\u00a0 y://ab", 1)]
    [InlineData("x://ab?\0#\0", 1)]
    public void UriCountsUseCompleteScalarsWithFundedExecution(string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes("pad" + text + "tail");
        var length = Encoding.UTF8.GetByteCount(text);
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(options, 100000);
            Assert.Equal(expected, regex.Count(input.AsSpan(3, length)));
            Assert.Equal(expected, regex.Count(input.AsSpan(3, length), cancellation.Token));
            Assert.Equal(expected != 0, regex.IsMatch(input.AsSpan(3, length)));
            Assert.Equal(expected != 0, regex.IsMatch(input.AsSpan(3, length), cancellation.Token));
        }
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4095)]
    public void DelimiterWindowsPreserveAllThreeBytesAndSchemeContext(int schemeLength)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', schemeLength) + "://ab?q#f");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            Assert.Equal(1, Create(options, 100000).Count(input));
            Assert.True(Create(options, 100000).IsMatch(input));
        }
    }

    [Theory]
    [InlineData(15, "\t")]
    [InlineData(16, "\u2003")]
    [InlineData(17, "\t")]
    [InlineData(31, "\u2003")]
    [InlineData(32, "\t")]
    [InlineData(63, "\u2003")]
    [InlineData(64, "\t")]
    [InlineData(65, "\u2003")]
    public void NegatedUriRepeatsKeepWideScalarsAndStopAtWhitespace(int asciiRun, string separator)
    {
        var first = "x://a" + new string('b', asciiRun) + "😀tail?q=é#片";
        var input = Encoding.UTF8.GetBytes(first + separator + "y://cd");
        using var regex = Create(RegexOptions.Compiled, 100000);
        using var cancellation = new CancellationTokenSource();
        var match = regex.Match(input, cancellationToken: cancellation.Token);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(first), match.EndOffsetInBytes);
        Assert.Equal(2, regex.Count(input, cancellation.Token));
    }

    [Fact]
    public void InsufficientWorkStillDeniesCount()
    {
        var tiny = Create(RegexOptions.None, 1);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tiny.Count(Encoding.UTF8.GetBytes(new string('x', 8192)))).ResourceKind);
        Assert.Equal(1, Create(RegexOptions.None, 100000).Count("x://ab"u8));
    }

    [Fact]
    public void LongSchemeCountNeedsOnlyTheScalarBesideTheDelimiter()
    {
        const string rejected = "a/://ab ";
        var input = Encoding.UTF8.GetBytes(rejected + new string('w', 32768) + "𐐀://ab");
        using var source = new CancellationTokenSource();
        using var count = Create(RegexOptions.Compiled, 64);
        using var ranges = Create(RegexOptions.Compiled, 100000);

        Assert.Equal(1, count.Count(input, source.Token));
        var match = ranges.Match(input, cancellationToken: source.Token);
        Assert.True(match.Success);
        Assert.Equal((Encoding.UTF8.GetByteCount(rejected), input.Length),
            (match.StartOffsetInBytes, match.EndOffsetInBytes));
    }

    [Fact]
    public void BmpCountsAgreeWithIndependentRegexSemantics()
    {
        const string alphabet = "ab09_:/?#.- é中\u0301\u0903\u00a0\u0085\0";
        var random = new Random(30927);
        var oracle = new Regex(Pattern);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(options, 100000);
            for (var sample = 0; sample < 400; sample++)
            {
                var chars = new char[random.Next(128)];
                for (var index = 0; index < chars.Length; index++) chars[index] = alphabet[random.Next(alphabet.Length)];
                var text = new string(chars);
                Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
            }
        }
    }

    [Theory]
    [InlineData(RegexOptions.IgnoreCase, 3)]
    [InlineData(RegexOptions.RightToLeft, 3)]
    [InlineData(RegexOptions.NonBacktracking, 3)]
    [InlineData(RegexOptions.ECMAScript, 1)]
    public void DeclinedOptionsRetainTheirScalarBehavior(RegexOptions options, int expected)
    {
        var regex = new Utf8Regex(Pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 128,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            }
        });
        Assert.Equal(expected, regex.Count("é://ab 𐐀://ab a://ab"u8));
    }
}
