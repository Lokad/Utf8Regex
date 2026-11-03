using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8AnchoredLinePairTests
{
    private static readonly Utf8ResourcePolicy Policy = new()
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 1024,
        MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000,
    };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2045)] [InlineData(2046)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4095)]
    public void BlockEdgesOffsetsAndCapturesUseOriginalVerification(int prefixLength)
    {
        const string pattern = @"^i(?<x>[0-9]+)x(?=$)";
        var text = new string('z', prefixLength) + "\ni12x\niQx\nx i3x\né😀\ni45x\n";
        var input = Encoding.UTF8.GetBytes(text);
        byte[] storage = [255, .. input, 255];
        using var live = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.Multiline, RegexOptions.Multiline | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Policy);
            Assert.Equal(oracle.Count(text), regex.Count(storage.AsSpan(1, input.Length), live.Token));
            foreach (var floor in new[] { 0, prefixLength + 1, prefixLength + 2, text.Length })
            {
                var expected = oracle.Match(text, floor);
                var actual = regex.MatchDetailedFromUtf16Offset(input, floor, live.Token);
                Assert.Equal(expected.Success, actual.Success);
                if (!expected.Success) continue;
                Assert.Equal(expected.Index, actual.IndexInUtf16);
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.IndexInBytes);
                Assert.Equal(expected.Length, actual.LengthInBytes);
                Assert.Equal(expected.Groups["x"].Value, actual.GetGroup("x").GetValueString());
            }
        }
    }

    [Fact]
    public void EveryAsciiFirstLiteralIncludesLineFeedAndOneByteTails()
    {
        for (var first = 0; first < 128; first++)
        {
            var literal = ((char)first).ToString();
            var pattern = "^" + Regex.Escape(literal) + "[0-9]";
            var oracle = new Regex(pattern, RegexOptions.Multiline);
            var regex = new Utf8Regex(pattern, RegexOptions.Multiline, Regex.InfiniteMatchTimeout, Policy);
            foreach (var text in new[] { "", literal, literal + "7", "\n" + literal + "7\n",
                "é😀\n" + literal + "7\nx" + literal + "8\n", new string('x', 2047) + "\n" + literal + "7" })
                Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Theory]
    [InlineData(RegexOptions.None, "^i[0-9]+x")]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.Multiline, "^I[0-9]+x")]
    [InlineData(RegexOptions.RightToLeft | RegexOptions.Multiline, "^i[0-9]+x")]
    [InlineData(RegexOptions.NonBacktracking | RegexOptions.Multiline, "^i[0-9]+x")]
    [InlineData(RegexOptions.Multiline, "^i?[0-9]+x")]
    [InlineData(RegexOptions.Multiline, "^é[0-9]+x")]
    public void UnsupportedPrefixesAndOptionsKeepTheirSemantics(RegexOptions options, string pattern)
    {
        const string text = "i7x\nI8x\n9x\né12x\nx i3x\ni45x\n";
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Policy);
        Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        var expected = oracle.Match(text);
        var actual = regex.Match(Encoding.UTF8.GetBytes(text));
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success) Assert.Equal(expected.Index, actual.IndexInUtf16);
    }

    [Fact]
    public void DenseRejectedLinesUseBoundedWorkAndPreserveWorkingAdmission()
    {
        // A non-literal assertion keeps this public Count on the scalar seeker.
        const string pattern = @"^i(?<x>[0-9]+)x(?=$)";
        var input = Encoding.UTF8.GetBytes(new string('\n', 16_384) + "x i7x\n");
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline, Regex.InfiniteMatchTimeout, Policy);
        Assert.Equal(0, regex.Count(input));
        // Measure this production seeker's work independently of the public
        // operation's existing preparation and capture-slot admission.
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            Timeout.InfiniteTimeSpan, source.Token);
        var outcome = Utf8SliceRunner.CountFromBytes(regex.Inspection.PreparedRegex.ScalarProgram!,
            input, 0, [], [], ref invocation);
        Assert.Equal(0, outcome.Count);
        Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        Assert.InRange(invocation.Budget.Steps, 1UL, 32UL);
        var hit = new Utf8Regex(pattern, RegexOptions.Multiline, Regex.InfiniteMatchTimeout,
            Policy with { MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => hit.Count("\ni7x\n"u8)).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern,
                RegexOptions.Multiline, Regex.InfiniteMatchTimeout,
                Policy with { MaxWorkSteps = 1 }).Count(input)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProductionSeekerObservesOriginalCancellationAndDeadline(bool expire)
    {
        var regex = new Utf8Regex(@"^i[0-9]+x", RegexOptions.Multiline);
        var program = regex.Inspection.PreparedRegex.ScalarProgram!;
        var input = Encoding.UTF8.GetBytes(new string('\n', 16_384) + "x i7x\n");
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var checkpoints = 0;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (++checkpoints == 1) { if (expire) ticks = 50; else source.Cancel(); }
        });
        try
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
                TimeSpan.FromMilliseconds(20), source.Token);
            // Hooks fire every 64 steps. The seeker's entry consumes step63;
            // its first bounded search block consumes step64 and invokes it.
            for (var step = 0; step < 62; step++) invocation.TryChargeStep();
            if (expire)
                Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                    Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation));
            else
                Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
                    Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation)).CancellationToken);
            Assert.Equal(1, checkpoints);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void WholeSubjectValidationPrecedesEarlyResults()
    {
        var regex = new Utf8Regex(@"^i[0-9]+x", RegexOptions.Multiline, Regex.InfiniteMatchTimeout, Policy);
        foreach (var input in new byte[][] { [255, 10, 105, 55, 120], [105, 55, 120, 10, 255], [105, 55, 120, 0xf0, 0x9f] })
        {
            Assert.Throws<ArgumentException>(() => regex.Count(input));
            Assert.Throws<ArgumentException>(() => regex.Match(input));
        }
    }
}
