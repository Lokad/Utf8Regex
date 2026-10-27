using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WhitespacePrefixCountTests
{
    [Theory]
    [InlineData(@"(\s*)#x")]
    [InlineData(@"\s*#")]
    [InlineData(@"((\s*))#(?:x|y)+")]
    [InlineData(@"(\s*)#\1X")]
    [InlineData(@"(?<s>\s*)#\k<s>X")]
    [InlineData(@"(\s*)#(?(1)X|Y)")]
    [InlineData(@"(\s*)()#X")]
    [InlineData(@"(\s*)#(?<=#)X")]
    [InlineData(@"(\s*)#X(?=\n|\z)")]
    [InlineData(@"\s*#a\s*#b")]
    [InlineData(@"(\s*)((?:# [Nn][Oo][Qq][Aa])(?::\s?(([A-Z]+[0-9]+(?:[,\s]+)?)+))?)")]
    public void CandidateWindowsPreserveCapturesReferencesAndContext(string pattern)
    {
        var random = new Random(314159);
        string[] parts = ["x", " ", "\t", "\r\n", "\u0085", "\u00a0", "\u2003", "😀", "#", "# X", "#x", "#a #b", "# noqa: F401,E123", "Y"];
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(pattern, options);
            var oracle = new Regex(pattern, options & ~RegexOptions.Compiled);
            for (var sample = 0; sample < 30; sample++)
            {
                var text = new string('z', 300) + string.Concat(Enumerable.Range(0, 80).Select(_ => parts[random.Next(parts.Length)]));
                var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
                Assert.Equal(oracle.Count(text), regex.Count(storage.AsSpan(7, Encoding.UTF8.GetByteCount(text))));
            }
        }
    }

    [Fact]
    public void BackreferenceCanMakeALaterWhitespaceStartWin()
    {
        var regex = new Utf8Regex(@"(\s*)#\1X");
        var input = Encoding.UTF8.GetBytes(new string('z', 300) + "  # X");
        Assert.Equal(1, regex.Count(input));
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, cancellation.Token));
    }

    [Fact]
    public void ControlsAndMalformedTailsRemainEnforced()
    {
        const string pattern = @"(\s*)#x";
        var input = Encoding.UTF8.GetBytes(new string('z', 300) + "\u00a0 #x");
        var bounded = new Utf8Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => bounded.Count(input));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => new Utf8Regex(pattern).Count(input, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        byte[] malformed = [.. input, 0xF0, 0x80];
        Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern).Count(malformed));
    }
}
