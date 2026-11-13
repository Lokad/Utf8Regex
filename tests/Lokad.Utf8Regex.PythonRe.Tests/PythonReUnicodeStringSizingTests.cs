using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReUnicodeStringSizingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4092)]
    [InlineData(4093)]
    [InlineData(4094)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void InteriorUnicodeStringsKeepOffsetsCapturesAndBlockTails(int prefixLength)
    {
        var word = new string('a', prefixLength) + "é中😀";
        var input = Encoding.UTF8.GetBytes("😀" + word + "，" + word + ",尾😀");
        foreach (var funded in new[] { false, true })
        {
            var policy = funded ? new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1_000_000, MaxDepth = 1024,
                MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576,
            } : default;
            using var plain = new Utf8PythonRegex("[,，]", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            using var captured = new Utf8PythonRegex("([,，])", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            Assert.Equal(new string?[] { word, word, "尾😀" }, plain.SplitToStrings(input, 0, 4));
            Assert.Equal(new string?[] { word, "，", word, ",", "尾😀" }, captured.SplitToStrings(input, 0, 4));
            Assert.Equal(new string?[] { word, word + ",尾😀" }, plain.SplitToStrings(input, 1, 4));
            using var findall = new Utf8PythonRegex("([^,，]+)");
            Assert.Equal(new string?[] { word, word, "尾😀" },
                findall.FindAllToStrings(Encoding.UTF8.GetBytes(word + "，" + word + ",尾😀")).ScalarValues);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InteriorSizingObservesOriginalInterruptionBeforeStringAdmission(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var core = new global::Lokad.Utf8Regex.Utf8Regex(".+");
        var program = core.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes("x" + new string('é', 10_000) + "z");
        var cursor = new PythonReScalarMatchCursor(input, program, 0, timeout, null, ".+",
            new(new(0, 0, 1UL << 20), timeout, token.Token) { InputValidation = Utf8InputValidation.AssumeValid });
        try
        {
            Assert.True(cursor.MoveNext());
            var baseline = cursor.ReservedBytes;
            ref var invocation = ref cursor.WorkingInvocation;
            while ((invocation.Budget.Steps & 63) != 62)
                Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            var entered = false;
            invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else token.Cancel(); };
            Exception? failure = null;
            string? result = null;
            try { result = cursor.GetText(1, input.Length - 1); }
            catch (Exception error) { failure = error; }
            Assert.True(entered);
            Assert.Null(result);
            if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(baseline, cursor.ReservedBytes);
            Assert.Equal(0UL, cursor.ResultReservation);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
    }
}
