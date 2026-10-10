using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8CompiledOperationCursorFactory
{
    public static Utf8ValueMatchEnumerator CreateMatchEnumerator(
        Utf8CompiledEngineRuntime compiledRuntime,
        Utf8PreparedRegex regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionDeadline budget)
    {
        if (compiledRuntime.GetGlobalMatchKernel(validation, budget) is { } kernelMatcher)
        {
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, kernelMatcher, budget));
        }

        return new Utf8ValueMatchEnumerator(
            CreateMatchCursor(regexPlan, input, validation, budget));
    }

    // R05: offset enumeration floors the owned cursor scan at the start unit; arm
    // selection mirrors CreateMatchCursor.
    public static Utf8OperationMatchCursor CreateMatchCursorFromOffset(
        Utf8PreparedRegex regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionDeadline budget,
        int startByteOffset,
        int startUtf16Offset)
    {
        return CreateMatchCursor(regexPlan, input, validation, budget)
            .WithStartFloor(startByteOffset, startUtf16Offset);
    }

    public static Utf8OperationMatchCursor CreateMatchCursor(
        Utf8PreparedRegex regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionDeadline budget)
    {
        if (regexPlan.ScalarProgram is { RightToLeft: true } or { IsNonBacktracking: true })
            return CreateOwnedMatchCursor(regexPlan, input, validation, budget, false, out _);
        var literal = regexPlan.LiteralUtf8;
        // R05: single ASCII literals (exact and folded) project incrementally on
        // non-ASCII input, so no BCL verification divert remains here. Kelvin and
        // culture divergences stay fenced upstream of the cursor factory.
        return regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: 0 }
                => new Utf8OperationMatchCursor(input, validation, budget),
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8OperationMatchCursor(input, regexPlan, literal, regexPlan.ExecutionKind, budget, validation.IsAscii),
            NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                => new Utf8OperationMatchCursor(
                    input,
                    regexPlan,
                    literal,
                    Utf8Validation.Validate(literal).Utf16Length,
                    budget),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8OperationMatchCursor(input, regexPlan, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8OperationMatchCursor(
                    input,
                    regexPlan,
                    regexPlan.ExecutionKind,
                    validation.IsAscii,
                    budget),
            NativeExecutionKind.AsciiOrderedLiteralWindow when validation.IsAscii || Utf8InputAnalyzer.IsAscii(input)
                => new Utf8OperationMatchCursor(input, regexPlan, useStructuralLinearProgram: true, budget, asciiInput: true),
            NativeExecutionKind.AsciiStructuralIdentifierFamily when CanUseStructuralFamilyCursor(regexPlan, validation)
                => new Utf8OperationMatchCursor(input, regexPlan,
                    new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None)),
            NativeExecutionKind.AsciiSimplePattern when
                regexPlan.StructuralLinearProgram.DeterministicProgram.HasValue &&
                (validation.IsAscii || regexPlan.StructuralLinearProgram.AllowsUtf8ByteSafe)
                => new Utf8OperationMatchCursor(input, regexPlan, useStructuralLinearProgram: true, budget, validation.IsAscii),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii || regexPlan.SimplePatternPlan.IsUtf8ByteSafe
                => new Utf8OperationMatchCursor(
                    input,
                    regexPlan,
                    useStructuralLinearProgram: false,
                    budget,
                    validation.IsAscii),
            _ => CreateOwnedMatchCursor(regexPlan, input, validation, budget, false, out _),
        };
    }

    public static Utf8ValueSplitEnumerator CreateSplitEnumerator(
        Utf8PreparedRegex regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int totalUtf16Length,
        int count,
        Utf8ExecutionDeadline budget)
    {
        if (regexPlan.ScalarProgram is { RightToLeft: true } or { IsNonBacktracking: true })
            return CreateOwnedSplitEnumerator(regexPlan, input, validation, count, budget);
        // R05: single ASCII literals (exact and folded) split through the owned
        // incremental-projection cursor on every input. Kelvin, culture, and RTL
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
                    count,
                    totalUtf16Length),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8ValueSplitEnumerator(
                    input,
                    new Utf8OperationMatchCursor(input, regexPlan, budget),
                    count,
                    totalUtf16Length),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8ValueSplitEnumerator(
                    input,
                    new Utf8OperationMatchCursor(
                        input,
                        regexPlan,
                        regexPlan.ExecutionKind,
                        totalUtf16Length == input.Length,
                        budget),
                    count,
                    totalUtf16Length),
            NativeExecutionKind.AsciiOrderedLiteralWindow when validation.IsAscii || Utf8InputAnalyzer.IsAscii(input)
                => new Utf8ValueSplitEnumerator(
                    input,
                    new Utf8OperationMatchCursor(input, regexPlan, useStructuralLinearProgram: true, budget, asciiInput: true),
                    count,
                    totalUtf16Length),
            NativeExecutionKind.AsciiStructuralIdentifierFamily when regexPlan.Features.CaptureCount <= 1 &&
                CanUseStructuralFamilyCursor(regexPlan, validation)
                => new Utf8ValueSplitEnumerator(input, new Utf8OperationMatchCursor(input, regexPlan,
                    new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None)), count, totalUtf16Length),
            NativeExecutionKind.AsciiSimplePattern when
                regexPlan.StructuralLinearProgram.DeterministicProgram.HasValue &&
                (validation.IsAscii || regexPlan.StructuralLinearProgram.AllowsUtf8ByteSafe)
                => new Utf8ValueSplitEnumerator(
                    input,
                    new Utf8OperationMatchCursor(input, regexPlan, useStructuralLinearProgram: true, budget, validation.IsAscii),
                    count,
                    totalUtf16Length),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii || regexPlan.SimplePatternPlan.IsUtf8ByteSafe
                => new Utf8ValueSplitEnumerator(
                    input,
                    new Utf8OperationMatchCursor(input, regexPlan, useStructuralLinearProgram: false, budget, validation.IsAscii),
                    count,
                    totalUtf16Length),
            _ => CreateOwnedSplitEnumerator(regexPlan, input, validation, count, budget),
        };
    }

    private static bool CanUseStructuralFamilyCursor(Utf8PreparedRegex regexPlan, Utf8ValidationResult validation)
        => validation.IsAscii && regexPlan.StructuralSearchPlan.HasValue &&
            regexPlan.StructuralVerifier.AsciiProgram.LinearProgram.HasValue &&
            !regexPlan.StructuralIdentifierFamilyPlan.HasPrefixOverlap &&
            !regexPlan.SearchPlan.HasAlternateLiteralPrefixOverlap &&
            (regexPlan.SemanticRegex.Options & (System.Text.RegularExpressions.RegexOptions.RightToLeft |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.NonBacktracking)) == 0;

    private static Utf8OperationMatchCursor CreateOwnedMatchCursor(Utf8PreparedRegex prepared,
        ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline deadline,
        bool retainMap, out Utf8BoundaryMap map)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, default);
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned cursor preparation exceeded {breach}.");
        if (validation.ByteLength != input.Length) validation = Utf8Validation.Validate(input, ref invocation);
        var program = prepared.ScalarProgram ?? throw new InvalidOperationException("Owned cursor preparation requires its admitted scalar program.");
        return Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref invocation, retainMap, out map);
    }

    private static Utf8OperationMatchCursor CreateLiteralMatchCursor(
        ReadOnlySpan<byte> input,
        Utf8PreparedRegex regexPlan,
        byte[] literal,
        bool asciiInput,
        Utf8ExecutionDeadline budget)
    {
        return regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal
            ? new Utf8OperationMatchCursor(
                input,
                regexPlan,
                literal,
                Utf8Validation.Validate(literal).Utf16Length,
                budget)
            : new Utf8OperationMatchCursor(
                input,
                regexPlan,
                literal,
                regexPlan.ExecutionKind,
                budget,
                asciiInput);
    }

    private static Utf8ValueSplitEnumerator CreateOwnedSplitEnumerator(Utf8PreparedRegex prepared,
        ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionDeadline deadline)
    {
        var cursor = CreateOwnedMatchCursor(prepared, input, validation, deadline, true, out var map);
        return new Utf8ValueSplitEnumerator(input, null, cursor, map, count, map.Utf16Length,
            prepared.ScalarProgram is { RightToLeft: true });
    }
}
