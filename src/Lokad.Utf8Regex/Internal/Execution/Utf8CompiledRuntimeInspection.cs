namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledRuntimeInspection
{
    public static bool TryCountValidatedThreeByte(
        Utf8CompiledEngineRuntime runtime,
        ReadOnlySpan<byte> input,
        out int count)
    {
        var literal = GetLiteralRuntime(runtime);
        return literal is not null && literal.TryInspectCountValidatedThreeByte(input, out count) ||
            ReturnNoCount(out count);
    }

    public static bool TryCountLeadingScalarAnchored(
        Utf8CompiledEngineRuntime runtime,
        ReadOnlySpan<byte> input,
        out int count)
    {
        var literal = GetLiteralRuntime(runtime);
        return literal is not null && literal.TryInspectCountLeadingScalarAnchored(input, out count) ||
            ReturnNoCount(out count);
    }

    public static bool TryCountPreparedSearch(
        Utf8CompiledEngineRuntime runtime,
        ReadOnlySpan<byte> input,
        out int count)
    {
        var literal = GetLiteralRuntime(runtime);
        return literal is not null && literal.TryInspectCountPreparedSearch(input, out count) ||
            ReturnNoCount(out count);
    }

    public static bool TryCountAnchored(
        Utf8CompiledEngineRuntime runtime,
        ReadOnlySpan<byte> input,
        out int count)
    {
        var literal = GetLiteralRuntime(runtime);
        return literal is not null && literal.TryInspectCountAnchored(input, out count) ||
            ReturnNoCount(out count);
    }

    public static bool TryMatchAsciiLiteralFamily(
        Utf8CompiledEngineRuntime runtime,
        ReadOnlySpan<byte> input,
        out int index,
        out int matchedByteLength)
    {
        var literal = GetLiteralRuntime(runtime);
        if (literal is not null)
        {
            return literal.TryInspectMatchAsciiLiteralFamily(input, out index, out matchedByteLength);
        }

        index = -1;
        matchedByteLength = 0;
        return false;
    }

    public static bool TryIsMatchLiteralFamily(
        Utf8CompiledEngineRuntime runtime,
        ReadOnlySpan<byte> input,
        out bool isMatch)
    {
        var literal = GetLiteralRuntime(runtime);
        if (literal is not null)
        {
            return literal.TryInspectIsMatchLiteralFamily(input, out isMatch);
        }

        isMatch = false;
        return false;
    }

    public static bool TryIsMatchExactLiteral(
        Utf8CompiledEngineRuntime runtime,
        ReadOnlySpan<byte> input,
        out bool isMatch)
    {
        var literal = GetLiteralRuntime(runtime);
        if (literal is not null)
        {
            return literal.TryInspectIsMatchExactLiteral(input, out isMatch);
        }

        isMatch = false;
        return false;
    }

    private static Utf8LiteralCompiledEngineRuntime? GetLiteralRuntime(Utf8CompiledEngineRuntime runtime)
    {
        return runtime switch
        {
            Utf8LiteralCompiledEngineRuntime direct => direct,
            Utf8ExactLiteralCompiledEngineRuntime exact => exact.Inner,
            Utf8LiteralFamilyCompiledEngineRuntime family => family.Inner,
            _ => null,
        };
    }

    private static bool ReturnNoCount(out int count)
    {
        count = 0;
        return false;
    }
}
