using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFamilyByteUnionTests
{
    [Theory]
    [InlineData(0)] [InlineData(2042)] [InlineData(2043)] [InlineData(2044)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(4091)] [InlineData(4096)]
    public void CommonPrefixWindowsKeepCapturesScalarOffsetsAndTails(int padding)
    {
        var prefix = "😀" + new string('i', padding);
        var text = prefix + "item-x item-12 item-345";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        var matches = regex.FindIterDetailed(input, 4);
        Assert.Equal(new[] { "12", "345" }, regex.FindAllToStrings(input, 4).ScalarValues);
        Assert.Equal(2, matches.Length);
        Assert.Equal(prefix.Length + 7, matches[0].Value.StartOffsetInUtf16);
        Assert.Equal(Encoding.UTF8.GetByteCount(prefix) + 7, matches[0].Value.StartOffsetInBytes);
        Assert.Equal("12", matches[0].Groups[1].ValueText);
        Assert.False(regex.Match(input, 4).Success);
        Assert.True(regex.FullMatch(input, matches[1].Value.StartOffsetInBytes).Success);
        Assert.Equal(new[] { "345" }, regex.FindAllToStrings(input, matches[1].Value.StartOffsetInBytes).ScalarValues);
        Assert.Throws<ArgumentException>(() => regex.FindAllToStrings([.. input, 0xff]));
    }

    [Theory]
    [InlineData("item-([0-9]+)|item-([a-z]+)", "item-abc item-12 item-?")]
    [InlineData("(item-1|item-12)[0-9]*", "item-12 item-123")]
    [InlineData("(item-[0-9]+)(?=!)", "item-12? item-345!")]
    [InlineData("префикс([0-9]+)", "😀префиксx префикс12 префикс345")]
    public void PrefixIsNecessaryOnlyAndOriginalOrderedVerificationWins(string pattern, string text)
    {
        var expected = new Regex(pattern).Matches(text);
        var actual = new Utf8PythonRegex(pattern).FindIterDetailed(Encoding.UTF8.GetBytes(text));
        Assert.Equal(expected.Count, actual.Length);
        for (var index = 0; index < actual.Length; index++)
        {
            Assert.Equal(expected[index].Value, actual[index].Value.ValueText);
            for (var group = 1; group < expected[index].Groups.Count; group++)
            {
                Assert.Equal(expected[index].Groups[group].Success, actual[index].Groups[group].Success);
                if (expected[index].Groups[group].Success)
                    Assert.Equal(expected[index].Groups[group].Value, actual[index].Groups[group].ValueText);
            }
        }
    }

    [Fact]
    public void EveryWorkCutoffAndCursorAliasRetainsTheCallerLoan()
    {
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
                finally { cursor.Dispose(); }
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
        for (ulong cap = 1; cap < total; cap++) Run(cap, false);
        Run(total, true);

        using var durable = regex.CreateMatchCursor(input);
        var alias = durable;
        Assert.True(durable.MoveNext()); var retained = alias.Current;
        Assert.False(alias.MoveNext()); alias.Dispose();
        Assert.Equal("12", retained.Groups[1].ValueText);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
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
        cursor.WorkingInvocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else cancellation.Cancel(); };
        Exception? failure = null;
        try { while (cursor.MoveNext()) { } }
        catch (Exception error) { failure = error; }
        finally { cursor.Dispose(); }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
