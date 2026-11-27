using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WhitespacePrefixCountTests
{
    private static Utf8ResourcePolicy Funded => new() { MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

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
            var regex = new Utf8Regex(pattern, options: options);
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
    public void CountObservesOriginalCancellation()
    {
        const string pattern = @"(\s*)#x";
        var input = Encoding.UTF8.GetBytes(new string('z', 300) + "\u00a0 #x");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => new Utf8Regex(pattern).Count(input, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
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
        var projected = new Utf8ScalarCaptureFixture.Subject(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking, RegexOptions.IgnoreCase })
        {
            if (mode == RegexOptions.NonBacktracking && pattern.Contains(@"\k<s>")) continue;
            if (mode == RegexOptions.NonBacktracking && pattern.Contains("(?=!")) continue;
            var regex = new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            var oracle = Utf8ScalarCaptureFixture.Project(new Regex(pattern, mode));
            using var token = new CancellationTokenSource();
            for (var start = 2040; start <= text.Length; start++)
            {
                if (start < text.Length && char.IsLowSurrogate(text[start])) continue;
                var expected = oracle.Count(projected.Projected, Array.IndexOf(projected.Units, start));
                Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(storage.AsSpan(1, input.Length), Encoding.UTF8.GetByteCount(text.AsSpan(0, start)))));
                Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), token.Token)));
            }
        }
    }

    [Fact]
    public void DenseFalsePrefixesDoNotSpendScalarVmWork()
    {
        const string pattern = @"(?<space>\s*)#[Nn][Oo][Qq][Aa](?=!)";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(new string('x', 128) + " # normal comment ", 512)) + "\u2003#NoQa!");
        var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Funded
        });
        using var token = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, token.Token));
        Assert.Equal("\u2003", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor("x\u2003#NoQa!"u8.ToArray())).GetGroupMemory(regex.GroupNumberFromUtf8Name("space"u8)).Span));
        using var limited = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Funded with { MaxWorkingBytes = 1 }
        });
        Assert.Equal(1, limited.Count(input, token.Token));
        var captures = new Utf8CaptureRange[2];
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Match(input, captures: captures)).ResourceKind);
    }

    [Fact]
    public void EveryDiscoveryWorkCutoffPreservesItsParentLoan()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = new Utf8Regex(@"(?<s>\s*)#[aA]bc\k<s>X");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('z', 260) + "#abY  #abc X");
        var complete = new Utf8SliceInvocation(new(0, 0), Timeout.InfiniteTimeSpan, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
            new int[program.GroupCount], ref complete, regex.Inspection.PreparedRegex.SearchPlan);
        Assert.Equal((1, Utf8ExecutionLimitKind.None), (result.Count, result.Breach));
        Assert.Equal(13UL, complete.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionCoversLongPrefixSearch(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 200000) + "\u2003#abc");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(@"\s*#[aA]bc", options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Funded
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 128) return;
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
