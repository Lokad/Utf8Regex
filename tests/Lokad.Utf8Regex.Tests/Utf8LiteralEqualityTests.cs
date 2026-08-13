using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralEqualityTests
{
    [Fact]
    public void FastShortEqualityExhaustsLengthsBoundsAndMismatchPositions()
    {
        for (var length = 0; length <= 40; length++)
        {
            var literal = Enumerable.Range(0, length)
                .Select(static value => (byte)(value * 17 + 3))
                .ToArray();
            var input = new byte[length + 4];
            literal.CopyTo(input, 2);

            Assert.True(Utf8LiteralEquality.EqualsAt(
                input,
                2,
                literal,
                Utf8LiteralComparisonKind.FastShort));
            Assert.False(Utf8LiteralEquality.EqualsAt(
                input,
                -1,
                literal,
                Utf8LiteralComparisonKind.FastShort));
            Assert.False(Utf8LiteralEquality.EqualsAt(
                input,
                input.Length - literal.Length + 1,
                literal,
                Utf8LiteralComparisonKind.FastShort));

            for (var mismatch = 0; mismatch < length; mismatch++)
            {
                input[mismatch + 2] ^= 0x5A;
                Assert.False(Utf8LiteralEquality.EqualsAt(
                    input,
                    2,
                    literal,
                    Utf8LiteralComparisonKind.FastShort));
                input[mismatch + 2] ^= 0x5A;
            }
        }
    }

    [Fact]
    public void KnownPrefixEqualityChecksEveryRemainingByte()
    {
        var literal = Enumerable.Range(0, 32).Select(static value => (byte)(value + 1)).ToArray();
        var input = new byte[40];
        literal.CopyTo(input, 3);

        for (var prefixLength = 0; prefixLength <= literal.Length; prefixLength++)
        {
            Assert.True(Utf8LiteralEquality.EqualsAtKnownPrefix(
                input,
                3,
                literal,
                prefixLength,
                Utf8LiteralComparisonKind.FastShort));

            if (prefixLength < literal.Length)
            {
                input[3 + prefixLength] ^= 0x7F;
                Assert.False(Utf8LiteralEquality.EqualsAtKnownPrefix(
                    input,
                    3,
                    literal,
                    prefixLength,
                    Utf8LiteralComparisonKind.FastShort));
                input[3 + prefixLength] ^= 0x7F;
            }
        }

        Assert.False(Utf8LiteralEquality.EqualsAtKnownPrefix(
            input,
            3,
            literal,
            literal.Length + 1,
            Utf8LiteralComparisonKind.FastShort));
    }
}
