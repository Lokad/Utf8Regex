using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReAtomicRepeatCommitTests
{
    [Theory]
    [InlineData("a++b", "xxaaaabzz", "aaaab")]
    [InlineData("(?>ab+)c", "xxabbbczz", "abbbc")]
    [InlineData("(?>ab+)c", "xxabbbdzz", null)]
    [InlineData("(?>(a+))b", "xxaaaabzz", "aaaab")]
    [InlineData("(?>x(?>a+))b", "xxaaaabzz", "xaaaab")]
    [InlineData("a{2,3}+a", "aaaa", "aaaa")]
    [InlineData("a{2,3}+b", "ab aaab", "aaab")]
    [InlineData("a*+b", "b", "b")]
    [InlineData("a*+", "b", "")]
    [InlineData("(?>[^x]*)x", "é😀xZ", "é😀x")]
    [InlineData("😀++X", "é😀😀X", "😀😀X")]
    [InlineData("(?>.+)X", "😀X", null)]
    [InlineData("(?>.+)X|😀", "😀X", "😀")]
    [InlineData("(?>a+?)b", "xxaaaabzz", "ab")]
    [InlineData("(?>a+?)a", "aaaa", "aa")]
    [InlineData("(a)++b", "aaab", "aaab")]
    public void SearchRetainsAtomicScalarEndpoints(string pattern, string text, string? expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        var match = regex.Match(input, mode: Utf8PythonMatchMode.Search);
        Assert.Equal(expected is not null, match.Success);
        if (expected is null)
        {
            return;
        }
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(expected, Encoding.UTF8.GetString(match.GetValueBytes()));
        _ = regex.Count(input);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Fact]
    public void ValidTextAndMeaningfulLimitsRemain()
    {
        foreach (var (pattern, input, policy, expected) in new[]
        {
            ("a{1,2}+a", "aaa", new Utf8ResourcePolicy { MaxWorkingBytes = 1 }, Utf8ResourceLimitKind.ExecutionWorkingBytes),
        })
        {
            using var denied = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = policy
            });
            var bytes = Encoding.UTF8.GetBytes(input);
            Assert.Equal(expected, Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Match(bytes, mode: Utf8PythonMatchMode.Search)).ResourceKind);
        }
        var results = new Utf8PythonRegex("a++b", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxResultBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                ReadOnlyMemory<byte> snapshots1Input = ("aaab aab"u8).ToArray();
                var snapshots1 = new List<Utf8PythonRangeMatch>();
                using (var snapshots1Cursor = results.CreateMatchCursor(snapshots1Input, 0, default))
                {
                    while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                        snapshots1.Add(snapshots1Match);
                }
                return snapshots1;
            }).ResourceKind);
        var depth = new Utf8PythonRegex("(?>x(?>a+))b", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => depth.Match("xaaab"u8, mode: Utf8PythonMatchMode.Search)).ResourceKind);
    }

    [Fact]
    public void FundedInvocationReleasesLoansAndRetainedCapturesSurviveDispose()
    {
        var allowance = new Allowance(16 << 20);
        var regex = Utf8PythonRegex.Compile("(?>(a+))b", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = allowance
        }, cancellationToken: default);
        var held = allowance.Used;
        Utf8PythonRangeMatch retained = default;
        using (var captureCursor1 = regex.CreateMatchCursor(("xxaaaabzz"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out retained);
        }
        Assert.True(retained.Success);
        Assert.Equal("aaaa", System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained.GetGroupMemory(retained.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(held, allowance.Used);
        Assert.False(regex.Match("aaaabX"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(held, allowance.Used);
        regex.Dispose();
        Assert.Equal(0UL, allowance.Used);
        Assert.Equal("aaaa", System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained.GetGroupMemory(retained.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionDuringGreedyRunDoesNotPoisonReuse(bool expire)
    {
        var input = new byte[(1 << 19) + 1];
        input.AsSpan(0, input.Length - 1).Fill((byte)'a');
        input[^1] = (byte)'b';
        const ulong cutoff = 128;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("a++b", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 8,
                MaxWorkingBytes = 1 << 20
            }
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= cutoff)
            {
                return;
            }
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
                Assert.Equal("a++b", Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, mode: Utf8PythonMatchMode.Search)).Pattern);
            }
            else
            {
                Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                                regex.Match(input, 0, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.Search)).CancellationToken);
            }
        }
        finally
        {
            session.Complete();
        }
        Assert.True(entered);
        ticks = 0;
        Assert.True(regex.Match("ab"u8, mode: Utf8PythonMatchMode.Search).Success);
    }
}
