using Lokad.Utf8Regex.Internal.Execution;
using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8AsciiCultureInvariantReplacement
{
    public static byte[] Replace(
        Utf8AsciiCultureInvariantStrategy strategy,
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement)
    {
        if (replacement.IsLiteral)
        {
            var cursor = strategy.CreateMatchCursor(input);
            return Utf8CursorReplaceEngine.Replace(input, replacement.LiteralUtf8, ref cursor);
        }

        return Encoding.UTF8.GetBytes(strategy.FallbackRegex.Replace(
            Encoding.UTF8.GetString(input),
            replacement.OriginalText));
    }

    public static string ReplaceToString(
        Utf8AsciiCultureInvariantStrategy strategy,
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement) =>
        strategy.FallbackRegex.Replace(Encoding.UTF8.GetString(input), replacement.OriginalText);

    public static OperationStatus TryReplace(
        Utf8AsciiCultureInvariantStrategy strategy,
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement,
        Span<byte> destination,
        out int bytesWritten)
    {
        if (replacement.IsLiteral)
        {
            var cursor = strategy.CreateMatchCursor(input);
            return Utf8CursorReplaceEngine.TryReplace(
                input,
                replacement.LiteralUtf8,
                destination,
                ref cursor,
                out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        var result = Replace(strategy, input, replacement);
        if (result.Length <= destination.Length)
        {
            result.CopyTo(destination);
            bytesWritten = result.Length;
            return OperationStatus.Done;
        }

        bytesWritten = 0;
        return OperationStatus.DestinationTooSmall;
    }
}
