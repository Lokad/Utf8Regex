using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteBranchEnumerationTests
{
    private static Utf8ResourcePolicy Tiny(ulong work = 1000000) => new()
        { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };

    [Theory]
    [InlineData("a?b[0-9]{2}")]
    [InlineData("a{1,2}b[0-9]{2}")]
    [InlineData("ab[0-9]{1,2}d")]
    [InlineData("(?<n>a?b[0-9]{2})")]
    [InlineData("(?m:^a?b[0-9]{2}$)")]
    public void CompleteAsciiValuesFitWithoutScalarScratch(string pattern)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = RegexOptions.CultureInvariant | mode;
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Tiny());
            var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
            using var source = new CancellationTokenSource();
            foreach (var text in new[] { "", "a", "b1", "b12", "ab12", "aab12", "ab1d ab12d",
                " abxx b12 ab34 ", "b12ab34b56", "\nab12\nb34\n", new string('a', 4096) + "b12" })
            {
                var expected = oracle.Matches(text).Select(m => (m.Index, m.Length)).ToArray();
                var bytes = Encoding.UTF8.GetBytes(text);
                for (var offset = 0; offset < 4; offset++)
                {
                    var storage = new byte[offset + bytes.Length + 3];
                    bytes.CopyTo(storage, offset);
                    var actual = new List<(int, int)>();
                    foreach (var match in regex.EnumerateMatches(storage.AsSpan(offset, bytes.Length), source.Token))
                        actual.Add((match.IndexInBytes, match.LengthInBytes));
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
        var funded = new Utf8Regex("a?b[0-9]{2}", options, Regex.InfiniteMatchTimeout, Tiny());
        var measured = funded.EnumerateMatches(input);
        Assert.True(measured.MoveNext());
        Assert.True(measured.MoveNext());
        var twoMatches = measured.DebugInvocationSteps;
        Assert.False(measured.MoveNext());
        var complete = measured.DebugInvocationSteps;
        for (ulong work = 1; work < complete; work++)
        {
            var regex = new Utf8Regex("a?b[0-9]{2}", options, Regex.InfiniteMatchTimeout, Tiny(work));
            var denied = Assert.Throws<Utf8ResourceLimitExceededException>(() => Drain(regex, input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, denied.ResourceKind);
            Assert.Equal(work, denied.Limit);
            Assert.True(denied.Requested > work);
        }
        var bounded = new Utf8Regex("a?b[0-9]{2}", options, Regex.InfiniteMatchTimeout, Tiny(twoMatches));
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
        Assert.Equal((5, 3), (retained.IndexInBytes, retained.LengthInBytes));
        Assert.Equal((9, 4), (second.IndexInBytes, second.LengthInBytes));
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
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void TerminalMoveObservesOriginalAuthorityAndRetainsPublishedValues(bool compiled, bool expire)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var input = "b12 ab34"u8.ToArray();
        var measured = new Utf8Regex("a?b[0-9]{2}", options, Regex.InfiniteMatchTimeout, Tiny()).EnumerateMatches(input);
        while (measured.MoveNext()) { }
        var fullCost = measured.DebugInvocationSteps;
        var second = new Utf8Regex("a?b[0-9]{2}", options, Regex.InfiniteMatchTimeout, Tiny()).EnumerateMatches(input);
        Assert.True(second.MoveNext()); Assert.True(second.MoveNext());
        var terminalCost = fullCost - second.DebugInvocationSteps;

        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        ulong target = 0;
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(step =>
        {
            if (step != target || target == 0) return;
            entered = true;
            if (expire) ticks = 50; else source.Cancel();
        });
        try
        {
            var regex = new Utf8Regex("a?b[0-9]{2}", options, TimeSpan.FromMilliseconds(20), Tiny());
            var cursor = regex.EnumerateMatches(input, source.Token);
            Assert.True(cursor.MoveNext()); Assert.True(cursor.MoveNext());
            var retained = cursor.Current;
            var invocation = cursor.ExportWorkingInvocation();
            Assert.True(invocation.Ledger.TryReserve(1));
            var padding = (64 - (invocation.Budget.Steps + terminalCost) % 64) % 64;
            target = invocation.Budget.Steps + padding + terminalCost;
            for (ulong i = 0; i < padding; i++)
                Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            Exception? observed = null;
            try { cursor.MoveNext(); } catch (Exception error) { observed = error; }
            Assert.True(entered);
            if (expire) Assert.Equal(string.Empty, Assert.IsType<RegexMatchTimeoutException>(observed).Input);
            else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
            Assert.Equal(retained, cursor.Current);
            Assert.Equal(1UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(1);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DenseFiniteValuesDoNotAcquirePerResultScratch(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("(?<n>a?b[0-9]{2})", options, Regex.InfiniteMatchTimeout, Tiny());
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("b12 ab34 ", 1024)));
        for (var i = 0; i < 16; i++) Assert.Equal(2048, Drain(regex, input));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = Drain(regex, input);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(2048, count);
        Assert.InRange(allocated, 0, 4096);
        Assert.Throws<ArgumentException>(() => Drain(regex, [.. input, 255]));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
        { var cursor = regex.EnumerateMatches(input, source.Token); cursor.MoveNext(); }).CancellationToken);
        // Abandoning one cursor does not lend its allowance to a new invocation.
        var abandoned = regex.EnumerateMatches(input);
        Assert.True(abandoned.MoveNext());
        var retained = abandoned.Current;
        Assert.Equal(2048, Drain(regex, input));
        Assert.Equal((0, 3), (retained.IndexInBytes, retained.LengthInBytes));
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
        var limits = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1024,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        var regex = new Utf8Regex(pattern, mode | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, limits);
        var oracle = new Regex(pattern, (mode & ~RegexOptions.NonBacktracking) | RegexOptions.CultureInvariant);
        var actual = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(text)))
            actual.Add((match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(oracle.Matches(text).Select(m => (m.Index, m.Length)), actual);
        var scalar = new Utf8Regex("a?b[0-9]{2}", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, limits);
        actual.Clear();
        foreach (var match in scalar.EnumerateMatches("😀ab12é b34"u8))
            actual.Add((match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(new[] { (4, 4), (11, 3) }, actual);
    }

    private static int Drain(Utf8Regex regex, byte[] input)
    {
        var count = 0;
        foreach (var match in regex.EnumerateMatches(input)) count++;
        return count;
    }
}
