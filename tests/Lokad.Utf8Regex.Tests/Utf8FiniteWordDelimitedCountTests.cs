using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteWordDelimitedCountTests
{
    private const string Pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
    private static Utf8Regex Create(RegexOptions options)
        => new(Pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 8192,
                MaxWorkingBytes = 8 << 20,
                MaxResultBytes = 1
            }
        });

    [Theory]
    [InlineData("", 0)]
    [InlineData("a@b.c", 1)]
    [InlineData("a@..", 0)]
    [InlineData("a@...", 1)]
    [InlineData("a@.b", 0)]
    [InlineData("a@.b.c", 1)]
    [InlineData("x@a@y.z", 1)]
    [InlineData("a@b.c tail d@e.f", 2)]
    [InlineData("équipe@exemple.fr", 1)]
    [InlineData("e\u0301quipe@exemple.fr", 1)]
    [InlineData("\u0301@b.c", 1)]
    [InlineData("𐐀@b.c", 1)]
    [InlineData("a@𐐀.c", 1)]
    [InlineData("😀@b.c", 0)]
    [InlineData("a@b.c😀d@e.f", 2)]
    public void ScalarCountsUseFundedExecution(string text, int expected)
    {
        var bytes = Encoding.UTF8.GetBytes("pad" + text + "tail");
        var length = Encoding.UTF8.GetByteCount(text);
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(options);
            Assert.Equal(expected, regex.Count(bytes.AsSpan(3, length)));
            Assert.Equal(expected, regex.Count(bytes.AsSpan(3, length), cancellation.Token));
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4096)]
    public void DelimiterWindowsRetainTheWholeHeadContext(int headLength)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', headLength) + "@b.c");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
            Assert.Equal(1, Create(options).Count(input));
    }

    [Fact]
    public void BmpCountsAgreeWithTheIndependentCodeUnitOracle()
    {
        const string alphabet = "ab09_@.+- é中\u0301/!";
        var random = new Random(18901);
        var oracle = new Regex(Pattern);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(options);
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
    [InlineData(RegexOptions.ECMAScript, 2)]
    public void DeclinedOptionsKeepTheirScalarContract(RegexOptions options, int expected)
    {
        var regex = new Utf8Regex(Pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 128,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            }
        });
        Assert.Equal(expected, regex.Count("équipe@exemple.fr 𐐀@b.c a@b.c"u8));
    }

    [Fact]
    public void AsciiRunBlocksKeepUnicodeScalarBoundariesAndDotNeighbours()
    {
        foreach (var word in new[] { "é", "中", "\u0301", "𐐀" })
        foreach (var length in new[] { 0, 15, 31, 2047, 2048, 4095 })
        {
            var run = new string('a', length);
            var texts = new[] { $"{word}{run}@b.c", $"a@{word}{run}.c", $"a@b.{run}{word}",
                $"a@{word}{run}.", $"a@.{run}{word}", $"{word}@{word}.{word}" };
            var expected = new[] { 1, 1, 1, 0, 0, 1 };
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
            {
                var regex = Create(options);
                for (var index = 0; index < texts.Length; index++)
                    Assert.Equal(expected[index], regex.Count(Encoding.UTF8.GetBytes(texts[index])));
            }
        }
    }
}
