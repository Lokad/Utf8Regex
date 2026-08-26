using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ExactLiteralReplacement
{
    internal static byte[] Replace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal,
        ReadOnlySpan<byte> replacement,
        Utf8ExecutionDeadline budget,
        out int replacementCount)
    {
        if (literal.IsEmpty)
        {
            throw new ArgumentException("The exact replacement literal must not be empty.", nameof(literal));
        }

        replacementCount = 0;
        var remaining = input;
        while (true)
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
        while (true)
        {
            budget.Step();
            var relativeIndex = input[sourcePosition..].IndexOf(literal);
            if (relativeIndex < 0)
            {
                input[sourcePosition..].CopyTo(output.AsSpan(destinationPosition));
                return output;
            }

            input.Slice(sourcePosition, relativeIndex).CopyTo(output.AsSpan(destinationPosition));
            destinationPosition += relativeIndex;
            replacement.CopyTo(output.AsSpan(destinationPosition));
            destinationPosition += replacement.Length;
            sourcePosition += relativeIndex + literal.Length;
        }
    }
}
