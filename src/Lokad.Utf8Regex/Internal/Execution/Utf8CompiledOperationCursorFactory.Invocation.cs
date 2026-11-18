using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8CompiledOperationCursorFactory
{
    internal static bool CanCreateInvocationMatchCursor(Utf8PreparedRegex prepared, ref Utf8SliceInvocation invocation)
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
            default: return false;
        }
    }

    // Transfer the original invocation without resetting the deadline or token.
    internal static Utf8OperationMatchCursor CreateInvocationMatchCursor(Utf8PreparedRegex prepared, ReadOnlySpan<byte> input, Utf8SliceInvocation invocation, int byteFloor)
    {
        var cursor = prepared.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase
                => new Utf8OperationMatchCursor(input, prepared, false, invocation),
            NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8OperationMatchCursor(input, prepared, invocation),
            _ => throw new InvalidOperationException("The prepared plan cannot transfer a native invocation cursor.")
        };
        return cursor.WithStartFloor(byteFloor);
    }
}
