using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReWorkspaceLifetimeTests
{
    [Fact]
    public void PositionedScratchStaysChargedAcrossMovesAndEofReleasesBorrowedLoans()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 1000)));
        var program = new global::Lokad.Utf8Regex.Utf8Regex("(a)(b)").Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var invocation = new Utf8SliceInvocation(new(1000000, 1024, 1UL << 20), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        using var cursor = new Utf8PythonMatchCursor(input, program, 0,
            Regex.InfiniteMatchTimeout, "(a)(b)", invocation, null);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        var positioned = invocation.Ledger.ReservedBytes;
        Assert.True(positioned > 13 + 2 * Utf8ConstructionBudget.ArrayBytes<int>(program.GroupCount));
        Assert.True(invocation.Budget.PeakWorkingBytes > positioned); // Publication overlaps scratch.
        for (var index = 1; index < 1000; index++)
        {
            Assert.True(alias.MoveNext(out cursorRange));
            Assert.Equal(positioned, invocation.Ledger.ReservedBytes);
            Assert.Equal(index * 3, cursorRange.Value.StartOffsetInBytes);
            Assert.Equal(2, cursorRange.LastIndex);
        }
        Assert.False(cursor.MoveNext(out cursorRange)); Assert.False(alias.MoveNext(out cursorRange));
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.Equal(0, retained.Value.StartOffsetInBytes);
        cursor.Dispose(); alias.Dispose(); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Fact]
    public void FailedCandidatesAssertionsAndDifferentGroupsCannotLeakIntoTheNextSnapshot()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1UL << 20, MaxResultBytes = 1UL << 20 };
        var regex = new Utf8PythonRegex("(?:(?P<x>a+)(?=b)b|(?P<y>c+))", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        using var cursor = regex.CreateMatchCursor("aaaxcc aab c"u8.ToArray());
        Assert.True(cursor.MoveNext(out cursorRange)); var first = cursorRange;
        Assert.Equal("cc", System.Text.Encoding.UTF8.GetString(first.GetValueMemory().Span)); Assert.Equal(2, first.LastIndex);
        Assert.True(first.TryGetGroup(1, out var x)); Assert.False(x.Success);
        Assert.True(cursor.MoveNext(out cursorRange)); var second = cursorRange;
        Assert.Equal("aab", System.Text.Encoding.UTF8.GetString(second.GetValueMemory().Span)); Assert.Equal(1, second.LastIndex);
        Assert.True(second.TryGetGroup(2, out var y)); Assert.False(y.Success);
        Assert.True(cursor.MoveNext(out cursorRange)); var third = cursorRange;
        Assert.Equal("c", System.Text.Encoding.UTF8.GetString(third.GetValueMemory().Span)); Assert.Equal(2, third.LastIndex);
        Assert.False(cursor.MoveNext(out cursorRange));
        Assert.Equal("cc", System.Text.Encoding.UTF8.GetString(first.GetValueMemory().Span)); Assert.Equal("aab", System.Text.Encoding.UTF8.GetString(second.GetValueMemory().Span));
        Assert.Equal((4, 7, 11), (first.Value.StartOffsetInBytes, second.Value.StartOffsetInBytes, third.Value.StartOffsetInBytes));
    }

    [Fact]
    public void StackAdmissionFailureAfterHeadsAcquisitionReleasesExclusiveScratch()
    {
        var program = new global::Lokad.Utf8Regex.Utf8Regex("(a)(b)").Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var limit = 13 + 3 * Utf8ConstructionBudget.ArrayBytes<int>(program.GroupCount);
        var invocation = new Utf8SliceInvocation(new(1000000, 1024, limit), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        using var cursor = new Utf8PythonMatchCursor("ab"u8.ToArray(), program, 0,
            Regex.InfiniteMatchTimeout, "(a)(b)", invocation, null);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext(out _));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        Assert.Equal(limit, failure.Limit); Assert.True(failure.Requested > limit);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        cursor.Dispose(); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }
}
