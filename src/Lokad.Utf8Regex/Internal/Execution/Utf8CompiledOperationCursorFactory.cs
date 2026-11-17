using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8CompiledOperationCursorFactory
{
    // R05: offset enumeration floors the owned cursor scan at the start unit; arm
    // selection mirrors CreateMatchCursor.
    public static Utf8OperationMatchCursor CreateMatchCursorFromOffset(
        Utf8PreparedRegex regexPlan, ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget, int startByteOffset)
    {
        return CreateMatchCursor(regexPlan, input, validation, budget)
            .WithStartFloor(startByteOffset);
    }

    public static Utf8OperationMatchCursor CreateMatchCursor(
        Utf8PreparedRegex regexPlan, ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (regexPlan.ScalarProgram is { RightToLeft: true } or { IsNonBacktracking: true })
            return CreateOwnedMatchCursor(regexPlan, input, validation, budget);
        var literal = regexPlan.LiteralUtf8;
        // Single ASCII literals (exact and folded) publish byte ranges on
        // Unicode input. Kelvin and
        // culture divergences stay fenced upstream of the cursor factory.
        return regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: 0 }
                => new Utf8OperationMatchCursor(input, budget),
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8OperationMatchCursor(input, regexPlan, literal, regexPlan.ExecutionKind, budget),
            NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                => new Utf8OperationMatchCursor(
                    input, regexPlan, literal, budget),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8OperationMatchCursor(input, regexPlan, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8OperationMatchCursor(
                    input, regexPlan, regexPlan.ExecutionKind, budget),
            NativeExecutionKind.AsciiStructuralIdentifierFamily when CanUseStructuralFamilyCursor(regexPlan, validation)
                => new Utf8OperationMatchCursor(input, regexPlan, new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None)),
            _ => CreateOwnedMatchCursor(regexPlan, input, validation, budget),
        };
    }

    public static Utf8ValueSplitEnumerator CreateSplitEnumerator(
        Utf8PreparedRegex regexPlan, ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionDeadline budget)
    {
        if (regexPlan.ScalarProgram is { RightToLeft: true } or { IsNonBacktracking: true })
            return CreateOwnedSplitEnumerator(regexPlan, input, count, budget);
        // R05: single ASCII literals (exact and folded) split through the owned
        // byte-range cursor on every input. Kelvin, culture, and RTL
        // stay fenced in the split router upstream of this factory.
        var literal = regexPlan.LiteralUtf8;
        return regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8ValueSplitEnumerator(
                    input,
                    CreateLiteralMatchCursor(input, regexPlan, literal, validation.IsAscii, budget),
                    count),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8ValueSplitEnumerator(
                    input,
                    new Utf8OperationMatchCursor(input, regexPlan, budget),
                    count),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8ValueSplitEnumerator(
                    input,
                    new Utf8OperationMatchCursor(
                        input, regexPlan, regexPlan.ExecutionKind, budget),
                    count),
            NativeExecutionKind.AsciiStructuralIdentifierFamily when regexPlan.Features.CaptureCount <= 1 &&
                CanUseStructuralFamilyCursor(regexPlan, validation)
                => new Utf8ValueSplitEnumerator(input, new Utf8OperationMatchCursor(input, regexPlan, new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None)), count),
            _ => CreateOwnedSplitEnumerator(regexPlan, input, count, budget),
        };
    }

    private static bool CanUseStructuralFamilyCursor(Utf8PreparedRegex regexPlan, Utf8ValidationResult validation)
        => validation.IsAscii && regexPlan.StructuralSearchPlan.HasValue &&
            regexPlan.StructuralVerifier.AsciiProgram.LinearProgram.HasValue &&
            !regexPlan.StructuralIdentifierFamilyPlan.HasPrefixOverlap &&
            !regexPlan.SearchPlan.HasAlternateLiteralPrefixOverlap &&
            (regexPlan.SemanticRegex.Options & (System.Text.RegularExpressions.RegexOptions.RightToLeft |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.NonBacktracking)) == 0;

    private static Utf8OperationMatchCursor CreateOwnedMatchCursor(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline deadline)
    {
        // Public entry requires caller-valid bytes; the scalar cursor needs no
        // coordinate description or validity pass.
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, default)
;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned cursor preparation exceeded {breach}.");

        var program = prepared.ScalarProgram ?? throw new InvalidOperationException("Owned cursor preparation requires its admitted scalar program.");
        invocation.ShareStorageForCursor();
        return new Utf8OperationMatchCursor(program, input, invocation);
    }

    private static Utf8OperationMatchCursor CreateLiteralMatchCursor(
        ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, byte[] literal, bool asciiInput, Utf8ExecutionDeadline budget)
    {
        return regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal
            ? new Utf8OperationMatchCursor(
                input, regexPlan, literal, budget)
            : new Utf8OperationMatchCursor(
                input, regexPlan, literal, regexPlan.ExecutionKind, budget);
    }

    private static Utf8ValueSplitEnumerator CreateOwnedSplitEnumerator(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input, int count, Utf8ExecutionDeadline deadline)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, default)
;
        var program = prepared.ScalarProgram ?? throw new InvalidOperationException("Split preparation requires its admitted scalar program.");
        return Utf8OwnedSplitPreparation.Create(program, input, count, ref invocation);
    }
}
