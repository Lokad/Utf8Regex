using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceClassSubtractionTests
{
    [Theory]
    [InlineData(@"[a-[b]c]")]
    [InlineData(@"[a-z-[b]-[c]]")]
    [InlineData(@"[a-[b]")]
    [InlineData(@"[\c1]")]
    [InlineData(@"[\c`]")]
    [InlineData(@"[a-\d]")]
    public void InvalidClassSyntaxKeepsOracleConstructionFailures(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options));
            Assert.Equal(expected.Error, actual.Error);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Message, actual.Message);
        }
    }

    [Fact]
    public void AWorkLimitCanStopInsideOneClassAndRecover()
    {
        var program = Utf8SliceParser.TryParse(@"[a-z-[aeiou-[e]]]", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var predicate = Assert.Single(program.CategorySetMembers[0]).Table.ClassProgram;
        Assert.NotNull(predicate);

        var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(2, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.False(predicate.TryMatch(new Rune('e'), Utf8ScalarCaseBehavior.Invariant, ref denied, out var breach));
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach);
        Assert.Equal(3UL, denied.Budget.Steps);
        Assert.Equal(0UL, denied.Ledger.ReservedBytes);

        var admitted = new Utf8SliceInvocation(new Utf8ExecutionLimits(3, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(predicate.TryMatch(new Rune('e'), Utf8ScalarCaseBehavior.Invariant, ref admitted, out breach));
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(3UL, admitted.Budget.Steps);
        Assert.Equal(0UL, admitted.Ledger.ReservedBytes);
    }

    [Fact]
    public void SupplementarySubtractionChargesEverySegmentAndRecovers()
    {
        var program = Utf8SliceParser.TryParse(@"[\p{L}-[\p{Lu}-[\p{Ll}]]]", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var predicate = Assert.Single(program.CategorySetMembers[0]).Table.ClassProgram;
        Assert.NotNull(predicate);
        var scalar = new Rune(0x10428);
        var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(2, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.False(predicate.TryMatch(scalar, Utf8ScalarCaseBehavior.Invariant, ref denied, out var breach));
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach);
        Assert.Equal(3UL, denied.Budget.Steps);
        Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        var admitted = new Utf8SliceInvocation(new Utf8ExecutionLimits(3, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(predicate.TryMatch(scalar, Utf8ScalarCaseBehavior.Invariant, ref admitted, out breach));
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(3UL, admitted.Budget.Steps);
        Assert.Equal(0UL, admitted.Ledger.ReservedBytes);
    }

    [Fact]
    public void CancellationInterruptsOneDeepClassBeforeItsOuterSegment()
    {
        var pattern = "[a]";
        for (var level = 0; level < 64; level++) pattern = "[a-" + pattern + "]";
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var predicate = Assert.Single(program.CategorySetMembers[0]).Table.ClassProgram;
        Assert.NotNull(predicate);
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
            var failure = Assert.Throws<OperationCanceledException>(() => predicate.TryMatch(new Rune('a'), Utf8ScalarCaseBehavior.Invariant, ref invocation, out _));
            Assert.Equal(source.Token, failure.CancellationToken);
            Assert.Equal(64UL, invocation.Budget.Steps);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }

        var recovered = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(predicate.TryMatch(new Rune('a'), Utf8ScalarCaseBehavior.Invariant, ref recovered, out var breach));
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(65UL, recovered.Budget.Steps);
    }

    [Fact]
    public void NestingAdmissionKeepsValidPublicPatternsAccepted()
    {
        var pattern = "[a]";
        for (var level = 0; level < 66; level++) pattern = "[a-" + pattern + "]";
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(new Regex(pattern, options).IsMatch("a"), regex.IsMatch(Encoding.UTF8.GetBytes("a")));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }
}
