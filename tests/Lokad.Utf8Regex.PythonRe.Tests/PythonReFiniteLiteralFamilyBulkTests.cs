using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteLiteralFamilyBulkTests
{
    private static Utf8ResourcePolicy Generous => new()
    {
        MaxWorkSteps = 1_000_000,
        MaxDepth = 128,
        MaxWorkingBytes = 1 << 20,
        MaxResultBytes = 1 << 20
    };

    [Fact]
    public void FiniteFamiliesKeepScalarFloorsPriorityAndRequiredValues()
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
            var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Generous
            });
            var input = Encoding.UTF8.GetBytes(text);
            using var cancellation = new CancellationTokenSource();
            Assert.Equal(expected, string.Join("|", (regex).FindAllToUtf8(input, start, cancellation.Token).ScalarValues.Select(Encoding.UTF8.GetString)));
            Assert.Equal(expected, string.Join("|", (regex).FindAllToUtf8(input, start, default).ScalarValues.Select(Encoding.UTF8.GetString)));

            Assert.Throws<ArgumentOutOfRangeException>(() => string.Join("|", (regex).FindAllToUtf8(input, 1, default).ScalarValues.Select(Encoding.UTF8.GetString)));
        }
        var wholeMatches10 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor10 = new Utf8PythonRegex("é|😀", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Generous
        }).CreateMatchCursor(("πé😀é"u8).ToArray(), 2, default))
        {
            while (rangeCursor10.MoveNext(out var rangeMatch10))
            {
                wholeMatches10.Add(rangeMatch10);
            }
        }
        var ranges = wholeMatches10.ToArray();
    }

    [Fact]
    public void NativeFamilyAdmitsOnlyItsActualWorkingAndDepthRequirements()
    {
        var regex = new Utf8PythonRegex("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 10_000,
                MaxDepth = 1,
                MaxWorkingBytes = 1024,
                MaxResultBytes = 4096
            }
        });
        var input = Encoding.UTF8.GetBytes("π😀" + new string('x', 4096) + " cat horse cat");
        Assert.Equal("cat|horse|cat", string.Join("|", (regex).FindAllToUtf8(input, 6, default).ScalarValues.Select(Encoding.UTF8.GetString)));
    }

    [Fact]
    public void EveryCoveredPublicCutoffHasItsExactFailureKindAndFreshAllowance()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat horse ", 16)));
        var expected = string.Join("|", Enumerable.Range(0, 16).SelectMany(_ => new[] { "cat", "horse" }));
        foreach (var kind in new[] { Utf8ResourceLimitKind.ExecutionWorkSteps,
            Utf8ResourceLimitKind.ExecutionWorkingBytes, Utf8ResourceLimitKind.ExecutionResultBytes })
        {
            Utf8PythonRegex Create(ulong cap) => new("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = kind switch
                {
                    Utf8ResourceLimitKind.ExecutionWorkSteps => new()
                    {
                        MaxWorkSteps = cap
                    },
                    Utf8ResourceLimitKind.ExecutionWorkingBytes => new()
                    {
                        MaxWorkingBytes = cap
                    },
                    _ => new()
                    {
                        MaxResultBytes = cap
                    },
                }
            });
            void Denied(ulong cap)
            {
                var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => string.Join("|", (Create(cap)).FindAllToUtf8(input, 0, default).ScalarValues.Select(Encoding.UTF8.GetString)));
                Assert.Equal(kind, error.ResourceKind); Assert.Equal(cap, error.Limit); Assert.True(error.Requested > cap);
            }
            Denied(1);
            ulong low = 1, high = 1_000_000;
            while (high - low > 1)
            {
                var middle = low + (high - low) / 2;
                try { Assert.Equal(expected, string.Join("|", (Create(middle)).FindAllToUtf8(input, 0, default).ScalarValues.Select(Encoding.UTF8.GetString))); high = middle; }
                catch (Utf8ResourceLimitExceededException error)
                { Assert.Equal(kind, error.ResourceKind); Assert.Equal(middle, error.Limit); low = middle; }
            }
            var admitted = Create(high);
            var retained = string.Join("|", (admitted).FindAllToUtf8(input, 0, default).ScalarValues.Select(Encoding.UTF8.GetString));
            Assert.Equal(expected, retained); Denied(high - 1);
            Assert.Equal(retained, string.Join("|", (admitted).FindAllToUtf8(input, 0, default).ScalarValues.Select(Encoding.UTF8.GetString)));
        }
    }

    [Fact]
    public void CombinedLimitsCannotBypassCollectionOrFinalOutputAdmission()
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
            var regex = new Utf8PythonRegex("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy
            });
            Assert.Equal(kind, Assert.Throws<Utf8ResourceLimitExceededException>(() => string.Join("|", (regex).FindAllToUtf8("cat horse"u8.ToArray(), 0, default).ScalarValues.Select(Encoding.UTF8.GetString))).ResourceKind);
        }
    }

    [Fact]
    public void ByteBoundedEarlyTimeoutUsesCallerLimitsAndOriginalToken()
    {
        foreach (var policy in new[] { new Utf8ResourcePolicy { MaxWorkingBytes = 1 << 20 },
            new Utf8ResourcePolicy { MaxResultBytes = 1 << 20 }, Generous })
        {
            var regex = new Utf8PythonRegex("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = TimeSpan.FromMilliseconds(1),
                ResourcePolicy = policy
            });
            var input = new byte[] { 0xFF, 99, 97, 116 };
            long reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000);
            var error = Assert.Throws<RegexMatchTimeoutException>(() => string.Join("|", (regex).FindAllToUtf8(input, 1, default).ScalarValues.Select(Encoding.UTF8.GetString)));
            Assert.Empty(error.Input); Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                string.Join("|", (regex).FindAllToUtf8(input, 1, cancellation.Token).ScalarValues.Select(Encoding.UTF8.GetString))).CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCollectionAndShapingShareMonotonicWorkAndOriginalInterruption(bool expire)
    {
        var regex = new Utf8PythonRegex("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Generous
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat horse ", 512)));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var retained = string.Join("|", (regex).FindAllToUtf8("cat horse"u8.ToArray(), 0, default).ScalarValues.Select(Encoding.UTF8.GetString));
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
            if (expire) Assert.Empty(Assert.Throws<RegexMatchTimeoutException>(() => string.Join("|", (regex).FindAllToUtf8(input, 0, default).ScalarValues.Select(Encoding.UTF8.GetString))).Input);
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                string.Join("|", (regex).FindAllToUtf8(input, 0, cancellation.Token).ScalarValues.Select(Encoding.UTF8.GetString))).CancellationToken);
        }
        finally { interruption.Complete(); }
        Assert.True(entered); ticks = 0;
        Assert.Equal(retained, string.Join("|", (regex).FindAllToUtf8("cat horse"u8.ToArray(), 0, default).ScalarValues.Select(Encoding.UTF8.GetString)));
    }
}
