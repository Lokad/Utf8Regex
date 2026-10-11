using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeOneShotInvocationTests
{
    [Theory]
    [InlineData(0, "é😀", "xé😀z", 0, 1, 7, 1, 4, "é😀")]
    [InlineData(1, "é😀", "é😀z", 0, 0, 6, 0, 3, "é😀")]
    [InlineData(1, "é😀", "xé😀z", 0, 0, 0, 0, 0, null)]
    [InlineData(0, "", "😀", 0, 0, 0, 0, 0, "")]
    [InlineData(0, "abc", "abc😀", 0, 0, 3, 0, 3, "abc")]
    [InlineData(0, "abc", "😀abc", 0, 4, 7, 2, 5, "abc")]
    [InlineData(1, "ab[0-9]+", "ab123😀", 0, 0, 5, 0, 5, "ab123")]
    [InlineData(1, "ab[0-9]+", "ab😀", 0, 0, 0, 0, 0, null)]
    [InlineData(2, "ab.*xy", "éab😀xy", 2, 2, 10, 1, 7, "ab😀xy")]
    [InlineData(2, "ab.*xy", "éab😀xz", 2, 0, 0, 0, 0, null)]
    [InlineData(2, "ab.*xy", "abxy", 0, 0, 4, 0, 4, "abxy")]
    [InlineData(2, ".*", "", 0, 0, 0, 0, 0, "")]
    public void NativeValueAndTextOutcomesAgreeWithScalarEdges(int kind, string pattern, string subject,
        int start, int first, int last, int firstUtf16, int lastUtf16, string? expected)
    {
        var input = Encoding.UTF8.GetBytes(subject);
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { PythonReCompileOptions.None, PythonReCompileOptions.DotAll })
        foreach (var cancellation in new[] { CancellationToken.None, token.Token })
        {
            var regex = new Utf8PythonRegex(pattern, options);
            var value = kind switch
            {
                0 => regex.Search(input, start, cancellation),
                1 => regex.Match(input, start, cancellation),
                _ => regex.FullMatch(input, start, cancellation),
            };
            var text = kind switch
            {
                0 => regex.SearchToString(input, start, cancellation),
                1 => regex.MatchToString(input, start, cancellation),
                _ => regex.FullMatchToString(input, start, cancellation),
            };
            Assert.Equal(expected, text); Assert.Equal(expected is not null, value.Success);
            Assert.Equal((first, last), (value.StartOffsetInBytes, value.EndOffsetInBytes));
            Assert.Equal((firstUtf16, lastUtf16), (value.StartOffsetInUtf16, value.EndOffsetInUtf16));
            if (expected is not null) Assert.Equal(Encoding.UTF8.GetBytes(expected), value.GetValueBytes().ToArray());
        }
    }

    [Theory]
    [InlineData(0, "abc", "abc")]
    [InlineData(1, "ab[0-9]+", "ab123")]
    [InlineData(2, "ab.*xy", "abxy")]
    public void NativeHitsStillRejectMalformedWholeSubjectTails(int kind, string pattern, string prefix)
    {
        var regex = new Utf8PythonRegex(pattern, kind == 2 ? PythonReCompileOptions.DotAll : PythonReCompileOptions.None);
        var input = Encoding.UTF8.GetBytes(prefix).Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() =>
        {
            if (kind == 0) _ = regex.Search(input);
            else if (kind == 1) _ = regex.Match(input);
            else _ = regex.FullMatch(input);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            if (kind == 0) _ = regex.SearchToString(input);
            else if (kind == 1) _ = regex.MatchToString(input);
            else _ = regex.FullMatchToString(input);
        });
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(3, false)] [InlineData(3, true)]
    public void EveryNativeValueOrTextWorkCutoffIsCumulativeAndUnpublished(int kind, bool text)
    {
        var pattern = kind switch { 0 => "Шерлок", 1 => "ab[0-9]+", 2 => "ab.*xy", _ => "é😀" };
        var input = Encoding.UTF8.GetBytes(kind switch { 0 => "π Шерлок 𝒜", 1 => "ab123😀", 2 => "éab😀xy", _ => "xé😀z" });
        var expected = kind switch { 0 => "Шерлок", 1 => "ab123", 2 => "ab😀xy", _ => "é😀" };
        var regex = new Utf8PythonRegex(pattern, kind == 2 ? PythonReCompileOptions.DotAll : PythonReCompileOptions.None);
        if (kind == 0) Assert.True(regex.DebugUsesZeroOffsetUtf8SearchValueFastPath);
        if (kind == 1) Assert.True(regex.DebugUsesAsciiLiteralPrefixDigitMatchFastPath);
        if (kind == 2) Assert.True(regex.DebugUsesAsciiDotAllFullMatchFastPath);
        // The supplementary sequence has an exact core backend, while Python's
        // narrower public classifier keeps scalar discovery. Retain this case
        // as an internal borrowed-backend witness, separately from public routes.
        if (kind == 3) Assert.Equal("ExactUtf8Literal", regex.DebugUtf8ExecutionKind);
        var start = kind == 2 ? 2 : 0;
        var operationKind = kind == 3 ? 0 : kind;
        const ulong seed = 7;
        var measured = new Utf8SliceInvocation(new(100000, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
        for (var index = 0; index < (int)seed; index++) Assert.Equal(Utf8ExecutionLimitKind.None, measured.TryChargeInputStep());
        Utf8InvocationResultLedger measurement = default;
        if (text) Assert.Equal(expected, regex.MaterializeNativeMatchText(input, start, operationKind, ref measured, ref measurement));
        else Assert.True(regex.MaterializeNativeMatchValue(input, start, operationKind, ref measured).Success);
        var required = measured.Budget.Steps - seed;
        measurement.Release(ref measured);
        Assert.InRange(required, 1UL, 100000UL);
        for (ulong cutoff = 0; cutoff <= required; cutoff++)
        {
            var invocation = new Utf8SliceInvocation(new(seed + cutoff, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            for (var index = 0; index < (int)seed; index++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            Utf8InvocationResultLedger results = default;
            var published = false;
            Exception? failure = null;
            try
            {
                try
                {
                    if (text) Assert.Equal(expected, regex.MaterializeNativeMatchText(input, start, operationKind, ref invocation, ref results));
                    else Assert.True(regex.MaterializeNativeMatchValue(input, start, operationKind, ref invocation).Success);
                    published = true;
                }
                catch (InvalidOperationException error) { failure = error; }
                Assert.Equal(cutoff == required, published);
                if (published) Assert.Null(failure); else Assert.NotNull(failure);
                Assert.Equal(13 + results.ReservedBytes, invocation.Ledger.ReservedBytes);
                if (!text) Assert.Equal(0UL, results.ReservedBytes);
                Assert.True(invocation.Budget.Steps >= seed + cutoff);
            }
            finally { results.Release(ref invocation); }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        }
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void NativeScansObserveTokenAndDeadlineAfterValidation(int lane, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var prefix = Encoding.ASCII.GetBytes(new string('a', 65536));
        var input = lane == 0 ? Encoding.ASCII.GetBytes("a" + new string('1', 65536))
            : lane == 1 ? prefix : Encoding.ASCII.GetBytes("payload:" + new string('a', 65536));
        var invocation = new Utf8SliceInvocation(new(100000, 0, 1UL << 20),
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        _ = Utf8Validation.Validate(input, ref invocation);
        while ((invocation.Budget.Steps & 63) != 0)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        var seed = invocation.Budget.Steps;
        var entered = false;
        invocation.CheckpointHook = step =>
        {
            Assert.True(step > seed); entered = true;
            if (expire) ticks = 50; else token.Cancel();
        };
        var published = false;
        Exception? failure = null;
        try
        {
            if (lane == 0) _ = PythonReAsciiPrefixDigitMatcher.MatchUnchecked(input, "a"u8, ref invocation);
            else _ = PythonReAsciiPrefixDigitMatcher.MatchesPrefix(lane == 1 ? input : input.AsSpan(8), prefix, ref invocation);
            published = true;
        }
        catch (Exception error) { failure = error; }
        Assert.True(entered); Assert.False(published);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        Assert.True(invocation.Budget.Steps > seed);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void NativeProjectionBorrowsTheTokenInsideLongDiscovery(int family, bool text)
    {
        var fragment = new string('a', 65536);
        if (family != 0)
        {
            // Keep the original large-pattern capacity observation. Long scan
            // polling must be tested on a pattern admitted by the unchanged cap.
            var rejected = family == 1 ? fragment + ".*z" : "z.*" + fragment;
            var denied = Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8PythonRegex(rejected, PythonReCompileOptions.DotAll));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, denied.ResourceKind);
            fragment = new string('a', 16384);
        }
        var pattern = family switch { 0 => "a[0-9]+", 1 => fragment + ".*z", _ => "z.*" + fragment };
        var input = Encoding.UTF8.GetBytes(family switch
        {
            0 => "a" + new string('1', 65536), 1 => fragment + "😀z", _ => "z😀" + fragment,
        });
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.DotAll);
        var probe = new Utf8SliceInvocation(new(100000, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref probe);
        var validationSteps = probe.Budget.Steps;
        using var token = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(100000, 0, 1UL << 20), Regex.InfiniteMatchTimeout, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var entered = false;
        invocation.CheckpointHook = steps =>
        {
            if (steps > validationSteps + 4) { entered = true; token.Cancel(); }
        };
        Utf8InvocationResultLedger results = default;
        var published = false;
        Exception? failure = null;
        try
        {
            try
            {
                if (text) _ = regex.MaterializeNativeMatchText(input, 0, family == 0 ? 1 : 2, ref invocation, ref results);
                else _ = regex.MaterializeNativeMatchValue(input, 0, family == 0 ? 1 : 2, ref invocation);
                published = true;
            }
            catch (Exception error) { failure = error; }
            Assert.True(entered); Assert.False(published);
            Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(0UL, results.ReservedBytes); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        finally { results.Release(ref invocation); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
    }
}
