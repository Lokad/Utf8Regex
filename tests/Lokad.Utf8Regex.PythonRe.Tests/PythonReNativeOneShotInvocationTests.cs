using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeOneShotInvocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeValueAtFarByteStartNeedsNoPrefixCoordinates(bool finiteDeadline)
    {
        var timeout = finiteDeadline ? TimeSpan.FromSeconds(10) : Timeout.InfiniteTimeSpan;
        using var regex = new Utf8PythonRegex("needle", policy: new Utf8RegexPolicy()
        {
            MatchTimeout = timeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 64,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("é😀", 65536)) + "needle");
        using var token = new CancellationTokenSource();
        var value = regex.Match(input, input.Length - 6, cancellationToken: token.Token, mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(value.Success);
        Assert.Equal((input.Length - 6, input.Length), (value.StartOffsetInBytes, value.EndOffsetInBytes));
        Assert.True(value.GetValueBytes().SequenceEqual("needle"u8));
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Match(input, input.Length - 6, cancellationToken: token.Token, mode: Utf8PythonMatchMode.FullMatch)).CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralSearchObservesOriginalAuthorityDuringDiscovery(bool expire)
    {
        const string pattern = "abcdefghijklmnopq";
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = timeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxWorkingBytes = 1UL << 20
            }
        });
        var input = Encoding.UTF8.GetBytes(new string('x', 131072) + pattern);
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var limits = new Utf8ExecutionLimits(100000, 0, 1UL << 20);
        var invocation = new Utf8SliceInvocation(limits, timeout, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var enteredSearch = false;
        invocation.CheckpointHook = step =>
        {
            if (step <= 2) return;
            enteredSearch = true;
            if (expire) ticks = 50; else token.Cancel();
        };
        Exception? failure = null;
        // Exercise discovery itself. FullMatch can reject this unequal-length
        // subject immediately and need not search its tail.
        try { _ = regex.MaterializeNativeMatchValue(input, 0, 0, ref invocation); }
        catch (Exception error) { failure = error; }
        Assert.True(enteredSearch);
        if (expire)
        {
            var error = Assert.IsType<RegexMatchTimeoutException>(failure);
            Assert.Equal(pattern, error.Pattern);
            Assert.Equal(timeout, error.MatchTimeout);
            Assert.Empty(error.Input);
        }
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Theory]
    [InlineData(0, "é😀", "xé😀z", 0, 1, 7, "é😀")]
    [InlineData(1, "é😀", "é😀z", 0, 0, 6, "é😀")]
    [InlineData(1, "é😀", "xé😀z", 0, 0, 0, null)]
    [InlineData(0, "", "😀", 0, 0, 0, "")]
    [InlineData(0, "abc", "abc😀", 0, 0, 3, "abc")]
    [InlineData(0, "abc", "😀abc", 0, 4, 7, "abc")]
    [InlineData(0, "Шерлок", "😀 π Шерлок", 0, 8, 20, "Шерлок")]
    [InlineData(0, "Шерлок", "😀 π Шерлок 𝒜", 0, 8, 20, "Шерлок")]
    [InlineData(0, "needle", "😀éneedle", 0, 6, 12, "needle")]
    [InlineData(0, "Шерлок", "😀 Ватсон", 0, 0, 0, null)]
    [InlineData(1, "ab[0-9]+", "ab123😀", 0, 0, 5, "ab123")]
    [InlineData(1, "ab[0-9]+", "ab😀", 0, 0, 0, null)]
    [InlineData(2, "ab.*xy", "éab😀xy", 2, 2, 10, "ab😀xy")]
    [InlineData(2, "ab.*xy", "éab😀xz", 2, 0, 0, null)]
    [InlineData(2, "ab.*xy", "abxy", 0, 0, 4, "abxy")]
    [InlineData(2, ".*", "", 0, 0, 0, "")]
    public void NativeValueOutcomesAgreeWithScalarEdges(int kind, string pattern, string subject, int start, int first, int last, string? expected)
    {
        var input = Encoding.UTF8.GetBytes(subject);
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { PythonReCompileOptions.None, PythonReCompileOptions.DotAll })
            foreach (var cancellation in new[] { CancellationToken.None, token.Token })
            {
                var regex = new Utf8PythonRegex(pattern, options: options);
                var value = kind switch
                {
                    0 => regex.Match(input, start, cancellationToken: cancellation, mode: Utf8PythonMatchMode.Search),
                    1 => regex.Match(input, start, cancellationToken: cancellation),
                    _ => regex.Match(input, start, cancellationToken: cancellation, mode: Utf8PythonMatchMode.FullMatch),
                };
                Assert.Equal(expected is not null, value.Success);
                Assert.Equal((first, last), (value.StartOffsetInBytes, value.EndOffsetInBytes));
                if (expected is not null) Assert.Equal(Encoding.UTF8.GetBytes(expected), value.GetValueBytes().ToArray());
            }
    }



    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryNativeValueWorkCutoffIsCumulativeAndUnpublished(int kind)
    {
        var pattern = kind switch { 0 => "Шерлок", 4 => "needle", _ => "é😀" };
        var input = Encoding.UTF8.GetBytes(kind switch { 0 => "π Шерлок 𝒜", 4 => "needle", _ => "xé😀z" });
        var expected = kind switch { 0 => "Шерлок", 4 => "needle", _ => "é😀" };
        var regex = new Utf8PythonRegex(pattern);
        if (kind == 0) Assert.True(regex.DebugUsesZeroOffsetUtf8SearchValueFastPath);
        // The supplementary sequence has an exact core backend, while Python's
        // narrower public classifier keeps scalar discovery. Retain this case
        // as an internal borrowed-backend witness, separately from public routes.
        if (kind == 3) Assert.Equal("ExactUtf8Literal", regex.DebugUtf8ExecutionKind);
        const int start = 0;
        var operationKind = kind switch { 3 => 0, 4 => 2, _ => kind };
        const ulong seed = 7;
        var measured = new Utf8SliceInvocation(new(100000, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
        for (var index = 0; index < (int)seed; index++) Assert.Equal(Utf8ExecutionLimitKind.None, measured.TryChargeInputStep());
        PythonReMatchAssert.Equal(expected, regex.MaterializeNativeMatchValue(input, start, operationKind, ref measured));
        var required = measured.Budget.Steps - seed;
        Assert.InRange(required, 1UL, 100000UL);
        for (ulong cutoff = 0; cutoff <= required; cutoff++)
        {
            var invocation = new Utf8SliceInvocation(new(seed + cutoff, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            for (var index = 0; index < (int)seed; index++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            var published = false;
            Exception? failure = null;
            try
            {
                PythonReMatchAssert.Equal(expected, regex.MaterializeNativeMatchValue(input, start, operationKind, ref invocation));
                published = true;
            }
            catch (InvalidOperationException error) { failure = error; }
            Assert.Equal(cutoff == required, published);
            if (published) Assert.Null(failure); else Assert.NotNull(failure);
            Assert.True(invocation.Budget.Steps >= seed + cutoff);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        }
    }
}
