using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ConfirmationExecutor
{
    public static bool IsMatch(
        in Utf8SearchPlan plan,
        Utf8ConfirmationPlan confirmation,
        ReadOnlySpan<byte> input,
        int startIndex,
        int literalLength)
    {
        return confirmation.Kind switch
        {
            Utf8ConfirmationKind.None => true,
            Utf8ConfirmationKind.BoundaryRequirements or Utf8ConfirmationKind.BoundaryAndTrailingLiteral
                => MatchesBoundaryRequirementsFast(in plan, input, startIndex, literalLength),
            Utf8ConfirmationKind.FallbackVerifier => true,
            _ => false,
        };
    }

    public static bool TryFindConfirmedAlternateAtSameStart(
        in Utf8SearchPlan plan,
        Utf8ConfirmationPlan confirmation,
        ReadOnlySpan<byte> input,
        in PreparedSearchMatch rejectedMatch,
        out PreparedSearchMatch confirmedMatch)
    {
        confirmedMatch = default;
        if (!plan.HasAlternateLiteralPrefixOverlap ||
            plan.AlternateLiteralsUtf8 is not { Length: > 1 } literals)
        {
            return false;
        }

        var remaining = input[rejectedMatch.Index..];
        for (var literalId = 0; literalId < literals.Length; literalId++)
        {
            var literal = literals[literalId];
            if (remaining.StartsWith(literal) &&
                IsMatch(in plan, confirmation, input, rejectedMatch.Index, literal.Length))
            {
                confirmedMatch = new PreparedSearchMatch(rejectedMatch.Index, literal.Length, literalId);
                return true;
            }
        }

        return false;
    }

    private static bool MatchesBoundaryRequirementsFast(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int startIndex, int literalLength)
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

        return Utf8SearchExecutor.MatchesBoundaryRequirements(in plan, input, startIndex, literalLength);
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
        return DotNetUtf8WordBoundary.TryGetAsciiBoundary(input, byteOffset, out isBoundary);
    }
}
