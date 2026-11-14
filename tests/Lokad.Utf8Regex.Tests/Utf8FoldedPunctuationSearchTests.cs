using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FoldedPunctuationSearchTests
{
    [Theory]
    [InlineData("John Watson", 4)] [InlineData("hello world", 5)]
    [InlineData("ABC@DEFGHI", 3)] [InlineData("@abcdefghij", 4)]
    [InlineData("\0abcdefghij", 5)] [InlineData("abcdefghi\0", 9)]
    [InlineData("1234567890", 5)]
    public void CorrelatedAnchorsKeepExactNonlettersAcrossBorrowedWindows(string literal, int compareIndex)
    {
        var needle = Encoding.ASCII.GetBytes(literal);
        var upper = Encoding.ASCII.GetBytes(literal.ToUpperInvariant());
        var search = new PreparedSubstringSearch(needle, ignoreCase: true);
        var alias = (byte[])upper.Clone();
        var aliasPosition = IsLetter(needle[0]) ? compareIndex : 0;
        alias[aliasPosition] ^= 0x20;
        foreach (var prefix in new[] { 0, 1, 15, 16, 17, 31, 32, 33, 63, 64, 65 })
        foreach (var tail in new[] { 0, 1, 15, 16, 31, 32 })
        foreach (var includeMatch in new[] { false, true })
        {
            byte[] value = [.. alias, .. Enumerable.Repeat((byte)'x', prefix),
                .. (includeMatch ? upper : alias), .. Enumerable.Repeat((byte)'x', tail), .. alias];
            foreach (var offset in new[] { 0, 1, 7, 31 })
            {
                var storage = Enumerable.Repeat((byte)0xff, offset + value.Length + needle.Length).ToArray();
                value.CopyTo(storage.AsSpan(offset));
                needle.CopyTo(storage.AsSpan(offset + value.Length));
                var input = storage.AsSpan(offset, value.Length);
                var expected = Find(input, needle);
                Assert.Equal(expected, search.IndexOf(input));
                var candidates = 0;
                var verifies = 0;
                Assert.Equal(expected, search.IndexOfIgnoreCaseWithMetrics(input, compareIndex, ref candidates, ref verifies));
                var expectedCount = 0;
                for (var start = 0; start <= input.Length - needle.Length;)
                {
                    var found = Find(input[start..], needle);
                    if (found < 0) break;
                    expectedCount++;
                    start += found + needle.Length;
                }
                Assert.Equal(expectedCount, search.CountIgnoreCaseWithTier(input, search.IgnoreCaseTier, out _, out _));
            }
        }

        // Every raw byte at either anchor must obey the independent ASCII fold,
        // including non-UTF8 bytes and punctuation aliases differing by bit0x20.
        var raw = Enumerable.Repeat((byte)0xfe, 64 + needle.Length + 33).ToArray();
        foreach (var position in new[] { 0, compareIndex })
        for (var value = 0; value <= 255; value++)
        {
            upper.CopyTo(raw.AsSpan(64));
            raw[64 + position] = (byte)value;
            var candidates = 0;
            var verifies = 0;
            Assert.Equal(Find(raw, needle), search.IndexOfIgnoreCaseWithMetrics(raw, compareIndex, ref candidates, ref verifies));
        }
        Assert.Equal(-1, search.IndexOf([]));
        Assert.Equal(-1, search.IndexOf(needle.AsSpan(0, needle.Length - 1)));
    }

    [Theory]
    [InlineData("John Watson")] [InlineData("hello world")]
    [InlineData("ABC@DEFGHI")] [InlineData("1234567890")]
    public void PublicFoldedPunctuationKeepsScalarResultsAndControlledTextPolicies(string literal)
    {
        var pattern = Regex.Escape(literal);
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var reference = new Regex(pattern, options);
        foreach (var prefix in new[] { 0, 31, 32, 2048 })
        foreach (var validation in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        foreach (var finite in new[] { false, true })
        {
            var text = "é😀 " + new string('x', prefix) + literal.ToUpperInvariant() + " z " + literal.ToLowerInvariant() + "\0";
            var input = Encoding.UTF8.GetBytes(text);
            var policy = finite ? new Utf8ResourcePolicy
            { MaxWorkSteps = 1_000_000, MaxDepth = 2048, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 } : default;
            var regex = new Utf8Regex(pattern, options, resourcePolicy: policy, inputValidation: validation);
            Assert.Equal(reference.Count(text), regex.Count(input));
            Assert.Equal(reference.IsMatch(text), regex.IsMatch(input));
            var expected = reference.Match(text);
            var actual = regex.Match(input);
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Length, actual.LengthInUtf16);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.IndexInBytes);
            Assert.Equal(reference.Replace(text, "X"), regex.ReplaceToString(input, "X"));
            if (validation == Utf8InputValidation.Validate)
                Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
        }
    }

    private static bool IsLetter(byte value) => Fold(value) is >= (byte)'a' and <= (byte)'z';
    private static byte Fold(byte value) => value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value + 32) : value;

    private static int Find(ReadOnlySpan<byte> input, ReadOnlySpan<byte> needle)
    {
        for (var start = 0; start <= input.Length - needle.Length; start++)
        {
            var matched = true;
            for (var i = 0; i < needle.Length; i++)
                if (Fold(input[start + i]) != Fold(needle[i])) { matched = false; break; }
            if (matched) return start;
        }
        return -1;
    }
}
