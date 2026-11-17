using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteUriCountTests
{
    private const string Pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
    private static Utf8Regex Create(RegexOptions options, ulong work)
        => new(Pattern, options, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = work, MaxDepth = 8192, MaxWorkingBytes = 8 << 20, MaxResultBytes = 1 });

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

    [Fact]
    public void EntireInputValidityAndWorkDenialRemainAuthoritative()
    {
        var tiny = Create(RegexOptions.None, 1);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tiny.Count(Encoding.UTF8.GetBytes(new string('x', 8192)))).ResourceKind);
        Assert.Equal(1, Create(RegexOptions.None, 100000).Count("x://ab"u8));
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

    private static byte[] PhaseInput(int phase) => Encoding.UTF8.GetBytes(phase switch
    {
        0 => new string('x', 200000),
        1 => new string('x', 512) + "://ab",
        // Exercise enough bounded ASCII blocks to interrupt the live loop,
        // rather than requiring one diagnostic checkpoint per scalar.
        2 => "x://" + new string('b', 200000),
        3 => "x://ab?" + new string('b', 200000),
        _ => "x://ab#" + new string('b', 200000),
    });

    [Theory]
    [InlineData(RegexOptions.IgnoreCase, 3)]
    [InlineData(RegexOptions.RightToLeft, 3)]
    [InlineData(RegexOptions.NonBacktracking, 3)]
    [InlineData(RegexOptions.ECMAScript, 1)]
    public void DeclinedOptionsRetainTheirScalarBehavior(RegexOptions options, int expected)
    {
        var regex = new Utf8Regex(Pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(expected, regex.Count("é://ab 𐐀://ab a://ab"u8));
    }
}
