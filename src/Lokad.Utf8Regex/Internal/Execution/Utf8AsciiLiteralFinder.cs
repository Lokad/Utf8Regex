namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8AsciiLiteralFinderKind : byte
{
    Empty = 0,
    SingleByte = 1,
    DoubleByte = 2,
    FirstByteThenTail = 3,
    SpanIndexOf = 4,
}

internal readonly struct Utf8AsciiLiteralFinder
{
    private readonly byte[] _literal;
    private readonly Utf8AsciiLiteralFinderKind _kind;

    public Utf8AsciiLiteralFinder(ReadOnlySpan<byte> literal)
    {
        _literal = literal.ToArray();
        _kind = literal.Length switch
        {
            0 => Utf8AsciiLiteralFinderKind.Empty,
            1 => Utf8AsciiLiteralFinderKind.SingleByte,
            2 => Utf8AsciiLiteralFinderKind.DoubleByte,
            <= 8 => Utf8AsciiLiteralFinderKind.FirstByteThenTail,
            _ => Utf8AsciiLiteralFinderKind.SpanIndexOf,
        };
    }

    public ReadOnlySpan<byte> Literal => _literal;

    public bool TryFindNext(ReadOnlySpan<byte> input, int startIndex, out int index)
    {
        index = -1;
        if ((uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        var relative = IndexOf(input[startIndex..]);
        if (relative < 0)
        {
            return false;
        }

        index = startIndex + relative;
        return true;
    }

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        return _kind switch
        {
            Utf8AsciiLiteralFinderKind.Empty => 0,
            Utf8AsciiLiteralFinderKind.SingleByte => input.IndexOf(_literal[0]),
            Utf8AsciiLiteralFinderKind.DoubleByte => IndexOf2(input, _literal[0], _literal[1]),
            Utf8AsciiLiteralFinderKind.FirstByteThenTail => IndexOfFirstByteThenTail(input),
            Utf8AsciiLiteralFinderKind.SpanIndexOf => input.IndexOf(_literal),
            _ => -1,
        };
    }

    private int IndexOfFirstByteThenTail(ReadOnlySpan<byte> input)
    {
        var literal = _literal;
        var start = 0;
        while ((uint)start <= (uint)(input.Length - literal.Length))
        {
            var relative = input[start..].IndexOf(literal[0]);
            if (relative < 0)
            {
                return -1;
            }

            start += relative;
            if (input[start..].StartsWith(literal))
            {
                return start;
            }

            start++;
        }

        return -1;
    }

    private static int IndexOf2(ReadOnlySpan<byte> input, byte first, byte second)
    {
        var index = 0;
        while (index < input.Length - 1)
        {
            var relative = input[index..].IndexOf(first);
            if (relative < 0)
            {
                return -1;
            }

            index += relative;
            if (input[index + 1] == second)
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}
