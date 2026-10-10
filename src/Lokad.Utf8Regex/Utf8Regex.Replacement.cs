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

// PLAN.md D09: replacement execution cores, split from the matching facade along the replacement ownership boundary.
/// <summary>Replacement execution cores for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    private byte[] ReplaceCore(
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement,
        CancellationToken cancellationToken)
    {
        if (GetOwnedReplacementProgram(input, replacement, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceReplace(input, ownedProgram, replacement, Utf8SliceReplacementResultKind.Bytes, default, cancellationToken).Bytes;
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        if (UsesOwnedTemplateBeforeNativePlan() &&
            TryReplaceTemplateWithOwnedGroups(input, replacement, out var ownedTemplate))
        {
            return Encoding.UTF8.GetBytes(ownedTemplate);
        }

        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceWithScalarProgram(input, replacement, cancellationToken));
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes);
    }

    private Utf8AnalyzedReplacement GetParsedReplacement(string replacement)
    {
        return _replacementCache.GetOrAdd(
            replacement,
            (GroupNumbers: _groupNumbers, GroupNames: _groupNames, Ecma: (Options & RegexOptions.ECMAScript) != 0),
            static (replacementText, state) => Utf8FrontEndReplacementAnalyzer.Analyze(
                replacementText,
                state.GroupNumbers,
                state.GroupNames,
                state.Ecma));
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

    private static bool TryGetDirectLiteralReplacementBytes(
        ReadOnlySpan<byte> replacementUtf8,
        out byte[] replacementBytes)
    {
        if (replacementUtf8.IndexOf((byte)'$') >= 0)
        {
            replacementBytes = [];
            return false;
        }

        replacementBytes = replacementUtf8.ToArray();
        return true;
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

    private string ReplaceToStringCore(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement, CancellationToken cancellationToken)
    {
        if (GetOwnedReplacementProgram(input, replacement, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceReplace(input, ownedProgram, replacement, Utf8SliceReplacementResultKind.Text, default, cancellationToken).Text;
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        if (UsesOwnedTemplateBeforeNativePlan() &&
            TryReplaceTemplateWithOwnedGroups(input, replacement, out var ownedTemplate))
        {
            return ownedTemplate;
        }


        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            return Encoding.UTF8.GetString(nativeResult);
        }

        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            return Encoding.UTF8.GetString(Replace(input, exactLiteralReplacementBytes));
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return Encoding.UTF8.GetString(Replace(input, replacementBytes));
        }

        return ReplaceWithScalarProgram(input, replacement, cancellationToken);
    }

    private OperationStatus TryReplaceCore(
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement,
        Span<byte> destination,
        out int bytesWritten,
        CancellationToken cancellationToken)
    {
        if (GetOwnedReplacementProgram(input, replacement, cancellationToken) is { } ownedProgram)
        {
            var result = OwnedSliceReplace(input, ownedProgram, replacement, Utf8SliceReplacementResultKind.Destination, destination, cancellationToken);
            bytesWritten = result.BytesWritten;
            return result.Status;
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        if (UsesOwnedTemplateBeforeNativePlan() &&
            TryReplaceTemplateWithOwnedGroups(input, replacement, out var ownedTemplate) &&
            TryEncodeUtf8ToDestination(ownedTemplate, destination, out bytesWritten))
        {
            return OperationStatus.Done;
        }

        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            return TryReplaceLiteralBytesCore(
                input,
                validation,
                exactLiteralReplacementBytes,
                destination,
                out bytesWritten);
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return TryReplaceLiteralBytesCore(
                input,
                validation,
                replacementBytes,
                destination,
                out bytesWritten);
        }

        return TryEncodeUtf8ToDestination(ReplaceWithScalarProgram(input, replacement, cancellationToken), destination, out bytesWritten)
            ? OperationStatus.Done
            : OperationStatus.DestinationTooSmall;
    }

    private byte[] ReplaceLiteralBytesCore(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        ReadOnlySpan<byte> replacementBytes) =>
        ReplaceLiteralBytesCore(input, validation, replacementBytes, out _);

    private byte[] ReplaceLiteralBytesCore(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        ReadOnlySpan<byte> replacementBytes,
        out int replacementCount)
    {
        var budget = CreateExecutionBudget();
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

    internal byte[] ReplaceLiteralWithCount(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literalReplacementUtf8,
        out int replacementCount)
        => ReplaceLiteralWithCount(
            input,
            literalReplacementUtf8,
            int.MaxValue,
            out replacementCount);

    internal byte[] ReplaceLiteralWithCount(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literalReplacementUtf8,
        int maximumReplacementCount,
        out int replacementCount)
    {
        try
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maximumReplacementCount);
            var validation = TryUseAsciiInputValidationShortcut(input)
                ? default
                : Utf8Validation.Validate(input);
            _ = Utf8Validation.Validate(literalReplacementUtf8);
            if (!UsesRightToLeft() &&
                _preparedRegex.ExecutionKind == NativeExecutionKind.ExactAsciiLiteral &&
                _preparedRegex.LiteralUtf8 is { Length: > 0 } literal)
            {
                return Utf8ExactLiteralReplacement.Replace(
                    input,
                    literal,
                    literalReplacementUtf8,
                    maximumReplacementCount,
                    CreateExecutionBudget(),
                    out replacementCount);
            }

            if (maximumReplacementCount != int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Bounded literal replacement requires the exact ASCII literal execution route.");
            }

            return ReplaceLiteralBytesCore(input, validation, literalReplacementUtf8, out replacementCount);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private OperationStatus TryReplaceLiteralBytesCore(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        ReadOnlySpan<byte> replacementBytes,
        Span<byte> destination,
        out int bytesWritten)
    {
        var budget = CreateExecutionBudget();
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

    private string ReplaceWithScalarProgram(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement,
        CancellationToken cancellationToken)
    {
        return OwnedSliceReplace(input, _ownedSliceProgram, replacement,
            Utf8SliceReplacementResultKind.Text, default, cancellationToken).Text;
    }

    // Keep the qualified template/native allocation order. Finite deadlines
    // use a cursor that retains the operation budget rather than this template worker.
    private bool UsesOwnedTemplateBeforeNativePlan()
    {
        // The decoded template worker does not retain a finite invocation budget.
        if (MatchTimeout != Regex.InfiniteMatchTimeout)
        {
            return false;
        }

        return _preparedRegex.ExecutionKind is NativeExecutionKind.FallbackRegex or NativeExecutionKind.AsciiSimplePattern;
    }

    // R05: expands group-reference templates for grouped shapes from owned discovery
    // hits without executing the fallback engine. Groupless shapes use the dedicated
    // arm below so this worker never allocates for them (see the destination
    // allocation bound test). The first hit validates the projection channel before
    // any output; a later projection failure throws instead of emitting a half-owned
    // result, mirroring the evaluator contract.
    private bool TryReplaceTemplateWithOwnedGroups(
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement,
        out string replaced)
    {
        // The decode below runs only for map-covered, groupless, or single-wrap
        // shapes, so other routes keep their allocation profile (see the
        // destination allocation bound test). Finite deadlines use the cursor path.
        replaced = string.Empty;
        if (MatchTimeout != Regex.InfiniteMatchTimeout)
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

        var decoded = Encoding.UTF8.GetString(input);
        var analysis = Utf8InputAnalyzer.Analyze(input);
        var builder = new StringBuilder(decoded.Length);
        var sourcePosition = 0;
        var first = true;
        foreach (var valueMatch in EnumerateMatches(input))
        {
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

            first = false;
            builder.Append(decoded, sourcePosition, valueMatch.IndexInUtf16 - sourcePosition);
            builder.Append(Utf8ReplacementPlanInterpreter.ApplyOwned(replacement.Plan, decoded, in context));
            sourcePosition = valueMatch.IndexInUtf16 + valueMatch.LengthInUtf16;
        }

        builder.Append(decoded, sourcePosition, decoded.Length - sourcePosition);
        replaced = builder.ToString();
        return true;
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
            !Utf8NativeReplacementExecutor.CanExecute(plan) ||
            plan.ReferencedCaptureGroups.Length > 0)
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

    private bool TryReplaceViaNativePlan(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8AnalyzedReplacement replacement,
        Utf8ExecutionDeadline budget,
        Span<byte> destination,
        out int bytesWritten)
    {
        var plan = RewriteWholeLiteralCapturePlan(replacement.Plan);
        if (replacement.IsLiteral ||
            !Utf8NativeReplacementExecutor.CanExecute(plan) ||
            plan.ReferencedCaptureGroups.Length > 0)
        {
            bytesWritten = 0;
            return false;
        }

        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            input,
            validation,
            budget);
        return Utf8NativeReplacementExecutor.TryReplace(
            input,
            plan,
            ref cursor,
            destination,
            out bytesWritten);
    }

    private Utf8ReplacementPlan RewriteWholeLiteralCapturePlan(Utf8ReplacementPlan plan)
    {
        var captureNumber = GetWholeLiteralCaptureNumber();
        if (captureNumber <= 0)
        {
            return plan;
        }

        List<Utf8ReplacementInstruction>? rewritten = null;
        for (var i = 0; i < plan.Instructions.Count; i++)
        {
            var instruction = plan.Instructions[i];
            if (instruction.Kind == Utf8ReplacementInstructionKind.Group &&
                instruction.GroupNumber == captureNumber)
            {
                rewritten ??= [.. plan.Instructions];
                rewritten[i] = Utf8ReplacementInstruction.WholeMatch();
            }
        }

        return rewritten is null ? plan : new Utf8ReplacementPlan(rewritten);
    }

    private int GetWholeLiteralCaptureNumber()
    {
        if (_groupNumbers.Length == 2 &&
            Pattern.Length >= 3 &&
            Pattern[0] == '(' &&
            Pattern[^1] == ')' &&
            IsPlainLiteralPattern(Pattern[1..^1]))
        {
            return 1;
        }

        if (_preparedRegex.ExecutionTree?.Root is not { } root ||
            root.Kind != Utf8ExecutionNodeKind.Capture ||
            root.Children.Count != 1)
        {
            return 0;
        }

        return TryGetWholeLiteralCaptureNumber(root.Children[0], out var captureNumber)
            ? captureNumber
            : 0;
    }

    private static bool TryGetWholeLiteralCaptureNumber(Utf8ExecutionNode node, out int captureNumber)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Capture when node.Children.Count == 1:
                if (TryGetWholeLiteralCaptureNumber(node.Children[0], out _))
                {
                    captureNumber = node.CaptureNumber;
                    return captureNumber > 0;
                }

                break;

            case Utf8ExecutionNodeKind.Group when node.Children.Count == 1:
                return TryGetWholeLiteralCaptureNumber(node.Children[0], out captureNumber);

            case Utf8ExecutionNodeKind.Concatenate:
                Utf8ExecutionNode? substantiveChild = null;
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var child = node.Children[i];
                    if (IsZeroWidthBoundaryNode(child.Kind))
                    {
                        continue;
                    }

                    if (substantiveChild is not null)
                    {
                        captureNumber = 0;
                        return false;
                    }

                    substantiveChild = child;
                }

                if (substantiveChild is not null)
                {
                    return TryGetWholeLiteralCaptureNumber(substantiveChild, out captureNumber);
                }

                break;

            case Utf8ExecutionNodeKind.One:
            case Utf8ExecutionNodeKind.Multi:
                captureNumber = 0;
                return true;

            case Utf8ExecutionNodeKind.Alternate:
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (!TryGetWholeLiteralCaptureNumber(node.Children[i], out _))
                    {
                        captureNumber = 0;
                        return false;
                    }
                }

                captureNumber = 0;
                return true;
        }

        captureNumber = 0;
        return false;
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

    private static bool IsPlainLiteralPattern(string pattern)
    {
        foreach (var ch in pattern)
        {
            if (Utf8RegexSyntax.IsRegexMetaCharacter(ch))
            {
                return false;
            }
        }

        return true;
    }
}
