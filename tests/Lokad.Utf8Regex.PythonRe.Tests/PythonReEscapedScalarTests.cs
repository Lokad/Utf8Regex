using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Pinned CPython 3.13.2: artifacts/python-escaped-scalars-20261018/cpython.json.
public sealed class PythonReEscapedScalarTests
{
    [Theory]
    [InlineData(@"\😀", "😀", PythonReCompileOptions.None, true)]
    [InlineData(@"[\😀]", "😀", PythonReCompileOptions.None, true)]
    [InlineData(@"[\😀-\😁]+", "😀😁", PythonReCompileOptions.None, true)]
    [InlineData(@"\😀+", "😀😀", PythonReCompileOptions.None, true)]
    [InlineData(@"\é", "É", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\K", "k", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\𐐀", "𐐨", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\𐐀", "𐐨", PythonReCompileOptions.IgnoreCase | PythonReCompileOptions.Ascii, false)]
    [InlineData(@"[\𐐀]", "𐐨", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\uD83D\uDE00", "😀", PythonReCompileOptions.None, false)]
    [InlineData(@"\U0001F600", "😀", PythonReCompileOptions.None, true)]
    [InlineData(@"[^\😀]+", "😀", PythonReCompileOptions.None, false)]
    [InlineData(@"[^\😀]+", "é😁", PythonReCompileOptions.None, true)]
    [InlineData(@"\é+", "ÉÉ", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"\İ", "i", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"(?ai:\K)", "k", PythonReCompileOptions.None, false)]
    [InlineData(@"(?i:\𐐀)(?-i:\𐐀)", "𐐨𐐀", PythonReCompileOptions.None, true)]
    [InlineData(@"(?i:\𐐀)(?-i:\𐐀)", "𐐨𐐨", PythonReCompileOptions.None, false)]
    public void RawEscapesConsumeCompleteScalarsWithLexicalFolding(string pattern, string text,
        PythonReCompileOptions options, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var bytes in new[] { false, true })
        {
            var policy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 128,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            };
            var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options, Timeout.InfiniteTimeSpan, policy)
                : new Utf8PythonRegex(pattern, options, Timeout.InfiniteTimeSpan, policy);
            Utf8PythonRangeMatch actual = default;
            using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor1.MoveNext(out actual);
            }
            Assert.Equal(expected, actual.Success);
            if (expected)
            {
                Assert.Equal(text, System.Text.Encoding.UTF8.GetString(actual.GetValueMemory().Span));
                Assert.Equal((0, input.Length), (actual.Value.StartOffsetInBytes, actual.Value.EndOffsetInBytes));
            }
        }
    }

    [Fact]
    public void EscapedScalarRangesRetainOwnedCapturesAcrossCursorAliasesAndBulkOperations()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = "π😀😁é 😀"u8.ToArray();
        var regex = new Utf8PythonRegex(@"(?P<x>[\😀-\😁]+)", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 128,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            });
        using var cancellation = new CancellationTokenSource();
        var cursor = regex.CreateMatchCursor(input, 0, cancellation.Token);
        var alias = cursor;
        Utf8PythonRangeMatch retained;
        try
        {
            Assert.True(cursor.MoveNext(out cursorRange));
            retained = cursorRange;
            Assert.Equal((2, 10), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
            Assert.Equal(1, retained.LastIndex);
            Assert.Equal("x", retained.LastGroup);
            Assert.True(alias.MoveNext(out cursorRange));
            Assert.Equal((13, 17), (cursorRange.Value.StartOffsetInBytes, cursorRange.Value.EndOffsetInBytes));
            Assert.False(cursor.MoveNext(out cursorRange));
        }
        finally { alias.Dispose(); }
        Assert.Equal("😀😁", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        Assert.Equal(2, regex.Count(input, 0, cancellation.Token));
        var values = regex.FindAllToUtf8(input, 0, cancellation.Token);
        Assert.Equal(2, values.Count);
        Assert.True(values.ScalarValues[0].AsSpan().SequenceEqual("😀😁"u8));
        cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, cancellation.Token)).CancellationToken);
        Assert.Equal("😀😁", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }



    [Fact]
    public void RepeatedEscapedScalarsKeepConstructionWorkAdmissionAndRecovery()
    {
        var pattern = string.Concat(Enumerable.Repeat(@"\😀", 1024));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8PythonRegex.Compile(pattern,
            PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, default,
            new() { MaxWorkSteps = 128 }, Timeout.InfiniteTimeSpan, CancellationToken.None));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.True(new Utf8PythonRegex(@"\😀").FullMatch("😀"u8).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscapedScalarAcquisitionKeepsOriginalConstructionInterruptionAndUnwind(bool expire)
    {
        var pattern = string.Concat(Enumerable.Repeat(@"\😀", 1024));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 1000000, 128))
        {
            owner.Budget.CancellationToken = cancellation.Token;
            owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
            owner.Budget.CheckpointHook = steps =>
            {
                if (steps < 128) return;
                entered = true;
                if (expire) ticks = 50;
                else cancellation.Cancel();
            };
            if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                new PythonReParser(pattern).Parse(PythonReCompileOptions.None)).CancellationToken);
            Assert.True(entered);
            Assert.True(owner.Budget.PeakBytes > 0);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void CorrectEscapedCapturesKeepResultAdmissionAndFreshCallRecovery()
    {
        var regex = new Utf8PythonRegex(@"(?P<x>\😀)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1 });
        Assert.Equal(1, regex.Count("😀"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                Utf8PythonRangeMatch snapshot2 = default;
                using (var captureCursor2 = regex.CreateMatchCursor(("😀"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
                {
                    captureCursor2.MoveNext(out snapshot2);
                }

                _ = snapshot2;
            }).ResourceKind);
        Assert.Equal(1, regex.Count("😀"u8));
    }
}
