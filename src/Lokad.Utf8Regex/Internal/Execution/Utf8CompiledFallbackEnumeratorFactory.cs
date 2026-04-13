using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledFallbackEnumeratorFactory
{
    public static Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Regex regex)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), regex, analysis.BoundaryMap);
    }

    public static Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Regex regex, int count)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), regex, count, analysis.BoundaryMap);
    }
}
