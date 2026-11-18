using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ControlledRequiredLiteralTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingRequiredLiteralNeedsNoVmWorkingCapacity(bool count)
    {
        var regex = new Utf8Regex(@"[a-z]+@[a-z]+\.[a-z]+", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1024,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.True(regex.Inspection.SearchPlan.RequiredPrefilterSearcher.HasValue);
        var input = Encoding.UTF8.GetBytes(new string('a', 100000));
        if (count) Assert.Equal(0, regex.Count(input));
        else Assert.False(regex.IsMatch(input));
    }

    [Theory]
    [InlineData(@"[a-z]+@[a-z]+\.[a-z]+", "alice@example.com", 1)]
    [InlineData(@"[a-z]+@[a-z]+\.[a-z]+", "@", 0)]
    [InlineData("(?:foo)?bar", "bar foo", 1)]
    [InlineData("(?:foo|bar)baz", "barbaz foobaz", 2)]
    [InlineData("foo|bar", "foo bar", 2)]
    [InlineData("foo|(?<empty>)", "bar", 4)]
    [InlineData("(?<=foo)bar", "foobar", 1)]
    [InlineData("foo(?=bar)", "foobar", 1)]
    [InlineData("bar(?!foo)", "bar", 1)]
    [InlineData("(?i:ab)+@end", "ABab@end", 1)]
    [InlineData("(?<x>é+)😀fin", "éé😀fin é😀fin", 2)]
    [InlineData(@"[^a]+\.[^z]+", "This regex has the potential to be optimized further", 0)]
    [InlineData(@"[^a]+\.[^z]+", "abc.é😀", 1)]
    [InlineData(@"[^a]+\.[^z]+", ".", 0)]
    public void RequiredFactsNeverApproveMatchesOrRejectValidScalarAlternatives(
        string pattern, string text, int expected)
    {
        foreach (var extra in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
        foreach (var unlimited in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | extra;
            var regex = unlimited ? new Utf8Regex(pattern, options: options): new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1000000,
                    MaxDepth = 8192,
                    MaxWorkingBytes = 1048576,
                    MaxResultBytes = 1048576
                }
            });
                var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(expected != 0, regex.IsMatch(input));
            Assert.Equal(expected, regex.Count(input));
        }
    }

    [Fact]
    public void WholeSubjectScreeningPreservesLookbehindBeforeAnOffset()
    {
        var regex = new Utf8Regex("(?<=foo)bar", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 10000
            }
        });
        Assert.True(regex.Match("éfoobar"u8, 5, []).Success);
        Assert.Equal(1, Utf8MatchSequence.Count(regex.EnumerateMatches("éfoobar"u8, 5)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ScreeningPollsOriginalAuthorityAfterValidation(bool count, bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', (1 << 20)));
        // Locate the next existing diagnostic interval after strict validation.
        // The bounded literal scan then supplies that checkpoint without VM state.
        const ulong threshold = 64;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        const string pattern = @"[a-z]+@[a-z]+\.[a-z]+";
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1024,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        var entered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        Exception? failure = null;
        int? published = null;
        try { published = count ? regex.Count(input, token.Token) : regex.IsMatch(input, token.Token) ? 1 : 0; }
        catch (Exception error) { failure = error; }
        finally { diagnostics.Complete(); }
        Assert.True(entered);
        Assert.Null(published);
        if (expire)
        {
            var error = Assert.IsType<RegexMatchTimeoutException>(failure);
            Assert.Equal(pattern, error.Pattern);
            Assert.Equal(timeout, error.MatchTimeout);
            Assert.Empty(error.Input);
        }
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }
}
