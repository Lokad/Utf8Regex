using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LeadingSetCountTests
{
    [Theory]
    [InlineData("(a+)(b)", "😀aaabéab", 2)]
    [InlineData("([ab]+)(c)", "😀aabcébc", 2)]
    [InlineData(@"(a+)\1", "😀aaaaéaa", 2)]
    [InlineData("(a+)(?=b)b", "😀aaabéab", 2)]
    public void AdmittedLeadingSetsPreserveVerificationAndScalarOffsets(string pattern, string text, int expected)
    {
        var regex = Create(pattern);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSet, regex.DebugCompiledProgram.CandidateSearch.Kind);
        var padded = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = padded.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, regex.Count(input, 4, Pcre2MatchOptions.None, token.Token));
        Assert.Equal(0, regex.Count(input, input.Length));
        Assert.True(regex.IsMatch(input));
        Assert.True(regex.IsMatch(input, 4, Pcre2MatchOptions.None, token.Token));
        Assert.False(regex.IsMatch(input, input.Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(Encoding.UTF8.GetBytes(text), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(Encoding.UTF8.GetBytes(text), 1));
    }

    [Fact]
    public void CountUsesTheFirstScreenedStartWithinOneWorkAllowance()
    {
        using var regex = Create("(a)b+", working: 1);
        using var source = new CancellationTokenSource();
        var subject = Encoding.UTF8.GetBytes("ab😀" + new string('x', 65536) + "a ax ab abb aabc aa😀abbb");

        Assert.Equal(4, regex.Count(subject, startOffsetInBytes: 2, cancellationToken: source.Token));
    }

    [Theory]
    [InlineData("(a)b+")]
    [InlineData("((a)(b+?))")]
    public void TerminalLiteralPairCountKeepsStartsAtBlockEdges(string pattern)
    {
        var subject = Encoding.UTF8.GetBytes("😀ab" + new string('x', 2045) + "abéabbb a");
        var skippedBlock = Encoding.UTF8.GetBytes("😀ab" + new string('x', 4094) + "ab");
        using var token = new CancellationTokenSource();
        using var regex = Create(pattern, working: 1);
        using var denied = Create(pattern, working: 1);

        Assert.Equal(3, regex.Count(subject, 4, cancellationToken: token.Token));
        Assert.Equal(2, regex.Count(subject, 6, cancellationToken: token.Token));
        Assert.Equal(2, regex.Count(skippedBlock, 4, cancellationToken: token.Token));
    }

    [Theory]
    [InlineData("(a)a+", "aaaaa", 1)]
    [InlineData("(a)b{2,}", "ab abb abbb", 2)]
    [InlineData("(a)b+c", "abb abbc", 1)]
    [InlineData("(?i)(a)b+", "AB ab", 2)]
    [InlineData(@"\A(a)b+", "xab", 0)]
    public void TerminalLiteralPairReductionRequiresIndependentCompleteHits(string pattern, string text, int expected)
    {
        using var regex = Create(pattern);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData("(a)b+", "ab", false)]
    [InlineData("(a)b+", "ab", true)]
    [InlineData("(?m)^ab.+$", "ab😀", false)]
    [InlineData("(?m)^ab.+$", "ab😀", true)]
    public void CountBlockSearchKeepsOriginalInterruption(string pattern, string hit, bool deadline)
    {
        var input = Encoding.UTF8.GetBytes(hit + "\n" + new string('x', 2048 * 70) + "\n" + hit);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Pcre2Regex(pattern, policy: new() { MatchTimeout = TimeSpan.FromMilliseconds(20) });
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (deadline) ticks = 25;
            else token.Cancel();
        });
        try
        {
            if (deadline)
                Assert.Equal(Pcre2ErrorKind.Timeout,
                    Assert.Throws<Pcre2MatchException>(() => regex.Count(input, cancellationToken: token.Token)).ErrorKind);
            else
                Assert.Equal(token.Token,
                    Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellationToken: token.Token)).CancellationToken);
        }
        finally { diagnostics.Complete(); }
    }

    [Theory]
    [InlineData("(a*)")]
    [InlineData("([ab]*)")]
    [InlineData("(a)?")]
    public void NullableLanguagesKeepTheirEmptyMatchesWithoutARequiredLeadingByte(string pattern)
    {
        var regex = Create(pattern);
        Assert.Equal(Pcre2CandidateSearchKind.None, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(3, regex.Count("xx"u8));
        Assert.True(regex.IsMatch("xx"u8));
        ReadOnlySpan<byte> result1Input = "xx"u8;
        var result1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var result1Result = regex.Match(result1Input, captures: result1Captures);
        Assert.Equal(string.Empty, System.Text.Encoding.UTF8.GetString((result1Captures[0].Success ? result1Input.Slice(result1Captures[0].StartOffsetInBytes, result1Captures[0].EndOffsetInBytes - result1Captures[0].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        var matches = regex.EnumerateMatches("xx"u8);
        var count = 0;
        while (matches.MoveNext()) count++;
        Assert.Equal(3, count);
    }

    [Fact]
    public void FlagsAssertionsAndResetKeepTheirExistingExclusions()
    {
        foreach (var pattern in new[] { "(?i)(a+)(b)", "(?<=a)(b+)", @"(a+)\Kb", "(?:(a+)|b+)(?(1)c|d)" })
            Assert.Equal(Pcre2CandidateSearchKind.None, Create(pattern).DebugCompiledProgram.CandidateSearch.Kind);
        var regex = Create("(a+)(b)");
        Assert.Equal(1, regex.Count("xxab"u8));
        Assert.Equal(0, regex.Count("xxab"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(0, regex.Count("ab😀"u8, 2));
        Assert.True(regex.IsMatch("xxab"u8));
        Assert.False(regex.IsMatch("xxab"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.False(regex.IsMatch("ab😀"u8, 2));
        var anchored = new Utf8Pcre2Regex("(a+)(b)", options: Pcre2CompileOptions.Anchored);
        Assert.Equal(Pcre2CandidateSearchKind.None, anchored.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(0, anchored.Count("xxab"u8));
    }

    [Theory]
    [InlineData("(a+)(b)", 'x', "aaab", false)]
    [InlineData("[a-z]+@[a-z]+", 'a', "foo@bar", false)]
    [InlineData(@"[^a]+\.[^z]+", 'b', "ébc.😀tail", true)]
    public void ImpossibleSubjectsNeedNoVmScratchWhileRealAttemptsStillAdmitIt(
        string pattern, char filler, string hit, bool needsStorage)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = Create(pattern, working: 1, result: 1);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        var bytes = Encoding.UTF8.GetBytes(new string(filler, 65536));
        Pcre2Runner.AdmitStart(bytes, 0, ref invocation);
        ReadOnlySpan<byte> input = bytes;
        var validation = invocation.Budget.DiagnosticCheckpoints;
        Assert.True(Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, input,
            new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
        Assert.Equal(0, count);
        Assert.True(invocation.Budget.DiagnosticCheckpoints > validation);
        Assert.Equal(0UL, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        if (needsStorage)
            Assert.Equal(Pcre2ErrorKind.HeapLimit,
                Assert.Throws<Pcre2MatchException>(() => regex.Count(Encoding.UTF8.GetBytes(hit))).ErrorKind);
        else Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes(hit)));

        var budget = new Pcre2ResourceBudget(regex.DebugCompiledProgram.Request, default);
        ref var matchInvocation = ref budget.WorkingInvocation;
        Pcre2Runner.AdmitStart(bytes, 0, ref matchInvocation);
        validation = matchInvocation.Budget.DiagnosticCheckpoints;
        Assert.True(Pcre2Runner.TryIsMatch(regex.DebugCompiledProgram, input,
            new Utf8BytePosition(0), Pcre2MatchOptions.None, ref budget, out var found));
        Assert.False(found);
        Assert.True(matchInvocation.Budget.DiagnosticCheckpoints > validation);
        Assert.Equal(0UL, matchInvocation.Budget.PeakWorkingBytes);
        Assert.Equal(0UL, matchInvocation.Ledger.ReservedBytes);
        Assert.False(regex.IsMatch(bytes));
        if (needsStorage)
            Assert.Equal(Pcre2ErrorKind.HeapLimit,
                Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(Encoding.UTF8.GetBytes(hit))).ErrorKind);
        else Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(hit)));
    }

    [Theory]
    [InlineData(false, false, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(true, false, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(false, false, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    [InlineData(true, false, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    [InlineData(false, true, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(true, true, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(false, true, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    [InlineData(true, true, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    public void AbsenceSearchKeepsOriginalInterruptionAndRetainedCaptures(bool cancel, bool isMatch, string pattern,
        string hit, string firstCapture, string? secondCapture)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        ReadOnlySpan<byte> retained2Input = Encoding.UTF8.GetBytes(hit);
        var retained2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained2Input, captures: retained2Captures);
        var request = regex.DebugCompiledProgram.Request;
        var budget = isMatch ? new Pcre2ResourceBudget(request, token.Token)
            : new Pcre2ResourceBudget(request.DefaultLimits, Pcre2SliceDriver.CreateInvocation(request, token.Token), false);
        ref var invocation = ref budget.WorkingInvocation;
        var bytes = Encoding.UTF8.GetBytes(new string('x', 262144));
        Pcre2Runner.AdmitStart(bytes, 0, ref invocation);
        ReadOnlySpan<byte> input = bytes;
        var validation = invocation.Budget.DiagnosticCheckpoints;
        var checkpoints = 0;
        invocation.CheckpointHook = _ => { checkpoints++; if (cancel) token.Cancel(); else ticks = 25; };
        Exception? failure = null;
        try
        {
            if (isMatch)
                Pcre2Runner.TryIsMatch(regex.DebugCompiledProgram, input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref budget, out _);
            else
                Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _);
        }
        catch (Exception error) { failure = error; }
        Assert.True(checkpoints > 0 && invocation.Budget.DiagnosticCheckpoints > validation);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0UL, invocation.Budget.PeakWorkingBytes);
        if (cancel) Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        Assert.Equal(firstCapture, System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        if (secondCapture is not null) Assert.Equal(secondCapture, System.Text.Encoding.UTF8.GetString((retained2Captures[2].Success ? retained2Input.Slice(retained2Captures[2].StartOffsetInBytes, retained2Captures[2].EndOffsetInBytes - retained2Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void DelimiterWindowsCountNonOverlappingHitsUnderOneWorkLimit()
    {
        using var regex = Create("[a-z]+@(?<tail>[a-z]+)");
        using var source = new CancellationTokenSource();
        var rejected = "😀" + new string('a', 2048) + "@!𐐨";
        var input = Encoding.UTF8.GetBytes(rejected + "abc@tailx@longer!xy@last!");
        var first = Encoding.UTF8.GetByteCount(rejected);
        var slice = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.Equal((int)'@', slice.DisjointClassDelimiter);

        Assert.Equal(0, regex.Count(input.AsSpan(0, first), 4, cancellationToken: source.Token));
        Assert.Equal(2, regex.Count(input, 4, cancellationToken: source.Token));
        Assert.Equal(2, regex.Count(input, first, cancellationToken: source.Token));
    }

    [Fact]
    public void CapturedDelimiterWindowsRetainTheFirstStartAndItsRanges()
    {
        using var regex = Create("([a-z]+)@([a-z]+)(![0-9]+)?");
        var slice = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.Equal((int)'@', slice.DisjointClassDelimiter);
        var prefix = string.Concat(Enumerable.Repeat("aa@!😀", 12));
        var input = Encoding.UTF8.GetBytes(prefix + "hello@world!12 yz@tail");
        var first = Encoding.UTF8.GetByteCount(prefix);
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[4];

        Assert.True(regex.IsMatch(input));
        Assert.Equal(first, regex.Match(input).StartOffsetInBytes);
        Assert.Equal(2, regex.Count(input, first));
        Assert.Equal(first + 2, regex.Match(input, first + 2, captures).StartOffsetInBytes);
        Assert.Equal((first + 2, first + 5), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.Equal((first + 6, first + 11), (captures[2].StartOffsetInBytes, captures[2].EndOffsetInBytes));
        Assert.Equal((first + 11, first + 14), (captures[3].StartOffsetInBytes, captures[3].EndOffsetInBytes));
        var probe = regex.Probe(input, Pcre2PartialMode.None).GetMatch();
        Assert.Equal(first, probe.Value.StartOffsetInBytes);
        Assert.True(probe.GetGroup(1).GetValueBytes().SequenceEqual("hello"u8));
    }

    [Theory]
    [InlineData(@"([a-z]+)@\1", "abcd@cd")]
    [InlineData("([a-z]{1,2})@tail", "abcd@tail")]
    public void CaptureReadsAndFiniteMaximaKeepLaterStartsInTheSameRun(string pattern, string suffix)
    {
        using var regex = Create(pattern);
        var slice = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.Equal(-1, slice.DisjointClassDelimiter);
        var input = Encoding.UTF8.GetBytes(suffix + new string('!', 128));
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[2];
        Assert.Equal(2, regex.Match(input, captures: captures).StartOffsetInBytes);
        Assert.Equal((2, 4), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
    }

    [Fact]
    public void RequiredLiteralMustOccurInTheRequestedSuffixAndStillNeedsVerification()
    {
        var regex = Create("[a-z]+@([a-z]+)");
        Assert.Equal(Pcre2CandidateSearchKind.LeadingRunThenLiteral, regex.DebugCompiledProgram.CandidateSearch.Kind);
        var padded = Encoding.UTF8.GetBytes("pad😀@bad foo@bar λaaa@bbbpad");
        var subject = padded.AsSpan(3, padded.Length - 6);
        Assert.Equal(2, regex.Count(subject));
        Assert.Equal(1, regex.Count(subject, Encoding.UTF8.GetByteCount("😀@bad foo@bar λ")));
        Assert.Equal(0, regex.Count("foo@bar"u8, 4));
        Assert.Equal(0, regex.Count("@bad aa@!"u8));
        Assert.True(regex.IsMatch(subject));
        Assert.False(regex.IsMatch("foo@bar"u8, 4));
        Assert.False(regex.IsMatch("@bad aa@!"u8));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀foo@bar"u8, 1));
    }

    private static Utf8Pcre2Regex Create(string pattern, ulong working = 1 << 20,
        ulong result = 1 << 20) =>
        new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 256,
                MaxWorkingBytes = (working) == 0 ? null : (working),
                MaxResultBytes = (result) == 0 ? null : (result)
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
}
