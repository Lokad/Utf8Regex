using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceCaptureCatalogTests
{
    [Theory]
    [InlineData("(a)+", "aa")]
    [InlineData("(?<x>a)+", "aa")]
    [InlineData("(?<x>a)(b)+", "abb")]
    [InlineData("(?<x>a)(?<x>b)", "ab")]
    [InlineData("(?<x>(?<x>a)b)", "ab")]
    [InlineData(@"(?<x>a)\k<1>", "aa")]
    [InlineData(@"\k<x>(?<x>a)", "aa")]
    [InlineData("(?<é>é)+", "éé")]
    [InlineData("(?<12>a)(b)+", "abb")]
    [InlineData("(?<12>a)(?<x>b)+", "abb")]
    public void MetadataQueriesAndCaptureRangesMatchOracle(string pattern, string text)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options: options);
            foreach (var name in new[] { "", "0", "00", "1", "01", "2", "12", "012", "x", "é", "missing", "-1", " 1", "2147483648" })
                Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromUtf8Name(System.Text.Encoding.UTF8.GetBytes(name)));
            var copiedNames17 = new string[regex.CaptureGroupCount + 1];
            for (var captureSlot17 = 0; captureSlot17 < copiedNames17.Length; captureSlot17++)
            {
                Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                    regex.TryCopyGroupNameUtf8(captureSlot17, [], out var nameLength17));
                var nameBytes17 = new byte[nameLength17];
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    regex.TryCopyGroupNameUtf8(captureSlot17, nameBytes17, out var written17));
                Assert.Equal(nameLength17, written17);
                copiedNames17[captureSlot17] = System.Text.Encoding.UTF8.GetString(nameBytes17);
            }
            var names = copiedNames17;
            var numbers = Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray();
            Assert.Equal(oracle.GetGroupNames(), names);
            Assert.Equal(oracle.GetGroupNumbers(), numbers);
            Assert.Equal(oracle.GetGroupNumbers(), Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray());
            foreach (var name in oracle.GetGroupNames())
                Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromUtf8Name(System.Text.Encoding.UTF8.GetBytes(name)));
            var context = Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text)));
            var expected = oracle.Match(text);
            Utf8MatchSequence.AssertCaptures(expected, context, Encoding.UTF8.GetBytes(text), text);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Theory]
    [InlineData(@"\k<x>")]
    [InlineData(@"\k<>")]
    [InlineData(@"\k<x")]
    [InlineData(@"\k'x>")]
    [InlineData(@"(?<x>a)\k<2>")]
    [InlineData(@"(?<x>a)(?<x>b)\2")]
    [InlineData(@"(?<x>a)(?<x>b)(?(2)c|d)")]
    public void UndefinedAndMalformedReferencesHaveOwnedConstructionFailures(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options: options));
            Assert.Equal("pattern", actual.ParamName);
        }
    }

    [Theory]
    [InlineData(false, "x", false)]
    [InlineData(true, "x", false)]
    [InlineData(false, "2147483647", false)]
    [InlineData(true, "2147483647", false)]
    [InlineData(false, "12", true)]
    [InlineData(true, "12", true)]
    [InlineData(false, "2147483647", true)]
    [InlineData(true, "2147483647", true)]
    public void CancellationInterruptsReferenceComparisonAndRecovers(bool compiled, string name, bool bare)
    {
        var literal = new string('a', 1024);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var reference = bare ? "\\" + name : @"\k<" + name + ">";
        var regex = new Utf8Regex("(?<" + name + ">" + literal + ")(?i:" + reference + ")", options: options);
        var program = regex.Inspection.OwnedSliceProgram;
        Assert.NotNull(program);
        Assert.Contains(program.Instructions, instruction => instruction.Kind == Utf8SliceInstructionKind.Backref);
        var input = Encoding.UTF8.GetBytes(literal + literal.ToUpperInvariant());
        using var source = new CancellationTokenSource();
        var checkpoints = new List<ulong>();
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            checkpoints.Add(steps);
            if (steps >= 1152) source.Cancel();
        });
        try
        {
            var failure = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, source.Token));
            Assert.Equal(source.Token, failure.CancellationToken);
            Assert.Equal(1152UL, checkpoints[^1]);
        }
        finally { session.Complete(); }
        Assert.True(regex.IsMatch(input));
        Assert.Equal(literal, System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor(input)).GetGroupMemory(regex.GroupNumberFromUtf8Name(System.Text.Encoding.UTF8.GetBytes(name))).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(@"(?<x>c(?<x>a)+b)")]
    [InlineData(@"(?<2147483647>c(?<2147483647>a)+b)")]
    [InlineData(@"(?<1>c(?<01>a)+b)")]
    public void CaptureOpeningJournalBreachesWorkAndStorageLimitsWithCleanup(string pattern)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var input = Encoding.ASCII.GetBytes("c" + new string('a', 40) + "b");
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends,
            collectHistory: false, ref baseline).Found);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends,
                collectHistory: false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.False(outcome.Found);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var storage = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.TrailReserveBytes - 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends,
                collectHistory: false, ref storage).Breach);
        Assert.Equal(0UL, storage.Ledger.ReservedBytes);
        Assert.True(new Utf8Regex(pattern).IsMatch(input));
    }

    [Theory]
    [InlineData(@"(?<x>c(?<x>a)+b)")]
    [InlineData(@"(?<2147483647>c(?<2147483647>a)+b)")]
    [InlineData(@"(?<1>c(?<01>a)+b)")]
    public void CancellationInterruptsCaptureOpeningSearchWithCleanup(string pattern)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var input = Encoding.ASCII.GetBytes("c" + new string('a', 40) + "b");
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends,
            collectHistory: false, ref baseline).Found);
        var cancelAt = ((baseline.Budget.Steps - 1) / 64 - 1) * 64;
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        invocation.CheckpointHook = steps => { if (steps >= cancelAt) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends, collectHistory: false, ref invocation));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Contains("TryFindCaptureOpening", failure.StackTrace);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.True(new Utf8Regex(pattern).IsMatch(input));
    }
}
