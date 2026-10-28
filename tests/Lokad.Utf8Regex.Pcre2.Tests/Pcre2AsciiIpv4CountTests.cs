using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2AsciiIpv4CountTests
{
    private const string Pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";

    [Fact]
    public void AdmittedCountPreservesOrderedMatchesAliasesAndScalarByteFloors()
    {
        foreach (var pattern in new[] { Pattern, Pattern.Replace("25", @"\x32\x35").Replace(@"\.", @"\x2e") })
        {
            var regex = Create(pattern);
            Assert.True(Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Count).HasAsciiIpv4CountProof);
            var oracle = new Regex(pattern);
            foreach (var tail in new[] { "", "1.2.3.4", "00.11.22.25500é", "255.255.255.255",
                "256.255.255.255", "999.999.999.999", "00.00.00.00", "٣٣.44.55.66",
                "000.001.002.003😀11.22.33.256" })
            {
                var text = "é😀" + tail + "中";
                var input = Encoding.UTF8.GetBytes(text);
                byte[] storage = [0xff, .. input, 0xff];
                using var live = new CancellationTokenSource();
                foreach (var floor in new[] { 0, 2, 6, input.Length - 3, input.Length })
                {
                    var expected = oracle.Count(Encoding.UTF8.GetString(input.AsSpan(floor)));
                    Assert.Equal(expected, regex.Count(storage.AsSpan(1, input.Length), floor));
                    Assert.Equal(expected, regex.Count(input, floor, Pcre2MatchOptions.None, live.Token));
                }
                var scalar = new Utf8Pcre2Regex("(" + pattern + ")");
                var matches = scalar.EnumerateMatches(input);
                var expectedCount = 0;
                while (matches.MoveNext()) expectedCount++;
                Assert.Equal(expectedCount, regex.Count(input));
            }
        }
    }

    [Fact]
    public void EveryBoundaryAndTruncatedTailRetainsOrderedOctets()
    {
        var regex = Create();
        var oracle = new Regex(Pattern);
        foreach (var token in new[] { "00.11.22.25500", "255.249.199.000", "11.22.33.256", "999.999.999.999" })
        for (var prefix = 2018; prefix <= 2050; prefix++)
        {
            var text = new string('x', prefix) + token + "😀00.11.22.255";
            Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
        const string tail = "é00.11.22.255😀";
        for (var end = 0; end <= tail.Length; end++)
        {
            if (end > 0 && char.IsHighSurrogate(tail[end - 1])) continue;
            var text = tail[..end];
            Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact]
    public void WorkIsCumulativeAndWholeSubjectValidityStillPrecedesCounting()
    {
        uint firstSuccess = 0;
        for (uint cap = 1; cap <= 32; cap++)
        {
            try
            {
                Assert.Equal(1, Create(work: cap).Count("00.11.22.255"u8));
                if (firstSuccess == 0) firstSuccess = cap;
            }
            catch (Pcre2MatchException error)
            {
                Assert.Equal(0u, firstSuccess);
                Assert.Equal(Pcre2ErrorKind.MatchLimit, error.ErrorKind);
            }
        }
        Assert.InRange(firstSuccess, 2u, 32u);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
            Create(work: firstSuccess).Count("00.11.22.255 00.11.22.255"u8)).ErrorKind);
        Assert.Throws<ArgumentOutOfRangeException>(() => Create().Count("😀00.11.22.255"u8, 1));
        var invalid = "00.11.22.255😀"u8.ToArray().Append((byte)255).ToArray();
        Assert.Throws<ArgumentException>(() => Create().Count(invalid));
        var lateInvalid = Encoding.UTF8.GetBytes(new string('x', 200000)).Append((byte)255).ToArray();
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => Create(work: 1).Count(lateInvalid)).ErrorKind);
    }

    [Fact]
    public void CapturesAndExplicitOptionsRetainTheirEstablishedExecutors()
    {
        var captured = Create("(" + Pattern + ")");
        Assert.False(Assert.IsType<Pcre2SliceDirectProgram>(captured.DebugCompiledProgram.Operations.Count).HasAsciiIpv4CountProof);
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => captured.Count("00.11.22.255"u8)).ErrorKind);
        var regex = Create();
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.Count("00.11.22.255"u8, 0, Pcre2MatchOptions.NotBol)).ErrorKind);
        var ordinary = new Utf8Pcre2Regex(Pattern);
        Assert.Equal(0, ordinary.Count("x 00.11.22.255"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, ordinary.Count("00.11.22.255 00.11.22.255"u8, 0, Pcre2MatchOptions.Anchored));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void BoundedSearchKeepsTheOriginalTokenDeadlineAndRetainedResults(bool denseDots, bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string(denseDots ? '.' : 'x', 200000) + "00.11.22.255");
        var regex = Create(timeout: expire ? TimeSpan.FromMilliseconds(20) : null);
        var calibration = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8ValidatedInput.TryCreate(input, ref calibration, out _));
        var threshold = ((calibration.Budget.Steps / 64) + 1) * 64;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var retained = new Utf8Pcre2Regex("(" + Pattern + ")").MatchDetailed("é00.11.22.255"u8);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        Exception? failure = null;
        try { regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token); }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal("00.11.22.255", Encoding.UTF8.GetString(retained.GetGroup(1).GetValueBytes()));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, 0, Pcre2MatchOptions.None, canceled.Token)).CancellationToken);
    }

    private static Utf8Pcre2Regex Create(string pattern = Pattern, uint work = 1000000, TimeSpan? timeout = null)
        => new(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = work, DepthLimit = 1, HeapLimitInBytes = 1, ResultLimitInBytes = 1 },
            timeout ?? Timeout.InfiniteTimeSpan);
}
