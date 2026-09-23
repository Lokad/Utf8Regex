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
        string replacementText,
        Utf8AnalyzedReplacement replacement,
        CancellationToken cancellationToken)
    {
        if (RequiresCultureSensitiveFallback || ShouldUseKelvinSignFallback(input))
        {
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Utf8Validation.DecodeStrict(input),
                replacementText));
        }

        if (_sliceProgram is not null)
        {
            return OwnedSliceReplace(input, _sliceProgram, replacement, cancellationToken);
        }

        if (ShouldUseFallbackForSimplePatternReplacement(replacement))
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText));
        }

        if (UsesRightToLeft())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText));
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceFallbackWithSharedPlan(input, replacement));
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes);
    }

    private Utf8AnalyzedReplacement GetParsedReplacement(string replacement)
    {
        return _replacementCache.GetOrAdd(
            replacement,
            (GroupNumbers: _groupNumbers, GroupNames: _groupNames),
            static (replacementText, state) => Utf8FrontEndReplacementAnalyzer.Analyze(
                replacementText,
                state.GroupNumbers,
                state.GroupNames));
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

    private string ReplaceToStringCore(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement)
    {
        if (RequiresCultureSensitiveFallback || ShouldUseKelvinSignFallback(input))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Utf8Validation.DecodeStrict(input),
                replacement.OriginalText);
        }


        if (ShouldUseFallbackForSimplePatternReplacement(replacement))
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacement.OriginalText);
        }

        if (UsesRightToLeft())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return ReplaceFallbackWithSharedPlan(input, replacement);
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
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

        return ReplaceFallbackWithSharedPlan(input, replacement);
    }

    private OperationStatus TryReplaceCore(
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement,
        string replacementText,
        Span<byte> destination,
        out int bytesWritten)
    {
        if (RequiresCultureSensitiveFallback || ShouldUseKelvinSignFallback(input))
        {
            var replaced = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Utf8Validation.DecodeStrict(input),
                replacementText);
            return TryEncodeUtf8ToDestination(replaced, destination, out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }


        if (ShouldUseFallbackForSimplePatternReplacement(replacement))
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            var replaced = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText);
            return TryEncodeUtf8ToDestination(replaced, destination, out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        if (UsesRightToLeft())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return TryEncodeUtf8ToDestination(ReplaceFallbackWithSharedPlan(input, replacement), destination, out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        if (TryReplaceViaNativePlan(input, validation, replacement, budget, destination, out bytesWritten))
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

        return TryEncodeUtf8ToDestination(ReplaceFallbackWithSharedPlan(input, replacement), destination, out bytesWritten)
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
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
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
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
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

    private string ReplaceFallbackWithSharedPlan(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement)
    {
        var decoded = Encoding.UTF8.GetString(input);
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
            decoded,
            match => Utf8ReplacementPlanInterpreter.Apply(replacement.Plan, match, decoded));
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
            _verifierRuntime,
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
            _verifierRuntime,
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
