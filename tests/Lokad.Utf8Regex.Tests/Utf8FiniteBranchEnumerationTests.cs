using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8FiniteBranchEnumerationTests
{
    private static Utf8ResourcePolicy Funded(ulong work = 1000000) => new()
    { MaxWorkSteps = work, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 };

    [Fact]
    public void CaptureCursorsAdmitWorkingStorageBeforePublishing()
    {
        const string pattern = "(?<n>a?b[0-9]{2})";
        var denied = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = Funded()with
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => { var cursor = denied.EnumerateMatches("b12"u8); cursor.MoveNext(); }).ResourceKind);
        Assert.Equal(1, new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = Funded()
        }).Count("b12"u8));
    }

    [Theory]
    [InlineData("a?b[0-9]{2}")]
    [InlineData("a{1,2}b[0-9]{2}")]
    [InlineData("ab[0-9]{1,2}d")]
    [InlineData("(?<n>a?b[0-9]{2})")]
    [InlineData("(?m:^a?b[0-9]{2}$)")]
    public void CompleteAsciiValuesRespectFiniteWorkingStorage(string pattern)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = RegexOptions.CultureInvariant | mode;
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded()
            });
            var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
            using var source = new CancellationTokenSource();
            foreach (var text in new[] { "", "a", "b1", "b12", "ab12", "aab12", "ab1d ab12d",
                " abxx b12 ab34 ", "b12ab34b56", "\nab12\nb34\n", new string('a', 4096) + "b12" })
            {
                var expected = oracle.Matches(text).Select(m => (System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, m.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(m.Index, m.Length)))).ToArray();
                var bytes = Encoding.UTF8.GetBytes(text);
                for (var offset = 0; offset < 4; offset++)
                {
                    var storage = new byte[offset + bytes.Length + 3];
                    bytes.CopyTo(storage, offset);
                    var actual = new List<(int, int)>();
                    foreach (var match in regex.EnumerateMatches(storage.AsSpan(offset, bytes.Length), cancellationToken: source.Token))
                        actual.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CumulativeWorkCutoffsAndAliasesKeepTheirOriginalAllowance(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var input = "abxx b12 ab34"u8.ToArray();
        var funded = new Utf8Regex("a?b[0-9]{2}", options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded()
        });
        var measured = funded.EnumerateMatches(input);
        Assert.True(measured.MoveNext());
        Assert.True(measured.MoveNext());
        var twoMatches = measured.DebugInvocationSteps;
        Assert.False(measured.MoveNext());
        var complete = measured.DebugInvocationSteps;
        for (ulong work = 1; work < complete; work++)
        {
            var regex = new Utf8Regex("a?b[0-9]{2}", options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded(work)
            });
            var denied = Assert.Throws<Utf8ResourceLimitExceededException>(() => Drain(regex, input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, denied.ResourceKind);
            Assert.Equal(work, denied.Limit);
            Assert.True(denied.Requested > work);
        }
        var bounded = new Utf8Regex("a?b[0-9]{2}", options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded(twoMatches)
        });
        var cursor = bounded.EnumerateMatches(input);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        var second = cursor.Current;
        try { alias.MoveNext(); Assert.Fail("The alias must share the exhausted allowance."); }
        catch (Utf8ResourceLimitExceededException error)
        { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
        Assert.Equal(retained, alias.Current);
        Assert.Equal((5, 3), (retained.StartOffsetInBytes, (retained.EndOffsetInBytes - retained.StartOffsetInBytes)));
        Assert.Equal((9, 4), (second.StartOffsetInBytes, (second.EndOffsetInBytes - second.StartOffsetInBytes)));
        var independent = bounded.EnumerateMatches(input);
        Assert.True(independent.MoveNext());
        Assert.True(independent.MoveNext());
        Assert.Equal(second, independent.Current);

        var original = funded.EnumerateMatches(input);
        Assert.True(original.MoveNext());
        var copy = original;
        Assert.True(original.MoveNext());
        Assert.True(copy.MoveNext());
        Assert.Equal(original.Current, copy.Current);
        Assert.False(original.MoveNext());
        Assert.False(copy.MoveNext());
        Assert.Equal(retained, alias.Current);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TerminalMoveObservesOriginalAuthorityAndRetainsPublishedValues(bool compiled, bool expire)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var input = "b12 ab34"u8.ToArray();
        using var source = new CancellationTokenSource();
        var armed = false;
        var entered = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!armed) return 0;
            entered = true;
            if (expire) return 50;
            source.Cancel();
            return 0;
        });
        var regex = new Utf8Regex("a?b[0-9]{2}", options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Funded()
        });
        var cursor = regex.EnumerateMatches(input, cancellationToken: source.Token);
        Assert.True(cursor.MoveNext());
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        armed = true;
        Exception? observed = null;
        try { cursor.MoveNext(); } catch (Exception error) { observed = error; }
        Assert.True(entered);
        if (expire)
        {
            var failure = Assert.IsType<RegexMatchTimeoutException>(observed);
            Assert.Empty(failure.Input);
            Assert.Equal(TimeSpan.FromMilliseconds(20), failure.MatchTimeout);
        }
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(retained, cursor.Current);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DenseFiniteValuesPreserveBorrowedRangesAndAliases(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("(?<n>a?b[0-9]{2})", options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded()
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("b12 ab34 ", 1024)));
        var count = Drain(regex, input);
        Assert.Equal(2048, count);
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
        { var cursor = regex.EnumerateMatches(input, cancellationToken: source.Token); cursor.MoveNext(); }).CancellationToken);
        // Abandoning one cursor does not lend its allowance to a new invocation.
        var abandoned = regex.EnumerateMatches(input);
        Assert.True(abandoned.MoveNext());
        var retained = abandoned.Current;
        Assert.Equal(2048, Drain(regex, input));
        Assert.Equal((0, 3), (retained.StartOffsetInBytes, (retained.EndOffsetInBytes - retained.StartOffsetInBytes)));
    }

    [Theory]
    [InlineData("a?b[0-9]{2}\\b", "ab12x b34", RegexOptions.None)]
    [InlineData("(?<=x)a?b[0-9]{2}", "xab12 xb34", RegexOptions.None)]
    [InlineData("(?<x>a?b[0-9]{2})\\k<x>", "ab12ab12 b34", RegexOptions.None)]
    [InlineData("(?<x>a)(?<x>b)?", "ab a", RegexOptions.None)]
    [InlineData("(?>a|ab)c", "abc ac", RegexOptions.None)]
    [InlineData("a?b[0-9]{2}", "Ab12 B34", RegexOptions.IgnoreCase)]
    [InlineData("a?b[0-9]{2}", "b12 ab34", RegexOptions.RightToLeft)]
    [InlineData("a?b[0-9]{2}", "b12 ab34", RegexOptions.NonBacktracking)]
    [InlineData("a??b[0-9]{2}", "ab12 b34", RegexOptions.None)]
    [InlineData("a??b[0-9]{2}", "ab12 b34", RegexOptions.Compiled)]
    [InlineData("ab[0-9]{2}", "ab12 ab34", RegexOptions.None)]
    [InlineData("a?|b", "ab", RegexOptions.None)]
    public void OtherSemanticsKeepTheirCompleteResults(string pattern, string text, RegexOptions mode)
    {
        var limits = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        var regex = new Utf8Regex(pattern, options: mode | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = limits
        });
        var oracle = new Regex(pattern, (mode & ~RegexOptions.NonBacktracking) | RegexOptions.CultureInvariant);
        var actual = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(text)))
            actual.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(oracle.Matches(text).Select(m => (System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, m.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(m.Index, m.Length)))), actual);
        var scalar = new Utf8Regex("a?b[0-9]{2}", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = limits
        });
        actual.Clear();
        foreach (var match in scalar.EnumerateMatches("😀ab12é b34"u8))
            actual.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(new[] { (4, 4), (11, 3) }, actual);
    }

    private static int Drain(Utf8Regex regex, byte[] input)
    {
        var count = 0;
        foreach (var match in regex.EnumerateMatches(input)) count++;
        return count;
    }
}
