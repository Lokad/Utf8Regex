using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Unicode;

namespace Lokad.Utf8Regex;

// replacement execution cores, split from the matching facade along the replacement ownership boundary.
/// <summary>Replacement execution cores for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    private static bool TryGetNativeReplacementBytes(
        Utf8AnalyzedReplacement replacement, out byte[] replacementBytes)
    {
        if (replacement.IsLiteral)
        {
            replacementBytes = replacement.LiteralUtf8;
            return true;
        }

        replacementBytes = [];
        return false;
    }

    private bool TryGetExactLiteralReplacementBytes(
        Utf8AnalyzedReplacement replacement, out byte[] replacementBytes)
    {
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase) ||
            _preparedRegex.LiteralUtf8 is not { Length: > 0 } literalUtf8)
        {
            replacementBytes = [];
            return false;
        }

        if (replacement.IsLiteral)
        {
            replacementBytes = replacement.LiteralUtf8;
            return true;
        }

        var plan = RewriteWholeLiteralCapturePlan(replacement.Plan);
        // R05: WholeMatch bytes are the pattern literal, which equals the hit only
        // under case-sensitive matching. Folded singles decline here so the
        // per-hit routes (native plan, owned template) splice the actual hit.
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCase)
        {
            foreach (var instruction in plan.Instructions)
            {
                if (instruction.Kind == Utf8ReplacementInstructionKind.WholeMatch)
                {
                    replacementBytes = [];
                    return false;
                }
            }
        }

        var totalLength = 0;
        foreach (var instruction in plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    totalLength += instruction.LiteralUtf8.Length;
                    break;

                case Utf8ReplacementInstructionKind.WholeMatch:
                    totalLength += literalUtf8.Length;
                    break;

                default:
                    replacementBytes = [];
                    return false;
            }
        }

        replacementBytes = new byte[totalLength];
        var written = 0;
        foreach (var instruction in plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal when instruction.LiteralUtf8.Length > 0:
                    instruction.LiteralUtf8.CopyTo(replacementBytes.AsSpan(written));
                    written += instruction.LiteralUtf8.Length;
                    break;

                case Utf8ReplacementInstructionKind.WholeMatch:
                    literalUtf8.CopyTo(replacementBytes.AsSpan(written));
                    written += literalUtf8.Length;
                    break;
            }
        }

        return true;
    }

    private OperationStatus TryReplaceCore(
        ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken, Utf8ExecutionDeadline deadline)
    {
        // Native emission still reads unmatched subject slices after writing
        // earlier pieces. Stage overlapping destinations in the existing owned
        // writer so expansion cannot overwrite those unread bytes.
        if ((input.Overlaps(destination) ? _ownedSliceProgram :
            GetOwnedReplacementProgram(input, replacement, cancellationToken)) is { } ownedProgram)
        {
            var result = OwnedSliceReplace(input, ownedProgram, replacement, Utf8SliceReplacementResultKind.Destination, destination, cancellationToken, deadline);
            bytesWritten = result.BytesWritten;
            return result.Status;
        }

        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            return TryReplaceLiteralBytesCore(
                input,
                exactLiteralReplacementBytes,
                destination,
                out bytesWritten, deadline);
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return TryReplaceLiteralBytesCore(
                input,
                replacementBytes,
                destination,
                out bytesWritten, deadline);
        }

        var scalarResult = OwnedSliceReplace(input, _ownedSliceProgram, replacement, Utf8SliceReplacementResultKind.Destination,
            destination, cancellationToken, deadline);
        bytesWritten = scalarResult.BytesWritten;
        return scalarResult.Status;
    }

    private byte[] ReplaceLiteralBytesCore(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementBytes, Utf8ExecutionDeadline deadline) =>
        ReplaceLiteralBytesCore(input, replacementBytes, out _, deadline);

    private byte[] ReplaceLiteralBytesCore(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementBytes, out int replacementCount, Utf8ExecutionDeadline budget)
    {

        // Reuse the prepared ASCII family search already used by Split. The
        // original range ledger still owns output admission and publication.
        var cursor = budget.IsInfinite && !_ownedSliceProgram.IsNonBacktracking &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null &&
            TryGetSmallAsciiLiteralFamilySearch(out var smallFamily)
                ? new Utf8OperationMatchCursor(input, smallFamily, budget)
                : Utf8CompiledOperationCursorFactory.CreateMatchCursor(_preparedRegex, input, budget);
        return Utf8CursorReplaceEngine.Replace(input, replacementBytes, ref cursor, out replacementCount);
    }

    // Borrow the adapter's operation and result owner through final shaping.
    // The caller supplies an immutable literal from its validated template.
    internal byte[]? ReplaceExactLiteralForResult(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> validatedLiteralUtf8, int maximumReplacementCount, ref Utf8SliceInvocation invocation, ref Utf8InvocationResultLedger results, out int replacementCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumReplacementCount);
        if (UsesRightToLeft() || _preparedRegex.ExecutionKind != NativeExecutionKind.ExactAsciiLiteral ||
            _preparedRegex.SearchPlan.LiteralSearch is not { Length: > 0, IgnoreCase: false } search)
            throw new InvalidOperationException("The exact replacement requires a nonempty ASCII literal plan.");
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        // ASCII needles select complete scalar ranges. The caller's result
        // ledger sizes text only when requested; bytes need no UTF-16 metadata.
        return Utf8ExactLiteralReplacement.Replace(input, search, validatedLiteralUtf8,
            maximumReplacementCount, ref invocation, ref results, out replacementCount);
    }

    private OperationStatus TryReplaceLiteralBytesCore(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementBytes, Span<byte> destination, out int bytesWritten, Utf8ExecutionDeadline budget)
    {

        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            input,
            budget);
        var success = Utf8CursorReplaceEngine.TryReplace(
            input,
            replacementBytes,
            destination,
            ref cursor,
            out bytesWritten);

        return success ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
    }

    private bool TryReplaceViaNativePlan(
        ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement, Utf8ExecutionDeadline budget, out byte[] replaced)
    {
        var plan = RewriteWholeLiteralCapturePlan(replacement.Plan);
        if (replacement.IsLiteral ||
            !Utf8NativeReplacementExecutor.CanExecute(plan))
        {
            replaced = [];
            return false;
        }

        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            input,
            budget);
        replaced = Utf8NativeReplacementExecutor.Replace(input, plan, ref cursor);
        return true;
    }

    private Utf8ReplacementPlan RewriteWholeLiteralCapturePlan(Utf8ReplacementPlan plan)
    {
        var captureNumber = _wholeLiteralCaptureNumber;
        if (captureNumber == 0) return plan;
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);

        Utf8ConstructionBuffer<Utf8ReplacementInstruction>? rewritten = null;
        for (var i = 0; i < plan.Instructions.Count; i++)
        {
            storage.Budget.ChargeWork(1);
            var instruction = plan.Instructions[i];
            if (instruction.Kind == Utf8ReplacementInstructionKind.Group && instruction.GroupNumber == captureNumber)
            {
                if (rewritten is null)
                {
                    rewritten = storage.Buffer<Utf8ReplacementInstruction>();
                    for (var j = 0; j < plan.Instructions.Count; j++)
                    {
                        storage.Budget.ChargeWork(1);
                        rewritten.Add(plan.Instructions[j]);
                    }
                }
                rewritten[i] = Utf8ReplacementInstruction.WholeMatch();
            }
        }
        if (rewritten is null) return plan;
        var result = Utf8ReplacementPlan.Create(storage, rewritten.Span);
        storage.Complete(); owner.Complete();
        return result;
    }

    private static int GetWholeLiteralCaptureNumber(Utf8TreeNode? root)
    {
        if (root is not { Kind: Utf8TreeNodeKind.Capture, ChildCount: 1 }) return 0;

        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);
        return TryGetWholeLiteralCaptureNumber(storage, root.Child(0), out var captureNumber)
            ? captureNumber
            : 0;
    }

    private readonly record struct ReplacementCaptureFrame(Utf8TreeNode Node, int NextChild);

    private static bool TryGetWholeLiteralCaptureNumber(Utf8ConstructionSession storage, Utf8TreeNode node, out int captureNumber)
    {
        var frames = storage.Buffer<ReplacementCaptureFrame>();
        var success = false;
        var number = 0;
        PushReplacementCaptureFrame(storage.Budget, frames, node);
        try
        {
            while (frames.Count > 0)
            {
                storage.Budget.ChargeWork(1);
                var frame = frames.Peek();
                var current = frame.Node;
                if (frame.NextChild < 0)
                {
                    switch (current.Kind)
                    {
                        case Utf8TreeNodeKind.Capture when current.ChildCount == 1:
                        case Utf8TreeNodeKind.Group when current.ChildCount == 1:
                            frames[frames.Count - 1] = frame with { NextChild = 0 };
                            PushReplacementCaptureFrame(storage.Budget, frames, current.Child(0));
                            continue;
                        case Utf8TreeNodeKind.Concatenate:
                            Utf8TreeNode? child = null;
                            var multiple = false;
                            for (var i = 0; i < current.ChildCount; i++)
                            {
                                storage.Budget.ChargeWork(1);
                                var candidate = current.Child(i);
                                if (IsZeroWidthBoundaryNode(candidate.Kind)) continue;
                                if (child is not null) { multiple = true; break; }
                                child = candidate;
                            }
                            if (child is not null && !multiple)
                            {
                                frames[frames.Count - 1] = frame with { NextChild = 0 };
                                PushReplacementCaptureFrame(storage.Budget, frames, child);
                                continue;
                            }
                            success = false; number = 0;
                            break;
                        case Utf8TreeNodeKind.One:
                        case Utf8TreeNodeKind.Multi:
                            success = true; number = 0;
                            break;
                        case Utf8TreeNodeKind.Alternate:
                            if (current.ChildCount > 0)
                            {
                                frames[frames.Count - 1] = frame with { NextChild = 1 };
                                PushReplacementCaptureFrame(storage.Budget, frames, current.Child(0));
                                continue;
                            }
                            success = true; number = 0;
                            break;
                        default:
                            success = false; number = 0;
                            break;
                    }
                }
                else if (current.Kind == Utf8TreeNodeKind.Capture)
                {
                    success &= current.M > 0;
                    number = success ? current.M : 0;
                }
                else if (current.Kind == Utf8TreeNodeKind.Alternate)
                {
                    if (success && frame.NextChild < current.ChildCount)
                    {
                        frames[frames.Count - 1] = frame with { NextChild = frame.NextChild + 1 };
                        PushReplacementCaptureFrame(storage.Budget, frames, current.Child(frame.NextChild));
                        continue;
                    }
                    number = 0;
                }
                frames.Pop(); storage.Budget.LeaveDepth();
            }
        }
        finally
        {
            while (frames.Count > 0) { frames.Pop(); storage.Budget.LeaveDepth(); }
        }
        captureNumber = number;
        return success;
    }

    private static void PushReplacementCaptureFrame(Utf8ConstructionBudget budget, Utf8ConstructionBuffer<ReplacementCaptureFrame> frames, Utf8TreeNode node)
    {
        budget.EnterDepth();
        try { frames.Push(new ReplacementCaptureFrame(node, -1)); }
        catch { budget.LeaveDepth(); throw; }
    }

    private static bool IsZeroWidthBoundaryNode(Utf8TreeNodeKind kind)
    {
        return kind is Utf8TreeNodeKind.Boundary or
            Utf8TreeNodeKind.NonBoundary or
            Utf8TreeNodeKind.Bol or
            Utf8TreeNodeKind.Eol or
            Utf8TreeNodeKind.Beginning or
            Utf8TreeNodeKind.Start or
            Utf8TreeNodeKind.EndZ or
            Utf8TreeNodeKind.End;
    }
}
