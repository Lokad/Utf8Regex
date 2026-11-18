using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFamilyByteUnionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2042)]
    [InlineData(2043)]
    [InlineData(2044)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(4091)]
    [InlineData(4096)]
    public void CommonPrefixWindowsKeepCapturesScalarOffsetsAndTails(int padding)
    {
        var prefix = "😀" + new string('i', padding);
        var text = prefix + "item-x item-12 item-345";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, 4, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var matches = collectedRanges1.ToArray();
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 4, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(new[] { "12", "345" }, snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(2, matches.Length);
        Assert.Equal(Encoding.UTF8.GetByteCount(prefix) + 7, matches[0].Value.StartOffsetInBytes);
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(matches[0].GetGroupMemory(1).Span));
        Assert.False(regex.Match(input, 4).Success);
        Assert.True(regex.Match(input, matches[1].Value.StartOffsetInBytes, mode: Utf8PythonMatchMode.FullMatch).Success);
        ReadOnlyMemory<byte> snapshots2Input = input;
        int snapshots2Start = matches[1].Value.StartOffsetInBytes;
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, snapshots2Start, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        Assert.Equal(new[] { "345" }, snapshots2.Select(captureSnapshot2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).Span)).ToArray());

    }

    [Theory]
    [InlineData("item-([0-9]+)|item-([a-z]+)", "item-abc item-12 item-?")]
    [InlineData("(item-1|item-12)[0-9]*", "item-12 item-123")]
    [InlineData("(item-[0-9]+)(?=!)", "item-12? item-345!")]
    [InlineData("префикс([0-9]+)", "😀префиксx префикс12 префикс345")]
    public void PrefixIsNecessaryOnlyAndOriginalOrderedVerificationWins(string pattern, string text)
    {
        var expected = new Regex(pattern).Matches(text);
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = new Utf8PythonRegex(pattern).CreateMatchCursor(Encoding.UTF8.GetBytes(text), 0, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }
        var actual = collectedRanges2.ToArray();
        Assert.Equal(expected.Count, actual.Length);
        for (var index = 0; index < actual.Length; index++)
        {
            Assert.Equal(expected[index].Value, System.Text.Encoding.UTF8.GetString(actual[index].GetValueMemory().Span));
            for (var group = 1; group < expected[index].Groups.Count; group++)
            {
                Assert.Equal(expected[index].Groups[group].Success, actual[index].Groups[group].Success);
                if (expected[index].Groups[group].Success)
                {
                    Assert.Equal(expected[index].Groups[group].Value, System.Text.Encoding.UTF8.GetString(actual[index].GetGroupMemory(group).Span));
                }
            }
        }
    }

    [Fact]
    public void EveryWorkCutoffAndCursorAliasRetainsTheCallerLoan()
    {
        Utf8PythonRangeMatch cursorRange_durable = default;
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var program = (Utf8SliceProgram)typeof(Utf8PythonRegex).GetField("_scalarProgram", flags)!.GetValue(regex)!;
        var hint = (Utf8Regex?)typeof(Utf8PythonRegex).GetField("_utf8Regex", flags)!.GetValue(regex);
        var input = Encoding.UTF8.GetBytes(new string('x', 4097) + "item-x item-12");
        ulong Run(ulong maximum, bool complete)
        {
            var invocation = new Utf8SliceInvocation(new(maximum, 32, 1 << 20, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var completed = false;
            try
            {
                var cursor = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan, hint, regex, invocation);
                try
                {
                    Assert.True(cursor.MoveNext());
                    Assert.False(cursor.MoveNext());
                    completed = true;
                }
                finally
                {
                    cursor.Dispose();
                }
            }
            catch (Utf8ResourceLimitExceededException failure)
            {
                Assert.False(complete);
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
            }
            Assert.Equal(complete, completed);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            return invocation.Budget.Steps;
        }
        var total = Run(ulong.MaxValue, true);
        for (ulong cap = 1; cap < total; cap++)
        {
            Run(cap, false);
        }
        Run(total, true);

        using var durable = regex.CreateMatchCursor(input);
        var alias = durable;
        Assert.True(durable.MoveNext(out cursorRange_durable));
        var retained = cursorRange_durable;
        Assert.False(alias.MoveNext(out cursorRange_durable));
        alias.Dispose();
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrefixWindowScanKeepsOriginalInterruptionAndParentLoan(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var program = (Utf8SliceProgram)typeof(Utf8PythonRegex).GetField("_scalarProgram", flags)!.GetValue(regex)!;
        var hint = (Utf8Regex?)typeof(Utf8PythonRegex).GetField("_utf8Regex", flags)!.GetValue(regex);
        var input = Encoding.UTF8.GetBytes("item-x " + new string('i', 300000) + "item-12");
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 32, 1 << 20, 1 << 20), timeout, cancellation.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var cursor = new PythonReScalarMatchCursor(input, program, 0, timeout, hint, regex, invocation);
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ =>
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
        };
        Exception? failure = null;
        try
        {
            while (cursor.MoveNext())
            {
            }
        }
        catch (Exception error)
        {
            failure = error;
        }
        finally
        {
            cursor.Dispose();
        }
        Assert.True(entered);
        if (expire)
        {
            Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        }
        else
        {
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
