using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteLiteralFamilyCursorTests
{
    private static Utf8ResourcePolicy Generous => new()
    {
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
            ReadOnlyMemory<byte> snapshots1Input = input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, start, cancellation.Token))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            Assert.Equal(expected, string.Join("|", snapshots1.Select(captureSnapshot1 => captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString)));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlyMemory<byte> snapshots3Input = input;
                var snapshots3 = new List<Utf8PythonRangeMatch>();
                using (var snapshots3Cursor = regex.CreateMatchCursor(snapshots3Input, 1, default))
                {
                    while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                        snapshots3.Add(snapshots3Match);
                }
                return string.Join("|", snapshots3.Select(captureSnapshot3 => captureSnapshot3.GetGroupMemory(captureSnapshot3.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
            });
        }

    }

    [Fact]
    public void NativeFamilyAdmitsOnlyItsActualWorkingAndDepthRequirements()
    {
        var regex = new Utf8PythonRegex("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 1,
                MaxWorkingBytes = 1024,
                MaxResultBytes = 4096
            }
        });
        var input = Encoding.UTF8.GetBytes("π😀" + new string('x', 4096) + " cat horse cat");
        ReadOnlyMemory<byte> snapshots4Input = input;
        var snapshots4 = new List<Utf8PythonRangeMatch>();
        using (var snapshots4Cursor = regex.CreateMatchCursor(snapshots4Input, 6, default))
        {
            while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                snapshots4.Add(snapshots4Match);
        }
        Assert.Equal("cat|horse|cat", string.Join("|", snapshots4.Select(captureSnapshot4 => captureSnapshot4.GetGroupMemory(captureSnapshot4.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString)));
    }

    [Fact]
    public void EveryCoveredPublicCutoffHasItsExactFailureKindAndFreshAllowance()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat horse ", 16)));
        var expected = string.Join("|", Enumerable.Range(0, 16).SelectMany(_ => new[] { "cat", "horse" }));
        foreach (var kind in new[] {             Utf8ResourceLimitKind.ExecutionWorkingBytes, Utf8ResourceLimitKind.ExecutionResultBytes })
        {
            Utf8PythonRegex Create(ulong cap) => new("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = kind switch
                {
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
                var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                {
                    var snapshots5Regex = (Create(cap));
                    ReadOnlyMemory<byte> snapshots5Input = input;
                    var snapshots5 = new List<Utf8PythonRangeMatch>();
                    using (var snapshots5Cursor = snapshots5Regex.CreateMatchCursor(snapshots5Input, 0, default))
                    {
                        while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                            snapshots5.Add(snapshots5Match);
                    }
                    return string.Join("|", snapshots5.Select(captureSnapshot5 => captureSnapshot5.GetGroupMemory(captureSnapshot5.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
                });
                Assert.Equal(kind, error.ResourceKind);
                Assert.Equal(cap, error.Limit);
                Assert.True(error.Requested > cap);
            }
            Denied(1);
            ulong low = 1, high = 1_000_000;
            while (high - low > 1)
            {
                var middle = low + (high - low) / 2;
                try
                {
                    var snapshots6Regex = (Create(middle));
                    ReadOnlyMemory<byte> snapshots6Input = input;
                    var snapshots6 = new List<Utf8PythonRangeMatch>();
                    using (var snapshots6Cursor = snapshots6Regex.CreateMatchCursor(snapshots6Input, 0, default))
                    {
                        while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                            snapshots6.Add(snapshots6Match);
                    }
                    Assert.Equal(expected, string.Join("|", snapshots6.Select(captureSnapshot6 => captureSnapshot6.GetGroupMemory(captureSnapshot6.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString)));
                    high = middle;
                }
                catch (Utf8ResourceLimitExceededException error)
                {
                    Assert.Equal(kind, error.ResourceKind);
                    Assert.Equal(middle, error.Limit);
                    low = middle;
                }
            }
            var admitted = Create(high);
            ReadOnlyMemory<byte> snapshots7Input = input;
            var snapshots7 = new List<Utf8PythonRangeMatch>();
            using (var snapshots7Cursor = admitted.CreateMatchCursor(snapshots7Input, 0, default))
            {
                while (snapshots7Cursor.MoveNext(out var snapshots7Match))
                    snapshots7.Add(snapshots7Match);
            }
            var retained = string.Join("|", snapshots7.Select(captureSnapshot7 => captureSnapshot7.GetGroupMemory(captureSnapshot7.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
            Assert.Equal(expected, retained);
            Denied(high - 1);
            ReadOnlyMemory<byte> snapshots8Input = input;
            var snapshots8 = new List<Utf8PythonRangeMatch>();
            using (var snapshots8Cursor = admitted.CreateMatchCursor(snapshots8Input, 0, default))
            {
                while (snapshots8Cursor.MoveNext(out var snapshots8Match))
                    snapshots8.Add(snapshots8Match);
            }
            Assert.Equal(retained, string.Join("|", snapshots8.Select(captureSnapshot8 => captureSnapshot8.GetGroupMemory(captureSnapshot8.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString)));
        }
    }

    [Fact]
    public void CombinedLimitsCannotBypassCursorSnapshotAdmission()
    {
        foreach (var kind in new[] {             Utf8ResourceLimitKind.ExecutionWorkingBytes, Utf8ResourceLimitKind.ExecutionResultBytes })
        {
            var policy = Generous;
            policy = policy with
            {
                MaxWorkingBytes = kind == Utf8ResourceLimitKind.ExecutionWorkingBytes ? 1UL : policy.MaxWorkingBytes,
                MaxResultBytes = kind == Utf8ResourceLimitKind.ExecutionResultBytes ? 1UL : policy.MaxResultBytes,
            };
            var regex = new Utf8PythonRegex("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy
            });
            Assert.Equal(kind, Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                ReadOnlyMemory<byte> snapshots9Input = "cat horse"u8.ToArray();
                var snapshots9 = new List<Utf8PythonRangeMatch>();
                using (var snapshots9Cursor = regex.CreateMatchCursor(snapshots9Input, 0, default))
                {
                    while (snapshots9Cursor.MoveNext(out var snapshots9Match))
                        snapshots9.Add(snapshots9Match);
                }
                return string.Join("|", snapshots9.Select(captureSnapshot9 => captureSnapshot9.GetGroupMemory(captureSnapshot9.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
            }).ResourceKind);
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
            var error = Assert.Throws<RegexMatchTimeoutException>(() =>
            {
                ReadOnlyMemory<byte> snapshots10Input = input;
                var snapshots10 = new List<Utf8PythonRangeMatch>();
                using (var snapshots10Cursor = regex.CreateMatchCursor(snapshots10Input, 1, default))
                {
                    while (snapshots10Cursor.MoveNext(out var snapshots10Match))
                        snapshots10.Add(snapshots10Match);
                }
                return string.Join("|", snapshots10.Select(captureSnapshot10 => captureSnapshot10.GetGroupMemory(captureSnapshot10.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
            });
            Assert.Empty(error.Input);
            Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
{
    ReadOnlyMemory<byte> snapshots11Input = input;
    var snapshots11 = new List<Utf8PythonRangeMatch>();
    using (var snapshots11Cursor = regex.CreateMatchCursor(snapshots11Input, 1, cancellation.Token))
    {
        while (snapshots11Cursor.MoveNext(out var snapshots11Match))
            snapshots11.Add(snapshots11Match);
    }
    return string.Join("|", snapshots11.Select(captureSnapshot11 => captureSnapshot11.GetGroupMemory(captureSnapshot11.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
}).CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CursorSharesMonotonicWorkAndOriginalInterruption(bool expire)
    {
        var regex = new Utf8PythonRegex("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Generous
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat horse ", 512)));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        ReadOnlyMemory<byte> snapshots12Input = "cat horse"u8.ToArray();
        var snapshots12 = new List<Utf8PythonRangeMatch>();
        using (var snapshots12Cursor = regex.CreateMatchCursor(snapshots12Input, 0, default))
        {
            while (snapshots12Cursor.MoveNext(out var snapshots12Match))
                snapshots12.Add(snapshots12Match);
        }
        var retained = string.Join("|", snapshots12.Select(captureSnapshot12 => captureSnapshot12.GetGroupMemory(captureSnapshot12.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
        ulong previous = 0;
        var monotonic = Utf8SearchDiagnosticsSession.Start(step =>
        {
            Assert.True(step >= previous);
            previous = step;
        });
        try
        {
            ReadOnlyMemory<byte> snapshots13Input = input;
            var snapshots13 = new List<Utf8PythonRangeMatch>();
            using (var snapshots13Cursor = regex.CreateMatchCursor(snapshots13Input, 0, default))
            {
                while (snapshots13Cursor.MoveNext(out var snapshots13Match))
                    snapshots13.Add(snapshots13Match);
            }
            Assert.Equal(1024, snapshots13.Count);
        }
        finally
        {
            monotonic.Complete();
        }
        Assert.True(previous >= 128);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        var interruption = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            entered = true;
            if (expire)
            {
                ticks = 50;
            }
            else
            {
                cancellation.Cancel();
            }
        });
        try
        {
            if (expire)
            {
                Assert.Empty(Assert.Throws<RegexMatchTimeoutException>(() =>
                {
                    ReadOnlyMemory<byte> snapshots14Input = input;
                    var snapshots14 = new List<Utf8PythonRangeMatch>();
                    using (var snapshots14Cursor = regex.CreateMatchCursor(snapshots14Input, 0, default))
                    {
                        while (snapshots14Cursor.MoveNext(out var snapshots14Match))
                            snapshots14.Add(snapshots14Match);
                    }
                    return string.Join("|", snapshots14.Select(captureSnapshot13 => captureSnapshot13.GetGroupMemory(captureSnapshot13.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
                }).Input);
            }
            else
            {
                Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            {
                ReadOnlyMemory<byte> snapshots15Input = input;
                var snapshots15 = new List<Utf8PythonRangeMatch>();
                using (var snapshots15Cursor = regex.CreateMatchCursor(snapshots15Input, 0, cancellation.Token))
                {
                    while (snapshots15Cursor.MoveNext(out var snapshots15Match))
                        snapshots15.Add(snapshots15Match);
                }
                return string.Join("|", snapshots15.Select(captureSnapshot14 => captureSnapshot14.GetGroupMemory(captureSnapshot14.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
            }).CancellationToken);
            }
        }
        finally
        {
            interruption.Complete();
        }
        Assert.True(entered);
        ticks = 0;
        ReadOnlyMemory<byte> snapshots16Input = "cat horse"u8.ToArray();
        var snapshots16 = new List<Utf8PythonRangeMatch>();
        using (var snapshots16Cursor = regex.CreateMatchCursor(snapshots16Input, 0, default))
        {
            while (snapshots16Cursor.MoveNext(out var snapshots16Match))
                snapshots16.Add(snapshots16Match);
        }
        Assert.Equal(retained, string.Join("|", snapshots16.Select(captureSnapshot15 => captureSnapshot15.GetGroupMemory(captureSnapshot15.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString)));
    }
}
