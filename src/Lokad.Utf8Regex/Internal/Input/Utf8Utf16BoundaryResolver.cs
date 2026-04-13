namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8Utf16BoundaryResolver
{
    public static Utf16Boundary ResolveBoundary(ReadOnlySpan<byte> input, int targetUtf16Offset)
    {
        return Utf8InputAnalyzer.Analyze(input).BoundaryMap.Resolve(targetUtf16Offset);
    }
}
