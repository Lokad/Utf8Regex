using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteLiteralFamilyBulkTests
{
    private static Utf8ResourcePolicy Generous => new()
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 128,
        MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
    };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void FiniteFamiliesKeepScalarFloorsPriorityAndRequiredValues(int operation)
    {
        foreach (var (pattern, text, start, expected) in new[]
        {
            ("cat|horse", "π😀cat horse cat", 6, "cat|horse|cat"),
            ("a|aa", "π😀aaaa", 6, "a|a|a|a"),
            ("aa|a", "π😀aaaa", 6, "aa|aa"),
            ("é|😀", "πé😀é", 2, "é|😀|é"),
            ("😀|😀x", "π😀x😀x", 2, "😀|😀"),
            ("cat|horse", "π😀nothing", 6, ""),
        })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, Generous);
            var input = Encoding.UTF8.GetBytes(text);
            using var cancellation = new CancellationTokenSource();
            Assert.Equal(expected, Run(regex, input, start, operation, cancellation.Token));
            Assert.Equal(expected, Run(regex, input, start, operation));
            Assert.Throws<ArgumentException>(() => Run(regex, input.Concat(new byte[] { 0xFF }).ToArray(), start, operation));
            Assert.Throws<ArgumentOutOfRangeException>(() => Run(regex, input, 1, operation));
        }
        var ranges = new Utf8PythonRegex("é|😀", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, Generous)
            .FindAll("πé😀é"u8, 2);
        Assert.Equal(new[] { (2, 4, 1, 2), (4, 8, 2, 4), (8, 10, 4, 5) }, ranges.Select(x =>
            (x.StartOffsetInBytes, x.EndOffsetInBytes, x.StartOffsetInUtf16, x.EndOffsetInUtf16)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void NativeFamilyAdmitsOnlyItsActualWorkingAndDepthRequirements(int operation)
    {
        var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 10_000, MaxDepth = 1, MaxWorkingBytes = 1024, MaxResultBytes = 4096 });
        var input = Encoding.UTF8.GetBytes("π😀" + new string('x', 4096) + " cat horse cat");
        Assert.Equal("cat|horse|cat", Run(regex, input, 6, operation));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EveryCoveredPublicCutoffHasItsExactFailureKindAndFreshAllowance(int operation)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat horse ", 16)));
        var expected = string.Join("|", Enumerable.Range(0, 16).SelectMany(_ => new[] { "cat", "horse" }));
        foreach (var kind in new[] { Utf8ResourceLimitKind.ExecutionWorkSteps,
            Utf8ResourceLimitKind.ExecutionWorkingBytes, Utf8ResourceLimitKind.ExecutionResultBytes })
        {
            Utf8PythonRegex Create(ulong cap) => new("cat|horse", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
                kind switch
                {
                    Utf8ResourceLimitKind.ExecutionWorkSteps => new() { MaxWorkSteps = cap },
                    Utf8ResourceLimitKind.ExecutionWorkingBytes => new() { MaxWorkingBytes = cap },
                    _ => new() { MaxResultBytes = cap },
                });
            void Denied(ulong cap)
            {
                var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(Create(cap), input, 0, operation));
                Assert.Equal(kind, error.ResourceKind); Assert.Equal(cap, error.Limit); Assert.True(error.Requested > cap);
            }
            Denied(1);
            ulong low = 1, high = 1_000_000;
            while (high - low > 1)
            {
                var middle = low + (high - low) / 2;
                try { Assert.Equal(expected, Run(Create(middle), input, 0, operation)); high = middle; }
                catch (Utf8ResourceLimitExceededException error)
                { Assert.Equal(kind, error.ResourceKind); Assert.Equal(middle, error.Limit); low = middle; }
            }
            var admitted = Create(high);
            var retained = Run(admitted, input, 0, operation);
            Assert.Equal(expected, retained); Denied(high - 1);
            Assert.Equal(retained, Run(admitted, input, 0, operation));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CombinedLimitsCannotBypassCollectionOrFinalOutputAdmission(int operation)
    {
        foreach (var kind in new[] { Utf8ResourceLimitKind.ExecutionWorkSteps,
            Utf8ResourceLimitKind.ExecutionWorkingBytes, Utf8ResourceLimitKind.ExecutionResultBytes })
        {
            var policy = Generous;
            policy = policy with
            {
                MaxWorkSteps = kind == Utf8ResourceLimitKind.ExecutionWorkSteps ? 1UL : policy.MaxWorkSteps,
                MaxWorkingBytes = kind == Utf8ResourceLimitKind.ExecutionWorkingBytes ? 1UL : policy.MaxWorkingBytes,
                MaxResultBytes = kind == Utf8ResourceLimitKind.ExecutionResultBytes ? 1UL : policy.MaxResultBytes,
            };
            var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            Assert.Equal(kind, Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(regex, "cat horse"u8.ToArray(), 0, operation)).ResourceKind);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ByteBoundedEarlyTimeoutUsesCallerLimitsAndOriginalToken(int operation)
    {
        foreach (var policy in new[] { new Utf8ResourcePolicy { MaxWorkingBytes = 1 << 20 },
            new Utf8ResourcePolicy { MaxResultBytes = 1 << 20 }, Generous })
        {
            var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1), policy);
            var input = new byte[] { 0xFF, 99, 97, 116 };
            long reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000);
            var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, input, 1, operation));
            Assert.Empty(error.Input); Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                Run(regex, input, 1, operation, cancellation.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void NativeCollectionAndShapingShareMonotonicWorkAndOriginalInterruption(int operation, bool expire)
    {
        var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20), Generous);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat horse ", 512)));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var retained = Run(regex, "cat horse"u8.ToArray(), 0, operation);
        ulong previous = 0;
        var monotonic = Utf8SearchDiagnosticsSession.Start(step => { Assert.True(step >= previous); previous = step; });
        try { Assert.Equal(1024, regex.FindAllToUtf8(input).Count); }
        finally { monotonic.Complete(); }
        Assert.True(previous >= 128);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        var interruption = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        try
        {
            if (expire) Assert.Empty(Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, input, 0, operation)).Input);
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                Run(regex, input, 0, operation, cancellation.Token)).CancellationToken);
        }
        finally { interruption.Complete(); }
        Assert.True(entered); ticks = 0;
        Assert.Equal(retained, Run(regex, "cat horse"u8.ToArray(), 0, operation));
    }

    private static string Run(Utf8PythonRegex regex, byte[] input, int start, int operation, CancellationToken token = default)
        => operation switch
        {
            0 => string.Join("|", regex.FindAll(input, start, token).Select(x => x.ValueText)),
            1 => string.Join("|", regex.FindAllToStrings(input, start, token).ScalarValues),
            _ => string.Join("|", regex.FindAllToUtf8(input, start, token).ScalarValues.Select(Encoding.UTF8.GetString)),
        };
}
