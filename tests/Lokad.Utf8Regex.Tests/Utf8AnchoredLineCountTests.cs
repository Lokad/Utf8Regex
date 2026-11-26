using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AnchoredLineCountTests
{
    [Fact]
    public void DisjointMultilinePrefixDoesNotRetryBlankLineStartsAfterRejection()
    {
        const string pattern = @"(?m)^\s*\w+:";
        var text = new string('\n', 8192) + "word without colon: nope\n" +
            "\u2003é: yes\n\n  two: yes\n";
        using var regex = new Utf8Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 256, MaxDepth = 4096 }
        });
        using var live = new CancellationTokenSource();
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(2, regex.Count(bytes, live.Token));
        Assert.True(regex.IsMatch(bytes, live.Token));
    }

    [Fact]
    public void DisjointMultilineFinderRetainsTheOriginalLineStart()
    {
        const string pattern = @"(?m)^\s*\w+:";
        const string text = "😀 no\n\n\r\n\u2003é: yes\n\tfinal:";
        var bytes = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern);
        using var regex = new Utf8Regex(pattern);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];
        for (var start = 0; start <= text.Length; start++)
        {
            if (start < text.Length && char.IsLowSurrogate(text[start])) continue;
            var expected = oracle.Match(text, start);
            var fromByte = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
            var range = regex.Match(bytes, fromByte, slots);
            Assert.Equal(expected.Success, range.Success);
            Assert.Equal(range, slots[0]);
            if (!range.Success) continue;
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), range.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index + expected.Length)), range.EndOffsetInBytes);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DisjointMultilineSearchObservesInterruptionWhileScanningBlankLines(bool expire, bool firstOnly)
    {
        var input = Encoding.UTF8.GetBytes(new string('\n', 8192) + "é: yes");
        using var live = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var regex = new Utf8Regex(@"(?m)^\s*\w+:", policy: new()
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = new() { MaxWorkSteps = 256, MaxDepth = 4096 }
        });
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (expire) ticks = 50;
            else live.Cancel();
        });
        try
        {
            var failure = firstOnly
                ? Record.Exception(() => regex.IsMatch(input, live.Token))
                : Record.Exception(() => regex.Count(input, live.Token));
            if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(live.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
    }

    public static IEnumerable<object[]> Shapes()
    {
        foreach (var pattern in new[] {
            @"^\s*///(?<title>#.*)$", @"^\s*(?<p>abc)(?<tail>.*)$", @"^(?<x>abc.*)$",
            @"(?<all>^\s*abc.*$)", @"\A\s*abc.*\z", @"^\s*abc.*\z", @"^\s*abc.*(?-m:$)",
            @"^\s*ab\nab.*\z", @"^\s*ab\nab.*$", @"^\s*\0.*$", @"^\s*abc.*\Z",
            @"^[\s\t]*abc.*$", @"^\s*abc.*(?<empty>)$" })
            yield return [pattern, true];
        foreach (var pattern in new[] {
            @"^\s*abc.*?$", @"^\s*(?:abc|def).*$", @"^\s*(?:abc)?.*$", @"^\s*abc(?s:.*)$",
            @"^\s*(?i:abc).*$", @"^\s*(?<x>abc).*(?<z>z)$", @"^\s*abc(?<x>.)*$",
            @"^(?<w>\s)*abc.*$", @"^\s+abc.*$", @"^\s*é.*$", @"^\s* abc.*$",
            "^\\s*" + new string('a', 70) + ".*$", @"\babc.*$" })
            yield return [pattern, false];
    }

    [Theory] [MemberData(nameof(Shapes))]
    public void PublicCountPreservesScalarCursorAndEveryValidOrigin(string pattern, bool native)
    {
        var texts = new[] { "", "abc", " abc é😀\r\nabc\n", "not abc\n\n \u2003\u0085abcé😀\nabc", "\0é😀\n \0x\n",
            "///#a\n ///#b\r\nnot///#c\n", "ab\nab\nab\n", "ab\nab\nab", "abc\r\n", "éabc\n abc\n",
            new string('a', 70) + "\n", " abc é😀\nxyz\n abc", "abc\nabc\n", " \t\v\f\r\nabc\n" };
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.NonBacktracking | RegexOptions.Multiline | RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : 0);
            var regex = new Utf8Regex(pattern, options: options);
            var oracle = new Regex(pattern, options);
            var program = regex.Inspection.AvailableOwnedSliceProgram!;
            foreach (var text in texts)
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                var expected = oracle.Count(text);
                Assert.Equal(expected, regex.Count(bytes));
                var cursor = regex.EnumerateMatches(bytes); var count = 0;
                while (cursor.MoveNext()) count++;
                Assert.Equal(expected, count);
                var offsets = new List<int> { 0 };
                foreach (var scalar in text.EnumerateRunes()) offsets.Add(offsets[^1] + scalar.Utf8SequenceLength);
                foreach (var offset in offsets)
                {
                    var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
                    Assert.True(invocation.Ledger.TryReserve(13));
                    var found = Utf8SliceRunner.CountFromBytes(program, bytes, offset,
                        new int[program.GroupCount], new int[program.GroupCount], ref invocation);
                    Assert.Equal(Utf8ExecutionLimitKind.None, found.Breach);
                    Assert.Equal(oracle.Count(text, Encoding.UTF8.GetCharCount(bytes.AsSpan(0, offset))), found.Count);
                    Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
                    if (native) Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
                }
            }
        }
    }

    [Theory]
    [InlineData("\n\u2003key é😀\r\n key x\n")]
    [InlineData("not key\n" + "key")]
    public void EveryFiniteCutoffKeepsBorrowedWorkingAdmission(string text)
    {
        var program = new Utf8Regex(@"^\s*key.*$", options: RegexOptions.NonBacktracking | RegexOptions.Multiline)            .Inspection.AvailableOwnedSliceProgram!;
        var bytes = Encoding.UTF8.GetBytes(text);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var measured = Utf8SliceRunner.CountFromBytes(program, bytes, 0, [], [], ref baseline);
        for (ulong limit = 1; limit <= baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 1, 13), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var found = Utf8SliceRunner.CountFromBytes(program, bytes, 0, [], [], ref invocation);
            Assert.Equal(limit == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, found.Breach);
            if (found.Breach == Utf8ExecutionLimitKind.None) Assert.Equal(measured.Count, found.Count);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
        }
        var smaller = new Utf8SliceInvocation(new(0, 0, 12), Timeout.InfiniteTimeSpan, default);
        Assert.True(smaller.Ledger.TryReserve(12));
        Assert.Equal(Utf8ExecutionLimitKind.None,
            Utf8SliceRunner.CountFromBytes(program, bytes, 0, [], [], ref smaller).Breach);
        Assert.Equal(12UL, smaller.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("seek", false)]
    [InlineData("seek", true)]
    [InlineData("white", false)]
    [InlineData("white", true)]
    [InlineData("tail", false)]
    [InlineData("tail", true)]
    [InlineData("last", false)]
    [InlineData("last", true)]
    public void RealScanQuantaAndPublicationObserveOriginalAuthority(string stage, bool expire)
    {
        var program = new Utf8Regex(@"^\s*key.*$", options: RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        var text = stage switch { "seek" => new string('x', 8192), "white" => new string(' ', 4096) + "key",
            "tail" => "key" + new string('x', 8192), _ => "key" };
        var padding = stage switch { "seek" => 60, "white" => 57, "tail" => 59, _ => 61 };
        long ticks = 0; var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            expire && stage == "last" && ++reads >= 6 ? 100 : ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 1, 13), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        for (var i = 0; i < padding; i++) _ = invocation.TryChargeStep();
        invocation.CheckpointHook = _ => { if (expire && stage != "last") ticks = 100; else if (!expire) live.Cancel(); };
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8SliceRunner.CountFromBytes(program, Encoding.UTF8.GetBytes(text), 0, [], [], ref invocation));
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.CountFromBytes(program, Encoding.UTF8.GetBytes(text), 0, [], [], ref invocation)).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void OverlappingNewlinePrefixesKeepLinearFailedEndWork()
    {
        var pattern = "^" + string.Concat(Enumerable.Repeat("ab\\n", 10)) + "ab.*\\z";
        var options = RegexOptions.NonBacktracking | RegexOptions.Multiline;
        var regex = new Utf8Regex(pattern, options: options);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.InRange(program.Instructions.Length, 1, 64);
        foreach (var hit in new[] { false, true })
        {
            ulong previous = 0;
            foreach (var lines in new[] { 32, 64, 128, 256, 512 })
            {
                var text = string.Concat(Enumerable.Repeat("ab\n", lines)) + "ab" + new string('x', 8192) + (hit ? "" : "\n");
                var bytes = Encoding.UTF8.GetBytes(text);
                Assert.Equal(hit ? 1 : 0, new Regex(pattern, options).Count(text));
                var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
                Assert.True(invocation.Ledger.TryReserve(13));
                var found = Utf8SliceRunner.CountFromBytes(program, bytes, 0, [], [], ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, found.Breach);
                Assert.Equal(hit ? 1 : 0, found.Count);
                Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
                Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
                if (previous != 0) Assert.InRange(invocation.Budget.Steps, previous, 2 * previous + 64);
                previous = invocation.Budget.Steps;
            }
        }
    }

    [Fact]
    public void AnchoredCountObservesTheOriginalPreCanceledToken()
    {
        var regex = new Utf8Regex(@"^\s*key.*$", options: RegexOptions.NonBacktracking | RegexOptions.Multiline);
        var bytes = "key\né"u8.ToArray();
        using var live = new CancellationTokenSource(); live.Cancel();
        Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, live.Token)).CancellationToken);
    }
}
