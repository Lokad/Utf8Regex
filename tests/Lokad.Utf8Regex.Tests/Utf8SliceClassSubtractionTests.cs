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
    [InlineData(@"[\[-\d]")]
    public void InvalidClassSyntaxHasOwnedConstructionFailures(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options: options));
            Assert.Equal("pattern", actual.ParamName);
        }
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
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }

        var recovered = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(predicate.TryMatch(new Rune('a'), Utf8ScalarCaseBehavior.Invariant, ref recovered, out var breach));
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
    }

    [Fact]
    public void NestingAdmissionKeepsValidPublicPatternsAccepted()
    {
        var pattern = "[a]";
        for (var level = 0; level < 66; level++) pattern = "[a-" + pattern + "]";
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(new Regex(pattern, options).IsMatch("a"), regex.IsMatch(Encoding.UTF8.GetBytes("a")));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }
}
