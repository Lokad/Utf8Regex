using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceFiniteLazyTests
{
    private static readonly string[] Patterns = [
        @"(([0-9]){0,2}?x)?", @"(([0-9]){1,3}?x)?", @"((a){0,2}?x)?",
        @"((a){1,3}?b)?c", @"((a){1,3}?x)?", @"(?<x>a){0,3}?",
        @"(?<x>a|ab){1,3}?b", @"(?<x>a?){0,3}?b", @"(?<x>){1,3}?b",
        @"(?:(?<x>a)(?<y>b)){0,3}?c", @"(?<x>a){1,3}?\k<x>",
        @"(?<x>a){0,3}?(?(x)b|c)", @"(?>(?<x>a){0,3}?)b",
        @"(?=(?<x>a){1,3}?b)", @"(?:(?=(?<x>a))a){0,3}?b",
        @"(?<x>(?<x>a)){0,3}?b", @"(?<12>a){1,3}?\k<12>",
        @"(?<2147483647>a){0,3}?(?(2147483647)b|c)",
        @"(?<01>a){0,3}?b(?<1>c)", @"(?:(?<x>a)(?<-x>b)){0,3}?c",
        @"(?<x>a){0,3}?(?<-x>b)", @"(?n:(?<x>a)){0,3}?b",
        @"(?:(?<x>[a-z-[e]]){0,3}?b)?", @"(?<x>.){0,3}?b",
        @"(?<x>\p{L}){0,3}?b", @"(?<x>\p{Cs}){0,3}?b",
        "(?<x>\u00e9){0,3}?b", "(?<x>\ud83d|\ude00){0,3}?b",
        "(?<x>\U0001f600){0,3}?b", @"(?<x>(?<y>a){0,2}?){0,3}?b",
        @"(?<x>a){2,3}?b", @"(?<x>){2,4}?b",
    ];

    private static readonly string[] Texts = ["", "a", "aaa", "123", "12x", "123x", "acaaa",
        "abcabc aaab abc aab ab", "c", "a\nab\nb", "\u00e9\u00e9b",
        "\U0001f600", "a\U0001f600b\U0001f600", "\U0001f600\U0001f600b"];

    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var fold in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None) |
                (fold ? RegexOptions.IgnoreCase : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CapturedFiniteLazyRangesUseStableInterpretedConformance(string pattern, RegexOptions options)
    {
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        Assert.Equal((options & RegexOptions.Compiled) != 0, program.UsesInterpretedLazyLoopSemantics);
        // .NET 10.0.12's compiled range unwind can leak abandoned captures or
        // repeat global positions forever. The interpreted engine is the stable
        // semantic oracle for this explicitly documented defect class.
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, Texts, options & ~RegexOptions.Compiled);
    }

    [Theory]
    [InlineData("(?<x>a){2}?b")]
    [InlineData("(?<x>a)??b")]
    [InlineData("(?<x>a){1,}?b")]
    [InlineData("a{0,3}?b")]
    public void UnaffectedLazyShapesDoNotRequireTheDispositionRoute(string pattern)
    {
        var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        Assert.False(program.UsesInterpretedLazyLoopSemantics);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, Texts, options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitCapturePreservesNamedRangeUnwind(bool rtl)
    {
        var options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(@"((?<x>a){0,3}?x)?", options, Texts, options & ~RegexOptions.Compiled);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CaseSensitiveDefaultCultureRangesRetainOwnedUnwind(bool compiled, bool rtl)
    {
        var options = (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(@"((?<x>a){0,3}?x)?", options, Texts, options & ~RegexOptions.Compiled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbandonedCapturedRangesRespectWorkStorageAndCancellationWithRecovery(bool compiled)
    {
        const string pattern = @"\A(?<x>(?<x>a){0,24}?b)?c";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var input = Encoding.ASCII.GetBytes(new string('a', 24) + "c");
        var map = Utf8BoundaryMap.Create(input);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.False(Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref baseline).IsMatch);
        Assert.True(baseline.Budget.Steps > 128);
        Assert.True(baseline.Budget.PeakDepth > 1);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            var outcome = Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.False(outcome.IsMatch);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        for (uint depth = 1; depth < baseline.Budget.PeakDepth; depth++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, depth, 0),
                Regex.InfiniteMatchTimeout, CancellationToken.None);
            var outcome = Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.Depth, outcome.Breach);
            Assert.False(outcome.IsMatch);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var storage = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.TrailReserveBytes - 1),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref storage).Breach);
        Assert.Equal(0UL, storage.Ledger.ReservedBytes);
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        canceled.CheckpointHook = steps => { if (steps >= 64) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref canceled));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        var regex = new Utf8Regex(pattern, options);
        Assert.False(regex.IsMatch(input));
        var recovered = regex.MatchDetailed("aaabc"u8);
        Assert.Equal("aaab", recovered.GetGroup("x").GetValueString());
        Assert.Equal(4, recovered.GetGroup("x").CaptureCount);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
