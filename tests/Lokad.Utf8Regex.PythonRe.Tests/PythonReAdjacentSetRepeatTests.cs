using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReAdjacentSetRepeatTests
{
    private static Utf8ResourcePolicy Generous => new() { MaxDepth = 1024, MaxWorkingBytes = 1 << 24, MaxResultBytes = 1 << 24 };

    [Theory]
    [InlineData(@"item \s+ [0-9]+", "item   12345", PythonReCompileOptions.Verbose)]
    [InlineData(@"item(\s+)[0-9]+", "item\u2003\u3000 12345", PythonReCompileOptions.None)]
    [InlineData(@"item[ 0-9]+[0-9]+", "item   12345", PythonReCompileOptions.None)]
    [InlineData(@"item[a-z]+[0-9]*[a-z]+", "itemabc", PythonReCompileOptions.None)]
    [InlineData(@"item[a-z]+?[0-9]+", "itemabc12", PythonReCompileOptions.None)]
    [InlineData(@"(?i:item[a-z]+)[K0-9]+", "itemabkK12", PythonReCompileOptions.None)]
    [InlineData(@"item[a-z]+[é0-9]+", "itemabcé12", PythonReCompileOptions.None)]
    [InlineData(@"item(😀{1,2})[0-9]+", "item😀😀123", PythonReCompileOptions.None)]
    public void PublicFullMatchesKeepScalarRangesAndDeclinedShapes(string pattern, string text, PythonReCompileOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Generous })
        {
            var owner = new Utf8PythonRegex(pattern, options: options, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = policy
            });
            var value = owner.Match(input, mode: Utf8PythonMatchMode.FullMatch);
            Assert.True(value.Success);
            Assert.Equal((0, input.Length), (value.StartOffsetInBytes, value.EndOffsetInBytes));
            PythonReMatchAssert.Equal(text, owner.Match(input, mode: Utf8PythonMatchMode.FullMatch));
            Utf8PythonRangeMatch retained = default;
            using (var captureCursor1 = owner.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor1.MoveNext(out retained);
            }
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
            Assert.Equal(1, owner.Count(input));
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(value.GetValueBytes()));

        }
    }

    [Fact]
    public void FundedCapturesOutliveProgramClosureAndReturnTemporaryLoans()
    {
        var allowance = new Allowance(16 << 20);
        var owner = Utf8PythonRegex.Compile(@"item(?P<gap>\s+)[0-9]+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = allowance
        }, cancellationToken: default);
        var held = allowance.Used;
        var input = "item\u2003 12345"u8.ToArray();
        Utf8PythonRangeMatch retained = default;
        using (var captureCursor2 = owner.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor2.MoveNext(out retained);
        }
        Assert.True(retained.Success);
        Assert.Equal("\u2003 ", System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained.GetGroupMemory(retained.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(1, retained.LastIndex);
        Assert.Equal(held, allowance.Used);
        Assert.Equal(1, owner.Count(input));
        Assert.Equal(held, allowance.Used);
        owner.Dispose();
        Assert.Equal(0UL, allowance.Used);
        Assert.Equal("\u2003 ", System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained.GetGroupMemory(retained.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void WorkWorkingAndResultAdmissionRemainUseful()
    {
        const string pattern = @"item\s+[0-9]+";
        var working = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.True(working.Match("item   12345"u8, mode: Utf8PythonMatchMode.Search).Success);
        var work = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 4096
            }
        });
        var results = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxResultBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                ReadOnlyMemory<byte> snapshots1Input = ("item   12345"u8).ToArray();
                var snapshots1 = new List<Utf8PythonRangeMatch>();
                using (var snapshots1Cursor = results.CreateMatchCursor(snapshots1Input, 0, default))
                {
                    while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                        snapshots1.Add(snapshots1Match);
                }
                return snapshots1;
            }).ResourceKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheDisjointClassRunKeepsTheOriginalTokenAndDeadline(bool expire)
    {
        const string pattern = @"item\s+[0-9]+";
        var input = Encoding.UTF8.GetBytes("item" + new string(' ', 8192) + "12345");
        const ulong cutoff = 128;
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var owner = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Generous
        });
        var interrupted = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= cutoff)
            {
                return;
            }
            interrupted = true;
            if (expire)
            {
                ticks = 50;
            }
            else
            {
                token.Cancel();
            }
        });
        try
        {
            if (expire)
            {
                Assert.Equal(pattern, Assert.Throws<RegexMatchTimeoutException>(() => owner.Match(input, mode: Utf8PythonMatchMode.Search)).Pattern);
            }
            else
            {
                Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => owner.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search)).CancellationToken);
            }
        }
        finally
        {
            session.Complete();
        }
        Assert.True(interrupted);
        ticks = 0;
        Assert.True(owner.Match(input, mode: Utf8PythonMatchMode.Search).Success);
    }
}
