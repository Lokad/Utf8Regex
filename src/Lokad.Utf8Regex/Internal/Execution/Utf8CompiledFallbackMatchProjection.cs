using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledFallbackMatchProjection
{
    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, Regex regex)
    {
        var decoded = Encoding.UTF8.GetString(input);
        var fallbackMatch = regex.Match(decoded);
        if (!fallbackMatch.Success)
        {
            return Utf8ValueMatch.NoMatch;
        }

        var boundaryMap = Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        if (boundaryMap.TryGetByteRange(fallbackMatch.Index, fallbackMatch.Length, out var indexInBytes, out var lengthInBytes))
        {
            return new Utf8ValueMatch(true, true, fallbackMatch.Index, fallbackMatch.Length, indexInBytes, lengthInBytes);
        }

        return new Utf8ValueMatch(true, false, fallbackMatch.Index, fallbackMatch.Length);
    }
}
