using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiLiteralRunTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in new[]
        {
            "(alpha)(bravo)(charlie)", "(?<x>abcde)(?:fg|fh)", "(?:(?<x>abcdef)g|abcde(?<y>fh))",
            "(?=(?<x>abcdef))abcdefg", "(?<=abcdef)(?<x>ghi)", "(?>abcdef|abcde)f",
            "(?:(?<x>abcdef)g)+h", "(?:(?<x>abcdef)g)??h", @"(?<x>abcdef)\k<x>",
            "(?i:ABCDEF)(ghij)", "abcde(?i:f)ghij", "abcdeé(fghi)", "abcde😀(fghi)",
            "abcde(?:fghij|klmno)pqrst"
        })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
            yield return [pattern, options | RegexOptions.CultureInvariant];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PublicOperationsRetainEffectsBranchOrderAndScalarBoundaries(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "a", "abcde", "abcdefg", "abcdefh", "abcdefghi", "abcdefabcdef", "abcdefghabcdefggh",
             "alphabravocharlie", "abcdeéfghi", "abcde😀fghi", "éabcdefg😀abcdefh", "abcdeklmnopqrst"], options);

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(31)] [InlineData(63)] [InlineData(64)]
    [InlineData(65)] [InlineData(127)] [InlineData(128)]
    public void EveryLiteralRetainsItsChargeAndEveryWorkCutoffReleasesParentLoan(int length)
    {
        var regex = new Utf8Regex(new string('a', length));
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.ASCII.GetBytes(new string('a', length));
        var measured = new Utf8SliceInvocation(new(100000, 8, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(measured.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.TryMatchAt(program, input, 0, [], [], true, ref measured);
        Assert.True(outcome.IsMatch);
        Assert.Equal((ulong)length + 1, measured.Budget.Steps); // Each literal, then Accept.
        Assert.Equal(13ul, measured.Ledger.ReservedBytes);
        for (ulong limit = 1; limit < measured.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 8, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.TryMatchAt(program, input, 0, [], [], true, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.False(denied.IsMatch);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
        for (var available = 0; available < length; available++)
        {
            var invocation = new Utf8SliceInvocation(new(100000, 8, 1 << 20), Timeout.InfiniteTimeSpan, default);
            var missing = Utf8SliceRunner.TryMatchAt(program, input.AsSpan(0, available), 0, [], [], true, ref invocation);
            Assert.False(missing.IsMatch);
            Assert.Equal((ulong)available + 1, invocation.Budget.Steps);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionInsideLiteralRunUnwindsCaptureAndScratch(bool timeout)
    {
        // Exceed the unchanged 2048-instruction deadline polling interval.
        var regex = new Utf8Regex("(?<x>" + new string('a', 4096) + ")Z");
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var input = Encoding.ASCII.GetBytes(new string('a', 4096) + "Z");
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (timeout) ticks = 25; else source.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(100000, 8, 1 << 20), TimeSpan.FromMilliseconds(20), source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var failure = Record.Exception(() => Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, true, ref invocation));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.All(starts, value => Assert.Equal(-1, value));
            Assert.All(ends, value => Assert.Equal(-1, value));
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void BorrowedOffsetAndMalformedSuffixRemainAuthoritative()
    {
        var regex = new Utf8Regex("item-([0-9]+)");
        var bytes = Encoding.UTF8.GetBytes("é😀item-12 item-345中");
        using var cursor = regex.CreateMatchCursor(bytes.AsMemory(6, bytes.Length - 9));
        Assert.True(cursor.MoveNext(out var first));
        Assert.True(cursor.MoveNext(out var second));
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal("item-12", Encoding.UTF8.GetString(first.GetValueMemory().Span));
        Assert.Equal("12", Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal("item-345", Encoding.UTF8.GetString(second.GetValueMemory().Span));
        var malformed = Encoding.ASCII.GetBytes("item-12").Concat(new byte[] { 255 }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.MatchDetailed(malformed));
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }
}
