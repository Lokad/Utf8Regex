using System.Buffers;
using System.Text;
using System.Text.Unicode;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValidationTests
{
    [Fact]
    public void EveryByteTagAtThreeByteBlockBoundariesKeepsTheFirstError()
    {
        var tail = Encoding.UTF8.GetBytes(new string('中', 80) + "é😀");
        foreach (var prefix in new[] { 0, 1, 28, 29, 30, 31, 32, 60, 61, 62, 63, 64, 125, 126, 127, 128, 255, 256, 257 })
        {
            var input = new byte[prefix + 3 + tail.Length];
            input.AsSpan(0, prefix).Fill((byte)'x');
            tail.CopyTo(input, prefix + 3);
            foreach (var second in new byte[] { 0x80, 0x9f, 0xa0, 0xbf })
            for (var tag = 0; tag < 256; tag++)
            {
                input[prefix] = (byte)tag;
                input[prefix + 1] = second;
                input[prefix + 2] = 0x80;
                var expected = Utf8.IsValid(input);
                Assert.Equal(expected, Utf8Validation.TryValidateOnly(input, out var vectorError));
                if (!expected)
                {
                    var validPrefix = tag < 128 ? 1 : tag is >= 0xc2 and <= 0xdf ? 2 : 0;
                    Assert.Equal(prefix + validPrefix, vectorError);
                }
            }
        }
    }

    [Fact]
    public void ThrowIfInvalidRejectsTruncatedSequence()
    {
        var invalid = new byte[] { 0xE2, 0x82 };

        var error = Assert.Throws<ArgumentException>(() => Utf8Validation.ThrowIfInvalidOnly(invalid));

        Assert.Contains("byte offset 0", error.Message);
    }

    [Fact]
    public void ThrowIfInvalidRejectsInvalidContinuation()
    {
        var invalid = new byte[] { 0x61, 0xE2, 0x28, 0xA1 };

        var error = Assert.Throws<ArgumentException>(() => Utf8Validation.ThrowIfInvalidOnly(invalid));

        Assert.Contains("byte offset 1", error.Message);
    }

    [Theory]
    [InlineData(0xE0, 0xA0, 0x9F)]
    [InlineData(0xED, 0x9F, 0xA0)]
    public void ThrowIfInvalidOnlyConsumesConsecutiveConstrainedThreeByteScalars(
        byte lead,
        byte validSecond,
        byte invalidSecond)
    {
        var input = Enumerable.Repeat((byte)'a', 37)
            .Concat(Enumerable.Range(0, 8).SelectMany(_ => new[] { lead, validSecond, (byte)0x80 }))
            .Concat(Enumerable.Repeat((byte)'b', 37))
            .ToArray();

        Utf8Validation.ThrowIfInvalidOnly(input);

        for (var scalarIndex = 1; scalarIndex < 8; scalarIndex++)
        {
            var scalarOffset = 37 + scalarIndex * 3;
            var malformedSecond = input.ToArray();
            malformedSecond[scalarOffset + 1] = invalidSecond;
            var secondError = Assert.Throws<ArgumentException>(
                () => Utf8Validation.ThrowIfInvalidOnly(malformedSecond));
            Assert.Contains($"byte offset {scalarOffset}", secondError.Message);

            var malformedThird = input.ToArray();
            malformedThird[scalarOffset + 2] = (byte)'x';
            var thirdError = Assert.Throws<ArgumentException>(
                () => Utf8Validation.ThrowIfInvalidOnly(malformedThird));
            Assert.Contains($"byte offset {scalarOffset}", thirdError.Message);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ValidityMatchesScalarDecoderAtEveryWideLaneAndSliceOffset(int sliceOffset)
    {
        var scalars = new[] { "é", "中", "ࠀ", "\uD7FF", "😀", "\U0010FFFF" };
        foreach (var scalar in scalars)
        for (var prefix = 0; prefix < 128; prefix++)
        {
            var bytes = Encoding.UTF8.GetBytes(new string('a', prefix) + scalar + new string('b', 128));
            var storage = new byte[sliceOffset + bytes.Length + 3];
            bytes.CopyTo(storage, sliceOffset);
            var input = storage.AsSpan(sliceOffset, bytes.Length);
            Assert.True(Utf8Validation.TryValidateOnly(input, out var validOffset));
            Assert.Equal(-1, validOffset);
            for (var index = prefix; index < prefix + Encoding.UTF8.GetByteCount(scalar); index++)
            {
                var original = input[index];
                foreach (var replacement in new byte[] { 0, 0x7F, 0x80, 0xBF, 0xC0, 0xC1, 0xC2, 0xDF, 0xE0, 0xFF })
                {
                    input[index] = replacement;
                    var expectedOffset = ScalarErrorOffset(input);
                    var expected = expectedOffset < 0;
                    var actual = Utf8Validation.TryValidateOnly(input, out var actualOffset);
                    Assert.True(expected == actual && expectedOffset == actualOffset,
                        $"slice={sliceOffset}, prefix={prefix}, scalar={scalar}, index={index}, byte={replacement:X2}: " +
                        $"expected={expected}/{expectedOffset}, actual={actual}/{actualOffset}");
                }
                input[index] = original;
            }
        }
    }

    private static int ScalarErrorOffset(ReadOnlySpan<byte> input)
    {
        for (var offset = 0; offset < input.Length;)
        {
            if (Rune.DecodeFromUtf8(input[offset..], out _, out var consumed) != OperationStatus.Done)
                return offset;
            offset += consumed;
        }
        return -1;
    }

    [Fact]
    public void ThrowIfInvalidOnlyMatchesBclForRandomByteSequences()
    {
        var random = new Random(0x5EED);
        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var input = new byte[random.Next(0, 160)];
            random.NextBytes(input);

            var error = Record.Exception(() => Utf8Validation.ThrowIfInvalidOnly(input));
            if (Utf8.IsValid(input))
            {
                Assert.Null(error);
            }
            else
            {
                Assert.IsType<ArgumentException>(error);
            }
        }
    }

    [Fact]
    public void ScalarStartRejectsEveryContinuationByte()
    {
        var input = "aé€😀z"u8.ToArray();

        for (var offset = 0; offset < input.Length; offset++)
        {
            if ((input[offset] & 0xC0) == 0x80)
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => ValidateBytePosition(input, offset));
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("a😀b")]
    [InlineData("é€😀")]
    [InlineData("\U00010000\U0010ffff")]
    [InlineData("e\u0301👩‍💻")]
    public void BytePositionAdvancesByScalar(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var boundaries = new List<(int Bytes, int Utf16)> { (0, 0) };
        foreach (var scalar in text.EnumerateRunes())
        {
            var last = boundaries[^1];
            boundaries.Add((last.Bytes + scalar.Utf8SequenceLength, last.Utf16 + scalar.Utf16SequenceLength));
        }
        Assert.Equal((input.Length, text.Length), boundaries[^1]);

        var position = Utf8BytePosition.CreateScalarChecked(input, 0, "position");
        for (var index = 0; index < boundaries.Count; index++)
        {
            Assert.Equal(boundaries[index].Bytes, position.Value);
            if (index == boundaries.Count - 1) break;
            Assert.True(position.TryAdvanceScalar(input, out var next));
            Assert.True(next.Value > position.Value);
            position = next;
        }
        Assert.False(position.TryAdvanceScalar(input, out _));
    }

    private static void ValidateBytePosition(byte[] input, int offset)
    {
        _ = Utf8BytePosition.CreateScalarChecked(input, offset, "startOffsetInBytes");
    }

    [Fact]
    public void ThrowIfInvalidOnlyAgreesWithBclOracleOnAdversarialLeads()
    {
        // guards the SIMD lead-classification matrix (ASCII,
        // two-byte, safe/constrained three-byte, four-byte, overlong and
        // surrogate leads) at every 32-byte alignment reviewers of the
        // validator may disturb.
        var random = new Random(0xC10);
        var pool = new byte[] { 0x41, 0x7F, 0xC2, 0xDF, 0xE0, 0xE1, 0xEC, 0xED, 0xEE, 0xEF, 0xF0, 0xF4, 0x80, 0xBF, 0xA0, 0x9F, 0xC0, 0xC1, 0xF5, 0xFF, 0xFE };
        for (var trial = 0; trial < 4000; trial++)
        {
            var length = 1 + random.Next(96);
            var buffer = new byte[length];
            for (var i = 0; i < length; i++)
            {
                buffer[i] = pool[random.Next(pool.Length)];
            }

            var padLeft = random.Next(40);
            var input = new byte[padLeft + length];
            Array.Fill(input, (byte)0x78);
            Array.Copy(buffer, 0, input, padLeft, length);
            var expected = Utf8.IsValid(input);
            var actual = IsValidViaThrow(input);
            Assert.True(
                expected == actual,
                "trial " + trial + ": expected " + expected + " but validated " + actual + " for " + BitConverter.ToString(input));
        }
    }

    private static bool IsValidViaThrow(byte[] input)
    {
        try
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
