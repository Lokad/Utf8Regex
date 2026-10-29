using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BoundedDotCandidateTests
{
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 20000000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Theory]
    [InlineData("Holmes.{0,25}Watson|Watson.{0,25}Holmes")]
    [InlineData("A(?<gap>.{8,25})Z")]
    [InlineData("(?:A(?<gap>.{0,25})Z|A(?<other>.{0,25})Y)")]
    [InlineData("(?<outer>(?<gap>.{0,25}))Z")]
    [InlineData("A.{0,25}?Z")]
    [InlineData("A(?>.{0,25})Z")]
    public void PublicRangesCapturesAndGreedyOrderKeepOverlappingBclSemantics(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.Singleline,
            RegexOptions.IgnoreCase, RegexOptions.RightToLeft })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Funded);
            foreach (var text in new[] { "", "AxZ", "AxxxxxxxxZxxZ", "AxxxxxxxxY AxxxxxxxxZ", "Axxxxxxxxz",
                "Axxxxxxxx\nZ", "HolmesWatson", "Holmes" + new string('x', 25) + "Watson",
                "Holmes" + new string('x', 26) + "Watson", "HolmesxxxxW-not-a-tail", "Watson\nHolmes" })
            {
                var input = Encoding.UTF8.GetBytes(text); var expected = oracle.Match(text);
                Assert.Equal(expected.Success, regex.IsMatch(input));
                Assert.Equal(expected.Success, regex.Match(input).Success);
                Assert.Equal(oracle.Count(text), regex.Count(input));
                var actual = regex.MatchDetailed(input);
                Assert.Equal((expected.Success, expected.Index, expected.Length, expected.Value),
                    (actual.Success, actual.IndexInUtf16, actual.LengthInUtf16, actual.GetValueString()));
                if (!actual.Success) continue;
                foreach (var number in oracle.GetGroupNumbers())
                {
                    var wanted = expected.Groups[number]; var group = actual.GetGroup(number);
                    Assert.Equal((wanted.Success, wanted.Value, wanted.Captures.Count),
                        (group.Success, group.GetValueString(), group.CaptureCount));
                    for (var i = 0; i < wanted.Captures.Count; i++)
                        Assert.Equal(wanted.Captures[i].Value, group.GetCapture(i).GetValueString());
                }
            }
        }
    }

    [Theory]
    [InlineData(7)] [InlineData(8)] [InlineData(25)] [InlineData(511)] [InlineData(512)]
    public void ConservativeByteWindowIncludesTheLastAllowedScalarEndpoint(int maximum)
    {
        var pattern = @"\AA(?<gap>.{0," + maximum + "})Z";
        // The nonempty lookahead prevents the repeat's single-atom shortcut;
        // the following Dot still determines newline behavior and scalar width.
        var referencePattern = pattern.Replace(".", @"(?:(?=[\s\S]).)");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.Singleline })
        {
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Funded);
            var reference = new Utf8Regex(referencePattern, options, Timeout.InfiniteTimeSpan, Funded);
            Assert.Contains(reference.Inspection.AvailableOwnedSliceProgram.Instructions,
                instruction => instruction.Kind == Utf8SliceInstructionKind.Fork && instruction.B == 1);
            foreach (var scalar in new[] { "x", "é", "中", "😀" })
            foreach (var length in new[] { maximum - 1, maximum, maximum + 1 })
            {
                var gap = string.Concat(Enumerable.Repeat(scalar, length));
                var input = Encoding.UTF8.GetBytes("A" + gap + "Z");
                var actual = regex.MatchDetailed(input); var expected = reference.MatchDetailed(input);
                Assert.Equal(length <= maximum, actual.Success);
                Assert.Equal((expected.Success, expected.IndexInBytes, expected.LengthInBytes, expected.GetValueString()),
                    (actual.Success, actual.IndexInBytes, actual.LengthInBytes, actual.GetValueString()));
                Assert.Equal(reference.Count(input), regex.Count(input));
                if (actual.Success) Assert.Equal(gap, actual.GetGroup("gap").GetValueString());
            }
        }
    }

    [Fact]
    public void NonBacktrackingAndNonzeroOriginsKeepTheirRanges()
    {
        var input = "😀xA😀😀Z xx AxxxxZ"u8.ToArray();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex("A.{0,25}Z", options, Timeout.InfiniteTimeSpan, Funded);
            Assert.Equal(1, regex.Count(input)); // Greedy selection reaches the final Z.
            Assert.Equal(5, regex.Match(input).IndexInBytes);
            Assert.Equal(5, regex.MatchFromUtf16Offset(input, 3).IndexInBytes);
            var next = regex.MatchFromUtf16Offset(input, 8);
            Assert.True(next.Success);
            Assert.Equal("AxxxxZ", Encoding.UTF8.GetString(input.AsSpan(next.IndexInBytes, next.LengthInBytes)));
        }
    }

    [Fact]
    public void EveryWorkCutoffPreservesTheBorrowedLoan()
    {
        var program = new Utf8Regex("A.{8,25}Z").Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.ASCII.GetBytes("A" + new string('x', 110));
        var baseline = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(0, Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline).Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work <= baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            Assert.Equal(work == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionIsObservedAfterValidation(bool expire)
    {
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("A" + new string('x', 110), 64)));
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.Validate(input, ref validation);
        long ticks = 0; using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex("A.{8,25}Z", RegexOptions.None, TimeSpan.FromMilliseconds(20), Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validation.Budget.Steps) return;
            entered = true; if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void RetainedCapturesAliasesAdmissionAndMalformedTailsRemainValid()
    {
        const string pattern = "A(?<gap>.{8,25})Z";
        var regex = new Utf8Regex(pattern); var input = "AxxxxxxxxZ"u8.ToArray();
        var retained = regex.MatchDetailed(input); var group = retained.GetGroup("gap");
        var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(cursor.Current, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal("xxxxxxxx", group.GetValueString());
        foreach (var policy in new[] { Funded with { MaxWorkingBytes = 1 }, Funded with { MaxResultBytes = 1 } })
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern, RegexOptions.None,
                Timeout.InfiniteTimeSpan, policy).MatchDetailed(input));
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailed(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, token.Token)).CancellationToken);
    }
}
