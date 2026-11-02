using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RepeatedSegmentCountTests
{
    [Theory]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}")]
    [InlineData(@"(?:[A-Z][a-z]{2,}\s+){1,3}")]
    [InlineData(@"(?:[AC][bd]+[ \t]*){2,4}")]
    [InlineData(@"(?:[A-Z][a-z]+[\sα😀]*){2,3}")]
    [InlineData(@"(?:[A-Z][a-z]+[ α]*){2,3}")]
    public void PublicCountAndOwnedDiscoveryAgreeAtEveryScalarOrigin(string pattern)
    {
        var texts = new[] { "", "Ab", "AbCd", "AbCdEf", "AbCdEfGh", "AbCdEfGhIjKlMn",
            "😀Ab\u00a0Cd\u2028EfGh!", "AbαCd😀Ef", "AbαCd", "ABcCd", "Ab Cd X", "Ab  Cd  EF",
            "Abbb\t Cddd \r\nAbbb Cddd ", "Ab\u2003Cd\u0085Ef\r\nGh", "Acbd Cbdd Acbdd Cbdd ",
            "xxxxAbCd\0EfGh", "Ab" + new string('b', 2050) + new string(' ', 2050) + "Cd" };
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            var reference = new Regex(pattern, options);
            using var live = new CancellationTokenSource();
            foreach (var text in texts)
            {
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Equal(reference.Count(text), regex.Count(input));
                Assert.Equal(reference.Count(text), regex.Count(input, live.Token));
                var cursor = regex.EnumerateMatches(input); var discovered = 0;
                while (cursor.MoveNext()) discovered++;
                Assert.Equal(reference.Count(text), discovered);
                var boundaries = new List<int> { 0 };
                foreach (var scalar in text.EnumerateRunes()) boundaries.Add(boundaries[^1] + scalar.Utf8SequenceLength);
                foreach (var start in boundaries)
                {
                    var invocation = new Utf8SliceInvocation(new(1_000_000, 1, 13), Timeout.InfiniteTimeSpan, live.Token);
                    Assert.True(invocation.Ledger.TryReserve(13));
                    var prepared = regex.Inspection.PreparedRegex;
                    if (!Utf8SliceRunner.TryCountPreparedRepeatedSegment(prepared.ScalarProgram!,
                        prepared.StructuralRepeatedSegmentPlan, input, start, ref invocation, out var found)) continue;
                    Assert.Equal(Utf8ExecutionLimitKind.None, found.Breach);
                    Assert.Equal(reference.Count(text, Encoding.UTF8.GetCharCount(input.AsSpan(0, start))), found.Count);
                    Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
                    Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
                }
            }
        }
    }

    [Theory]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}")]
    [InlineData(@"(?:[AC][bd]+[ \t]*){2,4}")]
    public void CompletePreparedRootRunsWithoutLocalStorage(string pattern)
    {
        var regex = new Utf8Regex(pattern);
        var prepared = regex.Inspection.PreparedRegex;
        var invocation = new Utf8SliceInvocation(new(1000, 1, 13), Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.True(Utf8SliceRunner.TryCountPreparedRepeatedSegment(prepared.ScalarProgram!,
            prepared.StructuralRepeatedSegmentPlan, "AbCd Acbd Cbdd "u8, 0, ref invocation, out var found));
        Assert.Equal(Utf8ExecutionLimitKind.None, found.Breach);
        Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0U, invocation.Budget.PeakDepth);
    }

    [Theory]
    [InlineData(@"(?<x>(?:[A-Z][a-z]+\s*){2,3})", RegexOptions.None)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){0,3}", RegexOptions.None)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}?", RegexOptions.None)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}!", RegexOptions.None)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}", RegexOptions.IgnoreCase)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}", RegexOptions.RightToLeft)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}", RegexOptions.NonBacktracking)]
    [InlineData(@"(?:[A-Z][A-Za-z]+\s*){2,3}", RegexOptions.None)]
    public void OtherProgramsRetainTheirVerifier(string pattern, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options);
        var prepared = regex.Inspection.PreparedRegex;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.False(Utf8SliceRunner.TryCountPreparedRepeatedSegment(prepared.ScalarProgram!,
            prepared.StructuralRepeatedSegmentPlan, "Ab Cd Ef !"u8, 0, ref invocation, out _));
        Assert.Equal(new Regex(pattern, options).Count("Ab Cd Ef !"), regex.Count("Ab Cd Ef !"u8));
        Assert.Equal(0UL, invocation.Budget.Steps);
    }

    [Fact]
    public void EveryFiniteCutoffRetainsLoansAndFirstDenial()
    {
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,3}");
        var prepared = regex.Inspection.PreparedRegex;
        var input = "xx Ab\u00a0Cd\u2028Ef GhIj "u8.ToArray();
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.True(Utf8SliceRunner.TryCountPreparedRepeatedSegment(prepared.ScalarProgram!,
            prepared.StructuralRepeatedSegmentPlan, input, 0, ref baseline, out var expected));
        for (ulong limit = 1; limit <= baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 1, 13), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.True(Utf8SliceRunner.TryCountPreparedRepeatedSegment(prepared.ScalarProgram!,
                prepared.StructuralRepeatedSegmentPlan, input, 0, ref invocation, out var found));
            Assert.Equal(limit == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, found.Breach);
            if (found.Breach == Utf8ExecutionLimitKind.None) Assert.Equal(expected.Count, found.Count);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        var denied = new Utf8SliceInvocation(new(1000, 1, 13), Timeout.InfiniteTimeSpan, default);
        Assert.True(denied.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, denied.TryReserveWorking(1));
        Assert.True(Utf8SliceRunner.TryCountPreparedRepeatedSegment(prepared.ScalarProgram!,
            prepared.StructuralRepeatedSegmentPlan, input, 0, ref denied, out var refusal));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, refusal.Breach);
        Assert.Equal(13UL, denied.Ledger.ReservedBytes);
        Assert.Equal(14UL, denied.Budget.PeakWorkingBytes);
    }

    [Theory]
    [InlineData("seek", false)] [InlineData("seek", true)]
    [InlineData("tail", false)] [InlineData("tail", true)]
    [InlineData("separator", false)] [InlineData("separator", true)]
    [InlineData("unicode", false)] [InlineData("unicode", true)]
    [InlineData("publication", false)] [InlineData("publication", true)]
    public void RealQuantaAndPublicationKeepOriginalTokenAndDeadline(string stage, bool expire)
    {
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,3}");
        var prepared = regex.Inspection.PreparedRegex;
        var text = stage switch { "seek" => new string('x', 8192), "tail" => "A" + new string('b', 8192) + "Cd",
            "separator" => "Ab" + new string(' ', 8192) + "Cd", "unicode" => "Ab" + new string('\u2003', 4096) + "Cd", _ => "AbCd" };
        var padding = stage switch { "seek" => 62, "tail" => 60, "separator" => 59, "unicode" => 58, _ => 56 };
        using var live = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(1_000_000, 1, 13), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        for (var index = 0; index < padding; index++) _ = invocation.TryChargeStep();
        invocation.CheckpointHook = _ => { if (expire) ticks = 100; else live.Cancel(); };
        var bytes = Encoding.UTF8.GetBytes(text);
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => Utf8SliceRunner.TryCountPreparedRepeatedSegment(
            prepared.ScalarProgram!, prepared.StructuralRepeatedSegmentPlan, bytes, 0, ref invocation, out _));
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => Utf8SliceRunner.TryCountPreparedRepeatedSegment(
            prepared.ScalarProgram!, prepared.StructuralRepeatedSegmentPlan, bytes, 0, ref invocation, out _)).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void PublicAdmissionAndStrictTailValidationStillApply()
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,3}";
        var regex = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy { MaxWorkSteps = 1000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(1, regex.Count("Ab\u2003Cd"u8));
        var malformed = "AbCd"u8.ToArray().Concat(new byte[] { 0xc3 }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var live = new CancellationTokenSource(); live.Cancel();
        Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, live.Token)).CancellationToken);
    }
}
