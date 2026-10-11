using System.Text;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CaptureProjectionBudgetTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_budget")]
    private static extern ref Pcre2ResourceBudget CursorBudget(ref Pcre2BacktrackingDetailedGlobalMatchCursor cursor);

    private static Pcre2ResourceBudget Budget(ulong bytes, uint steps = 0) => new(
        new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = bytes, MatchLimit = steps },
        Timeout.InfiniteTimeSpan, collectDiagnostics: true);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(65)]
    public void FinalGroupsAndHeapStagingShareExactAggregateAdmission(int slots)
    {
        var groupBytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(slots);
        var stagingBytes = slots <= 32 ? 0UL : Utf8ConstructionBudget.ArrayBytes<int>(checked(slots * 4));
        var starts = Enumerable.Repeat(1, slots).ToArray();
        var ends = Enumerable.Repeat(5, slots).ToArray();
        if (slots > 2) { starts[1] = -1; ends[1] = -1; starts[2] = 2; }
        foreach (var allow in new[] { false, true })
        {
            var budget = Budget(7 + groupBytes + stagingBytes - (allow || groupBytes + stagingBytes == 0 ? 0UL : 1UL));
            budget.ReserveWorking(7);
            var input = Utf8ValidatedInput.Create("x😀é"u8);
            Pcre2GroupData[]? groups = null;
            try
            {
                if (!allow && groupBytes + stagingBytes > 0)
                {
                    try { groups = Pcre2Runner.ProjectCaptures(slots, 1, 7, starts, ends, ref input, ref budget); Assert.Fail("Expected physical aggregate denial."); }
                    catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind); }
                    Assert.Null(groups);
                    Assert.Equal(7UL, budget.ReservedWorkingBytes);
                }
                else
                {
                    groups = Pcre2Runner.ProjectCaptures(slots, 1, 7, starts, ends, ref input, ref budget);
                    Assert.Equal(slots, groups.Length);
                    Assert.Equal(7 + groupBytes + stagingBytes, budget.HeapBytes);
                    Assert.Equal(7 + groupBytes, budget.ReservedWorkingBytes);
                    if (slots > 0) Assert.Equal((1, 7, 1, 4), (groups[0].StartOffsetInBytes, groups[0].EndOffsetInBytes, groups[0].StartOffsetInUtf16, groups[0].EndOffsetInUtf16));
                    if (slots > 2) { Assert.False(groups[1].Success); Assert.True(groups[2].Success); Assert.False(groups[2].HasUtf16Projection); }
                    Pcre2BacktrackingCaptureResult.FromProjectedGroups(groups).DetachProjectedWorking(ref budget);
                    Assert.Equal(7UL, budget.ReservedWorkingBytes);
                    if (slots > 0) Assert.Equal(7, groups[0].EndOffsetInBytes);
                }
            }
            finally { budget.ReleaseWorking(7); }
        }
    }

    [Theory]
    [InlineData(9)]
    [InlineData(33)]
    public void EveryProjectionWorkCutoffRestoresParentWithoutRefundingWork(int slots)
    {
        var subject = Encoding.UTF8.GetBytes(new string('é', 8192));
        var starts = Enumerable.Range(0, slots).Select(i => i * 2).ToArray();
        var ends = Enumerable.Repeat(subject.Length, slots).ToArray();
        var completed = false;
        for (uint limit = 1; limit <= 4096; limit++)
        {
            var budget = Budget(1UL << 20, limit);
            budget.ReserveWorking(7);
            var input = Utf8ValidatedInput.Create(subject);
            try
            {
                try
                {
                    var groups = Pcre2Runner.ProjectCaptures(slots, 0, subject.Length, starts, ends, ref input, ref budget);
                    Assert.Equal(8192, groups[0].EndOffsetInUtf16);
                    Pcre2BacktrackingCaptureResult.FromProjectedGroups(groups).DetachProjectedWorking(ref budget);
                    completed = true;
                }
                catch (Pcre2MatchException failure)
                {
                    Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind);
                    try { budget.ChargeCandidate(); Assert.Fail("Work must stay spent."); }
                    catch (Pcre2MatchException exhausted) { Assert.Equal(Pcre2ErrorKind.MatchLimit, exhausted.ErrorKind); }
                }
                Assert.Equal(7UL, budget.ReservedWorkingBytes);
            }
            finally { budget.ReleaseWorking(7); }
            if (completed) break;
        }
        Assert.True(completed);
    }

    [Theory]
    [InlineData("(*MARK:tag)(?=(a))a", "a", true)]
    [InlineData("(*MARK:tag)(?!(a))a", "a", false)]
    [InlineData("(*MARK:tag)(?(?=(a))a|b)", "a", true)]
    [InlineData("(*MARK:tag)(?(?!(a))b|a)", "a", true)]
    [InlineData("(*MARK:tag)(?<=(a))b", "ab", true)]
    public void AssertionAndConditionalPayloadsReturnThroughTheirParent(string pattern, string subject, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        var budget = Budget(1UL << 20);
        budget.ReserveWorking(7);
        var input = Utf8ValidatedInput.Create(Encoding.UTF8.GetBytes(subject));
        var match = Pcre2BacktrackingRunner.Match(direct.Program, regex.DebugCompiledProgram.CandidateSearch,
            ref input, new Utf8BytePosition(0), Pcre2MatchOptions.None, ref budget);
        Assert.Equal(expected, match.Success);
        Assert.Equal(7UL, budget.ReservedWorkingBytes);
        budget.ReleaseWorking(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(130)]
    public void EveryAssertionMergeWorkCutoffUnwindsResultAndVmLoans(int unusedCaptures)
    {
        var alternatives = string.Concat(Enumerable.Repeat("|(b)", unusedCaptures));
        var regex = new Utf8Pcre2Regex("(*MARK:tag)(?=(a)" + alternatives + ")a");
        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        var completed = false;
        for (uint limit = 1; limit <= 4096; limit++)
        {
            var budget = Budget(1UL << 20, limit);
            budget.ReserveWorking(7);
            var input = Utf8ValidatedInput.Create("a"u8);
            try
            {
                try
                {
                    var match = Pcre2BacktrackingRunner.Match(direct.Program, regex.DebugCompiledProgram.CandidateSearch,
                        ref input, new Utf8BytePosition(0), Pcre2MatchOptions.None, ref budget);
                    Assert.True(match.Success);
                    completed = true;
                }
                catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind); }
                Assert.Equal(7UL, budget.ReservedWorkingBytes);
            }
            finally { budget.ReleaseWorking(7); }
            if (completed) break;
        }
        Assert.True(completed);
    }

    [Fact]
    public void DetailedPayloadStaysChargedUntilPublicationAndThenRemainsValid()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:tag)(😀+)\\1");
        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        var budget = Budget(1UL << 20);
        budget.ReserveWorking(7);
        var input = Utf8ValidatedInput.Create("😀😀"u8);
        var match = Pcre2BacktrackingRunner.MatchDetailed(direct.Program, regex.DebugCompiledProgram.CandidateSearch,
            ref input, new Utf8BytePosition(0), Pcre2MatchOptions.None, ref budget);
        Assert.True(match.Success);
        var groups = match.CaptureResult.ProjectedGroups;
        Assert.Equal(7 + Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(groups.Length), budget.ReservedWorkingBytes);
        Assert.Equal((0, 8, 0, 4), (groups[0].StartOffsetInBytes, groups[0].EndOffsetInBytes, groups[0].StartOffsetInUtf16, groups[0].EndOffsetInUtf16));
        match.CaptureResult.DetachProjectedWorking(ref budget);
        Assert.Equal(7UL, budget.ReservedWorkingBytes);
        budget.ReleaseWorking(7);
        Assert.Equal(4, groups[1].EndOffsetInBytes);
    }

    [Fact]
    public void ProjectionWindowsCrossSupplementaryScalarsAtCompleteBoundaries()
    {
        var subject = Encoding.UTF8.GetBytes("a" + string.Concat(Enumerable.Repeat("😀", 3000)) + "é");
        var input = Utf8ValidatedInput.Create(subject);
        var budget = Budget(1UL << 20);
        int[] starts = [0, 1];
        int[] ends = [subject.Length, subject.Length - 2];
        var groups = Pcre2Runner.ProjectCaptures(2, 0, subject.Length, starts, ends, ref input, ref budget);
        Assert.Equal((0, subject.Length, 0, 6002), (groups[0].StartOffsetInBytes, groups[0].EndOffsetInBytes, groups[0].StartOffsetInUtf16, groups[0].EndOffsetInUtf16));
        Assert.Equal((1, subject.Length - 2, 1, 6001), (groups[1].StartOffsetInBytes, groups[1].EndOffsetInBytes, groups[1].StartOffsetInUtf16, groups[1].EndOffsetInUtf16));
        Pcre2BacktrackingCaptureResult.FromProjectedGroups(groups).DetachProjectedWorking(ref budget);
        Assert.Equal(0UL, budget.ReservedWorkingBytes);
    }

    [Fact]
    public void PublishedArraysRemainIndependentAcrossDetailedCursorAdvancement()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:tag)(a)");
        var compiled = regex.DebugCompiledProgram;
        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(compiled.Operations.Match);
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(direct.Program,
            new Pcre2CandidateSearchPlan(compiled.CandidateSearch), Utf8ValidatedInput.Create("aa"u8),
            new Utf8BytePosition(0), Pcre2MatchOptions.None, compiled.Request);
        Assert.True(cursor.MoveNext());
        var first = cursor.ProjectCurrentCaptures();
        Assert.Equal(0UL, CursorBudget(ref cursor).ReservedWorkingBytes);
        Assert.True(cursor.MoveNext());
        var second = cursor.ProjectCurrentCaptures();
        Assert.NotSame(first, second);
        Assert.Equal(0UL, CursorBudget(ref cursor).ReservedWorkingBytes);
        Assert.False(cursor.MoveNext());
        Assert.Equal(0UL, CursorBudget(ref cursor).ReservedWorkingBytes);
        Assert.Equal((0, 1), (first[1].StartOffsetInBytes, first[1].EndOffsetInBytes));
        Assert.Equal((1, 2), (second[1].StartOffsetInBytes, second[1].EndOffsetInBytes));
    }
}
