using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8CompiledOperationCursorFactory
{
    public static Utf8ValueMatchEnumerator CreateMatchEnumerator(
        Utf8CompiledEngineRuntime compiledRuntime,
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime verifierRuntime,
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
            CreateMatchCursor(regexPlan, verifierRuntime, input, validation, budget));
    }

    // R05: offset enumeration floors the owned cursor scan at the start unit; arm
    // selection mirrors CreateMatchCursor.
    public static Utf8OperationMatchCursor CreateMatchCursorFromOffset(
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime? verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionDeadline budget,
        int startByteOffset,
        int startUtf16Offset)
    {
        return CreateMatchCursor(regexPlan, verifierRuntime, input, validation, budget)
            .WithStartFloor(startByteOffset, startUtf16Offset);
    }

    public static Utf8OperationMatchCursor CreateMatchCursor(
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime? verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionDeadline budget)
    {
        var literal = regexPlan.LiteralUtf8;
        // R05: single ASCII literals (exact and folded) project incrementally on
        // non-ASCII input, so no BCL verification divert remains here. Kelvin and
        // culture divergences stay fenced upstream of the cursor factory.
        return regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: 0 }
                => new Utf8OperationMatchCursor(input, Utf8InputAnalyzer.Analyze(input).BoundaryMap, budget),
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
            _ => CreateFallbackMatchEnumerator(
                verifierRuntime ?? throw new InvalidOperationException("Fallback cursor construction requires verifier state."),
                input),
        };
    }

    public static Utf8ValueSplitEnumerator CreateSplitEnumerator(
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int totalUtf16Length,
        int count,
        Utf8ExecutionDeadline budget)
    {
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
            _ => CreateFallbackSplitEnumerator(verifierRuntime ?? throw new InvalidOperationException("Fallback cursor construction requires verifier state."), input, count),
        };
    }

    private static bool CanUseStructuralFamilyCursor(Utf8PreparedRegex regexPlan, Utf8ValidationResult validation)
        => validation.IsAscii && regexPlan.StructuralSearchPlan.HasValue &&
            regexPlan.StructuralVerifier.AsciiProgram.LinearProgram.HasValue &&
            !regexPlan.StructuralIdentifierFamilyPlan.HasPrefixOverlap &&
            !regexPlan.SearchPlan.HasAlternateLiteralPrefixOverlap &&
            (regexPlan.SemanticRegex.Options & (System.Text.RegularExpressions.RegexOptions.RightToLeft |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.NonBacktracking)) == 0;

    private static Utf8OperationMatchCursor CreateFallbackMatchEnumerator(
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        return new Utf8OperationMatchCursor(
            input,
            subject.GetDecodedString(),
            verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            subject.Utf16ProjectionMap);
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

    private static Utf8ValueSplitEnumerator CreateFallbackSplitEnumerator(
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        int count)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(
            input,
            subject.GetDecodedString(),
            verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            count,
            subject.BoundaryMap);
    }
}
