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
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.IsMatch);
        Assert.IsType<Pcre2EmptyUtf8ProgramSlot>(special.PrimaryUtf8);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.Match);
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSet, special.CandidateSearch.Kind);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(special.Operations.Match);
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
            @"(?:(?<n>foo)|(?<n>bar))\k<n>",
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
    }
}
