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
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement,
        CancellationToken cancellationToken, Utf8ExecutionDeadline deadline)
    {
        if (GetOwnedReplacementProgram(input, replacement, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceReplace(input, ownedProgram, replacement, Utf8SliceReplacementResultKind.Bytes, default, cancellationToken, deadline).Bytes;
        }

        var validation = Utf8Validation.Validate(input);
        var budget = deadline;
        if (UsesOwnedTemplateBeforeNativePlan() &&
            TryReplaceTemplateWithOwnedGroups(input, validation, replacement, Utf8SliceReplacementResultKind.Bytes,
                default, cancellationToken, deadline, out var ownedTemplate))
        {
            return ownedTemplate.Bytes;
        }

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
        Utf8AnalyzedReplacement replacement,
        out byte[] replacementBytes)
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
        Utf8AnalyzedReplacement replacement,
        out byte[] replacementBytes)
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

    private string ReplaceToStringCore(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement, CancellationToken cancellationToken,
        Utf8ExecutionDeadline deadline)
    {
        if (GetOwnedReplacementProgram(input, replacement, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceReplace(input, ownedProgram, replacement, Utf8SliceReplacementResultKind.Text, default, cancellationToken, deadline).Text;
        }

        var validation = Utf8Validation.Validate(input);
        var budget = deadline;
        if (UsesOwnedTemplateBeforeNativePlan() &&
            TryReplaceTemplateWithOwnedGroups(input, validation, replacement, Utf8SliceReplacementResultKind.Text,
                default, cancellationToken, deadline, out var ownedTemplate))
        {
            return ownedTemplate.Text;
        }

        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            var text = Encoding.UTF8.GetString(nativeResult);
            deadline.CheckNow();
            return text;
        }

        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            var text = Encoding.UTF8.GetString(ReplaceLiteralBytesCore(input, validation, exactLiteralReplacementBytes, deadline));
            deadline.CheckNow();
            return text;
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            var text = Encoding.UTF8.GetString(ReplaceLiteralBytesCore(input, validation, replacementBytes, deadline));
            deadline.CheckNow();
            return text;
        }

        return OwnedSliceReplace(input, _ownedSliceProgram, replacement, Utf8SliceReplacementResultKind.Text,
            default, cancellationToken, deadline).Text;
    }

    private OperationStatus TryReplaceCore(
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement,
        Span<byte> destination,
        out int bytesWritten,
        CancellationToken cancellationToken, Utf8ExecutionDeadline deadline)
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

        var validation = Utf8Validation.Validate(input);
        var budget = deadline;
        if (UsesOwnedTemplateBeforeNativePlan() &&
            TryReplaceTemplateWithOwnedGroups(input, validation, replacement, Utf8SliceReplacementResultKind.Destination,
                destination, cancellationToken, deadline, out var ownedTemplate))
        {
            bytesWritten = ownedTemplate.BytesWritten;
            return ownedTemplate.Status;
        }

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

    private byte[] ReplaceLiteralBytesCore(ReadOnlySpan<byte> input, Utf8ValidationResult validation,
        ReadOnlySpan<byte> replacementBytes, Utf8ExecutionDeadline deadline) =>
        ReplaceLiteralBytesCore(input, validation, replacementBytes, out _, deadline);

    private byte[] ReplaceLiteralBytesCore(ReadOnlySpan<byte> input, Utf8ValidationResult validation,
        ReadOnlySpan<byte> replacementBytes, out int replacementCount, Utf8ExecutionDeadline budget)
    {
        // R05: scalar-verifiable shapes replace literal templates through the scalar cursor
        // on astral-free non-ASCII input; ASCII and byte-safe inputs keep the factory cursor below.
        if (Utf8ScalarSimplePatternVerifier.CanServeScalar(_preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, input))
        {
            var scalarAnalysis = Utf8InputAnalyzer.Analyze(input);
            var scalarCursor = new Utf8OperationMatchCursor(input, _preparedRegex, scalarAnalysis.BoundaryMap, budget);
            return Utf8CursorReplaceEngine.Replace(input, replacementBytes, ref scalarCursor, out replacementCount);
        }

        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            input,
            validation,
            budget);
        return Utf8CursorReplaceEngine.Replace(input, replacementBytes, ref cursor, out replacementCount);
    }

    // Borrow the adapter's operation and result owner through final shaping.
    internal byte[] ReplaceExactLiteralForResult(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literalReplacementUtf8,
        int maximumReplacementCount,
        ref Utf8SliceInvocation invocation,
        ref Utf8InvocationResultLedger results,
        out int replacementCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumReplacementCount);
        if (UsesRightToLeft() || _preparedRegex.ExecutionKind != NativeExecutionKind.ExactAsciiLiteral ||
            _preparedRegex.LiteralUtf8 is not { Length: > 0 } literal)
            throw new InvalidOperationException("The exact replacement requires a nonempty ASCII literal plan.");
        _ = Utf8Validation.Validate(input, ref invocation);
        _ = Utf8Validation.Validate(literalReplacementUtf8, ref invocation);
        return Utf8ExactLiteralReplacement.Replace(input, literal, literalReplacementUtf8,
            maximumReplacementCount, ref invocation, ref results, out replacementCount);
    }

    private OperationStatus TryReplaceLiteralBytesCore(ReadOnlySpan<byte> input, Utf8ValidationResult validation,
        ReadOnlySpan<byte> replacementBytes, Span<byte> destination, out int bytesWritten, Utf8ExecutionDeadline budget)
    {
        // R05: scalar-verifiable shapes replace literal templates through the scalar cursor
        // on astral-free non-ASCII input; ASCII and byte-safe inputs keep the factory cursor below.
        if (Utf8ScalarSimplePatternVerifier.CanServeScalar(_preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, input))
        {
            var scalarAnalysis = Utf8InputAnalyzer.Analyze(input);
            var scalarCursor = new Utf8OperationMatchCursor(input, _preparedRegex, scalarAnalysis.BoundaryMap, budget);
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

    // Keep native discovery/capture-map eligibility. Finite deadlines
    // use a cursor that retains the operation budget rather than this template worker.
    private bool UsesOwnedTemplateBeforeNativePlan()
    {
        // Controlled calls retain their existing scalar invocation route.
        if (MatchTimeout != Regex.InfiniteMatchTimeout)
        {
            return false;
        }

        return _preparedRegex.ExecutionKind is NativeExecutionKind.FallbackRegex or NativeExecutionKind.AsciiSimplePattern;
    }

    // Expand original byte slices from the same native discovery/capture maps.
    // Validate the first projection before acquiring output. A later projection
    // failure throws instead of publishing a partially expanded result.
    private bool TryReplaceTemplateWithOwnedGroups(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, Utf8AnalyzedReplacement replacement,
        Utf8SliceReplacementResultKind resultKind, Span<byte> destination,
        CancellationToken token, Utf8ExecutionDeadline deadline, out Utf8SliceReplacementResult result)
    {
        // Keep the existing map-covered, groupless and single-wrap eligibility.
        // Finite deadlines use the existing scalar cursor path.
        result = default;
        if (MatchTimeout != Regex.InfiniteMatchTimeout || HasFiniteExecutionLimits || token.CanBeCanceled)
        {
            return false;
        }
        var hasMaps = _groupedLiteralSpanTable is not null || _alternationBranchSpanMap is not null;
        if (!hasMaps && _hasCaptures &&
            !(_groupNumbers.Length == 2 && GetSingleWrapCaptureNumber() == 1))
        {
            return false;
        }

        if (ShouldUseKelvinSignFallback(input))
        {
            return false;
        }

        var invocation = new Utf8SliceInvocation(InvocationLimits, deadline, token);
        var matches = EnumerateMatches(input, token);
        if (!matches.MoveNext())
        {
            result = CompleteUnchangedOwnedReplacement(input, validation.Utf16Length, resultKind,
                destination, ref invocation);
            return true;
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        var writer = default(Utf8SliceOutputWriter);
        var sourcePosition = 0;
        var first = true;
        try
        {
            do
            {
                var valueMatch = matches.Current;
                Utf8MatchContext context;
                if (_groupedLiteralSpanTable is not null &&
                    TryShapeGroupedLiteralDetailedMatch(input, analysis.BoundaryMap, valueMatch, out var grouped))
                {
                    context = grouped;
                }
                else if (_alternationBranchSpanMap is not null &&
                    TryShapeAlternationBranchDetailedMatch(input, analysis.BoundaryMap, valueMatch, out var branched))
                {
                    context = branched;
                }
                else if (!hasMaps && !_hasCaptures)
                {
                    context = new Utf8MatchContext(input, analysis.BoundaryMap, _program.CaptureCatalog, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16);
                }
                else if (!hasMaps && _hasCaptures &&
                    TryGetOwnedDetailedGroupCount(out var wrapGroups) &&
                    wrapGroups > 1)
                {
                    context = new Utf8MatchContext(input, analysis.BoundaryMap, _program.CaptureCatalog, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16, wrapGroups);
                }
                else if (!first)
                {
                    throw new InvalidOperationException("The grouped template projection failed after matching began.");
                }
                else
                {
                    return false;
                }

                if (first && !Utf8SliceOutputWriter.Rent(input.Length, ref invocation, out writer))
                    throw new InvalidOperationException("Owned replacement output could not be admitted.");
                first = false;
                if (!writer.Append(input.Slice(sourcePosition, valueMatch.IndexInBytes - sourcePosition), ref invocation))
                    throw new InvalidOperationException("Owned replacement output exceeded its capacity.");
                AppendOwnedGroupTemplate(replacement.Plan, input, in context, ref writer, ref invocation);
                sourcePosition = valueMatch.IndexInBytes + valueMatch.LengthInBytes;
            } while (matches.MoveNext());

            if (!writer.Append(input[sourcePosition..], ref invocation))
                throw new InvalidOperationException("Owned replacement output exceeded its capacity.");
            switch (resultKind)
            {
                case Utf8SliceReplacementResultKind.Bytes:
                    if (!writer.TryToArrayAndRelease(ref invocation, out var bytes))
                        throw new InvalidOperationException("Owned replacement output could not be published.");
                    result = new(string.Empty, bytes, OperationStatus.Done, 0);
                    break;
                case Utf8SliceReplacementResultKind.Text:
                    result = new(Utf8SliceBufferWork.DecodeUtf8(writer.WrittenBytes, ref invocation), [], OperationStatus.Done, 0);
                    break;
                case Utf8SliceReplacementResultKind.Destination:
                    if (writer.Length > destination.Length)
                        result = new(string.Empty, [], OperationStatus.DestinationTooSmall, 0);
                    else
                    {
                        var written = writer.Length;
                        Utf8SliceBufferWork.Copy(writer.WrittenBytes, destination, ref invocation);
                        result = new(string.Empty, [], OperationStatus.Done, written);
                    }
                    break;
                default: throw new InvalidOperationException("Unknown owned output kind.");
            }
            return true;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw CreateExecutionResourceFailure(ref invocation); }
        finally { writer.Release(ref invocation); }
    }

    private static void AppendOwnedGroupTemplate(Utf8ReplacementPlan plan, ReadOnlySpan<byte> input,
        in Utf8MatchContext context, ref Utf8SliceOutputWriter writer, ref Utf8SliceInvocation invocation)
    {
        foreach (var instruction in plan.Instructions)
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            ReadOnlySpan<byte> value;
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal: value = instruction.LiteralUtf8; break;
                case Utf8ReplacementInstructionKind.Group:
                case Utf8ReplacementInstructionKind.LastGroup:
                    var number = instruction.Kind == Utf8ReplacementInstructionKind.Group
                        ? instruction.GroupNumber : context.OwnedLastGroupNumber;
                    value = context.TryGetGroup(number, out var group) && group.Success ? group.GetValueBytes() : [];
                    break;
                case Utf8ReplacementInstructionKind.WholeMatch:
                    value = input.Slice(context.IndexInBytes, context.LengthInBytes); break;
                case Utf8ReplacementInstructionKind.LeftPortion: value = input[..context.IndexInBytes]; break;
                case Utf8ReplacementInstructionKind.RightPortion: value = input[(context.IndexInBytes + context.LengthInBytes)..]; break;
                case Utf8ReplacementInstructionKind.WholeString: value = input; break;
                default: throw new InvalidOperationException("Unsupported replacement instruction kind.");
            }
            if (!writer.Append(value, ref invocation))
                throw new InvalidOperationException("Owned replacement output exceeded its capacity.");
        }
    }

    private bool TryReplaceViaNativePlan(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8AnalyzedReplacement replacement,
        Utf8ExecutionDeadline budget,
        out byte[] replaced)
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

    private static bool TryGetWholeLiteralCaptureNumber(Utf8ConstructionSession storage,
        Utf8ExecutionNode node, out int captureNumber)
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

    private static void PushReplacementCaptureFrame(Utf8ConstructionBudget budget,
        Utf8ConstructionBuffer<ReplacementCaptureFrame> frames, Utf8ExecutionNode node)
    {
        budget.EnterDepth();
        try { frames.Push(new ReplacementCaptureFrame(node, -1)); }
        catch { budget.LeaveDepth(); throw; }
    }

    // R05: a lone capture wrapping the whole tree spans the hit exactly, no matter
    // what it encloses, as long as every enclosed node is span-transparent: plain
    // content, alternation branches, loops, atomics, and zero-width anchors.
    // Captures (including ones hidden in lookarounds), lookarounds themselves,
    // backreferences, and anything unrecognized decline so shaping never
    // misattributes a partial span.
    private int GetSingleWrapCaptureNumber()
    {
        if (_groupNumbers.Length != 2)
        {
            return 0;
        }

        if (_preparedRegex.ExecutionTree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
            root.Children.Count != 1 ||
            root.Children[0] is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 1 } wrap)
        {
            return 0;
        }

        for (var i = 0; i < wrap.Children.Count; i++)
        {
            if (!ContainsOnlySpanTransparentNodes(wrap.Children[i]))
            {
                return 0;
            }
        }

        return 1;
    }

    private static bool ContainsOnlySpanTransparentNodes(Utf8ExecutionNode node)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Concatenate:
            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Alternate:
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
            case Utf8ExecutionNodeKind.Atomic:
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (!ContainsOnlySpanTransparentNodes(node.Children[i]))
                    {
                        return false;
                    }
                }

                return true;
            case Utf8ExecutionNodeKind.One:
            case Utf8ExecutionNodeKind.Multi:
            case Utf8ExecutionNodeKind.Set:
            case Utf8ExecutionNodeKind.Empty:
            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Eol:
            case Utf8ExecutionNodeKind.Boundary:
            case Utf8ExecutionNodeKind.NonBoundary:
            case Utf8ExecutionNodeKind.ECMABoundary:
            case Utf8ExecutionNodeKind.NonECMABoundary:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
            case Utf8ExecutionNodeKind.EndZ:
            case Utf8ExecutionNodeKind.End:
                return node.Children.Count == 0;
            default:
                return false;
        }
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
