using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: substitution entry points, split from the Python facade along the substitution ownership boundary.
/// <summary>Substitution entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Replaces all matches and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement) => Replace(input, replacement, 0, 0);

    /// <summary>Replaces at most the requested number of matches and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count) =>
        Replace(input, replacement, count, 0);

    // PLAN.md D03: replacement templates are parsed once per distinct template and shared across operations; failures are never retained.
    private PythonReReplacementPlan GetReplacementPlan(string replacement) =>
        _replacementPlanCache.GetOrAdd(
            replacement,
            this,
            static (template, self) => PythonReReplacementParser.Parse(template, self._translation.CaptureGroupCount, self._namedGroups));

    /// <summary>Replaces matches after a byte offset and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes)
    {
        if (!_canMatchEmpty || _canUseManagedEmptyReplacementFastPath)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var plan = GetReplacementPlan(replacement);
            if (!_canMatchEmpty && TryReplaceViaUtf8Literal(
                    input,
                    plan,
                    count,
                    startOffsetInBytes,
                    out var resultBytes,
                    out _))
            {
                return resultBytes;
            }

            var subject = Decode(input);
            var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (TryReplaceOwnedTemplate(input, subject, plan, count, startOffsetInBytes, out var ownedText, out _))
        {
            return Encoding.UTF8.GetBytes(ownedText);
        }

            return Encoding.UTF8.GetBytes(ReplaceManagedSubject(subject, plan, count, startOffsetInUtf16));
        }

        return Encoding.UTF8.GetBytes(ReplaceToString(input, replacement, count, startOffsetInBytes));
    }

    /// <summary>Replaces all matches and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement) =>
        ReplaceToString(input, replacement, 0, 0);

    /// <summary>Replaces at most the requested number of matches and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int count) =>
        ReplaceToString(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes)
    {
        if (!_canMatchEmpty || _canUseManagedEmptyReplacementFastPath)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var plan = GetReplacementPlan(replacement);
            if (!_canMatchEmpty && TryReplaceViaUtf8Literal(
                    input,
                    plan,
                    count,
                    startOffsetInBytes,
                    out var resultBytes,
                    out _))
            {
                return Encoding.UTF8.GetString(resultBytes);
            }

            var subject = Decode(input);
            var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (TryReplaceOwnedTemplate(input, subject, plan, count, startOffsetInBytes, out var ownedText, out _))
        {
            return ownedText;
        }

            return ReplaceManagedSubject(subject, plan, count, startOffsetInUtf16);
        }

        return SubnToString(input, replacement, count, startOffsetInBytes).ResultText;
    }

    // R04: template substitution on literal shapes discovers through the shared owned cursor
    // and expands every replacement from owned group spans; the first hit validates the branch
    // channel before any output, so an unknown branch still declines cleanly to managed routes.
    private bool TryReplaceOwnedTemplate(
        ReadOnlySpan<byte> input,
        string subject,
        PythonReReplacementPlan plan,
        int count,
        int startOffsetInBytes,
        out string result,
        out int replacementCount)
    {
        result = string.Empty;
        replacementCount = 0;
        if (_utf8Regex is null || _canMatchEmpty)
        {
            return false;
        }

        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (!_utf8Regex.TryCreateOwnedSplitCursor(input, out var cursor, out var map) || map is null)
        {
            return false;
        }

        var builder = new StringBuilder(subject.Length);
        builder.Append(subject.AsSpan(0, startOffsetInUtf16));
        var replaced = 0;
        var lastIndex = startOffsetInUtf16;
        var indexMap = default(PythonReUtf8IndexMap);
        var hasIndexMap = false;
        var first = true;
        try
        {
            while ((count == 0 || replaced < count) && cursor.MoveNext())
            {
                var match = cursor.CurrentValueMatch;
                if (!match.Success || match.IndexInUtf16 < startOffsetInUtf16)
                {
                    continue;
                }

                var branch = cursor.LastLiteralFamilyBranchId;
                if (first)
                {
                    first = false;
                    if (branch < 0 || (uint)branch >= (uint)map.BranchGroups.Length)
                    {
                        return false;
                    }
                }

                if (!hasIndexMap)
                {
                    indexMap = PythonReUtf8IndexMap.Create(input, subject);
                    hasIndexMap = true;
                }

                builder.Append(subject.AsSpan(lastIndex, match.IndexInUtf16 - lastIndex));
                var groups = CreateOwnedPythonGroups(indexMap, match, map, branch);
                builder.Append(plan.Expand(new Utf8PythonMatchContext(input, groups, _nameEntries)));
                lastIndex = match.IndexInUtf16 + match.LengthInUtf16;
                replaced++;
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(subject, _translation.Pattern, MatchTimeout);
        }

        builder.Append(subject.AsSpan(lastIndex));
        result = builder.ToString();
        replacementCount = replaced;
        return true;
    }
    /// <summary>Replaces all matches and returns the string plus replacement count.</summary>

    public Utf8PythonSubnResult SubnToString(ReadOnlySpan<byte> input, string replacement) =>
        SubnToString(input, replacement, 0, 0);

    /// <summary>Replaces a bounded number of matches and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString(ReadOnlySpan<byte> input, string replacement, int count) =>
        SubnToString(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString(
        ReadOnlySpan<byte> input,
        string replacement,
        int count,
        int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var plan = GetReplacementPlan(replacement);
        if (!_canMatchEmpty || _canUseManagedEmptyReplacementFastPath)
        {
            if (!_canMatchEmpty && TryReplaceViaUtf8Literal(
                    input,
                    plan,
                    count,
                    startOffsetInBytes,
                    out var resultBytes,
                    out var replacementCount))
            {
                return new Utf8PythonSubnResult
                {
                    ResultText = Encoding.UTF8.GetString(resultBytes),
                    ReplacementCount = replacementCount,
                };
            }

        if (TryReplaceOwnedTemplate(input, Decode(input), plan, count, startOffsetInBytes, out var ownedText, out var ownedCount))
        {
            return new Utf8PythonSubnResult
            {
                ResultText = ownedText,
                ReplacementCount = ownedCount,
            };
        }

            return SubnToStringViaManagedRegex(input, plan, count, startOffsetInBytes);
        }

        var subject = Decode(input);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var builder = new StringBuilder();
        builder.Append(subject.AsSpan(0, startOffsetInUtf16));

        var replaced = 0;
        var lastIndex = startOffsetInUtf16;
        var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
        while (cursor.TryMoveNext(out var match))
        {
            var snapshot = CreateMatchSnapshot(match, indexMap);
            var context = snapshot.ToContext(input, _nameEntries);
            var value = context.Value;
            if (count == 0 || replaced < count)
            {
                builder.Append(subject.AsSpan(lastIndex, value.StartOffsetInUtf16 - lastIndex));
                builder.Append(plan.Expand(context));
                lastIndex = value.EndOffsetInUtf16;
                replaced++;
            }
            else
            {
                break;
            }
        }

        builder.Append(subject.AsSpan(lastIndex));
        return new Utf8PythonSubnResult
        {
            ResultText = builder.ToString(),
            ReplacementCount = replaced,
        };
    }

    /// <summary>Replaces all matches and returns UTF-8 bytes plus replacement count.</summary>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement) =>
        Subn(input, replacement, 0, 0);

    /// <summary>Replaces a bounded number of matches and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement, int count) =>
        Subn(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn(
        ReadOnlySpan<byte> input,
        string replacement,
        int count,
        int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var plan = GetReplacementPlan(replacement);
        if (!_canMatchEmpty || _canUseManagedEmptyReplacementFastPath)
        {
            if (!_canMatchEmpty && TryReplaceViaUtf8Literal(
                    input,
                    plan,
                    count,
                    startOffsetInBytes,
                    out var resultBytes,
                    out var replacementCount))
            {
                return new Utf8PythonSubnUtf8Result
                {
                    ResultBytes = resultBytes,
                    ReplacementCount = replacementCount,
                };
            }

        var ownedSubject = Decode(input);
        if (TryReplaceOwnedTemplate(input, ownedSubject, plan, count, startOffsetInBytes, out var ownedText, out var ownedCount))
        {
            return new Utf8PythonSubnUtf8Result
            {
                ResultBytes = Encoding.UTF8.GetBytes(ownedText),
                ReplacementCount = ownedCount,
            };
        }

            var managedResult = SubnToStringViaManagedRegex(input, plan, count, startOffsetInBytes);
            return new Utf8PythonSubnUtf8Result
            {
                ResultBytes = Encoding.UTF8.GetBytes(managedResult.ResultText),
                ReplacementCount = managedResult.ReplacementCount,
            };
        }

        return SubnManagedUtf8(
            input,
            startOffsetInBytes,
            count,
            (source, match, indexMap, utf16BaseOffset, state) =>
                state.ExpandToUtf8(source, CreateMatchSnapshot(match, indexMap, utf16BaseOffset).Groups),
            plan);
    }

    /// <summary>Replaces all matches with a stateful string evaluator and returns UTF-8 bytes.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => Replace(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful string evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => Replace(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful string evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return Subn(input, state, evaluator, count, startOffsetInBytes).ResultBytes;
    }

    /// <summary>Replaces all matches with a stateful evaluator and returns a string.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => ReplaceToString(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful evaluator.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => ReplaceToString(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful evaluator.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnToString(input, state, evaluator, count, startOffsetInBytes).ResultText;
    }

    /// <summary>Evaluates all replacements and returns the string plus replacement count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => SubnToString(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of replacements and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => SubnToString(input, state, evaluator, count, 0);

    /// <summary>Evaluates replacements after a byte offset and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);

        var subject = Decode(input);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var builder = new StringBuilder(subject.Length);
        builder.Append(subject.AsSpan(0, startOffsetInUtf16));

        var replaced = 0;
        var lastIndex = startOffsetInUtf16;
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            while (cursor.TryMoveNext(out var match))
            {
                var detailed = CreateDetailedMatchData(input, match, indexMap);
                var value = detailed.Value;
                if (count == 0 || replaced < count)
                {
                    builder.Append(subject.AsSpan(lastIndex, value.StartOffsetInUtf16 - lastIndex));
                    builder.Append(evaluator(state, detailed));
                    lastIndex = value.EndOffsetInUtf16;
                    replaced++;
                }
                else
                {
                    break;
                }
            }

        builder.Append(subject.AsSpan(lastIndex));
        return new Utf8PythonSubnResult
        {
            ResultText = builder.ToString(),
            ReplacementCount = replaced,
        };
    }

    /// <summary>Evaluates all string replacements and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => Subn(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of string replacements and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => Subn(input, state, evaluator, count, 0);

    /// <summary>Evaluates string replacements after a byte offset and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnManagedUtf8(
            input,
            startOffsetInBytes,
            count,
            (source, match, indexMap, utf16BaseOffset, state) => Encoding.UTF8.GetBytes(
                state.Evaluator(
                    state.Value,
                    CreateDetailedMatchData(source, match, indexMap, state.NameEntries, utf16BaseOffset))),
            (Value: state, Evaluator: evaluator, NameEntries: _nameEntries));
    }

    /// <summary>Replaces all matches with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator) => Replace(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count) => Replace(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return Subn(input, state, evaluator, count, startOffsetInBytes).ResultBytes;
    }

    /// <summary>Evaluates all UTF-8 replacements and returns bytes plus replacement count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator) => Subn(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of UTF-8 replacements and returns bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count) => Subn(input, state, evaluator, count, 0);

    /// <summary>Evaluates UTF-8 replacements after a byte offset and returns bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnManagedUtf8(
            input,
            startOffsetInBytes,
            count,
            (source, match, indexMap, utf16BaseOffset, state) => state.Evaluator(
                state.Value,
                CreateDetailedMatchData(source, match, indexMap, state.NameEntries, utf16BaseOffset)),
            (Value: state, Evaluator: evaluator, NameEntries: _nameEntries));
    }
}
