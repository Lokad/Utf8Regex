using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralEqualityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(31)]
    public void LongerLiteralsDeclineBeforeSlicing(int inputLength)
    {
        var storage = new byte[inputLength + 40];
        var input = storage.AsSpan(7, inputLength);
        var literal = new byte[inputLength + 1];
        foreach (var kind in new[] { Utf8LiteralComparisonKind.Scalar, Utf8LiteralComparisonKind.FastShort })
        foreach (var index in new[] { -1, 0, inputLength, inputLength + 1, int.MaxValue })
        {
            Assert.False(Utf8LiteralEquality.EqualsAt(input, index, literal, kind));
            for (var prefix = 0; prefix <= literal.Length; prefix++)
                Assert.False(Utf8LiteralEquality.EqualsAtKnownPrefix(input, index, literal, prefix, kind));
        }
    }

    [Fact]
    public void PreparedUnequalLiteralFamilyDeclinesTruncatedCandidates()
    {
        Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(
            ["abcdefgh"u8.ToArray(), "xy"u8.ToArray(), "zt"u8.ToArray()], out var search));
        foreach (var input in new[] { "ab"u8.ToArray(), "abc"u8.ToArray() })
        {
            Assert.Equal(0, search.Count(input));
            Assert.Equal(0, search.CountScalar(input));
            Assert.False(search.TryFindFirst(input, out _, out _));
            var start = 0;
            Assert.False(search.TryFindNextNonOverlapping(input, ref start, out _, out _));
        }
    }

    [Fact]
    public void PublicUnequalLiteralFamilyDeclinesTruncatedCandidates()
    {
        var input = "abcde"u8.ToArray();
        foreach (var mode in new[] { System.Text.RegularExpressions.RegexOptions.None, System.Text.RegularExpressions.RegexOptions.Compiled })
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8Regex("abcdefgh|xyzzz|ztzzz", mode,
                System.Text.RegularExpressions.Regex.InfiniteMatchTimeout,
                finite ? new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1024,
                    MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 } : Utf8ResourcePolicy.Unlimited);
            Assert.False(regex.IsMatch(input));
            Assert.Equal(0, regex.Count(input));
            Assert.False(regex.Match(input).Success);
        }
    }

    [Fact]
    public void EqualityRespectsUnalignedViewsAndEveryDispatchTail()
    {
        for (var length = 0; length <= 40; length++)
        for (var offset = 0; offset <= 32; offset++)
        {
            var literal = Enumerable.Range(0, length).Select(value => (byte)(value * 17 + 3)).ToArray();
            var storage = Enumerable.Repeat((byte)0xA5, offset + length + 33).ToArray();
            literal.CopyTo(storage, offset);
            var view = storage.AsSpan(offset, length);
            Assert.True(Utf8LiteralEquality.EqualsAt(view, 0, literal, Utf8LiteralComparisonKind.FastShort));
            Assert.False(Utf8LiteralEquality.EqualsAt(view, 1, literal, Utf8LiteralComparisonKind.FastShort));
            for (var prefix = 0; prefix <= length; prefix++)
                Assert.True(Utf8LiteralEquality.EqualsAtKnownPrefix(
                    view, 0, literal, prefix, Utf8LiteralComparisonKind.FastShort));
            if (length == 0) continue;
            view[^1] ^= 0x80;
            Assert.False(view.SequenceEqual(literal));
            Assert.False(Utf8LiteralEquality.EqualsAt(view, 0, literal, Utf8LiteralComparisonKind.FastShort));
            Assert.False(Utf8LiteralEquality.EqualsAtKnownPrefix(
                view, 0, literal, length - 1, Utf8LiteralComparisonKind.FastShort));
        }
    }

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
