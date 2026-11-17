using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Test-side text projection only. Every caller executes SplitRanges directly.
internal static class PythonReSplitAssert
{
    public static string?[] Decode(ReadOnlySpan<byte> input, ReadOnlySpan<Utf8PythonCaptureRange> ranges)
    {
        var values = new string?[ranges.Length];
        for (var index = 0; index < ranges.Length; index++)
        {
            var range = ranges[index];
            values[index] = range.Success
                ? Encoding.UTF8.GetString(input[range.StartOffsetInBytes..range.EndOffsetInBytes]) : null;
        }
        return values;
    }
}
