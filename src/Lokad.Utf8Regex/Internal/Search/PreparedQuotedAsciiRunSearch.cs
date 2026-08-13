namespace Lokad.Utf8Regex.Internal.Search;

internal readonly struct PreparedQuotedAsciiRunSearch
{
    public PreparedQuotedAsciiRunSearch(Utf8SearchAsciiSet asciiSet, int runLength)
    {
        if (!asciiSet.HasValue)
        {
            throw new ArgumentException("The ASCII set must not be empty.", nameof(asciiSet));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runLength);
        AsciiSet = asciiSet;
        RunLength = runLength;
    }

    public Utf8SearchAsciiSet AsciiSet { get; }

    public int RunLength { get; }

    public int MatchLength => RunLength + 2;

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        for (var i = 0; i <= input.Length - MatchLength; i++)
        {
            if (IsMatchAt(input, i))
            {
                return i;
            }
        }

        return -1;
    }

    public int LastIndexOf(ReadOnlySpan<byte> input)
    {
        for (var i = input.Length - MatchLength; i >= 0; i--)
        {
            if (IsMatchAt(input, i))
            {
                return i;
            }
        }

        return -1;
    }

    public bool IsMatchAt(ReadOnlySpan<byte> input, int index)
    {
        if ((uint)index > (uint)(input.Length - MatchLength))
        {
            return false;
        }

        var quote = input[index];
        if (quote is not ((byte)'"' or (byte)'\''))
        {
            return false;
        }

        for (var i = 0; i < RunLength; i++)
        {
            var value = input[index + 1 + i];
            if (!AsciiSet.Contains(value))
            {
                return false;
            }
        }

        return input[index + 1 + RunLength] == quote;
    }
}
