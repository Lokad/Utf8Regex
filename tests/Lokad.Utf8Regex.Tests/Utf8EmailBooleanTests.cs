using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8EmailBooleanTests
{
    private const string Pattern = @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    private static bool Scalar(Utf8Regex regex, byte[] input)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
        invocation.AnchorOriginByte = program.RightToLeft ? input.Length : 0;
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, invocation.AnchorOriginByte,
                slots.Starts, slots.Ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach); return outcome.Found;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Theory]
    [InlineData("a@a.ab", true)] [InlineData("a@a.1", true)]
    [InlineData("a@[1.2.3.4]", true)] [InlineData("a@[1.2.3.4", true)]
    [InlineData("a@a.ab]", true)] [InlineData("a@a.abcdefghijkl", true)]
    [InlineData("a@a.abcdefghijklm", false)] [InlineData("@a.ab", false)]
    [InlineData("a_b-c.d@sub-domain.a.ab\n", true)]
    [InlineData("a@a.ab\n\n", false)] [InlineData("a@a.ab\r\n", false)]
    [InlineData("a@a.ab#", false)] [InlineData("a@@a.ab", false)]
    public void BoundedAsciiWholeProofNeedsNoVmWorkspace(string text, bool expected)
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, mode, Timeout.InfiniteTimeSpan,
                Funded with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            var storage = Encoding.UTF8.GetBytes("pad" + text + "tail");
            var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(expected, regex.IsMatch(input, token.Token));
                Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, session.ExecutionRoute);
                Assert.Equal(expected, regex.IsMatch(input));
            }
            finally { session.Complete(); }
        }
    }

    [Fact]
    public void UnicodeAliasesExcludedModesAndLongSubjectsKeepScalarVerification()
    {
        var alias = Pattern.Replace("^", @"\A").Replace("[0-9]", @"\p{Nd}").Replace("$", @"\Z");
        foreach (var pattern in new[] { Pattern, alias, Pattern.Replace("$", @"\z"), Pattern.Replace("^", "(?m:^)") })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking, RegexOptions.RightToLeft, RegexOptions.IgnoreCase })
        {
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, Funded);
            using var token = new CancellationTokenSource();
            foreach (var text in new[] { "", "missing at sign", "a@a.ab", "a@a.ab\n", "a@a.ab\n\n",
                "é@a.ab", "a@[١.٢.٣.٤]", "a@[𝟙.𝟚.𝟛.𝟜]", "x\na@a.ab", new string('a', 2044) + "@x.ab" })
            {
                var input = Encoding.UTF8.GetBytes(text);
                var expected = Scalar(regex, input);
                Assert.Equal((pattern, mode, text, expected), (pattern, mode, text, regex.IsMatch(input)));
                Assert.Equal(expected, regex.IsMatch(input, token.Token));
            }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void NativeEnvelopeHasAnExplicitBound(int length)
    {
        var input = Encoding.ASCII.GetBytes(new string('a', length - 5) + "@x.ab");
        var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, Funded);
        Assert.True(regex.IsMatch(input));
        var tight = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        if (length <= 2048) Assert.True(tight.IsMatch(input));
        else Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tight.IsMatch(input)).ResourceKind);
    }

    [Fact]
    public void CapturesOffsetsAndMalformedTailsKeepTheirContracts()
    {
        var regex = new Utf8Regex(Pattern);
        var input = "a_b@x.ab\n"u8.ToArray();
        var retained = regex.MatchDetailed(input);
        Assert.Equal("a_b", retained.GetGroup(1).GetValueString());
        Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
        Assert.True(regex.IsMatchFromUtf16Offset(input, 0));
        Assert.False(regex.IsMatchFromUtf16Offset(input, 1));
        Assert.Equal("a_b", retained.GetGroup(1).GetValueString());
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(malformed, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData("a@a.ab", true)] [InlineData("a@a.ab#", false)]
    public void WorkDenialIsNeverPublishedAsAFalseBoolean(string text, bool expected)
    {
        var input = Encoding.ASCII.GetBytes(text);
        var succeeded = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
                Funded with { MaxWorkSteps = work, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            try { Assert.Equal(expected, regex.IsMatch(input)); succeeded = true; break; }
            catch (Utf8ResourceLimitExceededException error) { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
        }
        Assert.True(succeeded);
    }

    [Fact]
    public void FinalPublicationObservesOriginalCancellationAndDeadline()
    {
        foreach (var expire in new[] { false, true })
        {
            var reads = 0; var trigger = int.MaxValue; long ticks = 0;
            using var token = new CancellationTokenSource();
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            {
                if (++reads == trigger) { if (expire) ticks += 100; else token.Cancel(); }
                return ticks;
            });
            var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50), Funded);
            Assert.True(regex.IsMatch("a@a.ab"u8, token.Token));
            trigger = reads; reads = 0;
            if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch("a@a.ab"u8, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch("a@a.ab"u8, token.Token)).CancellationToken);
        }
    }
}
