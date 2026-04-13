namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiUntilByteStarExecutor
{
    public static int Count(ReadOnlySpan<byte> input, byte terminatorByte)
    {
        var count = 1; // Empty match at end of input.
        var start = 0;
        while (start < input.Length)
        {
            var relative = input[start..].IndexOf(terminatorByte);
            if (relative < 0)
            {
                count++;
                break;
            }

            if (relative > 0)
            {
                count++;
            }

            count++;
            start += relative + 1;
        }

        return count;
    }
}
