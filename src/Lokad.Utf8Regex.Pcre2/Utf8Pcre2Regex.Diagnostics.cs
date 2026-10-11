using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Pcre2;

// PLAN.md D09: diagnostic and debug-projection operations, split from the matching facade along the diagnostics ownership boundary.
/// <summary>Diagnostic and debug-projection operations for <see cref="Utf8Pcre2Regex"/>.</summary>
public sealed partial class Utf8Pcre2Regex
{
    internal int DebugCountRaw(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCount(
                _program,
                ref subject,
                start,
                Pcre2MatchOptions.None,
                CancellationToken.None,
                out var directCount))
        {
            return directCount;
        }

        if (_program.Operations.Count.Kind == Pcre2DirectProgramKind.Pcre2Literal)
        {
            return Count(input, startOffsetInBytes, Pcre2MatchOptions.None);
        }

        if (_program.Operations.Count.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            return PrimaryUtf8Regex.ByteOffsetExecution.CountPrepared(subject, start);
        }

        return Count(input, startOffsetInBytes, Pcre2MatchOptions.None);
    }

    internal Pcre2IsMatchDiagnostics DebugIsMatchWithDiagnostics(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var backtrackingProgram = _program.Operations.IsMatch switch
        {
            Pcre2BacktrackingDirectProgram direct => direct.Program,
            Pcre2AsciiRegularIsMatchDirectProgram asciiRegular => asciiRegular.Fallback,
            Pcre2LiteralFamilyDirectProgram literalFamily => literalFamily.Fallback,
            Pcre2MultilinePrefixDirectProgram multilinePrefix => multilinePrefix.Fallback,
            Pcre2LiteralPrefixRepeatDirectProgram literalPrefixRepeat => literalPrefixRepeat.Fallback,
            Pcre2PalindromeIsMatchDirectProgram palindrome => palindrome.Fallback,
            Pcre2LeadingDotStarLiteralIsMatchDirectProgram leadingDotStarLiteral => leadingDotStarLiteral.Fallback,
            Pcre2SeparatedRunsIsMatchDirectProgram separatedRuns => separatedRuns.Fallback,
            Pcre2AsciiBoundedIsMatchDirectProgram asciiBounded => asciiBounded.Fallback,
            Pcre2AsciiAnchoredDfaIsMatchDirectProgram asciiAnchoredDfa => asciiAnchoredDfa.Fallback,
            _ => null,
        };
        if (backtrackingProgram is null)
        {
            return new Pcre2IsMatchDiagnostics(IsMatch(input, startOffsetInBytes), default);
        }

        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(
            backtrackingProgram,
            _program.CandidateSearchPlan,
            subject,
            start,
            Pcre2MatchOptions.None,
            _program.Request,
            collectDiagnostics: true);
        return new Pcre2IsMatchDiagnostics(cursor.MoveNext(), cursor.Diagnostics);
    }

    internal Pcre2CountDiagnostics DebugCountWithDiagnostics(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var backtrackingProgram = _program.Operations.Enumerate switch
        {
            Pcre2BacktrackingDirectProgram direct => direct.Program,
            Pcre2MultilinePrefixDirectProgram multilinePrefix => multilinePrefix.Fallback,
            Pcre2LiteralPrefixRepeatDirectProgram literalPrefixRepeat => literalPrefixRepeat.Fallback,
            _ => null,
        };
        if (backtrackingProgram is null)
        {
            return new Pcre2CountDiagnostics(Count(input, startOffsetInBytes), default);
        }

        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(
            backtrackingProgram,
            _program.CandidateSearchPlan,
            subject,
            start,
            Pcre2MatchOptions.None,
            _program.Request,
            collectDiagnostics: true);
        var count = 0;
        while (cursor.MoveNext())
        {
            count = checked(count + 1);
        }

        return new Pcre2CountDiagnostics(count, cursor.Diagnostics);
    }

    internal int DebugEnumerateRawIndexSum(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                Pcre2MatchOptions.None,
                out var directCursor))
        {
            var directSum = 0;
            while (directCursor.MoveNext())
            {
                directSum += directCursor.Current.StartOffsetInBytes;
            }

            return directSum;
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Pcre2Literal)
        {
            return ExecutePublicEnumerateIndexSum(EnumerateMatches(
                input,
                startOffsetInBytes,
                Pcre2MatchOptions.None));
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            var sum = 0;
            var enumerator = PrimaryUtf8Regex.ByteOffsetExecution.EnumerateMatches(subject, start);
            while (enumerator.MoveNext())
            {
                if (!enumerator.Current.TryGetByteRange(out var indexInBytes, out _))
                {
                    throw new InvalidOperationException("Managed Utf8Regex fallback returned a match that is not aligned to byte boundaries.");
                }

                sum += indexInBytes;
            }

            return sum;
        }

        var fallbackSum = 0;
        var fallbackEnumerator = EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None);
        while (fallbackEnumerator.MoveNext())
        {
            fallbackSum += fallbackEnumerator.Current.StartOffsetInBytes;
        }

        return fallbackSum;
    }

    internal int DebugEnumeratePublicConstructionOnly(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        _ = EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None);
        return 1;
    }

    internal int DebugEnumerateNativeMaterializationOnly(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                Pcre2MatchOptions.None,
                out var directCursor))
        {
            var directCount = 0;
            while (directCursor.MoveNext())
            {
                directCount++;
            }

            return directCount;
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Pcre2Literal)
        {
            return ExecutePublicEnumerateMoveNextCount(EnumerateMatches(
                input,
                startOffsetInBytes,
                Pcre2MatchOptions.None));
        }

        return ExecutePublicEnumerateMoveNextCount(EnumerateMatches(
            input,
            startOffsetInBytes,
            Pcre2MatchOptions.None));
    }

    internal int DebugEnumerateInternalPublicMoveNextCount(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateMoveNextCount(EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None));
    }

    internal int DebugEnumerateInternalPublicIndexSum(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateIndexSum(EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None));
    }

    internal int DebugEnumerateInternalPublicCurrentCount(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateCurrentCount(EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None));
    }

    internal int DebugEnumerateInternalPublicCurrentStartSum(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateCurrentStartSum(EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None));
    }

    private static int ExecutePublicEnumerateMoveNextCount(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecutePublicEnumerateIndexSum(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var sum = 0;
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.StartOffsetInBytes;
        }

        return sum;
    }

    private static int ExecutePublicEnumerateCurrentCount(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var count = 0;
        while (enumerator.MoveNext())
        {
            _ = enumerator.Current;
            count++;
        }

        return count;
    }

    private static int ExecutePublicEnumerateCurrentStartSum(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var sum = 0;
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            sum += current.StartOffsetInBytes;
        }

        return sum;
    }

    internal int DebugEvaluateFirstReplacementOnly(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var match = MatchDetailed(input, startOffsetInBytes, Pcre2MatchOptions.None);
        if (!match.Success)
        {
            return 0;
        }

        var value = match.Value;
        var groups = CaptureGroups(match);
        var writer = CreateTemplateWriter(input, CancellationToken.None);
        try
        {
            // Attribution now measures the actual UTF-8 expansion and returns byte length.
            writer.AppendTemplate((substitutionOptions & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
                ? new Pcre2ReplacementPreparation(Pcre2SimpleReplacementParser.Literal(replacement), null)
                : GetReplacementPreparation(replacement, substitutionOptions), substitutionOptions, groups, value.StartOffsetInBytes,
                value.EndOffsetInBytes - value.StartOffsetInBytes, match.Mark);
            return writer.Length;
        }
        finally { writer.Release(); }
    }

    internal string DebugExecutionKindName => _program.Operations.Match.Kind.ToString();

    internal Pcre2CompiledProgram DebugCompiledProgram => _program;

    internal bool DebugUsesUtf8RegexTranslation => UsesUtf8Translation;


    internal string DebugUtf8RegexExecutionKindName => HasPrimaryUtf8Regex ? PrimaryUtf8Regex.Inspection.ExecutionKind.ToString() : "<none>";

    internal int DebugReplacementCacheEntryCount => _replacementComponent.Count;

    internal string DebugDescribeExecutionPlan()
        => $"IsMatch={_program.Operations.IsMatch.Kind}, Count={_program.Operations.Count.Kind}, Enumerate={_program.Operations.Enumerate.Kind}, Match={_program.Operations.Match.Kind}, Replace={_program.Operations.Replace.Kind}";
}
