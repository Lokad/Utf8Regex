namespace Lokad.Utf8Regex.Pcre2;

// PLAN.md D09: diagnostic and debug-projection operations, split from the matching facade along the diagnostics ownership boundary.
/// <summary>Diagnostic and debug-projection operations for <see cref="Utf8Pcre2Regex"/>.</summary>
public sealed partial class Utf8Pcre2Regex
{
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

    internal string DebugExecutionKindName => _program.Operations.Match.Kind.ToString();

    internal Pcre2CompiledProgram DebugCompiledProgram => _program;

    internal bool DebugUsesUtf8RegexTranslation => UsesUtf8Translation;


    internal string DebugUtf8RegexExecutionKindName => HasPrimaryUtf8Regex ? PrimaryUtf8Regex.Inspection.ExecutionKind.ToString() : "<none>";

    internal int DebugReplacementCacheEntryCount => _replacementComponent.Count;

    internal string DebugDescribeExecutionPlan()
        => $"IsMatch={_program.Operations.IsMatch.Kind}, Count={_program.Operations.Count.Kind}, Enumerate={_program.Operations.Enumerate.Kind}, Match={_program.Operations.Match.Kind}, Replace={_program.Operations.Replace.Kind}";
}
