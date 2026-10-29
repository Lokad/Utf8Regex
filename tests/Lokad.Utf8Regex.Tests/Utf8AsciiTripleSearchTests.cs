using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiTripleSearchTests
{
    [Theory]
    [InlineData("record", "struct", "class")]
    [InlineData("abca", "abcd", "abce")]
    [InlineData("abcd", "bcde", "cdef")]
    public void PackedTriplesRetainOrderedRangesAndBothScanDirections(string first, string second, string third)
    {
        var literals = new[] { first, second, third };
        var search = new PreparedMultiLiteralSearch(literals.Select(Encoding.ASCII.GetBytes).ToArray(), false);
        Assert.Equal(PreparedMultiLiteralKind.ExactPacked, search.Kind);
        var pattern = string.Join('|', literals.Select(Regex.Escape));
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options); var oracle = new Regex(pattern, options);
            foreach (var text in new[] { "", "abc", "abcx", first, second, third, "x" + third, second + first,
                first + second + third, "abcabcabcdabcde", "😀" + third + "é" + first + third[..^1], "record struct class", "recordx classx" })
            {
                var input = Encoding.UTF8.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var expected = oracle.Match(text); var actual = regex.Match(storage.AsSpan(1, input.Length));
                Assert.Equal(expected.Success, actual.Success);
                Assert.Equal(oracle.Count(text), regex.Count(input));
                if (!actual.Success) continue;
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.IndexInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), actual.LengthInBytes);
            }
        }
    }

    [Theory]
    [InlineData(2043)] [InlineData(2044)] [InlineData(2047)] [InlineData(2048)]
    public void StructuralVerificationKeepsBoundariesUnicodeWhitespaceAndOriginalBudgets(int prefixLength)
    {
        const string pattern = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, policy);
        var text = new string('!', prefixLength) + "class Foo record Ab struct Bc éclass Xx 😀class Cc struct\u2003Dd class Z";
        var input = Encoding.UTF8.GetBytes(text); using var token = new CancellationTokenSource();
        var expected = new Regex(pattern, RegexOptions.CultureInvariant).Count(text);
        Assert.Equal(expected, regex.Count(input, token.Token));
        var cursor = regex.EnumerateMatches(input); var count = 0;
        while (cursor.MoveNext()) count++;
        Assert.Equal(expected, count);
        byte[] invalid = [.. input, 0xff]; Assert.Throws<ArgumentException>(() => regex.Count(invalid));
        token.Cancel();
        Assert.Equal(token.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(invalid, token.Token)).CancellationToken);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern,
                RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, policy with { MaxWorkSteps = 1 }).Count(input)).ResourceKind);
    }

    [Fact]
    public void AmbiguousDiscriminatorsAndLongAsciiFamiliesRetainExistingSearches()
    {
        var prefixes = new PreparedMultiLiteralSearch(["abcd"u8.ToArray(), "abcde"u8.ToArray(), "abcdef"u8.ToArray()], false);
        Assert.NotEqual(PreparedMultiLiteralKind.ExactPacked, prefixes.Kind);
        var longer = new PreparedMultiLiteralSearch(["abcdefgh"u8.ToArray(), "ijklmnop"u8.ToArray(), "qrstuvwx"u8.ToArray()], false);
        Assert.Equal(PreparedMultiLiteralKind.ExactEarliest, longer.Kind);
        Assert.Equal(0, new Utf8Regex("abcd|abcde|abcdef").Match("abcdef"u8).IndexInBytes);
        Assert.Equal(4, new Utf8Regex("abcd|abcde|abcdef").Match("abcdef"u8).LengthInBytes);
    }
}
