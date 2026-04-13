using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Utilities;

internal readonly struct PreparedQuotedAsciiRunSearch
{
    public PreparedQuotedAsciiRunSearch(string asciiSet, int runLength)
    {
        ArgumentException.ThrowIfNullOrEmpty(asciiSet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runLength);
        AsciiSet = asciiSet;
        RunLength = runLength;
    }

    public string AsciiSet { get; }

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
            if (value >= 128 || !RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, AsciiSet))
            {
                return false;
            }
        }

        return input[index + 1 + RunLength] == quote;
    }
}
