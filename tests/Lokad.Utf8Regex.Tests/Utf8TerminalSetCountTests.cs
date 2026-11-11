using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8TerminalSetCountTests
{
    private static readonly Utf8ResourcePolicy Policy = new()
    { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };

    [Theory]
    [InlineData("[A-Za-z]{2,3}", "ab abcd abcdefg 😀é a", 4)]
    [InlineData("[A-Za-z]{2,3}", "ab😀cdeéfg", 3)]
    [InlineData("([A-Za-z]{2,3})", "ab😀cdeéfg", 3)]
    [InlineData("([A-Za-z]){2,3}", "ab😀cdeéfg", 3)]
    [InlineData("[^x]{2,3}", "😀éxabc😀xax😀😀😀😀", 3)]
    [InlineData("(?i:[ak]{2,4})", "KaKk K AAkkk 😀 a", 2)]
    public void WholeSetCountsUseScalarRunsWithoutVmCapacity(string pattern, string text, int expected)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Policy);
            using var live = new CancellationTokenSource();
            var bytes = Encoding.UTF8.GetBytes(text); byte[] backing = [0xff, .. bytes, 0xff];
            Assert.Equal(expected, regex.Count(backing.AsSpan(1, bytes.Length), live.Token));
            Assert.Equal(expected, regex.Count(bytes));
            byte[] malformed = [.. bytes, 0xff];
            Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        }
    }

    [Fact]
    public void MaximumChunksAndShortFinalRunsKeepThePublishedPatternSemantics()
    {
        var lengths = new[] { 7, 8, 13, 14, 20, 21, 26, 27, 255, 256, 257 };
        var text = string.Join(' ', lengths.Select(length => new string('a', length)));
        var expected = lengths.Sum(length => length / 13 + (length % 13 >= 8 ? 1 : 0));
        var regex = new Utf8Regex("[A-Za-z]{8,13}", RegexOptions.Compiled, Timeout.InfiniteTimeSpan, Policy);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(Regex.Count(text, "[A-Za-z]{8,13}"), expected);
    }

    [Theory]
    [InlineData("[A-Za-z]{2,3}", "ab😀cdeéfg")]
    [InlineData("([A-Za-z]{2,3})", "ab😀cdeéfg")]
    [InlineData("([A-Za-z]){2,3}", "ab😀cdeéfg")]
    [InlineData("(?i:[ak]{2,4})", "KaKk K AAkkk 😀 a")]
    public void MatchingAndCopiedCursorsKeepRangesAndProgression(string pattern, string text)
    {
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle, [text]);
        var bytes = Encoding.UTF8.GetBytes(text);
        var cursor = regex.EnumerateMatches(bytes); var alias = cursor;
        Assert.True(cursor.MoveNext()); var first = cursor.Current;
        Assert.True(alias.MoveNext()); Assert.Equal(first.IndexInBytes, alias.Current.IndexInBytes);
        Assert.Equal(first.LengthInBytes, alias.Current.LengthInBytes);
        Assert.True(cursor.MoveNext());
        Assert.Equal(first.IndexInBytes, alias.Current.IndexInBytes);
        Assert.Equal(Regex.Count(text, pattern, options), regex.Count(bytes));
    }

    [Fact]
    public void RequiredEndAssertionsAndCaptureShapesRetainTheirWorkspace()
    {
        foreach (var pattern in new[] { "(?<=[a-z]{2})x", "(?<=([a-z]{2}))x", "(?<v>[a-z]{2,3})", "[a-z]{2,3}?", "[a-z]{0,3}" })
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
            Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(pattern, options), new Regex(pattern, options), ["éabx abc abcdef x"]);
        var nonbacktracking = new Utf8Regex("[a-z]{2,3}", RegexOptions.NonBacktracking);
        Assert.Equal(4, nonbacktracking.Count("ab cde fghij"u8));
    }

    [Fact]
    public void EveryWorkCutoffKeepsTheEnclosingLoanAndRefusesPartialPublication()
    {
        var regex = new Utf8Regex("[^x]{2,3}");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = "😀éxabc😀xax😀😀😀😀"u8.ToArray();
        var baseline = new Utf8SliceInvocation(new(100000, 1, 1000, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(3, Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline).Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 1, 1000, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
                Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation).Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        Assert.Equal(1, regex.Count("😀éxabc😀"u8[4..]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LastScalarPublicationObservesOriginalInterruption(bool expire)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex("[a-z]+", RegexOptions.None, TimeSpan.FromMilliseconds(50), Policy);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        using var live = new CancellationTokenSource();
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ => { if (expire) ticks = 100; else live.Cancel(); });
        try
        {
            var invocation = new Utf8SliceInvocation(new(100000, 1, 1, 1), regex.MatchTimeout, live.Token);
            var input = Encoding.ASCII.GetBytes(new string('a', 62));
            if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation));
            else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation)).CancellationToken);
            Assert.Equal(64UL, invocation.Budget.Steps); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        finally { diagnostics.Complete(); }
    }
}
