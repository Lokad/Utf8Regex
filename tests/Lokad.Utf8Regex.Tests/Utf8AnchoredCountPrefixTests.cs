using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AnchoredCountPrefixTests
{
    [Theory]
    [InlineData("^i(?<tail>.+)$", "x\né\n😀line\ni1\r\nx\ni😀\n", RegexOptions.Multiline)]
    [InlineData("^import(?<tail>.+)$", "if\né\n😀line\nimporx\nimport1\r\nimport😀\n", RegexOptions.Multiline)]
    [InlineData("^im(?i:port)(?<tail>.+)$", "imPORT1\nimPort😀\n", RegexOptions.Multiline)]
    [InlineData("^im😀(?<tail>.+)$", "imx\nim😀a\nim😀\n", RegexOptions.Multiline)]
    [InlineData("^im\\nport(?<tail>.+)$", "im\nport1\nim\nport😀\n", RegexOptions.Multiline)]
    [InlineData("^i(?<tail>.*)$", "\n\ni\ni😀\n", RegexOptions.Multiline)]
    [InlineData(@"\Ai(?<tail>.*)$", "x\ni1", RegexOptions.None)]
    [InlineData("^i?(?<tail>.*)$", "x\n\ni\n", RegexOptions.Multiline)]
    [InlineData("^😀(?<tail>.*)$", "x\n😀a\n😀\n", RegexOptions.Multiline)]
    [InlineData("^(?i:i)(?<tail>.*)$", "x\nI1\ni😀\n", RegexOptions.Multiline)]
    public void AnchoredDiscoveryPreservesCapturesAndRetainedResults(string pattern, string text, RegexOptions options)
    {
        var expected = new Regex(pattern, options);
        var first = expected.Match(text);
        var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
        var length = Encoding.UTF8.GetByteCount(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var finite in new[] { false, true })
        {
            var policy = finite
                ? new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 }
                : new Utf8ResourcePolicy();
            var actual = new Utf8Regex(pattern, options | mode, Timeout.InfiniteTimeSpan, policy);
            var input = storage.AsSpan(7, length);
            Assert.Equal(first.Success, actual.IsMatch(input));
            var detailed = actual.MatchDetailed(input);
            Assert.Equal(first.Success, detailed.Success);
            if (first.Success)
                Assert.Equal(first.Groups["tail"].Value, detailed.GetGroup("tail").GetValueString());
            var ranges = new List<(int Index, int Length)>();
            foreach (var match in actual.EnumerateMatches(input))
                ranges.Add((match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(expected.Matches(text).Select(match => (
                Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)),
                Encoding.UTF8.GetByteCount(match.Value))).ToArray(), ranges);
            Assert.Equal(ranges.Count, actual.Count(input));
            if (first.Success)
                Assert.Equal(first.Groups["tail"].Value, detailed.GetGroup("tail").GetValueString());
        }
    }

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

    [Theory]
    [InlineData(31)] [InlineData(32)] [InlineData(33)]
    public void BoundedPrefixesPreserveBoundaryTailsAndLongerVerification(int width)
    {
        var prefix = new string('a', width - 1) + "Z";
        var pattern = "^" + prefix + "(?<tail>.+)$";
        var text = new string('x', 2047) + "\n" + prefix + "😀\r\n" + new string('a', width) + "!\n" + prefix;
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);
        var oracle = new Regex(pattern, RegexOptions.Multiline); var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(oracle.Count(text), regex.Count(input));
        foreach (var offset in new[] { 0, 2047, 2048, 2049, text.Length })
            Assert.Equal(oracle.Matches(text, offset).Count, regex.CountFromUtf16Offset(input, offset));
        Assert.Equal(oracle.Match(text).Groups["tail"].Value, regex.MatchDetailed(input).GetGroup("tail").GetValueString());
    }

    [Fact]
    public void RejectedFullPrefixesNeedNoVmWorkspaceAndSurvivorsStillNeedAdmission()
    {
        var regex = new Utf8Regex("^import[0-9]+$", RegexOptions.Multiline, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("if false\nimporx\nim😀\n", 256)));
        Assert.Equal(0, regex.Count(input));
        Assert.False(regex.IsMatch(input));
        Assert.False(regex.Match(input).Success);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count("importwrong\n"u8)).ResourceKind);
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Theory]
    [InlineData("i")] [InlineData("import")]
    public void EveryWorkCutoffKeepsBorrowedStorageAcrossAnchoredStarts(string prefix)
    {
        var regex = new Utf8Regex("^" + prefix + ".+$", RegexOptions.Multiline);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes("x\né\n😀\n" + prefix + "1\r\nx\n" + prefix + "2");
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AnchoredMissObservesOriginalCheckpointInterruption(bool expire, bool firstOnly)
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
        void Run()
        {
            if (firstOnly)
                _ = Utf8SliceRunner.TryFindFirst(program, input, 0,
                    new int[program.GroupCount], new int[program.GroupCount], false, ref invocation);
            else
                _ = Utf8SliceRunner.CountFromBytes(program, input, 0,
                    new int[program.GroupCount], new int[program.GroupCount], ref invocation);
        }
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(Run);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
        Assert.True(triggered);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
