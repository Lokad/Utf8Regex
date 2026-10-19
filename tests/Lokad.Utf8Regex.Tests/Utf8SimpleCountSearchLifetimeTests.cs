using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8SimpleCountSearchLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FallbackSearchPreparationDoesNotGrowWithCandidateCount(bool compiled, bool miss)
    {
        var regex = new Utf8Regex("a?b[0-9]{2}", Options(compiled));
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(2, regex.Inspection.SimplePatternPlan.SearchLiterals.Length);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
            miss ? " xx bxx abxx " : " xx b12 ab34 ", 1000)));
        var expected = miss ? 0 : 2000;
        for (var i = 0; i < 4; i++) Assert.Equal(expected, regex.Count(input));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = regex.Count(input);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected, count);
        // One bounded search payload is allowed; the former per-search rebuild
        // allocated 3.6/5.4 MB for these hit/miss subjects.
        Assert.InRange(allocated, 0, 8192);
    }

    [Theory]
    [InlineData("a?b[0-9]{2}")]
    [InlineData("a{0,2}b[0-9]{1,2}")]
    [InlineData("(?:a|ab)?b[0-9]{2}")]
    [InlineData("(?:ab|a)?b[0-9]{2}")]
    [InlineData("(?<n>a?b[0-9]{2})")]
    [InlineData("(?i:a?b[0-9]{2})")]
    [InlineData("a?b[^0-9]")]
    [InlineData("(?m:^a?b[0-9]{2}$)")]
    public void BorrowedTailsAndOrderedOptionalBranchesKeepPublicResults(string pattern)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, Options(compiled));
            foreach (var text in new[] { "", "b12", "ab12", "abb12", "b12ab34b56", " xx bxx abxx b12 ab34 ", "b12\nab34\nB56\n" })
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                for (var offset = 0; offset < 4; offset++)
                {
                    var padded = new byte[bytes.Length + offset + 3];
                    bytes.CopyTo(padded, offset);
                    var subject = padded.AsSpan(offset, bytes.Length);
                    Assert.Equal(oracle.Count(text), regex.Count(subject));
                    Assert.Equal(oracle.IsMatch(text), regex.IsMatch(subject));
                    var expected = oracle.Match(text);
                    var match = regex.Match(subject);
                    Assert.Equal(expected.Success, match.Success);
                    if (match.Success)
                        Assert.Equal((expected.Index, expected.Length), (match.IndexInBytes, match.LengthInBytes));
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScalarValidationAndControlledCallsKeepTheirAuthority(bool compiled)
    {
        var input = Encoding.UTF8.GetBytes("😀ab12é b34");
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1024,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        var ordinary = new Utf8Regex("a?b[0-9]{2}", Options(compiled));
        var finite = new Utf8Regex("a?b[0-9]{2}", Options(compiled), Regex.InfiniteMatchTimeout, policy);
        using var tokens = new CancellationTokenSource();
        Assert.Equal(2, ordinary.Count(input));
        Assert.Equal(2, finite.Count(input, tokens.Token));
        var match = ordinary.Match(input);
        Assert.Equal((4, 4), (match.IndexInBytes, match.LengthInBytes));
        var malformed = input.Concat(new byte[] { 255 }).ToArray();
        Assert.Throws<ArgumentException>(() => ordinary.Count(malformed));
        Assert.Throws<ArgumentException>(() => finite.Count(malformed));
        var denied = new Utf8Regex("a?b[0-9]{2}", Options(compiled), Regex.InfiniteMatchTimeout,
            policy with { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input));
        tokens.Cancel();
        Assert.Throws<OperationCanceledException>(() => ordinary.Count(input, tokens.Token));
        Assert.Throws<OperationCanceledException>(() => finite.Count(input, tokens.Token));
    }

    [Fact]
    public void ReusedSearchKeepsTheOriginalAbsoluteDeadline()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        var regex = new Utf8Regex("a?b[0-9]{2}");
        var prepared = regex.Inspection.PreparedRegex;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("b12", 4096)));
        ticks = 25;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8ExecutionInterpreter.CountSimplePattern(input, prepared.ExecutionProgram,
                prepared.SearchPlan, prepared.SimplePatternPlan, deadline));
    }

    private static RegexOptions Options(bool compiled) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : RegexOptions.None);
}
