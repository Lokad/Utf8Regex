using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ExecutionArchitectureTests
{
    [Fact]
    public void EnumeratorLayoutDiagnosticsReportEveryComponent()
    {
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugSizeInBytes > 0);
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugUtf8PreparedEnumeratorSizeInBytes > 0);
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugUtf8EnumeratorSizeInBytes > 0);
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugManagedEnumeratorSizeInBytes > 0);
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugMaterializedStateSizeInBytes > 0);
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugValueDataSizeInBytes > 0);
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugGroupDataSizeInBytes > 0);
        Assert.True(Utf8OperationMatchCursor.DebugSizeInBytes > 0);
        Assert.True(Pcre2GlobalMatchCursor.DebugSizeInBytes > 0);
        Assert.True(Pcre2DirectGlobalMatchCursor.DebugSizeInBytes > 0);
        Assert.True(Pcre2ResourceBudget.DebugSizeInBytes > 0);
        Assert.True(Pcre2ResourceBudget.DebugSizeInBytes < Pcre2ResourceBudget.DebugDiagnosticsSizeInBytes);
        Assert.True(Pcre2CandidateSearchProgram.DebugSizeInBytes > 0);
        Assert.True(Pcre2LiteralFamilyGlobalMatchCursor.DebugSizeInBytes > 0);
        Assert.True(Pcre2BacktrackingDetailedGlobalMatchCursor.DebugSizeInBytes > 0);
    }

    [Fact]
    public void CoreMatchCursorReferencesRegexOwnedPlansInsteadOfEmbeddingThem()
    {
        Assert.True(
            Utf8OperationMatchCursor.DebugSizeInBytes <
            Utf8OperationMatchCursor.DebugSearchPlanSizeInBytes);
        Assert.True(
            Utf8OperationMatchCursor.DebugSizeInBytes <
            Utf8OperationMatchCursor.DebugStructuralLinearProgramSizeInBytes);
    }

    [Fact]
    public void Pcre2CursorsReferenceRegexOwnedCandidateSearchPlan()
    {
        Assert.True(
            Pcre2DirectGlobalMatchCursor.DebugSizeInBytes <
            Pcre2CandidateSearchProgram.DebugSizeInBytes);
        Assert.True(
            Pcre2BacktrackingDetailedGlobalMatchCursor.DebugSizeInBytes <
            Pcre2CandidateSearchProgram.DebugSizeInBytes);
    }

    [Fact]
    public void Pcre2GlobalCursorCarriesOneSharedDirectRouteState()
    {
        Assert.True(
            Pcre2GlobalMatchCursor.DebugSizeInBytes <=
            Pcre2DirectGlobalMatchCursor.DebugSizeInBytes +
            Pcre2LiteralFamilyGlobalMatchCursor.DebugSizeInBytes +
            (2 * IntPtr.Size));
    }

    [Fact]
    public void CompiledProgramUsesTypedOperationBackendsWithOwnedPayloads()
    {
        var delegated = new Utf8Pcre2Regex("abc").DebugCompiledProgram;
        var special = new Utf8Pcre2Regex("(?|(abc)|(xyz))").DebugCompiledProgram;

        Assert.IsType<Pcre2LiteralDirectProgram>(delegated.Operations.IsMatch);
        Assert.IsType<Pcre2Utf8ProgramSlot>(delegated.PrimaryUtf8);
        Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(special.Operations.IsMatch);
        Assert.IsType<Pcre2EmptyUtf8ProgramSlot>(special.PrimaryUtf8);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.Match);
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSet, special.CandidateSearch.Kind);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.Match);
    }

    [Fact]
    public void AsciiRegularIsMatchReusesCoreOnlyForItsProvenSemanticSubset()
    {
        const string emailPattern = @"^([a-zA-Z0-9_\-\.]+)@([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,12}$";
        const string fixedFieldsPattern = @"^([a-z]+)-([0-9]{2})$";
        const string unanchoredIpPattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        var email = new Utf8Pcre2Regex(emailPattern);
        var fixedFields = new Utf8Pcre2Regex(fixedFieldsPattern);
        var unanchoredIp = new Utf8Pcre2Regex(unanchoredIpPattern);

        Assert.IsType<Pcre2AsciiRegularIsMatchDirectProgram>(email.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2AsciiRegularIsMatchDirectProgram>(fixedFields.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiRegularIsMatchDirectProgram>(unanchoredIp.DebugCompiledProgram.Operations.IsMatch);
        Assert.True(email.IsMatch("ops@northwind.example"u8));
        Assert.True(email.IsMatch("ops@northwind.example\n"u8));
        Assert.False(email.IsMatch("ops@northwind.example#"u8));
        Assert.False(email.IsMatch("é@northwind.example"u8));
        Assert.True(fixedFields.IsMatch("alpha-12"u8));
        Assert.False(fixedFields.IsMatch("alpha-123"u8));

        foreach (var unsupported in new[] { @"\w+", @"[^a]+", ".+", "é+", @"(?=a)a", @"(a)\1" })
        {
            Assert.IsNotType<Pcre2AsciiRegularIsMatchDirectProgram>(
                new Utf8Pcre2Regex(unsupported).DebugCompiledProgram.Operations.IsMatch);
        }
    }

    [Fact]
    public void AsciiRegularIsMatchRetainsBacktrackingForOffsetsOptionsAndLimits()
    {
        var ordinary = new Utf8Pcre2Regex("^a+$");
        var captured = new Utf8Pcre2Regex("(a)");
        var limited = new Utf8Pcre2Regex(
            "^(a+)+b$",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Timeout.InfiniteTimeSpan);

        Assert.False(ordinary.IsMatch("a"u8, 0, Pcre2MatchOptions.NotBol));
        Assert.False(ordinary.IsMatch("xa"u8, 1));
        Assert.False(captured.IsMatch("xa"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.True(captured.IsMatch("xa"u8, 1, Pcre2MatchOptions.Anchored));
        Assert.IsType<Pcre2AsciiRegularIsMatchDirectProgram>(limited.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("aaaaaaaa"u8)).ErrorKind);
    }

    [Fact]
    public void InvocationStateIsNeverRetainedByCompiledProgram()
    {
        Assert.DoesNotContain(
            typeof(Pcre2CompiledProgram).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            static field => field.FieldType == typeof(Pcre2InvocationState));

        var first = new Pcre2InvocationState(3, default, Timeout.InfiniteTimeSpan);
        var second = new Pcre2InvocationState(3, default, Timeout.InfiniteTimeSpan);
        Assert.NotSame(first.Captures, second.Captures);
        Assert.NotSame(first.Backtracking, second.Backtracking);
        Assert.NotSame(first.GlobalIteration, second.GlobalIteration);
        Assert.Equal(0UL, first.Budget.CandidateSteps);
        Assert.Equal(0UL, second.Budget.CandidateSteps);
    }

    [Fact]
    public void CompiledRegexCanBeSharedAcrossConcurrentInvocations()
    {
        var regex = new Utf8Pcre2Regex("(?|(abc)|(xyz))");
        var input = Encoding.UTF8.GetBytes("abc xyz abc xyz");
        var failures = 0;

        Parallel.For(
            0,
            256,
            _ =>
            {
                if (!regex.IsMatch(input) || regex.Count(input) != 4)
                {
                    Interlocked.Increment(ref failures);
                }
            });

        Assert.Equal(0, failures);
    }

    [Fact]
    public void ExecutionDiagnosticsPartitionVmAndWorkspaceTraffic()
    {
        var regex = new Utf8Pcre2Regex("(?:(?:ab|a)+c|d{2,3}e)");
        var input = "ababc ddde nope abac"u8;

        var result = regex.DebugCountWithDiagnostics(input, 0);
        var diagnostics = result.Execution;

        Assert.Equal(3, result.Count);
        Assert.True(diagnostics.CandidateAttempts > 0);
        Assert.True(diagnostics.BacktrackingSteps > 0);
        Assert.Equal(
            diagnostics.BacktrackingSteps,
            diagnostics.VmTokenSteps +
            diagnostics.VmBranchSteps +
            diagnostics.VmRepeatSteps +
            diagnostics.VmCaptureSteps +
            diagnostics.VmAssertionSubroutineSteps +
            diagnostics.VmControlSteps +
            diagnostics.VmAcceptSteps);
        Assert.Equal(
            diagnostics.VmTokenSteps,
            diagnostics.VmLiteralTokenSteps +
            diagnostics.VmClassTokenSteps +
            diagnostics.VmBoundaryAnchorTokenSteps +
            diagnostics.VmOtherTokenSteps);
        Assert.Equal(
            diagnostics.VmRepeatSteps,
            diagnostics.VmRepeatEnterSteps +
            diagnostics.VmRepeatEndSteps +
            diagnostics.VmRepeatExitSteps);
        Assert.True(diagnostics.VmPossessiveTokenScanSteps <= diagnostics.VmRepeatEnterSteps);
        Assert.Equal(
            diagnostics.WorkspacePoolRents,
            diagnostics.WorkspaceFixedRents +
            diagnostics.WorkspaceFrameRents +
            diagnostics.WorkspaceRepeatMutationRents +
            diagnostics.WorkspaceCaptureMutationRents +
            diagnostics.WorkspaceControlRents);
    }

    [Fact]
    public void BacktrackingCaptureStateUsesOneFixedRentPerCandidate()
    {
        var regex = new Utf8Pcre2Regex(
            @"(?:(?<n>f.o)|(?<n>b.r))\k<n>",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
            default,
            Timeout.InfiniteTimeSpan);

        var result = regex.DebugCountWithDiagnostics("foofoo barbar xx foofoo"u8, 0);

        Assert.Equal(3, result.Count);
        Assert.True(result.Execution.CandidateAttempts > 0);
        Assert.Equal(
            result.Execution.CandidateAttempts,
            result.Execution.WorkspaceFixedRents);
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSet, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(3UL, result.Execution.CandidateAttempts);
    }

    [Fact]
    public void FiniteLiteralCaptureLanguagesUseLiteralFamilyForCaptureIndependentOperations()
    {
        var branchResetBasic = new Utf8Pcre2Regex(@"(?|(abc)|(xyz))");
        var branchReset = new Utf8Pcre2Regex(@"(?|(abc)|(xyz))\1");
        var duplicateNames = new Utf8Pcre2Regex(
            @"(?:(?<n>foo)|(?<n>bar))\k<n>",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
            default,
            Timeout.InfiniteTimeSpan);
        var branchResetFollowup = new Utf8Pcre2Regex(
            @"(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\k'a'",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
            default,
            Timeout.InfiniteTimeSpan);
        var ambiguousPrefixes = new Utf8Pcre2Regex(@"(?|(a|ab))(c|bcd)");
        var relativeBackreference = new Utf8Pcre2Regex(@"(?|(ab))\g{-1}");
        var unicodeBackreference = new Utf8Pcre2Regex(@"(?|(é)|(λ))\1");
        var optionalCapture = new Utf8Pcre2Regex(@"foo(?<Bar>BAR)?");
        var greedyRepeat = new Utf8Pcre2Regex(@"(?:a){1,3}a");
        var lazyRepeat = new Utf8Pcre2Regex(@"(?:a){1,3}?a");
        var metered = new Utf8Pcre2Regex(
            @"(?|(abc)|(xyz))",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 100 },
            Timeout.InfiniteTimeSpan);
        var tightlyMetered = new Utf8Pcre2Regex(
            @"(?|(abc)|(xyz))\1",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Timeout.InfiniteTimeSpan);

        AssertFiniteLiteralLanguagePlan(branchResetBasic);
        AssertFiniteLiteralLanguagePlan(branchReset);
        AssertFiniteLiteralLanguagePlan(duplicateNames);
        AssertFiniteLiteralLanguagePlan(branchResetFollowup);
        AssertFiniteLiteralLanguagePlan(ambiguousPrefixes);
        AssertFiniteLiteralLanguagePlan(relativeBackreference);
        AssertFiniteLiteralLanguagePlan(unicodeBackreference);
        foreach (var repeated in new[] { optionalCapture, greedyRepeat, lazyRepeat })
        {
            Assert.IsType<Pcre2BacktrackingDirectProgram>(repeated.DebugCompiledProgram.Operations.IsMatch);
            Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(repeated.DebugCompiledProgram.Operations.Count);
            Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(repeated.DebugCompiledProgram.Operations.Enumerate);
            Assert.IsType<Pcre2BacktrackingDirectProgram>(repeated.DebugCompiledProgram.Operations.Match);
            Assert.IsType<Pcre2BacktrackingDirectProgram>(repeated.DebugCompiledProgram.Operations.Replace);
        }
        AssertFiniteLiteralLanguagePlan(metered);
        Assert.Equal(4, branchResetBasic.Count("abc xyz xyz abc q"u8));
        Assert.Equal(3, branchReset.Count("abcabc xyzxyz xx abcabc"u8));
        Assert.Equal(3, duplicateNames.Count("foofoo barbar xx foofoo"u8));
        Assert.Equal(3, branchResetFollowup.Count("aaaccccaaa bccccb xx aaaccccaaa"u8));
        Assert.Equal(1, relativeBackreference.Count("abab xx"u8));
        Assert.Equal(2, unicodeBackreference.Count("éé λλ xx"u8));
        Assert.Equal(4, optionalCapture.Count("fooBAR foo fooBAR foo"u8));
        Assert.Equal(1, greedyRepeat.Count("aaaa"u8));
        Assert.Equal(2, lazyRepeat.Count("aaaa"u8));
        Assert.Equal(4, metered.Count("abc xyz xyz abc q"u8));
        Assert.True(branchReset.IsMatch("xx xyzxyz"u8));
        Assert.False(branchReset.IsMatch("xx abcxyz"u8));
        Assert.False(branchReset.IsMatch("abcabc"u8, 1));
        Assert.True(branchReset.IsMatch("xabcabc"u8, 1, Pcre2MatchOptions.Anchored));
        Assert.True(metered.IsMatch("xx abc"u8));
        Assert.True(unicodeBackreference.IsMatch("xx λλ"u8));
        Assert.False(unicodeBackreference.IsMatch("xx éλ"u8));
        Assert.True(ambiguousPrefixes.IsMatch("xx abcd"u8));
        var optionalMatch = optionalCapture.MatchDetailed("fooBAR"u8);
        Assert.True(optionalMatch.TryGetFirstSetGroup("Bar", out var optionalGroup));
        Assert.Equal("BAR", optionalGroup.GetValueString());
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch("abcabc"u8)).ErrorKind);
        var ambiguousEnumerator = ambiguousPrefixes.EnumerateMatches("abcd"u8);
        Assert.True(ambiguousEnumerator.MoveNext());
        Assert.Equal(
            4,
            ambiguousEnumerator.Current.EndOffsetInBytes - ambiguousEnumerator.Current.StartOffsetInBytes);
        var enumerator = branchReset.EnumerateMatches("abcabc xyzxyz xx abcabc"u8);
        var byteRanges = new List<(int Start, int End)>();
        while (enumerator.MoveNext())
        {
            byteRanges.Add((enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        }

        Assert.Equal([(0, 6), (7, 13), (17, 23)], byteRanges);
        var optionalEnumerator = optionalCapture.EnumerateMatches("fooBAR foo fooBAR foo"u8);
        var optionalRanges = new List<(int Start, int End)>();
        while (optionalEnumerator.MoveNext())
        {
            optionalRanges.Add((
                optionalEnumerator.Current.StartOffsetInBytes,
                optionalEnumerator.Current.EndOffsetInBytes));
        }

        Assert.Equal([(0, 6), (7, 10), (11, 17), (18, 21)], optionalRanges);
        Span<Utf8Pcre2MatchData> optionalDestination = stackalloc Utf8Pcre2MatchData[3];
        Assert.Equal(3, optionalCapture.MatchMany(
            "fooBAR foo fooBAR foo"u8,
            optionalDestination,
            out var optionalIsMore));
        Assert.True(optionalIsMore);
        Assert.Equal((0, 6), (
            optionalDestination[0].StartOffsetInBytes,
            optionalDestination[0].EndOffsetInBytes));
        Assert.Equal("abc", branchReset.MatchDetailed("abcabc"u8).GetGroup(1).GetValueString());
        var duplicateMatch = duplicateNames.MatchDetailed("foofoo"u8);
        Assert.True(duplicateMatch.TryGetFirstSetGroup("n", out var duplicateGroup));
        Assert.Equal("foo", duplicateGroup.GetValueString());
        var followupMatch = branchResetFollowup.MatchDetailed("aaaccccaaa"u8);
        Assert.True(followupMatch.TryGetFirstSetGroup("a", out var followupGroup));
        Assert.Equal("aaa", followupGroup.GetValueString());
    }

    [Fact]
    public void FiniteLiteralLanguagePlanRejectsUnsupportedOrOversizedPatterns()
    {
        var nonExact = new Utf8Pcre2Regex(@"([a-z]+)\1");
        var caseless = new Utf8Pcre2Regex(@"(?i:(abc))\1");
        var nonRootAtomic = new Utf8Pcre2Regex(@"(?>a\Kbz|ab)c");
        var tooManyAlternatives = new Utf8Pcre2Regex(
            "(?|" + string.Join('|', Enumerable.Range(0, 65).Select(static i => $"(v{i:D2})")) + ")");
        var tooLong = new Utf8Pcre2Regex($"(?|({new string('a', 4097)}))");
        var unboundedRepeat = new Utf8Pcre2Regex(@"(?:ab|cd)+z");
        var possessiveRepeat = new Utf8Pcre2Regex(@"(?:a|b){1,2}+a");

        Assert.IsType<Pcre2BacktrackingDirectProgram>(nonExact.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(caseless.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(nonRootAtomic.DebugCompiledProgram.Operations.Enumerate);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(tooManyAlternatives.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(tooLong.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(unboundedRepeat.DebugCompiledProgram.Operations.Enumerate);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(possessiveRepeat.DebugCompiledProgram.Operations.Count);
    }

    [Fact]
    public void FiniteLiteralBoundaryResetLanguagesProjectReportedRangesWithoutChangingProgression()
    {
        var global = new Utf8Pcre2Regex(@"abc\K123");
        var alternative = new Utf8Pcre2Regex(@"(foo)(\Kbar|baz)");
        var rootAtomic = new Utf8Pcre2Regex(@"(?>a\Kbz|ab)");
        var duplicateLanguage = new Utf8Pcre2Regex(@"(?:a\Kb|ab)");
        var resetAtEnd = new Utf8Pcre2Regex(@"ab\K");
        var unicode = new Utf8Pcre2Regex(@"é\Kλ");
        var metered = new Utf8Pcre2Regex(
            @"abc\K123",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 100 },
            Timeout.InfiniteTimeSpan);

        AssertFiniteLiteralBoundaryResetPlan(global);
        AssertFiniteLiteralBoundaryResetPlan(alternative);
        AssertFiniteLiteralBoundaryResetPlan(rootAtomic);
        AssertFiniteLiteralBoundaryResetPlan(duplicateLanguage);
        AssertFiniteLiteralBoundaryResetPlan(resetAtEnd);
        AssertFiniteLiteralBoundaryResetPlan(unicode);
        AssertFiniteLiteralBoundaryResetPlan(metered);
        Assert.Equal(2, global.Count("abc123 abc123"u8));
        Assert.Equal(3, alternative.Count("foobar foobaz foobar"u8));
        Assert.Equal(3, rootAtomic.Count("abz ab ab"u8));
        Assert.Equal([(3, 6), (10, 13)], EnumerateByteRanges(global, "abc123 abc123"u8));
        Assert.Equal([(3, 6), (7, 13), (17, 20)], EnumerateByteRanges(alternative, "foobar foobaz foobar"u8));
        Assert.Equal([(1, 3), (4, 6), (7, 9)], EnumerateByteRanges(rootAtomic, "abz ab ab"u8));
        Assert.Equal([(1, 2)], EnumerateByteRanges(duplicateLanguage, "ab"u8));
        Assert.Equal([(2, 2), (4, 4)], EnumerateByteRanges(resetAtEnd, "abab"u8));
        Assert.Equal([(2, 4)], EnumerateByteRanges(unicode, "éλ"u8));
        Assert.Equal([(3, 6), (10, 13)], EnumerateByteRanges(metered, "abc123 abc123"u8));
        Assert.True(global.IsMatch("xx abc123"u8));
        Assert.True(resetAtEnd.IsMatch("xx ab"u8));
        Assert.False(unicode.IsMatch("éxλ"u8));

        Span<Utf8Pcre2MatchData> matches = stackalloc Utf8Pcre2MatchData[3];
        Assert.Equal(3, alternative.MatchMany("foobar foobaz foobar"u8, matches, out var isMore));
        Assert.False(isMore);
        Assert.Equal((3, 6), (matches[0].StartOffsetInBytes, matches[0].EndOffsetInBytes));
        Assert.Equal((7, 13), (matches[1].StartOffsetInBytes, matches[1].EndOffsetInBytes));
        Assert.Equal((17, 20), (matches[2].StartOffsetInBytes, matches[2].EndOffsetInBytes));
    }

    private static void AssertFiniteLiteralBoundaryResetPlan(Utf8Pcre2Regex regex)
    {
        Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var count = Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(
            regex.DebugCompiledProgram.Operations.Count);
        var enumerate = Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(
            regex.DebugCompiledProgram.Operations.Enumerate);
        Assert.Same(count, enumerate);
        Assert.NotNull(enumerate.BoundaryProjection);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
    }

    private static List<(int Start, int End)> EnumerateByteRanges(
        Utf8Pcre2Regex regex,
        ReadOnlySpan<byte> input)
    {
        var ranges = new List<(int Start, int End)>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            ranges.Add((enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        }

        return ranges;
    }

    private static void AssertFiniteLiteralLanguagePlan(Utf8Pcre2Regex regex)
    {
        Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
    }

    [Fact]
    public void IsMatchDiagnosticsExposeRejectedBacktrackingWork()
    {
        var regex = new Utf8Pcre2Regex("^(a+)+b$");

        var result = regex.DebugIsMatchWithDiagnostics("aaaaaaaa"u8, 0);

        Assert.False(result.IsMatch);
        Assert.Equal(1UL, result.Execution.CandidateAttempts);
        Assert.True(result.Execution.BacktrackingSteps > 0);
        Assert.True(result.Execution.WorkspacePoolRents > 0);
    }

    [Theory]
    [InlineData("^(a+)+b$", true)]
    [InlineData(@"\A(a+)+b$", true)]
    [InlineData(@"\G(a+)+b$", true)]
    [InlineData("(^a)+b", true)]
    [InlineData("(?:^a|^b)c", true)]
    [InlineData("(?:^a|b)c", false)]
    [InlineData("(?m)^(a+)+b$", false)]
    [InlineData("(?:^a)?b", false)]
    public void BacktrackingPlanProvesOnlyMandatoryInitialPositionAnchors(
        string pattern,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);

        Assert.Equal(expected, direct.Program.RestrictsSearchToInitialCandidate);
    }

    [Fact]
    public void InitialPositionRestrictionPreservesOffsetAndMultilineSemantics()
    {
        var subjectAnchored = new Utf8Pcre2Regex("^(a+)+b$");
        var firstMatchingPosition = new Utf8Pcre2Regex(@"\G(a+)+b$");
        var multiline = new Utf8Pcre2Regex("^(a+)+b$", Pcre2CompileOptions.Multiline);

        Assert.False(subjectAnchored.IsMatch("zaab"u8, 1));
        Assert.False(subjectAnchored.IsMatch("aab"u8, 0, Pcre2MatchOptions.NotBol));
        Assert.True(firstMatchingPosition.IsMatch("zaab"u8, 1));
        Assert.True(multiline.IsMatch("x\naaaaaaaab"u8));
        Assert.True(multiline.DebugIsMatchWithDiagnostics("x\naaaaaaaab"u8, 0).Execution.CandidateAttempts > 1);
    }

    [Theory]
    [InlineData("^([a-z]+)@$", "abc@", 1)]
    [InlineData(@"^(?:[a-z]+\.)+[a-z]+$", "one.two", 1)]
    [InlineData("^(?:a+z|b+z)$", "aaaz", 2)]
    [InlineData("^a*ab$", "aaab", 0)]
    [InlineData("(?i)^a*A$", "aaa", 0)]
    [InlineData("^a+?b$", "aaab", 0)]
    public void AutoPossessificationRequiresAProvablyDisjointFollowingLiteral(
        string pattern,
        string input,
        int expectedRepeatCount)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        Assert.Equal(expectedRepeatCount, direct.Program.AutoPossessiveRepeatCount);
        Assert.Equal(
            expectedRepeatCount,
            direct.Program.Instructions.Count(static instruction =>
                instruction.Kind == Pcre2BacktrackingInstructionKind.PossessiveTokenRepeat));
    }

    [Fact]
    public void AutoPossessificationDoesNotChangeMeteredInstructionAccounting()
    {
        var regex = new Utf8Pcre2Regex(
            "^a+b$",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 100 },
            Timeout.InfiniteTimeSpan);

        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);

        Assert.Equal(0, direct.Program.AutoPossessiveRepeatCount);
        Assert.True(regex.IsMatch("aaab"u8));
    }

    [Fact]
    public void AutoPossessificationPreservesGlobalProgressionAndCaptures()
    {
        var regex = new Utf8Pcre2Regex("([a-z]+),");

        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        var match = regex.MatchDetailed("one,two,"u8);

        Assert.Equal(1, direct.Program.AutoPossessiveRepeatCount);
        Assert.Equal(2, regex.Count("one,two,"u8));
        Assert.Equal("one", match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void PossessiveTokenRepeatExecutesAsOneVmStep()
    {
        var regex = new Utf8Pcre2Regex("^a+b$");

        var result = regex.DebugIsMatchWithDiagnostics("aaab"u8, 0);

        Assert.True(result.IsMatch);
        Assert.Equal(1UL, result.Execution.VmRepeatSteps);
        Assert.Equal(1UL, result.Execution.VmPossessiveTokenScanSteps);
        Assert.Equal(3UL, result.Execution.VmPossessiveTokenScanCharacters);
    }

    [Fact]
    public void PossessiveCharacterRepeatUsesPreparedAsciiScanWithRuneFallback()
    {
        var regex = new Utf8Pcre2Regex("^([^@]+)@$");
        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(
            regex.DebugCompiledProgram.Operations.Match);
        var instruction = Assert.Single(
            direct.Program.Instructions,
            static candidate =>
                candidate.Kind == Pcre2BacktrackingInstructionKind.PossessiveTokenRepeat);

        Assert.NotNull(instruction.Token.CharacterClass.AsciiSearchValues);
        Assert.True(regex.IsMatch("ascii@"u8));
        var unicode = regex.DebugIsMatchWithDiagnostics("éé@"u8, 0);
        Assert.True(unicode.IsMatch);
        Assert.Equal(2UL, unicode.Execution.VmPossessiveTokenScanCharacters);
    }
}
