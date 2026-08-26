using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ExactLiteralReplacement
{
    internal static byte[] Replace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal,
        ReadOnlySpan<byte> replacement,
        int maximumReplacementCount,
        Utf8ExecutionDeadline budget,
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

        replacementCount = 0;
        var remaining = input;
        while (replacementCount < maximumReplacementCount)
        {
            budget.Step();
            var index = remaining.IndexOf(literal);
            if (index < 0)
            {
                break;
            }

            replacementCount++;
            remaining = remaining[(index + literal.Length)..];
        }

        if (replacementCount == 0)
        {
            return input.ToArray();
        }

        var outputLength = checked(input.Length + replacementCount * (replacement.Length - literal.Length));
        var output = new byte[outputLength];
        var sourcePosition = 0;
        var destinationPosition = 0;
        var remainingReplacements = replacementCount;
        while (remainingReplacements > 0)
        {
            budget.Step();
            var relativeIndex = input[sourcePosition..].IndexOf(literal);
            if (relativeIndex < 0)
            {
                throw new InvalidOperationException(
                    "The exact replacement copy pass lost a preflighted match.");
            }

            input.Slice(sourcePosition, relativeIndex).CopyTo(output.AsSpan(destinationPosition));
            destinationPosition += relativeIndex;
            replacement.CopyTo(output.AsSpan(destinationPosition));
            destinationPosition += replacement.Length;
            sourcePosition += relativeIndex + literal.Length;
            remainingReplacements--;
        }

        input[sourcePosition..].CopyTo(output.AsSpan(destinationPosition));
        return output;
    }
}
