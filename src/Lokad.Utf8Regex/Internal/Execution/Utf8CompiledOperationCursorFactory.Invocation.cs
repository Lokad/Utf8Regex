using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8CompiledOperationCursorFactory
{
    internal static bool CanCreateInvocationMatchCursor(Utf8PreparedRegex prepared, Utf8ValidationResult validation,
        ref Utf8SliceInvocation invocation)
    {
        if ((prepared.SemanticRegex.Options & (RegexOptions.RightToLeft | RegexOptions.NonBacktracking)) != 0) return false;
        switch (prepared.ExecutionKind)
        {
            case NativeExecutionKind.ExactAsciiLiteral:
            case NativeExecutionKind.ExactUtf8Literal:
            case NativeExecutionKind.AsciiLiteralIgnoreCase:
                return prepared.LiteralUtf8 is { Length: > 0 };
            case NativeExecutionKind.ExactUtf8Literals:
            case NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals:
                if (prepared.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals) return false;
                // Empty alternatives require unit-by-unit progression. Inspect
                // prepared metadata in bounded blocks under the entry budget.
                for (var index = 0; index < literals.Length; index++)
                {
                    if ((index & 31) == 0)
                    {
                        var breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Literal-family admission exceeded {breach}.");
                        invocation.Budget.ThrowIfCancellationRequested();
                    }
                    if (literals[index].Length == 0) return false;
                }
                return true;
            case NativeExecutionKind.AsciiOrderedLiteralWindow:
                return validation.IsAscii && prepared.StructuralLinearProgram.OrderedLiteralWindowPlan.HasValue;
            case NativeExecutionKind.AsciiStructuralIdentifierFamily:
                return CanUseStructuralFamilyCursor(prepared, validation);
            case NativeExecutionKind.AsciiSimplePattern:
                return !prepared.Features.HasLookarounds &&
                    (prepared.StructuralLinearProgram.DeterministicProgram.HasValue
                        ? validation.IsAscii || prepared.StructuralLinearProgram.AllowsUtf8ByteSafe
                        : prepared.SimplePatternPlan.Branches is { Length: > 0 } &&
                            (validation.IsAscii || prepared.SimplePatternPlan.IsUtf8ByteSafe));
            default: return false;
        }
    }

    // Transfer once, after any temporary public coordinate map has been dropped
    // and its reservation released. No validation/deadline/token reset occurs.
    internal static Utf8OperationMatchCursor CreateInvocationMatchCursor(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, Utf8SliceInvocation invocation, int byteFloor, int unitFloor)
    {
        var cursor = prepared.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase
                => new Utf8OperationMatchCursor(input, prepared, validation.IsAscii, false, invocation),
            NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8OperationMatchCursor(input, prepared, validation.IsAscii, invocation),
            NativeExecutionKind.AsciiOrderedLiteralWindow or NativeExecutionKind.AsciiStructuralIdentifierFamily
                => new Utf8OperationMatchCursor(input, prepared, invocation),
            NativeExecutionKind.AsciiSimplePattern
                => new Utf8OperationMatchCursor(input, prepared, invocation, validation.IsAscii),
            _ => throw new InvalidOperationException("The prepared plan cannot transfer a native invocation cursor."),
        };
        return cursor.WithStartFloor(byteFloor, unitFloor);
    }
}
