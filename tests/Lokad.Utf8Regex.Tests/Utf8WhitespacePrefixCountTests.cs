using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WhitespacePrefixCountTests
{
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Theory]
    [InlineData(@"(\s*)#x")]
    [InlineData(@"\s*#")]
    [InlineData(@"((\s*))#(?:x|y)+")]
    [InlineData(@"(\s*)#\1X")]
    [InlineData(@"(?<s>\s*)#\k<s>X")]
    [InlineData(@"(\s*)#(?(1)X|Y)")]
    [InlineData(@"(\s*)()#X")]
    [InlineData(@"(\s*)#(?<=#)X")]
    [InlineData(@"(\s*)#X(?=\n|\z)")]
    [InlineData(@"\s*#a\s*#b")]
    [InlineData(@"(\s*)((?:# [Nn][Oo][Qq][Aa])(?::\s?(([A-Z]+[0-9]+(?:[,\s]+)?)+))?)")]
    public void CandidateWindowsPreserveCapturesReferencesAndContext(string pattern)
    {
        var random = new Random(314159);
        string[] parts = ["x", " ", "\t", "\r\n", "\u0085", "\u00a0", "\u2003", "😀", "#", "# X", "#x", "#a #b", "# noqa: F401,E123", "Y"];
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(pattern, options);
            var oracle = new Regex(pattern, options & ~RegexOptions.Compiled);
            for (var sample = 0; sample < 30; sample++)
            {
                var text = new string('z', 300) + string.Concat(Enumerable.Range(0, 80).Select(_ => parts[random.Next(parts.Length)]));
                var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
                Assert.Equal(oracle.Count(text), regex.Count(storage.AsSpan(7, Encoding.UTF8.GetByteCount(text))));
            }
        }
    }

    [Fact]
    public void BackreferenceCanMakeALaterWhitespaceStartWin()
    {
        var regex = new Utf8Regex(@"(\s*)#\1X");
        var input = Encoding.UTF8.GetBytes(new string('z', 300) + "  # X");
        Assert.Equal(1, regex.Count(input));
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, cancellation.Token));
    }

    [Fact]
    public void ControlsAndMalformedTailsRemainEnforced()
    {
        const string pattern = @"(\s*)#x";
        var input = Encoding.UTF8.GetBytes(new string('z', 300) + "\u00a0 #x");
        var bounded = new Utf8Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => bounded.Count(input));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => new Utf8Regex(pattern).Count(input, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        byte[] malformed = [.. input, 0xF0, 0x80];
        Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern).Count(malformed));
    }

    [Theory]
    [InlineData(@"(?<s>\s*)#[aA]bc\k<s>X")]
    [InlineData(@"(\s*)#[aA]bc(?=!)")]
    [InlineData(@"\s*#[aé😀]bc")]
    [InlineData(@"\s*#[^a]bc")]
    [InlineData(@"\s*#(?:abc|b)")]
    [InlineData(@"\s*#[aA](?<x>bc)")]
    [InlineData(@"\s*#[aA](?-i:bc)")]
    [InlineData(@"\s*#[aA]bc|x")]
    public void PrefixFilteringPreservesEveryScalarFloorAndVmDecision(string pattern)
    {
        var text = new string('z', 2046) + " #aZbc \u2003\u00a0#abc X #Abc! #ébc #😀bc #bbb #abc";
        var input = Encoding.UTF8.GetBytes(text);
        byte[] storage = [0xff, .. input, 0xff];
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking, RegexOptions.IgnoreCase })
        {
            if (mode == RegexOptions.NonBacktracking && pattern.Contains(@"\k<s>")) continue;
            if (mode == RegexOptions.NonBacktracking && pattern.Contains("(?=!")) continue;
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, Funded);
            using var token = new CancellationTokenSource();
            for (var start = 2040; start <= text.Length; start++)
            {
                if (start < text.Length && char.IsLowSurrogate(text[start])) continue;
                var cursor = regex.EnumerateMatchesFromUtf16Offset(input, start);
                var expected = 0;
                while (cursor.MoveNext()) expected++;
                Assert.Equal(expected, regex.CountFromUtf16Offset(storage.AsSpan(1, input.Length), start));
                Assert.Equal(expected, regex.CountFromUtf16Offset(input, start, token.Token));
            }
        }
    }

    [Fact]
    public void DenseFalsePrefixesDoNotSpendScalarVmWork()
    {
        const string pattern = @"(?<space>\s*)#[Nn][Oo][Qq][Aa](?=!)";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(new string('x', 128) + " # normal comment ", 512)) + "\u2003#NoQa!");
        var regex = new Utf8Regex(pattern, RegexOptions.Compiled, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkSteps = 4096 });
        using var token = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, token.Token));
        Assert.Equal("\u2003", regex.MatchDetailed("x\u2003#NoQa!"u8).GetGroup("space").GetValueString());
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
                    Funded with { MaxWorkingBytes = 1 }).Count(input)).ResourceKind);
    }

    [Fact]
    public void EveryDiscoveryWorkCutoffPreservesItsParentLoan()
    {
        var regex = new Utf8Regex(@"(?<s>\s*)#[aA]bc\k<s>X");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('z', 260) + "#abY  #abc X");
        var complete = new Utf8SliceInvocation(new(100000, 0, 0), Timeout.InfiniteTimeSpan, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
            new int[program.GroupCount], ref complete, regex.Inspection.PreparedRegex.SearchPlan);
        Assert.Equal((1, Utf8ExecutionLimitKind.None), (result.Count, result.Breach));
        Assert.Equal(13UL, complete.Ledger.ReservedBytes);
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation, regex.Inspection.PreparedRegex.SearchPlan);
            Assert.Equal(cap < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionIsObservedAfterValidation(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 200000) + "\u2003#abc");
        var calibration = new Utf8SliceInvocation(new(1000000, 0, 0), Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref calibration);
        var threshold = (calibration.Budget.Steps / 64 + 1) * 64;
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(@"\s*#[aA]bc", RegexOptions.Compiled, TimeSpan.FromMilliseconds(20), Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks += 100; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }
}
