using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class RegexBenchmarkResult
{
    public static byte[] ReplaceUtf8(Regex regex, string input, string replacement)
    {
        return Encoding.UTF8.GetBytes(regex.Replace(input, replacement));
    }

    public static int ReplaceUtf8Length(Regex regex, string input, string replacement)
    {
        return ReplaceUtf8(regex, input, replacement).Length;
    }
}
