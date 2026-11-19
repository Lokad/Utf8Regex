using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Auto-possessification soundness (native pcre2_auto_possess.c analog): a
// A disjoint literal or required word boundary can make a greedy single-token
// repeat's shorter alternatives impossible. Keep zero-minimum boundary runs,
// overlapping classes and nullable suffixes. Counters prove engagement; public
// ASCII results use BCL only on overlapping semantics.
public sealed class Pcre2AutoPossessifyTests
{
    private static void AssertAutoPossessCount(string pattern, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(expected, program.Program.AutoPossessiveRepeatCount);
    }

    private static void AssertAutoPossessCount(string pattern, Pcre2CompileOptions options, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(expected, program.Program.AutoPossessiveRepeatCount);
    }

    private static void AssertMatchesBcl(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        var expected = oracle.Match(inputText);
        var actual = regex.Match(input);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Value, System.Text.Encoding.UTF8.GetString(actual.GetValueBytes()));
        }

        Assert.Equal(oracle.Count(inputText), regex.Count(input));
    }

    [Theory]
    [InlineData(@"\d+b", "123b")]
    [InlineData(@"\d+b", "123c")]
    [InlineData(@"\d+bc", "12bc")]
    [InlineData(@"\d+bc", "12bd")]
    public void EngagedShapesMatchBcl(string pattern, string inputText)
    {
        AssertAutoPossessCount(pattern, 1);
        AssertMatchesBcl(pattern, inputText);
    }

    [Theory]
    [InlineData("[ab]+b", "abb")]
    [InlineData("[ab]+b", "aba")]
    [InlineData("a+b?", "aab")]
    [InlineData("a+b?", "aa")]
    [InlineData("a+", "aaa")]
    [InlineData("a+", "b")]
    public void DeclinedShapesMatchBcl(string pattern, string inputText)
    {
        AssertMatchesBcl(pattern, inputText);
    }

    [Theory]
    [InlineData("[ab]+b", Pcre2CompileOptions.Caseless)]
    public void OverlappingOrNullableShapesDecline(string pattern, Pcre2CompileOptions options)
    {
        // R03 class slice serves the default-options shape on the shared
        // runner; caseless still routes the backtracking program, where the
        // overlap decline is proven.
        AssertAutoPossessCount(pattern, options, 0);
    }

    [Fact]
    public void GreedyAcceptingTailKeepsLongestCaptureWithoutARewindFrame()
    {
        const string pattern = @"(\d{4}[- ]){3}(\d{3,4})";
        using var regex = new Utf8Pcre2Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 256, MaxWorkingBytes = 768 }
        });
        using var source = new CancellationTokenSource();
        var options = Pcre2MatchOptions.Anchored | Pcre2MatchOptions.EndAnchored | Pcre2MatchOptions.NotEmptyAtStart;
        ReadOnlySpan<byte> input = "é 1234-5678-1234-4567"u8;
        var groups = new Utf8Pcre2CaptureRange[3];
        Assert.True(regex.IsMatch(input, 3, options, source.Token));
        var result = regex.Match(input, 3, groups, options, source.Token);
        Assert.True(result.Success);
        Assert.Equal(22, result.EndOffsetInBytes);
        Assert.Equal(18, groups[2].StartOffsetInBytes);
        Assert.Equal(22, groups[2].EndOffsetInBytes);
        Assert.False(regex.IsMatch("é 1234-5678-1234-4567x"u8, 3, options, source.Token));
    }

    [Fact]
    public void RepeatBeforeConsumingSuffixStillCanGiveBackAScalar()
    {
        using var regex = new Utf8Pcre2Regex(@"(\d{4}[- ]){3}(\d{3,4})[0-9]\z");
        var groups = new Utf8Pcre2CaptureRange[3];
        var result = regex.Match("1234-5678-1234-4567"u8, captures: groups);
        Assert.True(result.Success);
        Assert.Equal(15, groups[2].StartOffsetInBytes);
        Assert.Equal(18, groups[2].EndOffsetInBytes);
    }

    [Fact]
    public void LongAcceptingTailObservesTheOriginalCancellationToken()
    {
        using var regex = new Utf8Pcre2Regex(@"(\d{4}[- ]){3}\d+",
            policy: new Utf8RegexPolicy { MatchTimeout = TimeSpan.FromSeconds(10) });
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var input = new byte[256 * 1024];
        input.AsSpan().Fill((byte)'5');
        "1234-5678-1234-"u8.CopyTo(input);
        using var source = new CancellationTokenSource();
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls == 32) source.Cancel();
            return 0;
        });
        var failure = Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(input, matchOptions: Pcre2MatchOptions.Anchored, cancellationToken: source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
    }

    [Fact]
    public void FundedGreedyClassRunAdmitsTheSuccessfulEndpointWithoutPerScalarState()
    {
        using var regex = new Utf8Pcre2Regex(@"\A(?P<run>[a-z]+)[Z!]\z", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 128, MaxDepth = 2, MaxWorkingBytes = 1024 }
        });
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match).Program;
        Assert.Contains(program.Instructions, instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.CollapsibleTokenRepeat);

        var input = new byte[16 * 1024 + 1];
        input.AsSpan(0, input.Length - 1).Fill((byte)'a');
        input[^1] = (byte)'Z';
        using var source = new CancellationTokenSource();
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[2];
        var result = regex.Match(input, captures: captures, cancellationToken: source.Token);
        Assert.Equal((0, input.Length), (result.StartOffsetInBytes, result.EndOffsetInBytes));
        Assert.Equal((0, input.Length - 1), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
    }

    [Fact]
    public void FundedGreedyClassRunPollsOriginalTokenDuringLongScan()
    {
        using var regex = new Utf8Pcre2Regex(@"\A(?P<run>[a-z]+)[Z!]\z", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(10),
            ResourcePolicy = new() { MaxWorkSteps = 512, MaxDepth = 2, MaxWorkingBytes = 1024 }
        });
        var input = new byte[256 * 1024 + 1];
        input.AsSpan(0, input.Length - 1).Fill((byte)'a');
        input[^1] = (byte)'Z';
        using var source = new CancellationTokenSource();
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls == 32) source.Cancel();
            return 0;
        });
        var failure = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, cancellationToken: source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
    }

    [Fact]
    public void GreedyClassRunAtMinimumNeedsNoReplayOnSuffixFailure()
    {
        using var regex = new Utf8Pcre2Regex(@"\A(?P<run>[a-z😀]+)[a-z]!\z", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 64, MaxDepth = 1, MaxWorkingBytes = 128 }
        });
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch).Program;
        Assert.Contains(program.Instructions, instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.CollapsibleTokenRepeat);

        using var source = new CancellationTokenSource();
        Assert.False(regex.IsMatch("a!!"u8, cancellationToken: source.Token));
        Assert.False(regex.IsMatch("😀!!"u8, cancellationToken: source.Token));
    }

    [Fact]
    public void FundedPlansCommitTheDateDelimitersButKeepTheWordBoundaryRetry()
    {
        const string pattern = @"\b\d{1,2}/\d{1,2}/\d{2,4}\b";
        // Count consumes the same lowered VM program even when Boolean calls
        // have an independently proved finite-language plan.
        var uncontrolled = new Utf8Pcre2Regex(pattern);
        Assert.Equal(3, Assert.IsType<Pcre2BacktrackingDirectProgram>(
            uncontrolled.DebugCompiledProgram.Operations.Count).Program.AutoPossessiveRepeatCount);
        var controlled = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100_000,
                MaxDepth = 512
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(controlled.DebugCompiledProgram.Operations.Count);
        Assert.Equal(2, program.Program.AutoPossessiveRepeatCount);
    }

    [Theory]
    [InlineData(@"\A(?P<field>[\w]+):\z", "𐐀x:", Pcre2CompileOptions.Ucp, 5, true)]
    [InlineData(@"\A(?P<field>(?i:[a-z]+))A\z", "aA", Pcre2CompileOptions.None, 1, false)]
    public void FundedLiteralProofUsesTheRunPredicate(
        string pattern, string subject, Pcre2CompileOptions options, int fieldEnd, bool commits)
    {
        using var regex = new Utf8Pcre2Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 64, MaxDepth = 32, MaxWorkingBytes = commits ? 128UL : 4096UL }
        });
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match).Program;
        Assert.Equal(commits ? 1 : 0, program.AutoPossessiveRepeatCount);

        using var source = new CancellationTokenSource();
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[2];
        var input = Encoding.UTF8.GetBytes(subject);
        var result = regex.Match(input, captures: captures, cancellationToken: source.Token);
        Assert.Equal((0, input.Length), (result.StartOffsetInBytes, result.EndOffsetInBytes));
        Assert.Equal((0, fieldEnd), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
    }

    [Theory]
    [InlineData(Pcre2CompileOptions.None, "-3.14159\n", 8)]
    [InlineData(Pcre2CompileOptions.Ucp, "+١.𝟚\n", 8)]
    [InlineData(Pcre2CompileOptions.None, "+١.𝟚\n", -1)]
    public void TerminalDigitRunsPreserveFinalLfAndUnicodeMode(Pcre2CompileOptions options, string input, int end)
    {
        using var regex = new Utf8Pcre2Regex(@"^[-+]?\d*\.?\d*$", options: options,
            policy: new Utf8RegexPolicy { ResourcePolicy = new() { MaxWorkSteps = 100_000 } });
        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(0, direct.Program.RepeatCount);

        var subject = Encoding.UTF8.GetBytes(input);
        Assert.Equal(end >= 0, regex.IsMatch(subject));
        var match = regex.Match(subject);
        Assert.Equal(end >= 0, match.Success);
        if (match.Success) Assert.Equal(end, match.EndOffsetInBytes);
    }

    [Theory]
    [InlineData(@"\d+\b", "123 ")]
    [InlineData(@"\d{2,4}\b", "12345 ")]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "11/18/2019 11/19/201A")]
    [InlineData(@"([A-Za-z0-9_]+)\b", "abc_12!")]
    [InlineData(@"\w+\b", "abc_12x")]
    [InlineData(@"a+(?:\bX|\bY)", "aaaY")]
    [InlineData(@"\d*\b1", "123x")]
    [InlineData(@"\d+\B3", "123")]
    [InlineData(@"[^a]+\bA", "12 A")]
    [InlineData(@"[\w ]+\bA", "12 A")]
    [InlineData(@"\d+(?:\b|3)", "123x")]
    public void BoundaryCommitmentPreservesPublicAsciiResults(string pattern, string inputText)
        => AssertMatchesBcl(pattern, inputText);

    [Theory]
    [InlineData(Pcre2CompileOptions.Ucp, "١٢ ", "١٢")]
    [InlineData(Pcre2CompileOptions.Ucp, "𝟘𝟙 ", "𝟘𝟙")]
    [InlineData(Pcre2CompileOptions.None, "١٢ ", "")]
    public void BoundaryWordProofUsesTheSameUnicodeMode(Pcre2CompileOptions options, string input, string expected)
    {
        var regex = new Utf8Pcre2Regex(@"\b\d+\b", options: options);
        var match = regex.Match(Encoding.UTF8.GetBytes(input));
        Assert.Equal(expected.Length != 0, match.Success);
        if (match.Success) Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }
}
