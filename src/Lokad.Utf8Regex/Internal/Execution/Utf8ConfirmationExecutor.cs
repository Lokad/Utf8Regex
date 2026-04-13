using Lokad.Utf8Regex.Internal.Planning;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ConfirmationExecutor
{
    public static bool IsMatch(
        Utf8SearchPlan plan,
        Utf8ConfirmationPlan confirmation,
        ReadOnlySpan<byte> input,
        int startIndex,
        int literalLength)
    {
        return confirmation.Kind switch
        {
            Utf8ConfirmationKind.None => true,
            Utf8ConfirmationKind.BoundaryRequirements or Utf8ConfirmationKind.BoundaryAndTrailingLiteral
                => MatchesBoundaryRequirementsFast(plan, input, startIndex, literalLength),
            Utf8ConfirmationKind.FallbackVerifier => true,
            _ => false,
        };
    }

    private static bool MatchesBoundaryRequirementsFast(Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, int literalLength)
    {
        if (plan.TrailingLiteralUtf8 is null &&
            plan.LeadingBoundary == Utf8BoundaryRequirement.Boundary &&
            plan.TrailingBoundary == Utf8BoundaryRequirement.Boundary &&
            TryGetAsciiWordBoundary(input, startIndex, out var leadingIsBoundary) &&
            TryGetAsciiWordBoundary(input, startIndex + literalLength, out var trailingIsBoundary))
        {
            return leadingIsBoundary && trailingIsBoundary;
        }

        if (TryMatchesBoundaryRequirementAscii(plan.LeadingBoundary, input, startIndex, out var leadingBoundaryMatch) &&
            TryMatchesBoundaryRequirementAscii(plan.TrailingBoundary, input, startIndex + literalLength, out var trailingBoundaryMatch))
        {
            return leadingBoundaryMatch &&
                (plan.TrailingLiteralUtf8 is null || input[(startIndex + literalLength)..].StartsWith(plan.TrailingLiteralUtf8)) &&
                trailingBoundaryMatch;
        }

        return Utf8SearchExecutor.MatchesBoundaryRequirements(plan, input, startIndex, literalLength);
    }

    private static bool TryMatchesBoundaryRequirementAscii(Utf8BoundaryRequirement requirement, ReadOnlySpan<byte> input, int byteOffset, out bool isMatch)
    {
        isMatch = false;
        if (!TryGetAsciiWordBoundary(input, byteOffset, out var isBoundary))
        {
            return false;
        }

        isMatch = requirement switch
        {
            Utf8BoundaryRequirement.None => true,
            Utf8BoundaryRequirement.Boundary => isBoundary,
            Utf8BoundaryRequirement.NonBoundary => !isBoundary,
            _ => false,
        };

        return true;
    }

    private static bool TryGetAsciiWordBoundary(ReadOnlySpan<byte> input, int byteOffset, out bool isBoundary)
    {
        var lookup = RuntimeFrontEnd.RegexCharClass.WordCharAsciiLookup;

        bool previousIsWord;
        if (byteOffset <= 0)
        {
            previousIsWord = false;
        }
        else
        {
            var previous = input[byteOffset - 1];
            if (previous >= 128)
            {
                isBoundary = false;
                return false;
            }

            previousIsWord = lookup[previous] != 0;
        }

        bool nextIsWord;
        if (byteOffset >= input.Length)
        {
            nextIsWord = false;
        }
        else
        {
            var next = input[byteOffset];
            if (next >= 128)
            {
                isBoundary = false;
                return false;
            }

            nextIsWord = lookup[next] != 0;
        }

        isBoundary = previousIsWord != nextIsWord;
        return true;
    }
}
