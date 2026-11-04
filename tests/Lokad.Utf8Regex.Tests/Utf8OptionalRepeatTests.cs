using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OptionalRepeatTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in new[]
        {
            @"(ab)?b", @"(ab)??b", @"(a?)?a", @"(a?)??a", @"((a)?)*b", @"((a)??)+b",
            @"(?<x>a)?(?(x)b|c)", @"(?<x>a)??(?(x)b|c)", @"(?<x>a)?\k<x>b",
            @"(?:(?<x>a)?b|(?<x>ab)?c)", @"(?=(a)?b)a?b", @"(?<=(a)?b)c",
            @"(?>(ab)?a)b", @"(?>(ab)??a)b", @"(?<x>a)(?<-x>b)?(?(x)c|d)",
            @"(?<x>a)(?<-x>b)??(?(x)c|d)", @"((é)?){0,1}b", @"((é)??){0,1}?b",
            @"(?i:(ab)?)b", @"(?:^)?a", @"(?:$)??", @"()?(a)?", @"()??(a)??"
        })
        foreach (var rtl in new[] { false, true })
        foreach (var compiled in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant |
                (rtl ? RegexOptions.RightToLeft : 0) | (compiled ? RegexOptions.Compiled : 0)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void BranchOrderCapturesAndRollbackKeepOverlappingSemantics(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "a", "b", "c", "ab", "aab", "abb", "abc", "ac", "ad", "ébéb", "baabc aab"], options);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScalarOptionalCapturesRetainByteBoundariesAndBorrowedResults(bool lazy)
    {
        var regex = new Utf8Regex("(?<value>😀)?" + (lazy ? "?" : "") + "X");
        var input = Encoding.UTF8.GetBytes("é😀X X中");
        var cursor = regex.CreateMatchCursor(input.AsMemory(2, input.Length - 5));
        using (cursor)
        {
            Assert.True(cursor.MoveNext(out var retained));
            Assert.Equal(0, retained.GetCaptures(0)[0].StartOffsetInBytes);
            Assert.Equal(5, retained.GetCaptures(0)[0].EndOffsetInBytes);
            var valueNumber = regex.GroupNumberFromName("value");
            Assert.Equal("😀", Encoding.UTF8.GetString(retained.GetGroupMemory(valueNumber).Span));
            Assert.True(cursor.MoveNext(out var second));
            Assert.Equal("X", Encoding.UTF8.GetString(second.GetValueMemory().Span));
            Assert.True(second.TryGetGroup(valueNumber, out var absent));
            Assert.False(absent.Success);
            Assert.False(cursor.MoveNext(out _));
            Assert.Equal("😀X", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryWorkCutoffKeepsParentLoanAndNoPartialCount(bool lazy)
    {
        var regex = new Utf8Regex("(?<v>(?:é|😀)?" + (lazy ? "?" : "") + ")?X");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes("éX 😀X X");
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var measured = new Utf8SliceInvocation(new(100000, 64, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(measured.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref measured);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(3, result.Count);
        Assert.Equal(13ul, measured.Ledger.ReservedBytes);
        for (ulong limit = 1; limit < measured.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 64, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
                Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation).Breach);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
            var publicRegex = new Utf8Regex(regex.Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
                new Utf8ResourcePolicy { MaxWorkSteps = limit, MaxDepth = 64, MaxWorkingBytes = 1 << 20 });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => publicRegex.Count(input)).ResourceKind);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionUnwindsOptionalAndOuterRepeat(bool timeout)
    {
        var regex = new Utf8Regex("(?:(?<v>a)?b)+");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("ab", 4096)));
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (timeout) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(1000000, 65536, 1 << 24), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var error = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0,
                new int[program.GroupCount], new int[program.GroupCount], ref invocation));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void OptionalMatchDoesNotHideMalformedSuffix()
    {
        var input = new byte[] { (byte)'a', (byte)'b', 0xFF };
        var regex = new Utf8Regex("(a)?b");
        Assert.Throws<ArgumentException>(() => regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailed(input));
    }
}
