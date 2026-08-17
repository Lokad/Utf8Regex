using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledOperationCursorFactory
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

    public static Utf8OperationMatchCursor CreateMatchCursor(
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime? verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionDeadline budget)
    {
        var literal = regexPlan.LiteralUtf8;
        return regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: 0 }
                => new Utf8OperationMatchCursor(input, Utf8InputAnalyzer.Analyze(input).BoundaryMap, budget),
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8OperationMatchCursor(input, regexPlan.SearchPlan, literal, regexPlan.ExecutionKind, budget),
            NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                => new Utf8OperationMatchCursor(
                    input,
                    regexPlan.SearchPlan,
                    literal,
                    Utf8Validation.Validate(literal).Utf16Length,
                    budget),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8OperationMatchCursor(input, regexPlan.SearchPlan, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8OperationMatchCursor(input, regexPlan.SearchPlan, regexPlan.ExecutionKind, budget),
            NativeExecutionKind.AsciiSimplePattern when
                regexPlan.StructuralLinearProgram.DeterministicProgram.HasValue &&
                (validation.IsAscii || regexPlan.StructuralLinearProgram.AllowsUtf8ByteSafe)
                => new Utf8OperationMatchCursor(input, regexPlan.StructuralLinearProgram, budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii || regexPlan.SimplePatternPlan.IsUtf8ByteSafe
                => new Utf8OperationMatchCursor(
                    input,
                    regexPlan.ExecutionProgram,
                    regexPlan.SearchPlan,
                    regexPlan.SimplePatternPlan,
                    budget),
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
        int count,
        Utf8ExecutionDeadline budget)
    {
        var literal = regexPlan.LiteralUtf8;
        return regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8ValueSplitEnumerator(
                    input,
                    regexPlan.SearchPlan,
                    literal,
                    regexPlan.ExecutionKind,
                    count,
                    boundaryMap: null,
                    budget),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8ValueSplitEnumerator(input, regexPlan.SearchPlan, count, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8ValueSplitEnumerator(input, regexPlan.SearchPlan, count, regexPlan.ExecutionKind, budget),
            NativeExecutionKind.AsciiSimplePattern when
                regexPlan.StructuralLinearProgram.DeterministicProgram.HasValue &&
                (validation.IsAscii || regexPlan.StructuralLinearProgram.AllowsUtf8ByteSafe)
                => new Utf8ValueSplitEnumerator(input, regexPlan.StructuralLinearProgram, count, budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii || regexPlan.SimplePatternPlan.IsUtf8ByteSafe
                => new Utf8ValueSplitEnumerator(
                    input,
                    regexPlan.SearchPlan,
                    regexPlan.ExecutionProgram,
                    regexPlan.SimplePatternPlan,
                    count,
                    budget),
            _ => CreateFallbackSplitEnumerator(verifierRuntime, input, count),
        };
    }

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
