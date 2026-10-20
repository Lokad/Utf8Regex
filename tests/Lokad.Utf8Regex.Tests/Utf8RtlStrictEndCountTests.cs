using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RtlStrictEndCountTests
{
    private static Utf8ResourcePolicy Limits(ulong work = 1000000) => new()
        { MaxWorkSteps = work, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    public static IEnumerable<object[]> Cases()
    {
        yield return [@"\A\k<g>(?<g>.{64})\z", new string('a', 128), RegexOptions.None];
        yield return [@"\A\k<g>(?<g>.{64})\z", "b" + new string('a', 127), RegexOptions.None];
        yield return [@"\A\k<g>(?<g>(?:😀é){2})\z", "😀é😀é😀é😀é", RegexOptions.None];
        yield return [@"a\z", "aaa", RegexOptions.None];
        yield return [@"a?\z", "aaa", RegexOptions.None];
        yield return [@"a?\z", "", RegexOptions.None];
        yield return [@"(?<g>)\z", "😀é", RegexOptions.None];
        yield return [@"\z", "😀é\n", RegexOptions.Multiline];
        yield return [@"\G\z", "a😀", RegexOptions.None];
        yield return [@"(?:a|b)\z", "abba", RegexOptions.None];
        yield return [@"a\z|b", "baba", RegexOptions.None];
        yield return [@"a\Z", "aaa\n", RegexOptions.None];
        yield return ["a$", "a\na\n", RegexOptions.Multiline];
        yield return [@"a(?=\z)", "aaa", RegexOptions.None];
        yield return [@"(?!x)a\z", "aaa", RegexOptions.None];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PublicCountsKeepScalarOffsetsContextAndRetainedCaptures(string pattern, string text, RegexOptions extra)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft | extra;
        var oracle = new Regex(pattern, options);
        var bytes = Encoding.UTF8.GetBytes(text);
        byte[] storage = [255, 255, .. bytes, 255];
        var input = storage.AsSpan(2, bytes.Length);
        using var token = new CancellationTokenSource();
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, options | (compiled ? RegexOptions.Compiled : 0),
                Regex.InfiniteMatchTimeout, finite ? Limits() : default);
            var expected = oracle.Match(text);
            var retained = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, retained.Success);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Assert.Equal(oracle.Count(text), regex.Count(input, token.Token));
            for (var offset = 0; offset <= text.Length; offset++)
            {
                // UTF-16 interoperability offsets must still name scalar boundaries.
                if (offset < text.Length && char.IsLowSurrogate(text[offset])) continue;
                var count = oracle.Matches(text, offset).Count;
                Assert.Equal(count, regex.CountFromUtf16Offset(input, offset));
                Assert.Equal(count, regex.CountFromUtf16Offset(input, offset, token.Token));
            }
            if (expected.Success)
            {
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), retained.IndexInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), retained.LengthInBytes);
                if (pattern.Contains("?<g>"))
                    Assert.Equal(expected.Groups["g"].Value, retained.GetGroup("g").GetValueString());
            }
            Assert.Throws<ArgumentException>(() => regex.Count([.. bytes, 255]));
            Assert.Throws<ArgumentException>(() => regex.CountFromUtf16Offset([.. bytes, 255], 0));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(64)] [InlineData(4096)]
    public void StrictEndCountsOnlyOnePossibleStartWithBoundedWork(int length)
    {
        var program = new Utf8Regex(@"\z", RegexOptions.RightToLeft).Inspection.PreparedRegex.ScalarProgram!;
        var input = Encoding.UTF8.GetBytes(new string('a', length));
        foreach (var start in new[] { input.Length, 0 })
        {
            var invocation = new Utf8SliceInvocation(new(1000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var result = Utf8SliceRunner.CountFromBytes(program, input, start, [], [], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            Assert.Equal(start == input.Length ? 1 : 0, result.Count);
            Assert.InRange(invocation.Budget.Steps, 1UL, 64UL);
            Assert.Equal(start, invocation.AnchorOriginByte);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void ImpossibleFloorAvoidsScratchAndEveryCutoffKeepsTheCallerLoan()
    {
        var regex = new Utf8Regex(@"\A\k<g>(?<g>.{64})\z", RegexOptions.RightToLeft);
        var program = regex.Inspection.PreparedRegex.ScalarProgram!;
        var input = Encoding.UTF8.GetBytes(new string('a', 128));
        var wrongFloor = new Utf8SliceInvocation(new(1000, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(wrongFloor.Ledger.TryReserve(7));
        var missed = Utf8SliceRunner.CountFromBytes(program, input, input.Length / 2,
            new int[program.GroupCount], new int[program.GroupCount], ref wrongFloor);
        Assert.Equal(Utf8ExecutionLimitKind.None, missed.Breach);
        Assert.Equal(0, missed.Count);
        Assert.Equal(7UL, wrongFloor.Ledger.ReservedBytes);

        var funded = new Utf8SliceInvocation(new(ulong.MaxValue, 1024, 1 << 20, 1 << 20),
            Regex.InfiniteMatchTimeout, default);
        Assert.True(funded.Ledger.TryReserve(7));
        var match = Utf8SliceRunner.Count(program, input, new int[program.GroupCount],
            new int[program.GroupCount], ref funded);
        Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach);
        Assert.Equal(1, match.Count);
        Assert.Equal(0, funded.AnchorOriginByte);
        for (ulong cap = 1; cap < funded.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 1024, 1 << 20, 1 << 20),
                Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var denied = Utf8SliceRunner.Count(program, input, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(regex.Pattern,
                RegexOptions.RightToLeft, Regex.InfiniteMatchTimeout, Limits(1)).Count(input)).ResourceKind);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void TerminalExhaustionKeepsOriginalAuthority(bool wrongFloor, bool expire)
    {
        var program = new Utf8Regex(@"\z", RegexOptions.RightToLeft).Inspection.PreparedRegex.ScalarProgram!;
        var input = "abcdef"u8.ToArray();
        var start = wrongFloor ? 0 : input.Length;
        var funded = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 7, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(funded.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None,
            Utf8SliceRunner.CountFromBytes(program, input, start, [], [], ref funded).Breach);
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 7, 1),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        ulong target = 0;
        var entered = false;
        invocation.CheckpointHook = step =>
        {
            if (target == 0 || step != target) return;
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        };
        var padding = (64 - funded.Budget.Steps % 64) % 64;
        for (ulong i = 0; i < padding; i++)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        target = invocation.Budget.Steps + funded.Budget.Steps;
        Exception? observed = null;
        try { _ = Utf8SliceRunner.CountFromBytes(program, input, start, [], [], ref invocation); }
        catch (Exception error) { observed = error; }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(observed);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }
}
