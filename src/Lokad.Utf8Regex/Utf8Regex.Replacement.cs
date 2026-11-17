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
    private byte[] ReplaceCore(
        ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement, CancellationToken cancellationToken, Utf8ExecutionDeadline deadline)
    {
        if (GetOwnedReplacementProgram(input, replacement, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceReplace(input, ownedProgram, replacement, Utf8SliceReplacementResultKind.Bytes, default, cancellationToken, deadline).Bytes;
        }

        var validation = Utf8InputAnalyzer.Describe(input);
        var budget = deadline;
        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return OwnedSliceReplace(input, _ownedSliceProgram, replacement, Utf8SliceReplacementResultKind.Bytes,
                default, cancellationToken, deadline).Bytes;
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes, deadline);
    }

    private Utf8AnalyzedReplacement GetParsedReplacement(string replacement)
    {
        return _replacementCache.GetOrAdd(
            replacement,
            this,
            static (replacementText, self) =>
            {
                if (self.ResourcePolicy.MaxWorkingBytes is not { } maximumBytes)
                    return Utf8FrontEndReplacementAnalyzer.Analyze((string)replacementText, self._groupNumbers,
                        self._groupNames, (self.Options & RegexOptions.ECMAScript) != 0);
                // A cache miss needs numeric byte admission, but this lane has
                // no invocation interruption to forward through a work hook.
                using var construction = Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes);
                var result = Utf8FrontEndReplacementAnalyzer.Analyze((string)replacementText, self._groupNumbers,
                    self._groupNames, (self.Options & RegexOptions.ECMAScript) != 0);
                construction.Complete();
                return result;
            });
    }

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

        var validation = Utf8InputAnalyzer.Describe(input);
        var budget = deadline;
        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            return TryReplaceLiteralBytesCore(
                input,
                validation,
                exactLiteralReplacementBytes,
                destination,
                out bytesWritten, deadline);
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return TryReplaceLiteralBytesCore(
                input,
                validation,
                replacementBytes,
                destination,
                out bytesWritten, deadline);
        }

        var scalarResult = OwnedSliceReplace(input, _ownedSliceProgram, replacement, Utf8SliceReplacementResultKind.Destination,
            destination, cancellationToken, deadline);
        bytesWritten = scalarResult.BytesWritten;
        return scalarResult.Status;
    }

    private byte[] ReplaceLiteralBytesCore(ReadOnlySpan<byte> input, Utf8ValidationResult validation, ReadOnlySpan<byte> replacementBytes, Utf8ExecutionDeadline deadline) =>
        ReplaceLiteralBytesCore(input, validation, replacementBytes, out _, deadline);

    private byte[] ReplaceLiteralBytesCore(ReadOnlySpan<byte> input, Utf8ValidationResult validation, ReadOnlySpan<byte> replacementBytes, out int replacementCount, Utf8ExecutionDeadline budget)
    {
        // R05: scalar-verifiable shapes replace literal templates through the scalar cursor
        // on astral-free non-ASCII input; ASCII and byte-safe inputs keep the factory cursor below.
        if (Utf8ScalarSimplePatternVerifier.CanServeScalar(_preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, input))
        {

            var scalarCursor = new Utf8OperationMatchCursor(input, _preparedRegex, useStructuralLinearProgram: false, budget, asciiInput: false);
            return Utf8CursorReplaceEngine.Replace(input, replacementBytes, ref scalarCursor, out replacementCount);
        }

        // Reuse the prepared ASCII family search already used by Split. The
        // original range ledger still owns output admission and publication.
        var cursor = budget.IsInfinite && !_ownedSliceProgram.IsNonBacktracking &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null &&
            TryGetSmallAsciiLiteralFamilySearch(input, out var smallFamily)
                ? new Utf8OperationMatchCursor(input, smallFamily, budget)
                : Utf8CompiledOperationCursorFactory.CreateMatchCursor(_preparedRegex, input, validation, budget);
        return Utf8CursorReplaceEngine.Replace(input, replacementBytes, ref cursor, out replacementCount);
    }

    // Borrow the adapter's operation and result owner through final shaping.
    internal byte[]? ReplaceExactLiteralForResult(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> literalReplacementUtf8, int maximumReplacementCount, ref Utf8SliceInvocation invocation, ref Utf8InvocationResultLedger results, out int replacementCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumReplacementCount);
        if (UsesRightToLeft() || _preparedRegex.ExecutionKind != NativeExecutionKind.ExactAsciiLiteral ||
            _preparedRegex.LiteralUtf8 is not { Length: > 0 } literal)
            throw new InvalidOperationException("The exact replacement requires a nonempty ASCII literal plan.");
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        Utf8Validation.ThrowIfInvalidOnly(literalReplacementUtf8, ref invocation);
        // ASCII needles select complete scalar ranges. The caller's result
        // ledger sizes text only when requested; bytes need no UTF-16 metadata.
        return Utf8ExactLiteralReplacement.Replace(input, literal, literalReplacementUtf8,
            maximumReplacementCount, ref invocation, ref results, out replacementCount);
    }

    private OperationStatus TryReplaceLiteralBytesCore(ReadOnlySpan<byte> input, Utf8ValidationResult validation, ReadOnlySpan<byte> replacementBytes, Span<byte> destination, out int bytesWritten, Utf8ExecutionDeadline budget)
    {
        // R05: scalar-verifiable shapes replace literal templates through the scalar cursor
        // on astral-free non-ASCII input; ASCII and byte-safe inputs keep the factory cursor below.
        if (Utf8ScalarSimplePatternVerifier.CanServeScalar(_preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, input))
        {

            var scalarCursor = new Utf8OperationMatchCursor(input, _preparedRegex, useStructuralLinearProgram: false, budget, asciiInput: false);
            var scalarSuccess = Utf8CursorReplaceEngine.TryReplace(
                input,
                replacementBytes,
                destination,
                ref scalarCursor,
                out bytesWritten);

            return scalarSuccess ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
        }

        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            input,
            validation,
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
        ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8AnalyzedReplacement replacement, Utf8ExecutionDeadline budget, out byte[] replaced)
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
            validation,
            budget);
        replaced = Utf8NativeReplacementExecutor.Replace(input, plan, ref cursor);
        return true;
    }

    private Utf8ReplacementPlan RewriteWholeLiteralCapturePlan(Utf8ReplacementPlan plan)
    {
        if (_groupNumbers.Length <= 1) return plan;
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);
        var captureNumber = GetWholeLiteralCaptureNumber(storage);
        if (captureNumber <= 0)
        {
            return plan;
        }

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

    private int GetWholeLiteralCaptureNumber()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);
        return GetWholeLiteralCaptureNumber(storage);
    }

    private int GetWholeLiteralCaptureNumber(Utf8ConstructionSession storage)
    {
        if (_groupNumbers.Length == 2 &&
            Pattern.Length >= 3 &&
            Pattern[0] == '(' &&
            Pattern[^1] == ')' &&
            IsPlainLiteralPattern(storage.Budget, Pattern.AsSpan(1, Pattern.Length - 2)))
        {
            return 1;
        }

        if (_preparedRegex.ExecutionTree?.Root is not { } root ||
            root.Kind != Utf8ExecutionNodeKind.Capture ||
            root.Children.Count != 1)
        {
            return 0;
        }

        return TryGetWholeLiteralCaptureNumber(storage, root.Children[0], out var captureNumber)
            ? captureNumber
            : 0;
    }

    private readonly record struct ReplacementCaptureFrame(Utf8ExecutionNode Node, int NextChild);

    private static bool TryGetWholeLiteralCaptureNumber(Utf8ConstructionSession storage, Utf8ExecutionNode node, out int captureNumber)
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
                        case Utf8ExecutionNodeKind.Capture when current.Children.Count == 1:
                        case Utf8ExecutionNodeKind.Group when current.Children.Count == 1:
                            frames[frames.Count - 1] = frame with { NextChild = 0 };
                            PushReplacementCaptureFrame(storage.Budget, frames, current.Children[0]);
                            continue;
                        case Utf8ExecutionNodeKind.Concatenate:
                            Utf8ExecutionNode? child = null;
                            var multiple = false;
                            foreach (var candidate in current.Children)
                            {
                                storage.Budget.ChargeWork(1);
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
                        case Utf8ExecutionNodeKind.One:
                        case Utf8ExecutionNodeKind.Multi:
                            success = true; number = 0;
                            break;
                        case Utf8ExecutionNodeKind.Alternate:
                            if (current.Children.Count > 0)
                            {
                                frames[frames.Count - 1] = frame with { NextChild = 1 };
                                PushReplacementCaptureFrame(storage.Budget, frames, current.Children[0]);
                                continue;
                            }
                            success = true; number = 0;
                            break;
                        default:
                            success = false; number = 0;
                            break;
                    }
                }
                else if (current.Kind == Utf8ExecutionNodeKind.Capture)
                {
                    success &= current.CaptureNumber > 0;
                    number = success ? current.CaptureNumber : 0;
                }
                else if (current.Kind == Utf8ExecutionNodeKind.Alternate)
                {
                    if (success && frame.NextChild < current.Children.Count)
                    {
                        frames[frames.Count - 1] = frame with { NextChild = frame.NextChild + 1 };
                        PushReplacementCaptureFrame(storage.Budget, frames, current.Children[frame.NextChild]);
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

    private static void PushReplacementCaptureFrame(Utf8ConstructionBudget budget, Utf8ConstructionBuffer<ReplacementCaptureFrame> frames, Utf8ExecutionNode node)
    {
        budget.EnterDepth();
        try { frames.Push(new ReplacementCaptureFrame(node, -1)); }
        catch { budget.LeaveDepth(); throw; }
    }

    private static bool IsZeroWidthBoundaryNode(Utf8ExecutionNodeKind kind)
    {
        return kind is Utf8ExecutionNodeKind.Boundary or
            Utf8ExecutionNodeKind.NonBoundary or
            Utf8ExecutionNodeKind.Bol or
            Utf8ExecutionNodeKind.Eol or
            Utf8ExecutionNodeKind.Beginning or
            Utf8ExecutionNodeKind.Start or
            Utf8ExecutionNodeKind.EndZ or
            Utf8ExecutionNodeKind.End;
    }

    private static bool IsPlainLiteralPattern(Utf8ConstructionBudget budget, ReadOnlySpan<char> pattern)
    {
        foreach (var ch in pattern)
        {
            budget.ChargeWork(1);
            if (Utf8RegexSyntax.IsRegexMetaCharacter(ch))
            {
                return false;
            }
        }

        return true;
    }
}
