using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8CompiledOperationCursorFactory
{
    public static Utf8OperationMatchCursor CreateMatchCursor(
        Utf8PreparedRegex regexPlan, ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (regexPlan.ScalarProgram is { RightToLeft: true } or { IsNonBacktracking: true })
            return CreateOwnedMatchCursor(regexPlan, input, budget);
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
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8OperationMatchCursor(input, regexPlan, budget, rightToLeft: false),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8OperationMatchCursor(input, regexPlan, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8OperationMatchCursor(
                    input, regexPlan, regexPlan.ExecutionKind, budget),
            _ => CreateOwnedMatchCursor(regexPlan, input, budget)
        };
    }

    public static Utf8ValueSplitEnumerator CreateSplitEnumerator(
        Utf8PreparedRegex regexPlan, ReadOnlySpan<byte> input, int count, Utf8ExecutionDeadline budget)
    {
        if (regexPlan.ScalarProgram is { RightToLeft: true } or { IsNonBacktracking: true })
            return CreateOwnedSplitEnumerator(regexPlan, input, count, budget);
        // The caller admits the literal cursor only for compatible folding and direction.
        var literal = regexPlan.LiteralUtf8;
        return regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8ValueSplitEnumerator(
                    input,
                    new Utf8OperationMatchCursor(input, regexPlan, budget, rightToLeft: false),
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
            _ => CreateOwnedSplitEnumerator(regexPlan, input, count, budget)
        };
    }

    private static Utf8OperationMatchCursor CreateOwnedMatchCursor(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input, Utf8ExecutionDeadline deadline)
    {
        // Public entry requires caller-valid bytes; the scalar cursor needs no
        // coordinate description or validity pass.
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, default);
        var breach = invocation.CheckInput();
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned cursor preparation exceeded {breach}.");

        var program = prepared.ScalarProgram ?? throw new InvalidOperationException("Owned cursor preparation requires its admitted scalar program.");
        invocation.ShareStorageForCursor();
        return new Utf8OperationMatchCursor(program, input, invocation);
    }

    private static Utf8ValueSplitEnumerator CreateOwnedSplitEnumerator(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input, int count, Utf8ExecutionDeadline deadline)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, default);
        var program = prepared.ScalarProgram ?? throw new InvalidOperationException("Split preparation requires its admitted scalar program.");
        return Utf8OwnedSplitPreparation.Create(program, input, count, ref invocation);
    }
}
