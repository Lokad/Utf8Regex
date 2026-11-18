using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ExecutionArchitectureTests
{
    [Fact]
    public void DefaultValueEnumeratorStaysEmptyAcrossCopiesAndRepeatedAdvances()
    {
        Utf8Pcre2ValueMatchEnumerator cursor = default;
        var alias = cursor;
        Assert.False(cursor.Current.Success);
        Assert.False(cursor.MoveNext());
        Assert.False(cursor.MoveNext());
        Assert.False(alias.MoveNext());
        Assert.False(cursor.Current.Success);
        Assert.False(alias.Current.Success);
    }

    [Fact]
    public void EnumeratorLayoutDiagnosticsReportEveryComponent()
    {
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugSizeInBytes > 0);
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugDirectCursorSizeInBytes > 0);
        Assert.True(Utf8Pcre2ValueMatchEnumerator.DebugSliceCursorSizeInBytes > 0);
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
    }

    [Fact]
    public void Pcre2CursorsDoNotEmbedLargeCandidateSearchState()
    {
        foreach (var cursor in new[] { typeof(Pcre2DirectGlobalMatchCursor), typeof(Pcre2BacktrackingDetailedGlobalMatchCursor) })
        {
            var fields = cursor.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.DoesNotContain(fields, static field => field.FieldType == typeof(Pcre2CandidateSearchProgram));
        }
        // Keep the actual cursor footprint bounded without prescribing a field
        // name or the class used to reference immutable candidate-search state.
        Assert.InRange(Pcre2DirectGlobalMatchCursor.DebugSizeInBytes, 1, 64 * IntPtr.Size);
        Assert.InRange(Pcre2BacktrackingDetailedGlobalMatchCursor.DebugSizeInBytes, 1, 64 * IntPtr.Size);
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
        var literal = new Utf8Pcre2Regex("abc").DebugCompiledProgram;
        var delegated = new Utf8Pcre2Regex("foo|bar").DebugCompiledProgram;
        var special = new Utf8Pcre2Regex("(?|(abc)|(xyz))").DebugCompiledProgram;

        Assert.IsType<Pcre2LiteralDirectProgram>(literal.Operations.IsMatch);
        Assert.Null(literal.PrimaryUtf8);
        var direct = Assert.IsType<Pcre2LiteralFamilyDirectProgram>(delegated.Operations.IsMatch);
        var primary = Assert.IsType<Utf8Regex>(delegated.PrimaryUtf8);
        Assert.Same(primary, direct.Regex);
        var finite = Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(special.Operations.IsMatch);
        Assert.Same(finite.Regex, Assert.IsType<Utf8Regex>(special.PrimaryUtf8));
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSet, special.CandidateSearch.Kind);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.Match);
    }

    [Fact]
    public void AsciiGrammarPreservesIndependentBooleanResults()
    {
        const string emailPattern = @"^([a-zA-Z0-9_\-\.]+)@([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,12}$";
        const string fixedFieldsPattern = @"^([a-z]+)-([0-9]{2})$";
        const string unanchoredIpPattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        var email = new Utf8Pcre2Regex(emailPattern);
        var fixedFields = new Utf8Pcre2Regex(fixedFieldsPattern);
        var unanchoredIp = new Utf8Pcre2Regex(unanchoredIpPattern);
        Assert.True(email.IsMatch("ops@northwind.example"u8));
        Assert.True(email.IsMatch("ops@northwind.example\n"u8));
        Assert.False(email.IsMatch("ops@northwind.example#"u8));
        Assert.False(email.IsMatch("é@northwind.example"u8));
        Assert.True(fixedFields.IsMatch("alpha-12"u8));
        Assert.False(fixedFields.IsMatch("alpha-123"u8));
        Assert.True(unanchoredIp.IsMatch("prefix 192.168.001.001 suffix"u8));
        Assert.False(unanchoredIp.IsMatch("prefix 999.999.999.999 suffix"u8));
    }

    [Fact]
    public void AsciiGrammarPreservesOffsetsOptionsAndLimits()
    {
        var ordinary = new Utf8Pcre2Regex("^a+$");
        var captured = new Utf8Pcre2Regex("(a)");
        var limited = new Utf8Pcre2Regex("^(a+)+b$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

        Assert.False(ordinary.IsMatch("a"u8, 0, Pcre2MatchOptions.NotBol));
        Assert.False(ordinary.IsMatch("xa"u8, 1));
        Assert.False(captured.IsMatch("xa"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.True(captured.IsMatch("xa"u8, 1, Pcre2MatchOptions.Anchored));
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("aaaaaaaa"u8)).ErrorKind);
    }

    [Fact]
    public void InvocationStateIsNeverRetainedByCompiledProgram()
    {
        foreach (var name in new[] { "Pcre2InvocationState", "Pcre2BacktrackingState", "Pcre2GlobalIterationState" })
            Assert.Null(typeof(Utf8Pcre2Regex).Assembly.GetType("Lokad.Utf8Regex.Pcre2." + name));
        Assert.DoesNotContain(
            typeof(Pcre2CompiledProgram).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            static field => field.FieldType == typeof(Pcre2ResourceBudget) || field.FieldType == typeof(Utf8SliceInvocation));

        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1, HeapLimitInBytes = 128 };
        var first = new Pcre2ResourceBudget(limits, Timeout.InfiniteTimeSpan, false);
        var second = new Pcre2ResourceBudget(limits, Timeout.InfiniteTimeSpan, false);
        first.ChargeCandidate(); first.ReserveWorking(64);
        Assert.Equal(1UL, first.CandidateSteps); Assert.Equal(64UL, first.ReservedWorkingBytes);
        Assert.Equal(0UL, second.CandidateSteps); Assert.Equal(0UL, second.ReservedWorkingBytes);
        Assert.Equal(0UL, second.HeapBytes);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => first.ChargeCandidate()).ErrorKind);
        second.ChargeCandidate();
        Assert.Equal(1UL, second.CandidateSteps);
        first.ReleaseWorking(64);
        Assert.Equal(0UL, first.ReservedWorkingBytes);

        var regex = new Utf8Pcre2Regex("(é)(😀)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxWorkingBytes = 1000000
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        var input = Utf8ValidatedInput.BorrowBytes("é😀é😀"u8);
        var left = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, input, new(0), regex.DebugCompiledProgram.Request, CancellationToken.None);
        var right = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, input, new(0), regex.DebugCompiledProgram.Request, CancellationToken.None);
        Assert.True(left.MoveNext());
        var leftGroups = left.CaptureGroupsAt(0, true);
        Assert.True(left.WorkingInvocation.Budget.Steps > 0);
        Assert.Equal(0UL, right.WorkingInvocation.Budget.Steps);
        Assert.True(left.MoveNext()); Assert.Equal(6, left.Current.StartOffsetInBytes);
        Assert.True(right.MoveNext()); Assert.Equal(0, right.Current.StartOffsetInBytes);
        var rightGroups = right.CaptureGroupsAt(0, true);
        Assert.NotSame(leftGroups, rightGroups);
        Assert.Equal((2, 6), (leftGroups[2].StartOffsetInBytes, leftGroups[2].EndOffsetInBytes));
        Assert.Equal(0UL, left.WorkingInvocation.Ledger.ReservedBytes);
        Assert.Equal(0UL, right.WorkingInvocation.Ledger.ReservedBytes);
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
        // The transparent wrapper now rides the slice, so the VM-traffic shape spells its group Python-style (the core slice declines it).
        var regex = new Utf8Pcre2Regex("(?P<g>(?:ab|a)+c|d{2,3}e)");
        var input = "ababc ddde nope abac"u8;

        var compiled = regex.DebugCompiledProgram;
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(compiled.Operations.Enumerate).Program;
        var subject = Utf8ValidatedInput.BorrowBytes(input);
        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(program, compiled.CandidateSearchPlan,
            subject, subject.GetBytePosition(0, "startOffsetInBytes"), Pcre2MatchOptions.None,
            compiled.Request, collectDiagnostics: true);
        var count = 0;
        while (cursor.MoveNext()) count = checked(count + 1);
        var result = (Count: count, Execution: cursor.Diagnostics);
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
        var regex = new Utf8Pcre2Regex(@"(?:(?<n>f.o)|(?<n>b.r))\k<n>", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowDuplicateNames = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

        var input = "foofoo barbar xx foofoo"u8;
        var compiled = regex.DebugCompiledProgram;
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(compiled.Operations.Enumerate).Program;
        var subject = Utf8ValidatedInput.BorrowBytes(input);
        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(program, compiled.CandidateSearchPlan,
            subject, subject.GetBytePosition(0, "startOffsetInBytes"), Pcre2MatchOptions.None,
            compiled.Request, collectDiagnostics: true);
        var count = 0;
        while (cursor.MoveNext()) count = checked(count + 1);
        var result = (Count: count, Execution: cursor.Diagnostics);

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
        var duplicateNames = new Utf8Pcre2Regex(@"(?:(?<n>foo)|(?<n>bar))\k<n>", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowDuplicateNames = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var branchResetFollowup = new Utf8Pcre2Regex(@"(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\k'a'", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowDuplicateNames = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var ambiguousPrefixes = new Utf8Pcre2Regex(@"(?|(a|ab))(c|bcd)");
        var relativeBackreference = new Utf8Pcre2Regex(@"(?|(ab))\g{-1}");
        var unicodeBackreference = new Utf8Pcre2Regex(@"(?|(é)|(λ))\1");
        var optionalCapture = new Utf8Pcre2Regex(@"foo(?<Bar>BAR)?");
        var greedyRepeat = new Utf8Pcre2Regex(@"(?:a){1,3}a");
        var lazyRepeat = new Utf8Pcre2Regex(@"(?:a){1,3}?a");
        var metered = new Utf8Pcre2Regex(@"(?|(abc)|(xyz))", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var tightlyMetered = new Utf8Pcre2Regex(@"(?|(abc)|(xyz))\1", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

        AssertFiniteLiteralLanguagePlan(branchResetBasic);
        AssertFiniteLiteralLanguagePlan(branchReset);
        AssertFiniteLiteralLanguagePlan(duplicateNames);
        AssertFiniteLiteralLanguagePlan(branchResetFollowup);
        AssertFiniteLiteralLanguagePlan(ambiguousPrefixes);
        AssertFiniteLiteralLanguagePlan(relativeBackreference);
        AssertFiniteLiteralLanguagePlan(unicodeBackreference);
        foreach (var repeated in new[] { optionalCapture, greedyRepeat, lazyRepeat })
        {
            Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(repeated.DebugCompiledProgram.Operations.IsMatch);
            Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(repeated.DebugCompiledProgram.Operations.Count);
            Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(repeated.DebugCompiledProgram.Operations.Enumerate);
        }
        foreach (var repeated in new[] { optionalCapture, greedyRepeat })
        {
            Assert.IsType<Pcre2BacktrackingDirectProgram>(repeated.DebugCompiledProgram.Operations.Match);
            Assert.IsType<Pcre2BacktrackingDirectProgram>(repeated.DebugCompiledProgram.Operations.Replace);
        }
        // The transparent lazy repeat now rides the shared slice runner instead of the generic VM.
        Assert.IsType<Pcre2SliceDirectProgram>(lazyRepeat.DebugCompiledProgram.Operations.Match);
        Assert.IsType<Pcre2SliceDirectProgram>(lazyRepeat.DebugCompiledProgram.Operations.Replace);
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
        Assert.True(optionalCapture.IsMatch("xx fooBAR"u8));
        Assert.True(optionalCapture.IsMatch("xx foo"u8, 3));
        Assert.False(optionalCapture.IsMatch("xx foo"u8, 4));
        ReadOnlyMemory<byte> optionalMatch1Input = ("fooBAR"u8).ToArray();
        using var optionalMatch1Cursor = optionalCapture.CreateMatchCursor(optionalMatch1Input);
        optionalMatch1Cursor.MoveNext(out var optionalMatch1Result);
        optionalMatch1Cursor.Dispose();
        var optionalMatch = optionalMatch1Result;
        Assert.True(optionalMatch.TryGetGroup(1, out var optionalGroup)); Assert.True(optionalGroup.Success);
        Assert.Equal("BAR", System.Text.Encoding.UTF8.GetString((optionalGroup.Success ? optionalMatch1Input.Span.Slice(optionalGroup.StartOffsetInBytes, optionalGroup.EndOffsetInBytes - optionalGroup.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
        ReadOnlySpan<byte> result2Input = "abcabc"u8;
        var result2Captures = new Utf8Pcre2CaptureRange[branchReset.CaptureGroupCount + 1];
        var result2Result = branchReset.Match(result2Input, captures: result2Captures);
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString((result2Captures[1].Success ? result2Input.Slice(result2Captures[1].StartOffsetInBytes, result2Captures[1].EndOffsetInBytes - result2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        ReadOnlyMemory<byte> duplicateMatch3Input = ("foofoo"u8).ToArray();
        using var duplicateMatch3Cursor = duplicateNames.CreateMatchCursor(duplicateMatch3Input);
        duplicateMatch3Cursor.MoveNext(out var duplicateMatch3Result);
        duplicateMatch3Cursor.Dispose();
        var duplicateMatch = duplicateMatch3Result;
        Assert.True(duplicateMatch.TryGetGroup(1, out var duplicateGroup)); Assert.True(duplicateGroup.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString((duplicateGroup.Success ? duplicateMatch3Input.Span.Slice(duplicateGroup.StartOffsetInBytes, duplicateGroup.EndOffsetInBytes - duplicateGroup.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        ReadOnlyMemory<byte> followupMatch4Input = ("aaaccccaaa"u8).ToArray();
        using var followupMatch4Cursor = branchResetFollowup.CreateMatchCursor(followupMatch4Input);
        followupMatch4Cursor.MoveNext(out var followupMatch4Result);
        followupMatch4Cursor.Dispose();
        var followupMatch = followupMatch4Result;
        Assert.True(followupMatch.TryGetGroup(1, out var followupGroup)); Assert.True(followupGroup.Success);
        Assert.Equal("aaa", System.Text.Encoding.UTF8.GetString((followupGroup.Success ? followupMatch4Input.Span.Slice(followupGroup.StartOffsetInBytes, followupGroup.EndOffsetInBytes - followupGroup.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void FiniteLiteralLanguagePlanRejectsUnsupportedOrOversizedPatterns()
    {
        var nonExact = new Utf8Pcre2Regex(@"([a-z]+)\1");
        var caseless = new Utf8Pcre2Regex(@"(?i:(abc))\1");
        var nonRootAtomic = new Utf8Pcre2Regex(@"(?>a\Kbz|ab)c");
        var tooManyAlternatives = new Utf8Pcre2Regex("(?|" + string.Join('|', Enumerable.Range(0, 65).Select(static i => $"(v{i:D2})")) + ")");
        var tooLong = new Utf8Pcre2Regex($"(?|({new string('a', 4097)}))");
        var unboundedRepeat = new Utf8Pcre2Regex(@"(?:ab|cd)+z");
        var possessiveRepeat = new Utf8Pcre2Regex(@"(?:a|b){1,2}+a");

        // R03 backreference slice: the finite literal language still rejects the class-plus-backref shape, but Count now rides the shared slice runner instead of the generic VM.
        Assert.IsType<Pcre2SliceDirectProgram>(nonExact.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(caseless.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(nonRootAtomic.DebugCompiledProgram.Operations.Enumerate);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(tooManyAlternatives.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(tooLong.DebugCompiledProgram.Operations.Count);
        // R03 transparent groups: the unbounded repeat now rides the shared slice runner instead of the generic VM.
        Assert.IsType<Pcre2SliceDirectProgram>(unboundedRepeat.DebugCompiledProgram.Operations.Enumerate);
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
        var metered = new Utf8Pcre2Regex(@"abc\K123", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

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

        var matches = alternative.EnumerateMatches("foobar foobaz foobar"u8);
        Assert.True(matches.MoveNext());
        Assert.Equal((3, 6), (matches.Current.StartOffsetInBytes, matches.Current.EndOffsetInBytes));
        Assert.True(matches.MoveNext());
        Assert.Equal((7, 13), (matches.Current.StartOffsetInBytes, matches.Current.EndOffsetInBytes));
        Assert.True(matches.MoveNext());
        Assert.Equal((17, 20), (matches.Current.StartOffsetInBytes, matches.Current.EndOffsetInBytes));
        Assert.False(matches.MoveNext());
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

        var input = "aaaaaaaa"u8;
        var compiled = regex.DebugCompiledProgram;
        var program = compiled.Operations.Match is Pcre2SliceDirectProgram slice ? slice.Fallback :
            Assert.IsType<Pcre2BacktrackingDirectProgram>(compiled.Operations.Match).Program;
        Assert.NotNull(program);
        var subject = Utf8ValidatedInput.BorrowBytes(input);
        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(program, compiled.CandidateSearchPlan,
            subject, subject.GetBytePosition(0, "startOffsetInBytes"), Pcre2MatchOptions.None,
            compiled.Request, collectDiagnostics: true);
        var result = (IsMatch: cursor.MoveNext(), Execution: cursor.Diagnostics);

        Assert.False(result.IsMatch);
        Assert.Equal(1UL, result.Execution.CandidateAttempts);
        Assert.True(result.Execution.BacktrackingSteps > 0);
        Assert.True(result.Execution.WorkspacePoolRents > 0);
    }

    [Theory]
    [InlineData("ac", 0, true)]
    [InlineData("bc", 0, true)]
    [InlineData("zac", 0, false)]
    [InlineData("xbc", 1, false)]
    [InlineData("ac", 1, false)]
    [InlineData("", 0, false)]
    public void MandatoryInitialAnchorsAcrossAlternativesPreservePublicResults(
        string input,
        int startOffsetInBytes,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex("(?:^a|^b)c");
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(expected, regex.IsMatch(bytes, startOffsetInBytes));
        var match = regex.Match(bytes, startOffsetInBytes: startOffsetInBytes);
        Assert.Equal(expected, match.Success);
        if (expected)
        {
            Assert.Equal(0, match.StartOffsetInBytes);
            Assert.Equal(2, match.EndOffsetInBytes);
        }
    }

    [Fact]
    public void InitialPositionRestrictionPreservesOffsetAndMultilineSemantics()
    {
        var subjectAnchored = new Utf8Pcre2Regex("^(a+)+b$");
        var firstMatchingPosition = new Utf8Pcre2Regex(@"\G(a+)+b$");
        var multiline = new Utf8Pcre2Regex("^(a+)+b$", options: Pcre2CompileOptions.Multiline);

        Assert.False(subjectAnchored.IsMatch("zaab"u8, 1));
        Assert.False(subjectAnchored.IsMatch("aab"u8, 0, Pcre2MatchOptions.NotBol));
        Assert.True(firstMatchingPosition.IsMatch("zaab"u8, 1));
        Assert.True(multiline.IsMatch("x\naaaaaaaab"u8));
        // Observe the actual selected scalar route; the old diagnostic's
        // default zero counters did not measure its execution.
        Assert.IsType<Pcre2SliceDirectProgram>(multiline.DebugCompiledProgram.Operations.IsMatch);
    }

    [Theory]
    [InlineData("^a*ab$", "aaab")]
    [InlineData("(?i)^a*A$", "aaa")]
    [InlineData("^a+?b$", "aaab")]
    [InlineData("^([a-z]+)@$", "abc@")]
    [InlineData(@"^(?:[a-z]+\.)+[a-z]+$", "one.two")]
    [InlineData("^(?:a+z|b+z)$", "aaaz")]
    [InlineData("^([^@]+)@$", "abc@")]
    public void AutoPossessificationPreservesIsMatchBehavior(
        string pattern,
        string input)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    // The transparent wrapper now rides the slice, so the VM-analysis shape spells its group Python-style (the core slice declines it).
    [InlineData("^(?P<g>a+z|b+z)$", "aaaz", 2)]
    [InlineData("(?i)^a*A$", "aaa", 0)]
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
    public void MeteredAnchorIsMatchServesSliceUnderVisitBudget()
    {
        var regex = new Utf8Pcre2Regex("^a+b$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);

        Assert.True(regex.IsMatch("aaab"u8));
    }

    [Fact]
    public void AutoPossessificationPreservesGlobalProgressionAndCaptures()
    {
        var regex = new Utf8Pcre2Regex("([a-z]+),");

        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        ReadOnlySpan<byte> match5Input = "one,two,"u8;
        var match5Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match5Input, captures: match5Captures);

        Assert.Equal(2, regex.Count("one,two,"u8));
        Assert.Equal("one", System.Text.Encoding.UTF8.GetString((match5Captures[1].Success ? match5Input.Slice(match5Captures[1].StartOffsetInBytes, match5Captures[1].EndOffsetInBytes - match5Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void PossessiveTokenRepeatExecutesAsOneVmStep()
    {
        var regex = new Utf8Pcre2Regex("^a+b$");

        var input = "aaab"u8;
        var compiled = regex.DebugCompiledProgram;
        var program = compiled.Operations.Match is Pcre2SliceDirectProgram slice ? slice.Fallback :
            Assert.IsType<Pcre2BacktrackingDirectProgram>(compiled.Operations.Match).Program;
        Assert.NotNull(program);
        var subject = Utf8ValidatedInput.BorrowBytes(input);
        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(program, compiled.CandidateSearchPlan,
            subject, subject.GetBytePosition(0, "startOffsetInBytes"), Pcre2MatchOptions.None,
            compiled.Request, collectDiagnostics: true);
        var result = (IsMatch: cursor.MoveNext(), Execution: cursor.Diagnostics);

        Assert.True(result.IsMatch);
        Assert.Equal(1UL, result.Execution.VmRepeatSteps);
        Assert.Equal(1UL, result.Execution.VmPossessiveTokenScanSteps);
        Assert.Equal(3UL, result.Execution.VmPossessiveTokenScanCharacters);
    }

    [Fact]
    public void NegatedClassRepeatMatchesAsciiAndBmpScalars()
    {
        var regex = new Utf8Pcre2Regex("^([^@]+)@$");
        Assert.IsType<Pcre2SliceDirectProgram>(
            regex.DebugCompiledProgram.Operations.Match);
        Assert.True(regex.IsMatch("ascii@"u8));
        Assert.True(regex.IsMatch("éé@"u8));
    }
}
