using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ExactLiteralReplacement
{
    // A miss lets the caller publish the unchanged subject with its own
    // output admission and lifetime.
    internal static byte[]? Replace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal,
        ReadOnlySpan<byte> replacement,
        int maximumReplacementCount,
        ref Utf8SliceInvocation invocation,
        ref Utf8InvocationResultLedger results,
        out int replacementCount)
    {
        if (literal.IsEmpty)
        {
            throw new ArgumentException("The exact replacement literal must not be empty.", nameof(literal));
        }

        if (maximumReplacementCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumReplacementCount));
        }

        var control = new Utf8InvocationBufferControl(ref invocation);
        replacementCount = 0;
        // Short substitutions already find these positions while sizing
        // the output. Keep them on the stack for the copy pass.
        Span<int> matchStarts = stackalloc int[8];
        var remaining = input;
        while (replacementCount < maximumReplacementCount)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var index = remaining.IndexOf(literal);
            if (index < 0)
            {
                break;
            }

            if (replacementCount < matchStarts.Length)
                matchStarts[replacementCount] = input.Length - remaining.Length + index;
            replacementCount++;
            remaining = remaining[(index + literal.Length)..];
        }

        if (replacementCount == 0) return null;

        var outputLength = checked(input.Length + replacementCount * (replacement.Length - literal.Length));
        var output = results.AllocateArray<byte>(outputLength, ref invocation);
        var sourcePosition = 0;
        var destinationPosition = 0;
        var copied = 0;
        while (copied < replacementCount)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var relativeIndex = copied < matchStarts.Length
                ? matchStarts[copied] - sourcePosition
                : input[sourcePosition..].IndexOf(literal);
            if (relativeIndex < 0)
            {
                throw new InvalidOperationException(
                    "The exact replacement copy pass lost a preflighted match.");
            }

            Utf8InvocationOutput.Copy(input.Slice(sourcePosition, relativeIndex), output.AsSpan(destinationPosition), control);
            destinationPosition += relativeIndex;
            Utf8InvocationOutput.Copy(replacement, output.AsSpan(destinationPosition), control);
            destinationPosition += replacement.Length;
            sourcePosition += relativeIndex + literal.Length;
            copied++;
        }

        Utf8InvocationOutput.Copy(input[sourcePosition..], output.AsSpan(destinationPosition), control);
        return output;
    }
}
