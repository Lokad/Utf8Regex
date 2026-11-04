using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FirstCandidateTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in new[]
        {
            "abcdef([0-9]+)", "(ab)c([0-9]+)", "([A-Z]{2,3})-([0-9]+)",
            "((ab)+)-", "(?<x>ab)(?:c|d)", "abc(?=(?<x>[0-9]+))", "(?<=(ab))c",
            "(?:ab|ac)([0-9]+)", "(ab)?([0-9]+)", @"\G(ab)([0-9]+)",
            "(?i:ab)([0-9]+)", "é(ab)([0-9]+)", "(?m)^(ab)([0-9]+)",
            @"(?<x>ab)\k<x>", "(?<x>ab)(?(x)c|d)", "(?>abc|ab)c"
        })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
            yield return [pattern, options | RegexOptions.CultureInvariant];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PublicCapturesCursorsReplacementAndExcludedRootsRemainCorrect(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "a", "ab", "ab12", "xxabcdef123 ab45 abc", "éab12 😀ab345", "AB-12 AAB-345",
             "xabab- ab-", "abc abdc abab", "ab12\nab345"], options);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void BorrowedSlicesTailsAndRetainedCursorAliasesUseByteCoordinates(int padding)
    {
        var regex = new Utf8Regex("ab([0-9]+)");
        var middle = new string('x', padding) + "ab12 😀ab345";
        var bytes = Encoding.UTF8.GetBytes("é" + middle + "中");
        var input = bytes.AsMemory(2, bytes.Length - 5);
        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(alias.MoveNext(out var first));
        Assert.Equal(padding, first.Value.StartOffsetInBytes);
        Assert.Equal("12", Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.True(cursor.MoveNext(out var second));
        Assert.Equal(Encoding.UTF8.GetByteCount(middle.AsSpan(0, middle.IndexOf("ab345", StringComparison.Ordinal))), second.Value.StartOffsetInBytes);
        Assert.False(alias.MoveNext(out _));
        Assert.Equal("ab12", Encoding.UTF8.GetString(first.GetValueMemory().Span));
        Assert.Equal("345", Encoding.UTF8.GetString(second.GetGroupMemory(1).Span));
        Assert.Throws<ArgumentException>(() => regex.Match(new byte[] { (byte)'a', (byte)'b', (byte)'1', 255 }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryWorkCutoffRetainsParentLoanForGapOrFalseHeads(bool falseHeads)
    {
        var regex = new Utf8Regex("abcdef([0-9]+)");
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.ASCII.GetBytes(new string(falseHeads ? 'a' : 'x', 4097) + "abcdef123");
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var measured = new Utf8SliceInvocation(new(100000, 64, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(measured.Ledger.TryReserve(13));
        var found = Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends, false, ref measured);
        Assert.True(found.Found);
        Assert.Equal(4097, found.StartByte);
        Assert.Equal(13ul, measured.Ledger.ReservedBytes);
        for (ulong limit = 1; limit < measured.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 64, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends, false, ref invocation);
            Assert.False(denied.Found);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TerminalMissObservesOriginalInterruptionAndEnclosingLoan(bool expire)
    {
        var program = new Utf8Regex("abc").Inspection.AvailableOwnedSliceProgram;
        var input = "xxxxxxxx"u8.ToArray();
        var measured = new Utf8SliceInvocation(new(10000, 8, 1000), Timeout.InfiniteTimeSpan, default);
        Assert.False(Utf8SliceRunner.TryFindFirst(program, input, 0, [], [], false, ref measured).Found);
        Assert.InRange(measured.Budget.Steps, 1ul, 63ul);
        using var token = new CancellationTokenSource(); long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(10000, 8, 1000), TimeSpan.FromMilliseconds(20), token.Token);
        for (ulong i = 0; i < 64 - measured.Budget.Steps; i++)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        Assert.True(invocation.Ledger.TryReserve(13));
        invocation.CheckpointHook = _ => { if (expire) ticks = 50; else token.Cancel(); };
        var error = Record.Exception(() => Utf8SliceRunner.TryFindFirst(program, input, 0, [], [], false, ref invocation));
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        var denied = new Utf8SliceInvocation(new(10000, 8, 12), Timeout.InfiniteTimeSpan, default);
        Assert.True(denied.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8SliceRunner.TryFindFirst(program, input, 0, [], [], false, ref denied).Breach);
        Assert.Equal(13ul, denied.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DenseRejectedNeighboursRemainInterruptible(bool expire)
    {
        var program = new Utf8Regex("abc").Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.ASCII.GetBytes(new string('a', 4096));
        using var token = new CancellationTokenSource(); long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (expire) ticks = 50; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(100000, 8, 1000), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var error = Record.Exception(() => Utf8SliceRunner.TryFindFirst(program, input, 0, [], [], false, ref invocation));
            if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }
}
