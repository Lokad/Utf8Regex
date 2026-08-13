using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackStartTransformExecutor
{
    public static int Apply(this Utf8FallbackStartTransform transform, ReadOnlySpan<byte> input, int matchIndex)
    {
        var start = matchIndex - transform.Offset;
        if ((uint)start > (uint)input.Length)
        {
            return -1;
        }

        if (transform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace)
        {
            while (start > 0 && IsAsciiWhitespace(input[start - 1]))
            {
                start--;
            }
        }

        return start;
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C;
    }
}
