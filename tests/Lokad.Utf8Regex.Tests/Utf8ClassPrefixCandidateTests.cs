using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ClassPrefixCandidateTests
{
    [Theory]
    [InlineData(@"\w+\s+Holmes")]
    [InlineData(@"[\w]+\s*Holmes")]
    [InlineData(@"(?<w>\w+)\s+(?<h>Holmes)")]
    [InlineData(@"\w+\s+H(?<x>olmes)")]
    [InlineData(@"(?<w>\w+)\s+Holmes\k<w>")]
    [InlineData(@"\p{L}+\s+Holmes")]
    [InlineData(@"[aé😀]+\s+Holmes")]
    [InlineData(@".+\s+Holmes")]
    [InlineData(@"[a ]+\s+Holmes")]
    [InlineData(@"[^x]+\s+Holmes")]
    [InlineData(@"\w+\s+Holmes|x")]
    public void CountsAgreeWithIndependentScalarEnumerationAtFloorsAndTails(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Funded);
            foreach (var tail in new[] { "", "Holmes", "ab Holmes", "ab Holmesb", "é\u0301\u2003Holmes",
                "𐐀\u00a0Holmes", "😀 Holmes", "aé😀\tHolmes", "aHolmes Holmes",
                "ab\nHolmes Holmes", "ab Holmesx cd Holmes", "ab H\né Holmes" })
            {
                var text = new string('!', 260) + tail + new string('!', 260);
                var input = Encoding.UTF8.GetBytes(text);
                byte[] storage = [0xff, .. input, 0xff];
                using var live = new CancellationTokenSource();
                foreach (var start in new[] { 0, 1, 259, 260, char.IsLowSurrogate(text[261]) ? 262 : 261, text.Length })
                {
                    var cursor = regex.EnumerateMatchesFromUtf16Offset(input, start);
                    var expected = 0;
                    while (cursor.MoveNext()) expected++;
                    Assert.Equal(expected, regex.CountFromUtf16Offset(storage.AsSpan(1, input.Length), start));
                    Assert.Equal(expected, regex.CountFromUtf16Offset(input, start, live.Token));
                }
            }
        }
        // A later start inside the same class run wins after the first capture
        // makes the backreference fail. Rewinding is not match acceptance.
        var captured = new Utf8Regex(@"(?<w>\w+)\s+Holmes\k<w>");
        var witness = Encoding.UTF8.GetBytes(new string('!', 260) + "ab Holmesb");
        Assert.Equal(1, captured.Count(witness));
        Assert.Equal("b", captured.MatchDetailed(witness).GetGroup("w").GetValueString());
    }

    [Fact]
    public void CandidateSearchPreservesBoundedWorkAndVmStorageAdmission()
    {
        var input = Encoding.UTF8.GetBytes(new string('!', 100000) + "é Holmes");
        var efficient = new Utf8Regex(@"\w+\s+Holmes", RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            Funded with { MaxWorkSteps = 4096 });
        Assert.Equal(1, efficient.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8Regex(@"\w+\s+Holmes", RegexOptions.None, Regex.InfiniteMatchTimeout,
                    Funded with { MaxWorkingBytes = 1 }).Count(input)).ResourceKind);
        var invalid = input.Append((byte)255).ToArray();
        Assert.Throws<ArgumentException>(() => efficient.Count(invalid));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8Regex(@"\w+\s+Holmes", RegexOptions.None, Regex.InfiniteMatchTimeout,
                    Funded with { MaxWorkSteps = 1 }).Count(invalid)).ResourceKind);
    }

    [Fact]
    public void EveryWorkCutoffRetainsTheOriginalScratchLoan()
    {
        var regex = new Utf8Regex(@"(?<w>\w+)\s+Holmes\k<w>");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('!', 260) + "ab Holmesb");
        var plan = regex.Inspection.PreparedRegex.SearchPlan;
        var complete = new Utf8SliceInvocation(new(100000, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
            new int[program.GroupCount], ref complete, plan);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(1, result.Count);
        Assert.Equal(13UL, complete.Ledger.ReservedBytes);
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation, plan);
            Assert.Equal(cap < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(3, false)] [InlineData(3, true)]
    public void OriginalInterruptionCoversSearchBothRewindsAndFailedCandidates(int phase, bool expire)
    {
        var text = phase switch
        {
            0 => "word Holmes!" + new string('!', 200000),
            1 => new string('x', 200000) + " Holmes",
            2 => "word" + new string(' ', 200000) + "Holmes",
            _ => string.Concat(Enumerable.Repeat("word Holmesx ", 16000)),
        };
        var input = Encoding.UTF8.GetBytes(text);
        var calibration = new Utf8SliceInvocation(new(4000000, 0, 0), Regex.InfiniteMatchTimeout, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref calibration);
        var threshold = ((calibration.Budget.Steps / 64) + 1) * 64;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(@"\w+\s+Holmes" + (phase == 3 ? "(?=!)" : ""), RegexOptions.Compiled,
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        Exception? failure = null;
        try { regex.Count(input, cancellation.Token); }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }

    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 4000000, MaxDepth = 4096,
        MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 };
}
