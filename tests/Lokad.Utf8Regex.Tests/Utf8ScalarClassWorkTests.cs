using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarClassWorkTests
{
    private static readonly string ManyRanges = string.Concat(
        Enumerable.Range(0, 4096).Select(index => (char)(0x100 + 2 * index)));
    private static readonly string ManyCategories = string.Concat(Enumerable.Repeat(@"\p{Lu}", 4096));

    [Theory]
    [InlineData(31)] [InlineData(32)] [InlineData(33)]
    [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void RangeBlocksPreserveHitsMissesAndSlicedInputs(int count)
    {
        var members = string.Concat(Enumerable.Range(0, count).Select(index => (char)(0x100 + 2 * index)));
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(@"\A[" + members + @"]\z", options, Regex.InfiniteMatchTimeout,
                new Utf8ResourcePolicy { MaxWorkSteps = 100_000, MaxWorkingBytes = 1_000_000,
                    MaxResultBytes = 1_000_000, MaxDepth = 128 });
            foreach (var index in new[] { 0, Math.Min(31, count - 1), count - 1 })
            {
                var input = Encoding.UTF8.GetBytes("x" + (char)(0x100 + 2 * index) + "y");
                Assert.True(regex.IsMatch(input.AsSpan(1, input.Length - 2)));
                Assert.Equal(1, regex.Count(input.AsSpan(1, input.Length - 2)));
            }
            Assert.False(regex.IsMatch("😀"u8));
            if ((options & RegexOptions.IgnoreCase) != 0) Assert.True(regex.IsMatch("ā"u8));
        }
    }

    public static IEnumerable<object[]> WorkCases()
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
        {
            yield return [@"\A[" + ManyRanges + @"]\z", "\U0010FFFF", false, options];
            yield return [@"\A[" + ManyCategories + @"]\z", "1", false, options];
            yield return [@"\A[a-[" + ManyRanges + @"]]\z", "a", true, options];
            yield return [@"\A[^" + ManyRanges + @"]\z", "\U0010FFFF", true, options];
            yield return [@"\A[" + ManyRanges + @"]\z", "\u212a", false, options | RegexOptions.IgnoreCase];
        }
    }

    [Theory]
    [MemberData(nameof(WorkCases))]
    public void LargePredicateCannotHideItsScanFromFiniteWork(string pattern, string text,
        bool expected, RegexOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, new Utf8Regex(pattern, options).IsMatch(input));
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 32 });
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
        Assert.Equal(32UL, failure.Limit);
        Assert.Equal(33UL, failure.Requested);
        failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
        Assert.Equal(33UL, failure.Requested);
        Assert.Equal(expected ? 1 : 0, new Utf8Regex(pattern, options).Count(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedShorthandsInSubtractionUseTheExistingBoundedBitSet(bool compiled)
    {
        var pattern = @"\A[a-[" + string.Concat(Enumerable.Repeat(@"\s", 4096)) + @"]]\z";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 32 });
        Assert.True(regex.IsMatch("a"u8));
        Assert.Equal(1, regex.Count("a"u8));
        Assert.False(regex.IsMatch(" "u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationInterruptsInsideOnePredicate(bool categories)
    {
        var regex = new Utf8Regex(@"\A[" + (categories ? ManyCategories : ManyRanges) + @"]\z",
            RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 });
        var input = Encoding.UTF8.GetBytes(categories ? "1" : "\U0010FFFF");
        using var cancellation = new CancellationTokenSource();
        ulong checkpoint = 0;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            checkpoint = steps;
            cancellation.Cancel();
        });
        try
        {
            var failure = Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token));
            Assert.Equal(cancellation.Token, failure.CancellationToken);
            Assert.Equal(64UL, checkpoint);
        }
        finally { session.Complete(); }
        Assert.Equal(0, regex.Count(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeadlineExpiresInsideOnePredicate(bool categories)
    {
        var regex = new Utf8Regex(@"\A[" + (categories ? ManyCategories : ManyRanges) + @"]\z",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(20),
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1_000_000,
                MaxResultBytes = 1_000_000 });
        var input = Encoding.UTF8.GetBytes(categories ? "1" : "\U0010FFFF");
        var expired = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => expired ? long.MaxValue / 4 : 0);
        var session = Utf8SearchDiagnosticsSession.Start(_ => expired = true);
        try
        {
            var failure = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input));
            Assert.Equal(TimeSpan.FromSeconds(20), failure.MatchTimeout);
            Assert.Equal(string.Empty, failure.Input);
            Assert.True(expired);
        }
        finally { session.Complete(); }
        expired = false;
        Assert.Equal(0, regex.Count(input));
    }
}
