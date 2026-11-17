using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteSimplePatternCountTests
{
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Theory]
    [InlineData("a?b[0-9]{2}")]
    [InlineData("a{0,2}b[0-9]{1,2}")]
    [InlineData("(?:a|ab)?b[0-9]{2}")]
    [InlineData("(?:ab|a)?b[0-9]{2}")]
    [InlineData("(?<n>a?b[0-9]{2})")]
    [InlineData("a?b[^0-9]")]
    [InlineData("(?m:^a?b[0-9]{2}$)")]
    [InlineData("\\Aa?b[0-9]{2}\\z")]
    [InlineData("a?b[0-9]{2}$")]
    public void CompleteOrderedBranchesKeepPublicCountsAtOffsetsAndTails(string pattern)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var options = mode | RegexOptions.CultureInvariant;
            // .NET10 NonBacktracking.Count can miss the first real match after
            // failed optional-prefix candidates (" xx bxx abxx b12 ab34 ").
            // Use the stable interpreted overlapping-semantics comparator;
            // the two complete matches remain b12 and ab34 in our NB route.
            var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Funded);
            using var live = new CancellationTokenSource();
            foreach (var text in new[] { "", "a", "b1", "b12", "ab12", "abb12", "aab123",
                "b12ab34b56", " xx bxx abxx b12 ab34 ", "b12\nab34\nB56\n", "ab12\n", "ab12\nx" })
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                for (var offset = 0; offset < 4; offset++)
                {
                    var storage = new byte[offset + bytes.Length + 3];
                    bytes.CopyTo(storage, offset);
                    Assert.Equal((text, options, oracle.Count(text)), (text, options, regex.Count(storage.AsSpan(offset, bytes.Length))));
                    Assert.Equal(oracle.Count(text), regex.Count(storage.AsSpan(offset, bytes.Length), live.Token));
                }
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteAsciiCountNeedsNoVmWorkingBuffers(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("a?b[0-9]{2}", options, Regex.InfiniteMatchTimeout,
            Funded with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(2000, regex.Count(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("b12 ab34 ", 1000)))));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count("ab12"u8, canceled.Token)).CancellationToken);
    }

    [Theory]
    [InlineData("(?i:a?b[0-9]{2})", "Ab12 B34")]
    [InlineData("(?<=x)a?b[0-9]{2}", "xab12 xb34")]
    [InlineData("a?b[0-9]{2}\\b", "ab12b34 b56")]
    [InlineData("(?<x>a?b[0-9]{2})\\k<x>", "ab12ab12 b34")]
    [InlineData("a?b[0-9]{2}", "😀ab12é b34")]
    [InlineData("(?<x>.)?b[0-9]{2}", "😀b12𐐨b34")]
    [InlineData("a?b[0-9]{2}", "ab12\u212ab34")]
    public void ScalarAndContextDependentCasesKeepTheirRoutes(string pattern, string text)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, Funded);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(new Regex(pattern, RegexOptions.CultureInvariant).Count(text), regex.Count(input));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("abxx", 0)]
    [InlineData("b12 ab34", 2)]
    public void EveryWorkCutoffBoundsPublicSearchAndPublication(string text, int expected)
    {
        var regex = new Utf8Regex("a?b[0-9]{2}", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, Funded);
        var input = Encoding.UTF8.GetBytes(text);
        ulong required = 0;
        for (ulong work = 1; work <= 128; work++)
        {
            var candidate = new Utf8Regex("a?b[0-9]{2}", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
                Funded with { MaxWorkSteps = work });
            try { Assert.Equal(expected, candidate.Count(input)); required = work; break; }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
        }
        Assert.InRange(required, 2UL, 128UL);
        Assert.Equal(expected, regex.Count(input));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PublicPostValidationInterruptionDoesNotPublishACount(bool compiled, bool expire)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Regex("(?<n>a?b[0-9]{2})", options, TimeSpan.FromMilliseconds(20), Funded);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("b12 ab34 ", 256)));
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        var measured = new Utf8SliceInvocation(new(1000000, 1024, 1 << 20, 1 << 20), Regex.InfiniteMatchTimeout, default);
        _ = Utf8InputAnalyzer.Describe(input, ref measured);
        var validation = measured.Budget.Steps;
        var entered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(step => {
            if (step <= validation) return;
            entered = true; if (expire) ticks = 50; else token.Cancel();
        });
        int? published = null;
        try
        {
            var failure = Record.Exception(() => published = regex.Count(input, token.Token));
            Assert.True(entered);
            Assert.Null(published);
            if (expire) Assert.Empty(Assert.IsType<RegexMatchTimeoutException>(failure).Input);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { diagnostics.Complete(); }
        Assert.Equal("b12", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(regex.GroupNumberFromName("n")).Span));
        Assert.Equal(512, regex.Count(input));
    }
}
