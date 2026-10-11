using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ExactLiteralReplacement
{
    internal static byte[] Replace(
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
        var remaining = input;
        while (replacementCount < maximumReplacementCount)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var index = remaining.IndexOf(literal);
            if (index < 0)
            {
                break;
            }

            replacementCount++;
            remaining = remaining[(index + literal.Length)..];
        }

        var outputLength = checked(input.Length + replacementCount * (replacement.Length - literal.Length));
        var output = results.AllocateArray<byte, Utf8InvocationBufferControl>(outputLength, control);
        var sourcePosition = 0;
        var destinationPosition = 0;
        var remainingReplacements = replacementCount;
        while (remainingReplacements > 0)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var relativeIndex = input[sourcePosition..].IndexOf(literal);
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
            remainingReplacements--;
        }

        Utf8InvocationOutput.Copy(input[sourcePosition..], output.AsSpan(destinationPosition), control);
        return output;
    }
}
