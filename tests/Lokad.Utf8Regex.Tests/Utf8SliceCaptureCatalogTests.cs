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
    public void MetadataQueriesAndReturnedCopiesMatchOracle(string pattern, string text)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options);
            foreach (var name in new[] { "", "0", "00", "1", "01", "2", "12", "012", "x", "é", "missing", "-1", " 1", "2147483648" })
                Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromName(name));
            foreach (var number in new[] { -1, 0, 1, 2, 3, 12, int.MaxValue })
                Assert.Equal(oracle.GroupNameFromNumber(number), regex.GroupNameFromNumber(number));
            var names = regex.GetGroupNames();
            var numbers = regex.GetGroupNumbers();
            Assert.Equal(oracle.GetGroupNames(), names);
            Assert.Equal(oracle.GetGroupNumbers(), numbers);
            Array.Fill(names, "corrupted");
            Array.Fill(numbers, -1);
            Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
            Assert.Equal(oracle.GetGroupNumbers(), regex.GetGroupNumbers());
            foreach (var name in oracle.GetGroupNames())
                Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromName(name));
            var context = regex.MatchDetailed(Encoding.UTF8.GetBytes(text));
            var expected = oracle.Match(text);
            foreach (var name in oracle.GetGroupNames())
                Assert.Equal(expected.Groups[name].Value, context.GetGroup(name).GetValueString());
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
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
    public void UndefinedAndMalformedReferencesKeepExactConstructionFailures(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options));
            Assert.Equal(expected.Error, actual.Error);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Message, actual.Message);
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
        var regex = new Utf8Regex("(?<" + name + ">" + literal + ")(?i:" + reference + ")", options);
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
        Assert.Equal(literal, regex.MatchDetailed(input).GetGroup(name).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(Utf8SliceRunner.IsMatch(program, input, starts, ends, ref baseline).IsMatch);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            var outcome = Utf8SliceRunner.IsMatch(program, input, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.False(outcome.IsMatch);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var storage = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.TrailReserveBytes - 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8SliceRunner.IsMatch(program, input, starts, ends, ref storage).Breach);
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
        Assert.True(Utf8SliceRunner.IsMatch(program, input, starts, ends, ref baseline).IsMatch);
        var cancelAt = ((baseline.Budget.Steps - 1) / 64 - 1) * 64;
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        invocation.CheckpointHook = steps => { if (steps >= cancelAt) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.IsMatch(program, input, starts, ends, ref invocation));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Contains("TryFindCaptureOpening", failure.StackTrace);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.True(new Utf8Regex(pattern).IsMatch(input));
    }
}
