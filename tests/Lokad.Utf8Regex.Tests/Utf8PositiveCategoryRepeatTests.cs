using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PositiveCategoryRepeatTests
{
    [Theory]
    [InlineData(@"([a\p{L}]+)@")]
    [InlineData(@"([a\p{Nd}]{2,4})@")]
    [InlineData(@"([a\p{L}]{3})@")]
    [InlineData(@"([a\P{L}]+)@")]
    [InlineData(@"([^a\p{L}]+)@")]
    [InlineData(@"((?i:[a\p{L}])+)-")]
    [InlineData(@"([a\p{L}]+?)@")]
    [InlineData(@"([a-z]+)@")]
    public void PartialMasksKeepTableMembersComplementsFoldingAndBounds(string pattern)
    {
        const string text = "aaAZé@ 12a٣@ a1@ aa@ aaaa@ 12345@ .-@ Azé-";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
                     RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var oracle = new Regex(pattern, options | RegexOptions.CultureInvariant);
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            var expected = oracle.Match(text);
            var actual = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Value, actual.GetValueString());
                Assert.Equal(expected.Groups[1].Value, actual.GetGroup(1).GetValueString());
            }
        }
    }

    [Fact]
    public void EveryAsciiValueCanFallThroughAPartialMask()
    {
        const string pattern = @"([a\p{L}\p{Nd}_]{2})!";
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        for (var value = 0; value < 128; value++)
        {
            var text = "a" + (char)value + "!";
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact]
    public void SupplementaryScalarsSlicesAndRetainedCapturesRemainValid()
    {
        var regex = new Utf8Regex(@"(?<run>[a\p{L}😀]{2,3})@");
        var input = "a😀@ éa@"u8.ToArray();
        byte[] storage = [0xff, .. input, 0xff];
        Assert.Equal(2, regex.Count(storage.AsSpan(1, input.Length)));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var first));
        Assert.True(cursor.MoveNext(out var second));
        cursor.Dispose();
        Assert.Equal("a😀@", Encoding.UTF8.GetString(first.GetValueMemory().Span));
        Assert.Equal("a😀", Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromName("run")).Span));
        Assert.Equal("éa@", Encoding.UTF8.GetString(second.GetValueMemory().Span));
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Fact]
    public void EveryPositiveWorkCutoffKeepsTheParentLoan()
    {
        var regex = new Utf8Regex(@"([a\p{L}]{2,4})@");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = "aaé@ Aaa@"u8.ToArray();
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, 16, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline);
        Assert.Equal((2, Utf8ExecutionLimitKind.None), (result.Count, result.Breach));
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong limit = 1; limit <= baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 16, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var actual = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            Assert.Equal(limit == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps,
                actual.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalControlsInterruptTheLiveRepeat(bool timeout)
    {
        var regex = new Utf8Regex(@"([a\p{L}]+)@");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('a', 200000) + "@");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (!new System.Diagnostics.StackTrace().GetFrames().Any(frame => frame.GetMethod()?.Name == "RunClassRepeat")) return;
            entered = true;
            if (timeout) ticks = 25; else token.Cancel();
        });
        var invocation = new Utf8SliceInvocation(new(1000000, 16, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var failure = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0,
                new int[program.GroupCount], new int[program.GroupCount], ref invocation));
            Assert.True(entered);
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }
}
