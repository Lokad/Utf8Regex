using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Counts invariant-ignore-case literals whose only case-bearing scalars are
/// the one-to-one paired letters in the basic Cyrillic alphabet.
/// </summary>
internal sealed class Utf8InvariantCyrillicLiteralCountStrategy
{
    private readonly Unit[] _units;
    private readonly int _literalByteLength;
    private readonly int _anchorByteOffset;
    private readonly byte _anchorFirst;
    private readonly byte _anchorSecond;

    private Utf8InvariantCyrillicLiteralCountStrategy(
        Unit[] units,
        int literalByteLength,
        int anchorByteOffset,
        byte anchorFirst,
        byte anchorSecond)
    {
        _units = units;
        _literalByteLength = literalByteLength;
        _anchorByteOffset = anchorByteOffset;
        _anchorFirst = anchorFirst;
        _anchorSecond = anchorSecond;
    }

    public static bool TryCreate(
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout,
        [NotNullWhen(true)] out Utf8InvariantCyrillicLiteralCountStrategy? strategy)
    {
        const RegexOptions allowedOptions =
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled;
        if (pattern.Length == 0 ||
            (options & (RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) !=
                (RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            (options & ~allowedOptions) != 0 ||
            matchTimeout != Regex.InfiniteMatchTimeout)
        {
            strategy = null;
            return false;
        }

        var units = new Unit[pattern.Length];
        var byteOffset = 0;
        var anchorByteOffset = -1;
        var anchorFirst = (byte)0;
        var anchorSecond = (byte)0;
        for (var i = 0; i < pattern.Length; i++)
        {
            var value = pattern[i];
            if (value <= 0x7F)
            {
                if (char.IsAsciiLetter(value) || Utf8RegexSyntax.IsRegexMetaCharacter(value))
                {
                    strategy = null;
                    return false;
                }

                units[i] = Unit.CreateAscii(byteOffset, (byte)value);
                byteOffset++;
                continue;
            }

            if (!TryGetBasicCyrillicPair(value, out var upper, out var lower))
            {
                strategy = null;
                return false;
            }

            var upperUtf8 = EncodeTwoByteScalar(upper);
            var lowerUtf8 = EncodeTwoByteScalar(lower);
            units[i] = Unit.CreatePair(byteOffset, upperUtf8, lowerUtf8);
            if (anchorByteOffset < 0)
            {
                anchorByteOffset = byteOffset + 1;
                anchorFirst = (byte)(upperUtf8 >> 8);
                anchorSecond = (byte)(lowerUtf8 >> 8);
            }

            byteOffset += 2;
        }

        if (anchorByteOffset < 0)
        {
            strategy = null;
            return false;
        }

        strategy = new Utf8InvariantCyrillicLiteralCountStrategy(
            units,
            byteOffset,
            anchorByteOffset,
            anchorFirst,
            anchorSecond);
        return true;
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        Utf8Validation.ThrowIfInvalidOnly(input);
        if (input.Length < _literalByteLength)
        {
            return 0;
        }

        var count = 0;
        var searchIndex = _anchorByteOffset;
        var maxStart = input.Length - _literalByteLength;
        while (searchIndex < input.Length)
        {
            var relative = input[searchIndex..].IndexOfAny(_anchorFirst, _anchorSecond);
            if (relative < 0)
            {
                break;
            }

            var anchorIndex = searchIndex + relative;
            var candidate = anchorIndex - _anchorByteOffset;
            searchIndex = anchorIndex + 1;
            if ((uint)candidate > (uint)maxStart || !MatchesAt(input, candidate))
            {
                continue;
            }

            count++;
            searchIndex = candidate + _literalByteLength + _anchorByteOffset;
        }

        return count;
    }

    private bool MatchesAt(ReadOnlySpan<byte> input, int candidate)
    {
        for (var i = 0; i < _units.Length; i++)
        {
            ref readonly var unit = ref _units[i];
            var offset = candidate + unit.ByteOffset;
            if (unit.IsAscii)
            {
                if (input[offset] != (byte)unit.First)
                {
                    return false;
                }

                continue;
            }

            var actual = BinaryPrimitives.ReadUInt16LittleEndian(input[offset..]);
            if (actual != unit.First && actual != unit.Second)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetBasicCyrillicPair(char value, out char upper, out char lower)
    {
        if (value is >= '\u0410' and <= '\u042F')
        {
            upper = value;
            lower = (char)(value + 0x20);
            return true;
        }

        if (value is >= '\u0430' and <= '\u044F')
        {
            upper = (char)(value - 0x20);
            lower = value;
            return true;
        }

        if (value is '\u0401' or '\u0451')
        {
            upper = '\u0401';
            lower = '\u0451';
            return true;
        }

        upper = default;
        lower = default;
        return false;
    }

    private static ushort EncodeTwoByteScalar(char value)
    {
        var first = (byte)(0xC0 | value >> 6);
        var second = (byte)(0x80 | value & 0x3F);
        return (ushort)(first | second << 8);
    }

    private readonly record struct Unit(int ByteOffset, ushort First, ushort Second, bool IsAscii)
    {
        public static Unit CreateAscii(int byteOffset, byte value) =>
            new(byteOffset, value, value, IsAscii: true);

        public static Unit CreatePair(int byteOffset, ushort first, ushort second) =>
            new(byteOffset, first, second, IsAscii: false);
    }
}
