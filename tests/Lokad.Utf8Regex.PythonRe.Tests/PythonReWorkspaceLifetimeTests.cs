using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReWorkspaceLifetimeTests
{
    [Fact]
    public void PositionedScratchStaysChargedAcrossMovesAndEofReleasesBorrowedLoans()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 1000)));
        var program = new global::Lokad.Utf8Regex.Utf8Regex("(a)(b)").Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var invocation = new Utf8SliceInvocation(new(1000000, 1024, 1UL << 20), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        using var cursor = new Utf8PythonMatchCursor(input, program, [], 0,
            Regex.InfiniteMatchTimeout, "(a)(b)", invocation);
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var positioned = invocation.Ledger.ReservedBytes;
        Assert.True(positioned > 13 + 2 * Utf8ConstructionBudget.ArrayBytes<int>(program.GroupCount));
        Assert.True(invocation.Budget.PeakWorkingBytes > positioned); // Publication overlaps scratch.
        for (var index = 1; index < 1000; index++)
        {
            Assert.True(alias.MoveNext());
            Assert.Equal(positioned, invocation.Ledger.ReservedBytes);
            Assert.Equal(index * 3, cursor.Current.Value.StartOffsetInBytes);
            Assert.Equal(2, cursor.Current.LastIndex);
        }
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("ab", retained.Value.ValueText);
        Assert.Equal(0, retained.Value.StartOffsetInBytes);
        cursor.Dispose(); alias.Dispose(); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Fact]
    public void FailedCandidatesAssertionsAndDifferentGroupsCannotLeakIntoTheNextSnapshot()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1UL << 20, MaxResultBytes = 1UL << 20 };
        var regex = new Utf8PythonRegex("(?:(?P<x>a+)(?=b)b|(?P<y>c+))", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
        using var cursor = regex.CreateMatchCursor("aaaxcc aab c"u8.ToArray());
        Assert.True(cursor.MoveNext()); var first = cursor.Current;
        Assert.Equal("cc", first.Value.ValueText); Assert.Equal("y", first.LastGroup);
        Assert.True(first.TryGetGroup(1, out var x)); Assert.False(x.Success);
        Assert.True(cursor.MoveNext()); var second = cursor.Current;
        Assert.Equal("aab", second.Value.ValueText); Assert.Equal("x", second.LastGroup);
        Assert.True(second.TryGetGroup(2, out var y)); Assert.False(y.Success);
        Assert.True(cursor.MoveNext()); var third = cursor.Current;
        Assert.Equal("c", third.Value.ValueText); Assert.Equal("y", third.LastGroup);
        Assert.False(cursor.MoveNext());
        Assert.Equal("cc", first.Value.ValueText); Assert.Equal("aab", second.Value.ValueText);
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
        using var cursor = new Utf8PythonMatchCursor("ab"u8.ToArray(), program, [], 0,
            Regex.InfiniteMatchTimeout, "(a)(b)", invocation);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext());
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        Assert.Equal(limit, failure.Limit); Assert.True(failure.Requested > limit);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
        cursor.Dispose(); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }
}
