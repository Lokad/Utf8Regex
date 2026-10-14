using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AnchoredCountPrefixTests
{
    [Theory]
    [InlineData("^a.*$", "x\n😀line\nab\r\néa\na\n", RegexOptions.Multiline)]
    [InlineData(@"\Aa.*$", "xabc", RegexOptions.None)]
    [InlineData(@"\Aa.*$", "abc", RegexOptions.None)]
    [InlineData("^(?i:a).+$", "x\nABC\nabc", RegexOptions.Multiline)]
    [InlineData("^a.+$", "x\nABC\nabc", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    [InlineData("^😀.*$", "😀x\nx😀\n😀", RegexOptions.Multiline)]
    [InlineData("^(a|b).+$", "x\nabc\nbcd", RegexOptions.Multiline)]
    [InlineData("^a?$", "x\n\na\n", RegexOptions.Multiline)]
    [InlineData("^a.+$", "x\nabc\naX", RegexOptions.Multiline | RegexOptions.RightToLeft)]
    [InlineData("^a.+$", "", RegexOptions.Multiline)]
    [InlineData("^a.*$", "x\na\n", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    public void AnchoredCountsPreserveLineBoundariesAndDeclines(string pattern, string text, RegexOptions options)
    {
        var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
        var length = Encoding.UTF8.GetByteCount(text);
        var expected = new Regex(pattern, options).Count(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var plain = new Utf8Regex(pattern, options | mode);
            var finite = new Utf8Regex(pattern, options | mode, Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 100000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
            Assert.Equal(expected, plain.Count(storage.AsSpan(7, length)));
            Assert.Equal(expected, finite.Count(storage.AsSpan(7, length)));
        }
    }

    [Fact]
    public void EveryWorkCutoffKeepsBorrowedStorageAcrossAnchoredStarts()
    {
        var regex = new Utf8Regex("^i.+$", RegexOptions.Multiline);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes("x\né\n😀\ni1\r\nx\nimport");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var complete = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, complete.Breach);
        Assert.Equal(2, complete.Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong cap = 1; cap <= baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            Assert.Equal(cap == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            if (outcome.Breach == Utf8ExecutionLimitKind.None) Assert.Equal(2, outcome.Count);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void DerivedStartFloorsKeepOriginalLineContextAndMalformedTailsFail()
    {
        const string text = "é😀\nabc\r\nxabc\nabc";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("^a.+$", RegexOptions.Multiline, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        var oracle = new Regex("^a.+$", RegexOptions.Multiline);
        foreach (var offset in new[] { 0, 1, 3, 4, 5, 8, 9, 10, 14, text.Length })
            Assert.Equal(oracle.Matches(text, offset).Count, regex.CountFromUtf16Offset(input, offset));
        byte[] malformed = [.. input, 0xF0, 0x9F];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnchoredMissObservesOriginalCheckpointInterruption(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var token = new CancellationTokenSource();
        var regex = new Utf8Regex("^a.+$", RegexOptions.Multiline);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("x\n", 32)));
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0),
            expire ? TimeSpan.FromMilliseconds(200) : Timeout.InfiniteTimeSpan, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        var triggered = false;
        invocation.CheckpointHook = _ => { triggered = true; if (expire) clock.Advance(250); else token.Cancel(); };
        void Run() => Utf8SliceRunner.CountFromBytes(program, input, 0,
            new int[program.GroupCount], new int[program.GroupCount], ref invocation);
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(Run);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
        Assert.True(triggered);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
